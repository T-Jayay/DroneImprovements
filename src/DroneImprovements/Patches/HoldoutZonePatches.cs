using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace DroneImprovements.Patches
{
    // The host setting DronesCanChargeHoldoutZones, for holdout zones (the teleporter, pillars, Void Fields cells,
    // ...). A zone's charge update (HoldoutZoneController.DoUpdate) charges at a rate based on the players in the zone
    // divided by the living players, and vanilla leaves drone players out of both. These patches add the drone players
    // inside the zone to both counts, so they help charge; a drone outside isn't counted anywhere, so it never slows
    // charging. Only that update is changed: the teleporter's "discovered" check, which also counts players near it,
    // stays vanilla.

    /// <summary>
    /// Remembers which zone's charge update is running. Its Prepare() checks, for the whole feature, that the game
    /// still has the method the drone count uses.
    /// </summary>
    [HarmonyPatch(typeof(HoldoutZoneController), "DoUpdate", typeof(float))]
    internal static class HoldoutZoneUpdatePatch
    {
        [ThreadStatic] private static HoldoutZoneController currentZone;

        /// <summary>The zone whose DoUpdate is running, or null outside it.</summary>
        internal static HoldoutZoneController CurrentZone => currentZone;

        private static bool Prepare()
        {
            return PatchSafety.GameHasMethod(typeof(HoldoutZoneController),
                nameof(HoldoutZoneController.IsBodyInChargingRadius), typeof(bool), typeof(CharacterBody));
        }

        // Only our own field changes here, so nothing can throw; the finalizer restores it even when DoUpdate throws.
        private static void Prefix(HoldoutZoneController __instance, out HoldoutZoneController __state)
        {
            __state = currentZone;
            currentZone = __instance;
        }

        private static void Finalizer(HoldoutZoneController __state)
        {
            currentZone = __state;
        }
    }

    /// <summary>The living players of the zone's own update: drone players inside the zone are added.</summary>
    [HarmonyPatch(typeof(HoldoutZoneController), "CountLivingPlayers", typeof(TeamIndex))]
    internal static class HoldoutZoneLivingPlayersPatch
    {
        private static bool failed;

        private static void Postfix(TeamIndex teamIndex, ref int __result)
        {
            try
            {
                __result += HoldoutZoneDrones.CountForLivingPlayers(teamIndex);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.HoldoutZones,
                    "HoldoutZoneController.CountLivingPlayers", e);
            }
        }
    }

    /// <summary>The players in the zone for the zone's own update: drone players inside the zone are added.</summary>
    [HarmonyPatch(typeof(HoldoutZoneController), nameof(HoldoutZoneController.CountPlayersInRadius),
        typeof(HoldoutZoneController), typeof(Vector3), typeof(float), typeof(TeamIndex))]
    internal static class HoldoutZonePlayersInRadiusPatch
    {
        private static bool failed;

        private static void Postfix(HoldoutZoneController holdoutZoneController, TeamIndex teamIndex, ref int __result)
        {
            try
            {
                __result += HoldoutZoneDrones.CountForPlayersInRadius(holdoutZoneController, teamIndex);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.HoldoutZones,
                    "HoldoutZoneController.CountPlayersInRadius", e);
            }
        }
    }

    /// <summary>Counts the drone players inside a holdout zone (see <see cref="PatchSafety"/>).</summary>
    internal static class HoldoutZoneDrones
    {
        /// <summary>Drone players to add to the living players, in the charge update of a zone.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CountForLivingPlayers(TeamIndex teamIndex)
        {
            HoldoutZoneController zone = HoldoutZoneUpdatePatch.CurrentZone;
            return zone && HostSettings.DronesCanChargeHoldoutZones ? CountInside(zone, teamIndex) : 0;
        }

        /// <summary>Drone players to add to the players in the zone, in the charge update of that zone.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CountForPlayersInRadius(HoldoutZoneController holdoutZoneController, TeamIndex teamIndex)
        {
            // DoUpdate passes the zone itself, its position and its current radius, which is exactly what
            // IsBodyInChargingRadius uses. Calls for other zones or from elsewhere stay vanilla.
            HoldoutZoneController zone = HoldoutZoneUpdatePatch.CurrentZone;
            return zone && ReferenceEquals(zone, holdoutZoneController) && HostSettings.DronesCanChargeHoldoutZones
                ? CountInside(zone, teamIndex)
                : 0;
        }

        /// <summary>Drone players of the team inside the zone's current charging radius.</summary>
        private static int CountInside(HoldoutZoneController zone, TeamIndex teamIndex)
        {
            int count = 0;
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                if (DroneUtil.IsDronePlayer(body) && zone.IsBodyInChargingRadius(body))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
