using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace DroneImprovements.DevTools
{
    /// <summary>Lets the game treat the stand-ins as players where the drone flow needs it.</summary>
    internal static class StandInHooks
    {
        public static void Init()
        {
            On.RoR2.CameraRigControllerSpectateControls.CanUserSpectateBody +=
                CameraRigControllerSpectateControls_CanUserSpectateBody;
            On.RoR2.HoldoutZoneController.CountLivingPlayers += HoldoutZoneController_CountLivingPlayers;
            On.RoR2.HoldoutZoneController.CountPlayersInRadius += HoldoutZoneController_CountPlayersInRadius;
        }

        /// <summary>
        /// Spectating only targets bodies owned by a NetworkUser, so solo there is nothing to spectate after dying,
        /// which also means no spectator HUD and no Remote Operation menu. Lets the stand-ins be spectated.
        /// </summary>
        private static bool CameraRigControllerSpectateControls_CanUserSpectateBody(
            On.RoR2.CameraRigControllerSpectateControls.orig_CanUserSpectateBody orig, NetworkUser viewer,
            CharacterBody body)
        {
            return orig(viewer, body) || StandIn.Is(body);
        }

        // Stand-ins are AI, which holdout zones ignore. These two count them as players, so a zone behaves as it would
        // with another living survivor in the game. They only work together.

        /// <summary>Living stand-ins on the zone's team count as living players.</summary>
        private static int HoldoutZoneController_CountLivingPlayers(
            On.RoR2.HoldoutZoneController.orig_CountLivingPlayers orig, TeamIndex teamIndex)
        {
            int result = orig(teamIndex);
            IReadOnlyList<CharacterBody> standIns = DevToolsPlugin.StandIns;
            for (int i = 0; i < standIns.Count; i++)
            {
                if (StandIn.IsAliveOnTeam(standIns[i], teamIndex))
                {
                    result++;
                }
            }
            return result;
        }

        /// <summary>Living stand-ins inside the radius count as players in the zone.</summary>
        private static int HoldoutZoneController_CountPlayersInRadius(
            On.RoR2.HoldoutZoneController.orig_CountPlayersInRadius orig, HoldoutZoneController holdoutZoneController,
            Vector3 origin, float chargingRadiusSqr, TeamIndex teamIndex)
        {
            int result = orig(holdoutZoneController, origin, chargingRadiusSqr, teamIndex);
            IReadOnlyList<CharacterBody> standIns = DevToolsPlugin.StandIns;
            for (int i = 0; i < standIns.Count; i++)
            {
                CharacterBody body = standIns[i];
                // The game's own test, so the zone's shape is respected.
                if (StandIn.IsAliveOnTeam(body, teamIndex) && HoldoutZoneController.IsPointInChargingRadius(
                    holdoutZoneController, origin, chargingRadiusSqr, body.corePosition))
                {
                    result++;
                }
            }
            return result;
        }
    }

    /// <summary>Checks on the stand-in players.</summary>
    internal static class StandIn
    {
        public static bool Is(CharacterBody body)
        {
            if (!body)
            {
                return false;
            }
            IReadOnlyList<CharacterBody> standIns = DevToolsPlugin.StandIns;
            for (int i = 0; i < standIns.Count; i++)
            {
                if (standIns[i] == body)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsAliveOnTeam(CharacterBody body, TeamIndex teamIndex)
        {
            return body && body.teamComponent.teamIndex == teamIndex && body.healthComponent
                && body.healthComponent.alive;
        }
    }
}
