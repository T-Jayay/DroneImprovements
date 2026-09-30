using System.Collections.ObjectModel;
using RoR2;
using UnityEngine;

namespace DroneImprovements.Hooks
{
    /// <summary>
    /// The host setting DronesCanChargeHoldoutZones, for holdout zones (the teleporter, pillars, Void Fields cells,
    /// ...). A zone's charge update (HoldoutZoneController.DoUpdate) charges at a rate based on the players in the zone
    /// divided by the living players, and vanilla leaves drone players out of both. These hooks add the drone players
    /// inside the zone to both counts, so they help charge; a drone outside isn't counted anywhere, so it never slows
    /// charging. Only that update is changed: the teleporter's "discovered" check, which also counts players near it,
    /// stays vanilla.
    /// </summary>
    internal static class HoldoutZoneHooks
    {
        /// <summary>The zone whose DoUpdate is running, or null outside it.</summary>
        private static HoldoutZoneController currentZone;

        public static void Init()
        {
            On.RoR2.HoldoutZoneController.DoUpdate += HoldoutZoneController_DoUpdate;
            On.RoR2.HoldoutZoneController.CountLivingPlayers += HoldoutZoneController_CountLivingPlayers;
            On.RoR2.HoldoutZoneController.CountPlayersInRadius += HoldoutZoneController_CountPlayersInRadius;
        }

        /// <summary>Remembers which zone's charge update is running.</summary>
        private static void HoldoutZoneController_DoUpdate(On.RoR2.HoldoutZoneController.orig_DoUpdate orig,
            HoldoutZoneController self, float deltaTime)
        {
            currentZone = self;
            try
            {
                orig(self, deltaTime);
            }
            finally
            {
                // Also when DoUpdate throws, so later counts are never taken for the zone's.
                currentZone = null;
            }
        }

        /// <summary>The living players of the zone's own update: drone players inside the zone are added.</summary>
        private static int HoldoutZoneController_CountLivingPlayers(
            On.RoR2.HoldoutZoneController.orig_CountLivingPlayers orig, TeamIndex teamIndex)
        {
            return orig(teamIndex) + CountDronesInside(teamIndex);
        }

        /// <summary>
        /// The players in the zone for the zone's own update: drone players inside the zone are added.
        /// </summary>
        private static int HoldoutZoneController_CountPlayersInRadius(
            On.RoR2.HoldoutZoneController.orig_CountPlayersInRadius orig, HoldoutZoneController holdoutZoneController,
            Vector3 origin, float chargingRadiusSqr, TeamIndex teamIndex)
        {
            int result = orig(holdoutZoneController, origin, chargingRadiusSqr, teamIndex);
            // DoUpdate passes the zone itself, its position and its current radius, which is exactly what
            // IsBodyInChargingRadius uses. Calls for other zones or from elsewhere stay vanilla.
            if (holdoutZoneController == currentZone)
            {
                result += CountDronesInside(teamIndex);
            }
            return result;
        }

        /// <summary>
        /// Drone players to add to the counts of the zone whose charge update is running: the drone players of the
        /// team inside the zone's current charging radius, when the host setting is on.
        /// </summary>
        private static int CountDronesInside(TeamIndex teamIndex)
        {
            HoldoutZoneController zone = currentZone;
            if (!zone || !HostSettings.DronesCanChargeHoldoutZones)
            {
                return 0;
            }
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
