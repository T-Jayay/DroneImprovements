using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace DroneImprovements.Hooks
{
    /// <summary>
    /// The host settings DroneCombatGold, DroneBarrelGold and DroneShrineOfBloodGold. All gold is paid on the host, so
    /// the host's settings decide; the mod never takes gold a drone player already has.
    /// <para>
    /// The game pays kills, money packs and barrels through the same TeamManager.GiveTeamMoney call, so gold is
    /// classified by what the game is doing when it is paid: a team payout made while the game processes a kill
    /// (DeathRewards.OnKilledServer), a money pack pickup (MoneyPickup.OnTriggerStay), a hit on an enemy
    /// (GlobalEventManager.OnHitEnemy) or damage taken (HealthComponent.TakeDamage) is combat gold, and so is a gold
    /// orb created during one of those; a team payout made while a barrel opens (BarrelInteraction.OnInteractionBegin)
    /// is barrel gold. Gold paid straight to a drone player's master (CharacterMaster.GiveMoney) during a combat
    /// context is combat gold too: the base game never pays a player that way, but another mod's on-kill or on-hit
    /// gold can. Outside a combat context it is never blocked. Shrine of Blood pays only the player who uses it, so
    /// DroneShrineOfBloodGold blocks using the shrine instead. A mod that writes CharacterMaster.money directly (such
    /// as ShareSuite's shared money) can't be classified, and the mod doesn't fight it.
    /// </para>
    /// <para>
    /// Where a hook here doesn't call orig, that also skips the hooks other mods added to the method before this one.
    /// </para>
    /// </summary>
    internal static class GoldHooks
    {
        // The gold orbs created in a combat context.
        private static readonly ConditionalWeakTable<GoldOrb, object> combatOrbs =
            new ConditionalWeakTable<GoldOrb, object>();
        private static readonly object marker = new object();

        // Combat contexts can nest, e.g. a kill during damage taken.
        private static int combatContextDepth;

        // A depth too, so the context ends with the outermost call.
        private static int barrelContextDepth;

        /// <summary>Whether the game is processing a kill, a money pack, a hit or damage right now.</summary>
        private static bool InCombatContext => combatContextDepth > 0;

        /// <summary>Whether the game is opening a barrel right now.</summary>
        private static bool InBarrelContext => barrelContextDepth > 0;

        /// <summary>Should drone players be left out of a team payout happening right now?</summary>
        private static bool ShouldExcludeDronesFromTeamPayout =>
            (!HostSettings.CombatGold && InCombatContext) || (!HostSettings.BarrelGold && InBarrelContext);

        public static void Init()
        {
            On.RoR2.DeathRewards.OnKilledServer += DeathRewards_OnKilledServer;
            On.RoR2.MoneyPickup.OnTriggerStay += MoneyPickup_OnTriggerStay;
            On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManager_OnHitEnemy;
            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            On.RoR2.TeamManager.GiveTeamMoney_TeamIndex_uint += TeamManager_GiveTeamMoney_TeamIndex_uint;
            On.RoR2.Orbs.OrbManager.AddOrb += OrbManager_AddOrb;
            On.RoR2.Orbs.GoldOrb.OnArrival += GoldOrb_OnArrival;
            On.RoR2.CharacterMaster.GiveMoney += CharacterMaster_GiveMoney;
            On.RoR2.BarrelInteraction.OnInteractionBegin += BarrelInteraction_OnInteractionBegin;
            On.RoR2.BarrelInteraction.GetInteractability += BarrelInteraction_GetInteractability;
            On.RoR2.PurchaseInteraction.GetInteractability += PurchaseInteraction_GetInteractability;
        }

        // --- Contexts: gold paid during these is combat or barrel gold ----------------------------------------------
        //
        // Combat: the time the game processes an enemy kill, a money pack (Ghor's Tome, Cleanup Drone and gilded
        // enemies drop them), a hit on an enemy (Brittle Crown) or damage taken (Roll of Pennies, ...). OnHitEnemy and
        // TakeDamage only call the private ProcessHitEnemy and TakeDamageProcess, which nothing else calls. Barrel: the
        // time the game opens a barrel, which pays the team inside OnInteractionBegin. Each context ends in a finally,
        // so it also ends when the game method throws.

        private static void DeathRewards_OnKilledServer(On.RoR2.DeathRewards.orig_OnKilledServer orig,
            DeathRewards self, DamageReport damageReport)
        {
            combatContextDepth++;
            try
            {
                orig(self, damageReport);
            }
            finally
            {
                combatContextDepth--;
            }
        }

        private static void MoneyPickup_OnTriggerStay(On.RoR2.MoneyPickup.orig_OnTriggerStay orig, MoneyPickup self,
            Collider other)
        {
            combatContextDepth++;
            try
            {
                orig(self, other);
            }
            finally
            {
                combatContextDepth--;
            }
        }

        private static void GlobalEventManager_OnHitEnemy(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig,
            GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            combatContextDepth++;
            try
            {
                orig(self, damageInfo, victim);
            }
            finally
            {
                combatContextDepth--;
            }
        }

        private static void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig,
            HealthComponent self, DamageInfo damageInfo)
        {
            combatContextDepth++;
            try
            {
                orig(self, damageInfo);
            }
            finally
            {
                combatContextDepth--;
            }
        }

        private static void BarrelInteraction_OnInteractionBegin(
            On.RoR2.BarrelInteraction.orig_OnInteractionBegin orig, BarrelInteraction self, Interactor activator)
        {
            barrelContextDepth++;
            try
            {
                orig(self, activator);
            }
            finally
            {
                barrelContextDepth--;
            }
        }

        // --- Payouts ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Leaves drone players out of a team payout (kills, money packs, barrels) when they shouldn't get it, and
        /// splits it between the survivors instead, so none of it is lost while a survivor is alive.
        /// <para>
        /// This replaces TeamManager.GiveTeamMoney(TeamIndex, uint) when drones are left out, mirroring it with
        /// survivors in place of all players: vanilla divides the amount by Run.livingPlayerCount (drone players
        /// included) and pays every player body on the team, both inside that one method, so there is no narrower place
        /// to change. Otherwise orig runs.
        /// </para>
        /// </summary>
        private static void TeamManager_GiveTeamMoney_TeamIndex_uint(
            On.RoR2.TeamManager.orig_GiveTeamMoney_TeamIndex_uint orig, TeamManager self, TeamIndex teamIndex,
            uint money)
        {
            // On a client the original only logs a warning.
            if (!NetworkServer.active || !ShouldExcludeDronesFromTeamPayout)
            {
                orig(self, teamIndex, money);
                return;
            }
            // The replacement: orig isn't called.
            int survivors = DroneUtil.CountSurvivorPlayers();
            uint share = survivors != 0 ? (uint)Mathf.CeilToInt((float)money / survivors) : money;
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                if (DroneUtil.IsSurvivorPlayer(body) && body.master)
                {
                    body.master.GiveMoney(share);
                }
            }
        }

        /// <summary>Remembers the gold orbs created during combat (Brittle Crown, Roll of Pennies, ...).</summary>
        private static void OrbManager_AddOrb(On.RoR2.Orbs.OrbManager.orig_AddOrb orig, OrbManager self, Orb orb)
        {
            if (orb is GoldOrb goldOrb && InCombatContext)
            {
                combatOrbs.GetValue(goldOrb, _ => marker);
            }
            orig(self, orb);
        }

        /// <summary>
        /// Combat gold orbs that would pay a drone player don't when DroneCombatGold is off: orig isn't called for
        /// them.
        /// </summary>
        private static void GoldOrb_OnArrival(On.RoR2.Orbs.GoldOrb.orig_OnArrival orig, GoldOrb self)
        {
            if (!HostSettings.CombatGold && combatOrbs.TryGetValue(self, out _))
            {
                HurtBox target = self.target;
                CharacterBody body = target && target.healthComponent ? target.healthComponent.body : null;
                if (DroneUtil.IsDronePlayer(body))
                {
                    return;
                }
            }
            orig(self);
        }

        /// <summary>
        /// Gold another mod pays straight to a drone player during a combat context doesn't pay them when
        /// DroneCombatGold is off: orig isn't called then.
        /// </summary>
        private static void CharacterMaster_GiveMoney(On.RoR2.CharacterMaster.orig_GiveMoney orig, CharacterMaster self,
            uint amount)
        {
            if (!HostSettings.CombatGold && InCombatContext && DroneUtil.IsDronePlayer(self.GetBody()))
            {
                return;
            }
            orig(self, amount);
        }

        // --- Gold interactables -------------------------------------------------------------------------------------
        //
        // Drone players can't use the interactables whose gold is turned off. The host re-checks interactability when
        // an interaction arrives, and clients use the host's settings for their prompts.

        /// <summary>
        /// Barrels (DroneBarrelGold). They don't check for Remote Operation at all, so drone players can open them in
        /// the base game and with DroneSurvivorActions off.
        /// </summary>
        private static Interactability BarrelInteraction_GetInteractability(
            On.RoR2.BarrelInteraction.orig_GetInteractability orig, BarrelInteraction self, Interactor activator)
        {
            Interactability result = orig(self, activator);
            if (result != Interactability.Disabled && !HostSettings.BarrelGold
                && DroneUtil.IsDronePlayer(activator.GetComponent<CharacterBody>()))
            {
                result = Interactability.Disabled;
            }
            return result;
        }

        /// <summary>Shrine of Blood, which trades health for gold (DroneShrineOfBloodGold).</summary>
        private static Interactability PurchaseInteraction_GetInteractability(
            On.RoR2.PurchaseInteraction.orig_GetInteractability orig, PurchaseInteraction self, Interactor activator)
        {
            Interactability result = orig(self, activator);
            if (result != Interactability.Disabled && !HostSettings.ShrineOfBloodGold
                && DroneUtil.IsDronePlayer(activator.GetComponent<CharacterBody>())
                && self.GetComponent<ShrineBloodBehavior>())
            {
                result = Interactability.Disabled;
            }
            return result;
        }
    }
}
