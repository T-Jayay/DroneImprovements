using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace DroneImprovements.Patches
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
    /// <para>This class also holds the logic of the payout patches below (see <see cref="PatchSafety"/>).</para>
    /// </summary>
    internal static class CombatGold
    {
        private static readonly ConditionalWeakTable<GoldOrb, object> combatOrbs =
            new ConditionalWeakTable<GoldOrb, object>();
        private static readonly object marker = new object();

        /// <summary>Should drone players be left out of a team payout happening right now?</summary>
        public static bool ShouldExcludeDronesFromTeamPayout
        {
            get
            {
                DroneGoldMode mode = HostSettings.GoldMode;
                return mode == DroneGoldMode.None || (mode == DroneGoldMode.NoKillGold && CombatContext.IsActive);
            }
        }

        /// <summary>Drone players get no gold at all.</summary>
        public static bool IsAllGoldBlocked => HostSettings.GoldMode == DroneGoldMode.None;

        /// <summary>
        /// Pays a team payout to the survivors only, when drone players shouldn't get it. False when the original
        /// should pay it as usual.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryPaySurvivorsOnly(TeamIndex teamIndex, uint money)
        {
            // On a client the original only logs a warning.
            if (!NetworkServer.active || !ShouldExcludeDronesFromTeamPayout)
            {
                return false;
            }
            int survivors = DroneUtil.CountSurvivorPlayers();
            uint share = survivors != 0 ? (uint)Mathf.CeilToInt((float)money / survivors) : money;
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                CharacterMaster master = DroneUtil.IsSurvivorPlayer(body) ? body.master : null;
                if (master)
                {
                    master.GiveMoney(share);
                }
            }
            return true;
        }

        /// <summary>Remembers a gold orb created in a combat context.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OnOrbAdded(Orb orb)
        {
            if (CombatContext.IsActive && orb is GoldOrb goldOrb && !combatOrbs.TryGetValue(goldOrb, out _))
            {
                combatOrbs.Add(goldOrb, marker);
            }
        }

        /// <summary>
        /// The gold orb is reaching a drone player who shouldn't get it (NoKillGold: a combat orb; None: any orb).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool BlocksOrbArrival(GoldOrb orb)
        {
            DroneGoldMode mode = HostSettings.GoldMode;
            bool blocked = mode == DroneGoldMode.None
                || (mode == DroneGoldMode.NoKillGold && combatOrbs.TryGetValue(orb, out _));
            if (!blocked)
            {
                return false;
            }
            HurtBox target = orb.target;
            CharacterBody body = target && target.healthComponent ? target.healthComponent.body : null;
            return DroneUtil.IsDronePlayer(body);
        }

        /// <summary>None: gold paid straight to a drone player's master is blocked too.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool BlocksDirectPayment(CharacterMaster master)
        {
            return IsAllGoldBlocked && DroneUtil.IsDronePlayer(master.GetBody());
        }
    }

    /// <summary>
    /// Whether the game is processing a kill, a money pack, a hit or damage right now (see <see cref="CombatGold"/>).
    /// A class of its own, without game types, so entering and leaving can't throw: the context hooks need no
    /// try/catch.
    /// </summary>
    internal static class CombatContext
    {
        [ThreadStatic] private static int depth;

        public static bool IsActive => depth > 0;

        /// <summary>A combat context starts (called from a prefix; contexts can nest).</summary>
        public static void Enter()
        {
            depth++;
        }

        /// <summary>
        /// A combat context ends (called from the matching finalizer, so also when the game throws). Only when its
        /// prefix ran: if another mod's prefix threw first, ours never entered.
        /// </summary>
        public static void Exit(bool entered)
        {
            if (entered)
            {
                depth = Math.Max(0, depth - 1);
            }
        }
    }

    // --- Combat contexts: gold paid during these is combat gold -------------------------------------------------

    /// <summary>Enemy kill gold.</summary>
    [HarmonyPatch(typeof(DeathRewards), nameof(DeathRewards.OnKilledServer), typeof(DamageReport))]
    internal static class DeathRewardsContextPatch
    {
        private static void Prefix(out bool __state)
        {
            CombatContext.Enter();
            __state = true;
        }

        private static void Finalizer(bool __state)
        {
            CombatContext.Exit(__state);
        }
    }

    /// <summary>Money packs: Ghor's Tome, Cleanup Drone and gilded enemies drop them.</summary>
    [HarmonyPatch(typeof(MoneyPickup), "OnTriggerStay", typeof(Collider))]
    internal static class MoneyPickupContextPatch
    {
        private static void Prefix(out bool __state)
        {
            CombatContext.Enter();
            __state = true;
        }

        private static void Finalizer(bool __state)
        {
            CombatContext.Exit(__state);
        }
    }

    /// <summary>
    /// Gold from hitting enemies (Brittle Crown). OnHitEnemy only calls the private ProcessHitEnemy, which nothing
    /// else calls.
    /// </summary>
    [HarmonyPatch(typeof(GlobalEventManager), nameof(GlobalEventManager.OnHitEnemy), typeof(DamageInfo),
        typeof(GameObject))]
    internal static class HitEnemyContextPatch
    {
        private static void Prefix(out bool __state)
        {
            CombatContext.Enter();
            __state = true;
        }

        private static void Finalizer(bool __state)
        {
            CombatContext.Exit(__state);
        }
    }

    /// <summary>
    /// Gold from getting hit (Roll of Pennies, ...). TakeDamage only calls the private TakeDamageProcess (on the host),
    /// which nothing else calls.
    /// </summary>
    [HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.TakeDamage), typeof(DamageInfo))]
    internal static class TakeDamageContextPatch
    {
        private static void Prefix(out bool __state)
        {
            CombatContext.Enter();
            __state = true;
        }

        private static void Finalizer(bool __state)
        {
            CombatContext.Exit(__state);
        }
    }

    // --- Payouts ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Leaves drone players out of a team payout (kills, money packs, barrels) when they shouldn't get it, and splits
    /// it between the survivors instead, so none of it is lost while a survivor is alive.
    /// <para>
    /// This replaces TeamManager.GiveTeamMoney(TeamIndex, uint) when drones are left out, mirroring it with survivors
    /// in place of all players: vanilla divides the amount by Run.livingPlayerCount (drone players included) and pays
    /// every player body on the team, both inside that one method, so there is no narrower place to change. Otherwise
    /// the original runs. Another mod that also replaces this method (a prefix returning false) would make both
    /// payouts happen.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(TeamManager), nameof(TeamManager.GiveTeamMoney), typeof(TeamIndex), typeof(uint))]
    internal static class GiveTeamMoneyPatch
    {
        private static bool failed;

        private static bool Prefix(TeamIndex teamIndex, uint money)
        {
            try
            {
                return !CombatGold.TryPaySurvivorsOnly(teamIndex, money);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.DroneGold, "TeamManager.GiveTeamMoney", e);
                return true;
            }
        }
    }

    /// <summary>Remembers the gold orbs created during combat (Brittle Crown, Roll of Pennies, ...).</summary>
    [HarmonyPatch(typeof(OrbManager), nameof(OrbManager.AddOrb), typeof(Orb))]
    internal static class AddOrbPatch
    {
        private static bool failed;

        private static void Prefix(Orb orb)
        {
            try
            {
                CombatGold.OnOrbAdded(orb);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.DroneGold, "OrbManager.AddOrb", e);
            }
        }
    }

    /// <summary>Gold orbs that shouldn't pay a drone player don't (NoKillGold: combat orbs; None: every orb).</summary>
    [HarmonyPatch(typeof(GoldOrb), nameof(GoldOrb.OnArrival))]
    internal static class GoldOrbArrivalPatch
    {
        private static bool failed;

        private static bool Prefix(GoldOrb __instance)
        {
            try
            {
                return !CombatGold.BlocksOrbArrival(__instance);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.DroneGold, "GoldOrb.OnArrival", e);
                return true;
            }
        }
    }

    /// <summary>
    /// None: blocks everything else that pays a drone player directly (Shrine of Blood, other mods' items, ...).
    /// </summary>
    [HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.GiveMoney), typeof(uint))]
    internal static class GiveMoneyPatch
    {
        private static bool failed;

        private static bool Prefix(CharacterMaster __instance)
        {
            try
            {
                return !CombatGold.BlocksDirectPayment(__instance);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.DroneGold, "CharacterMaster.GiveMoney", e);
                return true;
            }
        }
    }

    // --- Gold interactables (None) ------------------------------------------------------------------------------

    /// <summary>
    /// None: drone players can't use the interactables that only pay gold. The host re-checks interactability when
    /// an interaction arrives, and clients use the host's mode for their prompts. Interactability is checked every
    /// frame for each interactable near a player, so the answers that never change are remembered.
    /// </summary>
    internal static class GoldInteractables
    {
        // Nearly always the same Interactor, whose body is a drone player's or not for good: remember the last answer.
        private static Interactor lastActivator;
        private static bool lastActivatorIsDronePlayer;

        // Whether each purchase interactable is a Shrine of Blood, kept only as long as the interactable exists.
        private static readonly ConditionalWeakTable<PurchaseInteraction, object> bloodShrineAnswers =
            new ConditionalWeakTable<PurchaseInteraction, object>();
        private static readonly object isBloodShrine = new object();
        private static readonly object isNotBloodShrine = new object();

        /// <summary>A barrel's interactability for this activator.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Interactability ForBarrel(Interactor activator, Interactability interactability)
        {
            return interactability != Interactability.Disabled && IsBlockedFor(activator)
                ? Interactability.Disabled
                : interactability;
        }

        /// <summary>
        /// A purchase interactable's interactability for this activator; only a Shrine of Blood's can change.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Interactability ForPurchase(PurchaseInteraction purchase, Interactor activator,
            Interactability interactability)
        {
            return interactability != Interactability.Disabled && IsBlockedFor(activator) && IsBloodShrine(purchase)
                ? Interactability.Disabled
                : interactability;
        }

        private static bool IsBlockedFor(Interactor activator)
        {
            if (!CombatGold.IsAllGoldBlocked || !activator)
            {
                return false;
            }
            if (!ReferenceEquals(activator, lastActivator))
            {
                lastActivator = activator;
                lastActivatorIsDronePlayer = DroneUtil.IsDronePlayer(activator.GetComponent<CharacterBody>());
            }
            return lastActivatorIsDronePlayer;
        }

        private static bool IsBloodShrine(PurchaseInteraction purchase)
        {
            if (!bloodShrineAnswers.TryGetValue(purchase, out object answer))
            {
                answer = purchase.GetComponent<ShrineBloodBehavior>() ? isBloodShrine : isNotBloodShrine;
                bloodShrineAnswers.Add(purchase, answer);
            }
            return ReferenceEquals(answer, isBloodShrine);
        }
    }

    /// <summary>
    /// Barrels. They don't check for Remote Operation at all, so drone players can open them in the base game and
    /// with DroneSurvivorActions off.
    /// </summary>
    [HarmonyPatch(typeof(BarrelInteraction), nameof(BarrelInteraction.GetInteractability), typeof(Interactor))]
    internal static class BarrelInteractabilityPatch
    {
        private static bool failed;

        private static void Postfix(Interactor activator, ref Interactability __result)
        {
            try
            {
                __result = GoldInteractables.ForBarrel(activator, __result);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.DroneGold, "BarrelInteraction.GetInteractability", e);
            }
        }
    }

    /// <summary>Shrine of Blood (trades health for gold).</summary>
    [HarmonyPatch(typeof(PurchaseInteraction), nameof(PurchaseInteraction.GetInteractability), typeof(Interactor))]
    internal static class BloodShrineInteractabilityPatch
    {
        private static bool failed;

        private static void Postfix(PurchaseInteraction __instance, Interactor activator, ref Interactability __result)
        {
            try
            {
                __result = GoldInteractables.ForPurchase(__instance, activator, __result);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.DroneGold, "PurchaseInteraction.GetInteractability",
                    e);
            }
        }
    }
}
