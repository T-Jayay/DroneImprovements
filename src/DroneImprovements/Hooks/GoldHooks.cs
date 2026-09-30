using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace DroneImprovements.Hooks
{
    /// <summary>
    /// The host setting DroneGold (see <see cref="DroneGoldMode"/>). All gold is paid on the host, so the host's
    /// setting decides; the mod never takes gold a drone player already has.
    /// <para>
    /// The game pays kills, money packs and barrels through the same TeamManager.GiveTeamMoney call, so gold is
    /// classified by what the game is doing when it is paid: a team payout made while the game processes a kill
    /// (DeathRewards.OnKilledServer), a money pack pickup (MoneyPickup.OnTriggerStay), a hit on an enemy
    /// (GlobalEventManager.OnHitEnemy) or damage taken (HealthComponent.TakeDamage) is combat gold, and so is a gold
    /// orb created during one of those. Only team payouts and gold orbs are classified: gold paid straight to a master
    /// (CharacterMaster.GiveMoney, as Shrine of Blood or another mod's item does) never counts as combat gold.
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

        /// <summary>Whether the game is processing a kill, a money pack, a hit or damage right now.</summary>
        private static bool InCombatContext => combatContextDepth > 0;

        /// <summary>Should drone players be left out of a team payout happening right now?</summary>
        private static bool ShouldExcludeDronesFromTeamPayout
        {
            get
            {
                DroneGoldMode mode = HostSettings.GoldMode;
                return mode == DroneGoldMode.None || (mode == DroneGoldMode.NoKillGold && InCombatContext);
            }
        }

        /// <summary>Drone players get no gold at all.</summary>
        private static bool IsAllGoldBlocked => HostSettings.GoldMode == DroneGoldMode.None;

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
            On.RoR2.BarrelInteraction.GetInteractability += BarrelInteraction_GetInteractability;
            On.RoR2.PurchaseInteraction.GetInteractability += PurchaseInteraction_GetInteractability;
        }

        // --- Combat contexts: gold paid during these is combat gold -------------------------------------------------
        //
        // The time the game processes an enemy kill, a money pack (Ghor's Tome, Cleanup Drone and gilded enemies drop
        // them), a hit on an enemy (Brittle Crown) or damage taken (Roll of Pennies, ...). OnHitEnemy and TakeDamage
        // only call the private ProcessHitEnemy and TakeDamageProcess, which nothing else calls. Each context ends in a
        // finally, so it also ends when the game method throws.

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
        /// Gold orbs that shouldn't pay a drone player don't (NoKillGold: combat orbs; None: every orb): orig isn't
        /// called for them.
        /// </summary>
        private static void GoldOrb_OnArrival(On.RoR2.Orbs.GoldOrb.orig_OnArrival orig, GoldOrb self)
        {
            DroneGoldMode mode = HostSettings.GoldMode;
            bool blocked = mode == DroneGoldMode.None
                || (mode == DroneGoldMode.NoKillGold && combatOrbs.TryGetValue(self, out _));
            if (blocked)
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
        /// None: blocks everything else that pays a drone player directly (Shrine of Blood, other mods' items, ...).
        /// orig isn't called then.
        /// </summary>
        private static void CharacterMaster_GiveMoney(On.RoR2.CharacterMaster.orig_GiveMoney orig, CharacterMaster self,
            uint amount)
        {
            if (IsAllGoldBlocked && DroneUtil.IsDronePlayer(self.GetBody()))
            {
                return;
            }
            orig(self, amount);
        }

        // --- Gold interactables (None) ------------------------------------------------------------------------------
        //
        // None: drone players can't use the interactables that only pay gold. The host re-checks interactability when
        // an interaction arrives, and clients use the host's mode for their prompts.

        /// <summary>
        /// Barrels. They don't check for Remote Operation at all, so drone players can open them in the base game and
        /// with DroneSurvivorActions off.
        /// </summary>
        private static Interactability BarrelInteraction_GetInteractability(
            On.RoR2.BarrelInteraction.orig_GetInteractability orig, BarrelInteraction self, Interactor activator)
        {
            Interactability result = orig(self, activator);
            if (result != Interactability.Disabled && IsAllGoldBlocked
                && DroneUtil.IsDronePlayer(activator.GetComponent<CharacterBody>()))
            {
                result = Interactability.Disabled;
            }
            return result;
        }

        /// <summary>Shrine of Blood (trades health for gold).</summary>
        private static Interactability PurchaseInteraction_GetInteractability(
            On.RoR2.PurchaseInteraction.orig_GetInteractability orig, PurchaseInteraction self, Interactor activator)
        {
            Interactability result = orig(self, activator);
            if (result != Interactability.Disabled && IsAllGoldBlocked
                && DroneUtil.IsDronePlayer(activator.GetComponent<CharacterBody>())
                && self.GetComponent<ShrineBloodBehavior>())
            {
                result = Interactability.Disabled;
            }
            return result;
        }
    }
}
