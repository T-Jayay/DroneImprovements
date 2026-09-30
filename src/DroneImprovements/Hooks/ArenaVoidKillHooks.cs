using RoR2;
using UnityEngine;

namespace DroneImprovements.Hooks
{
    /// <summary>
    /// The host setting SpareDronesFromArenaVoidKill. When the Mithrix arena seals, MultiBodyTrigger's
    /// KillAllOutsideWithVoidDeath TrueKills every body that isn't on the arena trigger's list of bodies inside. Drone
    /// players inside are on that list and survive, as in vanilla (see AllPlayersCheckHooks'
    /// MultiBodyTrigger_SetEncounteredBodies). The method itself runs unchanged; while it runs, TrueKill skips drone
    /// players, so those outside are spared too. A TrueKill would also use up the player's Dio's Best Friend, and a
    /// spared drone can still teleport into the arena.
    /// </summary>
    internal static class ArenaVoidKillHooks
    {
        /// <summary>True while the arena's void kill is running.</summary>
        private static bool isVoidKillRunning;

        public static void Init()
        {
            On.RoR2.MultiBodyTrigger.KillAllOutsideWithVoidDeath += MultiBodyTrigger_KillAllOutsideWithVoidDeath;
            On.RoR2.CharacterMaster.TrueKill_GameObject_GameObject_DamageTypeCombo +=
                CharacterMaster_TrueKill_GameObject_GameObject_DamageTypeCombo;
        }

        /// <summary>Marks the time KillAllOutsideWithVoidDeath is running.</summary>
        private static void MultiBodyTrigger_KillAllOutsideWithVoidDeath(
            On.RoR2.MultiBodyTrigger.orig_KillAllOutsideWithVoidDeath orig, MultiBodyTrigger self)
        {
            isVoidKillRunning = true;
            try
            {
                orig(self);
            }
            finally
            {
                // Also when the kill throws, so later TrueKills are never taken for the arena's.
                isVoidKillRunning = false;
            }
        }

        /// <summary>
        /// During the arena's void kill, a drone player's master isn't killed: orig isn't called for it, which also
        /// skips the hooks other mods added to TrueKill before this one.
        /// </summary>
        private static void CharacterMaster_TrueKill_GameObject_GameObject_DamageTypeCombo(
            On.RoR2.CharacterMaster.orig_TrueKill_GameObject_GameObject_DamageTypeCombo orig, CharacterMaster self,
            GameObject killerOverride, GameObject inflictorOverride, DamageTypeCombo damageTypeOverride)
        {
            if (isVoidKillRunning && HostSettings.SpareDronesFromArenaVoidKill
                && DroneUtil.IsDronePlayer(self.GetBody()))
            {
                return;
            }
            orig(self, killerOverride, inflictorOverride, damageTypeOverride);
        }
    }
}
