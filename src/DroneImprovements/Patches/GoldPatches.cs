using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace DroneImprovements.Patches
{
    /// <summary>
    /// Host setting "DroneGold" (see <see cref="DroneGoldMode"/>). Drone players always keep the gold they have.
    ///   NoKillGold: no combat gold (enemy kills, Ghor's Tome money packs, Brittle Crown, Roll of Pennies);
    ///               barrels, Shrine of Blood and Executive Card cashback still pay.
    ///   None:       no gold from anything, and barrels / Shrine of Blood can't be used.
    /// All gold is handed out on the host, so the host's setting decides.
    ///
    /// The game pays barrels and kills through the same TeamManager.GiveTeamMoney call, so gold is classified by
    /// where it comes from: anything paid out while the game is processing a kill, a money pack or a damage hit
    /// counts as combat gold.
    /// </summary>
    internal static class CombatGold
    {
        [ThreadStatic] private static int depth;

        private static readonly ConditionalWeakTable<GoldOrb, object> combatOrbs = new ConditionalWeakTable<GoldOrb, object>();
        private static readonly object marker = new object();

        public static bool InCombatContext => depth > 0;

        private static DroneGoldMode Mode => HostSettings.GoldMode;

        /// <summary>Should drones be left out of a team payout happening right now?</summary>
        public static bool ExcludeDronesFromTeamPayout =>
            Mode == DroneGoldMode.None || (Mode == DroneGoldMode.NoKillGold && InCombatContext);

        public static bool BlockOrbForDrone(GoldOrb orb) =>
            Mode == DroneGoldMode.None || (Mode == DroneGoldMode.NoKillGold && IsCombatOrb(orb));

        public static bool BlocksAllGold => Mode == DroneGoldMode.None;

        public static void Enter() => depth++;

        public static void Exit() => depth = Math.Max(0, depth - 1);

        public static void MarkOrb(GoldOrb orb) => combatOrbs.GetValue(orb, _ => marker);

        public static bool IsCombatOrb(GoldOrb orb) => combatOrbs.TryGetValue(orb, out _);
    }

    // --- Combat contexts -------------------------------------------------------------------------------------

    /// <summary>Enemy kill gold.</summary>
    [HarmonyPatch(typeof(DeathRewards), nameof(DeathRewards.OnKilledServer))]
    internal static class DeathRewardsContextPatch
    {
        private static void Prefix() => CombatGold.Enter();
        private static void Finalizer() => CombatGold.Exit();
    }

    /// <summary>Money packs (Ghor's Tome drops from kills).</summary>
    [HarmonyPatch(typeof(MoneyPickup), "OnTriggerStay")]
    internal static class MoneyPickupContextPatch
    {
        private static void Prefix() => CombatGold.Enter();
        private static void Finalizer() => CombatGold.Exit();
    }

    /// <summary>Brittle Crown (gold on hitting enemies).</summary>
    [HarmonyPatch(typeof(GlobalEventManager), "ProcessHitEnemy")]
    internal static class HitEnemyContextPatch
    {
        private static void Prefix() => CombatGold.Enter();
        private static void Finalizer() => CombatGold.Exit();
    }

    /// <summary>Roll of Pennies (gold when hit).</summary>
    [HarmonyPatch(typeof(HealthComponent), "TakeDamageProcess")]
    internal static class TakeDamageContextPatch
    {
        private static void Prefix() => CombatGold.Enter();
        private static void Finalizer() => CombatGold.Exit();
    }

    // --- Payouts ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Kill gold, money packs and barrel gold are shared between all player bodies, drones included. When drones
    /// shouldn't get a payout, they're left out and it's split between the living survivors instead, so none of it
    /// is lost. With NoKillGold only combat payouts are affected; barrels (same call, outside a combat context) aren't.
    /// </summary>
    [HarmonyPatch(typeof(TeamManager), nameof(TeamManager.GiveTeamMoney), typeof(TeamIndex), typeof(uint))]
    internal static class GiveTeamMoneyPatch
    {
        private static bool Prefix(TeamIndex teamIndex, uint money)
        {
            if (!CombatGold.ExcludeDronesFromTeamPayout)
            {
                return true;
            }
            int survivors = DroneUtil.CountLivingNonDronePlayers();
            if (survivors > 0)
            {
                money = (uint)Mathf.CeilToInt((float)money / survivors);
            }
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].GetComponent<CharacterBody>();
                if (body && body.isPlayerControlled && !body.isRemoteOp && body.master)
                {
                    body.master.GiveMoney(money);
                }
            }
            return false;
        }
    }

    /// <summary>Gold orbs created during combat (Brittle Crown, Roll of Pennies) are remembered...</summary>
    [HarmonyPatch(typeof(OrbManager), nameof(OrbManager.AddOrb))]
    internal static class AddOrbPatch
    {
        private static void Prefix(Orb orb)
        {
            if (CombatGold.InCombatContext && orb is GoldOrb goldOrb)
            {
                CombatGold.MarkOrb(goldOrb);
            }
        }
    }

    /// <summary>...and don't pay out to a drone player. Executive Card cashback orbs are never marked (blocked only with None).</summary>
    [HarmonyPatch(typeof(GoldOrb), nameof(GoldOrb.OnArrival))]
    internal static class GoldOrbArrivalPatch
    {
        private static bool Prefix(GoldOrb __instance)
        {
            if (!CombatGold.BlockOrbForDrone(__instance))
            {
                return true;
            }
            HurtBox target = __instance.target;
            CharacterBody body = target && target.healthComponent ? target.healthComponent.body : null;
            return !DroneUtil.IsDronePlayer(body);
        }
    }

    /// <summary>
    /// None: catch-all for anything else that pays a drone player directly (Shrine of Blood, other mods' items, ...).
    /// </summary>
    [HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.GiveMoney))]
    internal static class GiveMoneyPatch
    {
        private static bool Prefix(CharacterMaster __instance)
        {
            return !CombatGold.BlocksAllGold || !DroneUtil.IsDronePlayer(__instance.GetBody());
        }
    }

    // --- Gold interactables (None) ---------------------------------------------------------------------------

    internal static class GoldInteractables
    {
        public static bool BlockedFor(Interactor activator)
        {
            return CombatGold.BlocksAllGold && activator && DroneUtil.IsDronePlayer(activator.GetComponent<CharacterBody>());
        }
    }

    [HarmonyPatch(typeof(BarrelInteraction), nameof(BarrelInteraction.GetInteractability))]
    internal static class BarrelInteractabilityPatch
    {
        private static void Postfix(Interactor activator, ref Interactability __result)
        {
            if (GoldInteractables.BlockedFor(activator))
            {
                __result = Interactability.Disabled;
            }
        }
    }

    /// <summary>Shrine of Blood (trades health for gold).</summary>
    [HarmonyPatch(typeof(PurchaseInteraction), nameof(PurchaseInteraction.GetInteractability))]
    internal static class BloodShrineInteractabilityPatch
    {
        private static void Postfix(PurchaseInteraction __instance, Interactor activator, ref Interactability __result)
        {
            if (__result != Interactability.Disabled && GoldInteractables.BlockedFor(activator) && __instance.GetComponent<ShrineBloodBehavior>())
            {
                __result = Interactability.Disabled;
            }
        }
    }
}
