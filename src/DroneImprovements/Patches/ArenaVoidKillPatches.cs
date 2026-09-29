using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace DroneImprovements.Patches
{
    // The host setting SpareDronesFromArenaVoidKill. When the Mithrix arena seals, MultiBodyTrigger's
    // KillAllOutsideWithVoidDeath TrueKills every body that isn't on the arena trigger's list of bodies inside. Drone
    // players inside are on that list and survive, as in vanilla (see MultiBodyTriggerCandidatesPatch). The method
    // itself runs unchanged; while it runs, TrueKill skips drone players, so those outside are spared too. A TrueKill
    // would also use up the player's Dio's Best Friend, and a spared drone can still teleport into the arena.

    /// <summary>Marks the time KillAllOutsideWithVoidDeath is running.</summary>
    [HarmonyPatch(typeof(MultiBodyTrigger), nameof(MultiBodyTrigger.KillAllOutsideWithVoidDeath))]
    internal static class ArenaVoidKillScopePatch
    {
        [ThreadStatic] private static bool isRunning;

        /// <summary>True while the arena's void kill is running.</summary>
        internal static bool IsRunning => isRunning;

        // Only our own field changes here, so nothing can throw; the finalizer restores it even when the kill throws.
        private static void Prefix(out bool __state)
        {
            __state = isRunning;
            isRunning = true;
        }

        private static void Finalizer(bool __state)
        {
            isRunning = __state;
        }
    }

    /// <summary>During the arena's void kill, a drone player's master isn't killed.</summary>
    [HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.TrueKill), typeof(GameObject), typeof(GameObject),
        typeof(DamageTypeCombo))]
    internal static class ArenaVoidKillSpareDronesPatch
    {
        private static bool failed;

        private static bool Prefix(CharacterMaster __instance)
        {
            try
            {
                return !ArenaVoidKill.ShouldSpare(__instance);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.ArenaVoidKill, "CharacterMaster.TrueKill", e);
                return true;
            }
        }
    }

    /// <summary>The logic of the patches above (see <see cref="PatchSafety"/>).</summary>
    internal static class ArenaVoidKill
    {
        /// <summary>This TrueKill is the arena's void kill of a drone player, who is spared.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ShouldSpare(CharacterMaster master)
        {
            return ArenaVoidKillScopePatch.IsRunning && HostSettings.SpareDronesFromArenaVoidKill
                && DroneUtil.IsDronePlayer(master.GetBody());
        }
    }
}
