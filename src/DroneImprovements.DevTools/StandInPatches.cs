using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace DroneImprovements.DevTools
{
    /// <summary>
    /// Spectating only targets bodies owned by a NetworkUser, so solo there is nothing to spectate after dying, which
    /// also means no spectator HUD and no Remote Operation menu. Lets the stand-ins be spectated.
    /// </summary>
    [HarmonyPatch(typeof(CameraRigControllerSpectateControls), "CanUserSpectateBody", typeof(NetworkUser),
        typeof(CharacterBody))]
    internal static class StandInSpectatePatch
    {
        private static void Postfix(CharacterBody body, ref bool __result)
        {
            if (!__result && StandIn.Is(body))
            {
                __result = true;
            }
        }
    }

    // Stand-ins are AI, which holdout zones ignore. These two count them as players, so a zone behaves as it would
    // with another living survivor in the game. They only work together.

    /// <summary>Living stand-ins on the zone's team count as living players.</summary>
    [HarmonyPatch(typeof(HoldoutZoneController), "CountLivingPlayers", typeof(TeamIndex))]
    internal static class StandInLivingPlayersPatch
    {
        private static void Postfix(TeamIndex teamIndex, ref int __result)
        {
            IReadOnlyList<CharacterBody> standIns = DevToolsPlugin.StandIns;
            for (int i = 0; i < standIns.Count; i++)
            {
                if (StandIn.IsAliveOnTeam(standIns[i], teamIndex))
                {
                    __result++;
                }
            }
        }
    }

    /// <summary>Living stand-ins inside the radius count as players in the zone.</summary>
    [HarmonyPatch(typeof(HoldoutZoneController), nameof(HoldoutZoneController.CountPlayersInRadius),
        typeof(HoldoutZoneController), typeof(Vector3), typeof(float), typeof(TeamIndex))]
    internal static class StandInPlayersInRadiusPatch
    {
        // The game's own (private) test, so the zone's shape is respected.
        private static Func<HoldoutZoneController, Vector3, float, Vector3, bool> isPointInChargingRadius;

        private static bool Prepare()
        {
            if (isPointInChargingRadius != null)
            {
                return true;
            }
            MethodInfo method = AccessTools.Method(typeof(HoldoutZoneController), "IsPointInChargingRadius",
                new[] { typeof(HoldoutZoneController), typeof(Vector3), typeof(float), typeof(Vector3) });
            if (method == null)
            {
                DevToolsPlugin.Log.LogWarning("HoldoutZoneController.IsPointInChargingRadius is gone.");
                return false;
            }
            isPointInChargingRadius =
                AccessTools.MethodDelegate<Func<HoldoutZoneController, Vector3, float, Vector3, bool>>(method);
            return true;
        }

        private static void Postfix(HoldoutZoneController holdoutZoneController, Vector3 origin,
            float chargingRadiusSqr, TeamIndex teamIndex, ref int __result)
        {
            IReadOnlyList<CharacterBody> standIns = DevToolsPlugin.StandIns;
            for (int i = 0; i < standIns.Count; i++)
            {
                CharacterBody body = standIns[i];
                if (StandIn.IsAliveOnTeam(body, teamIndex)
                    && isPointInChargingRadius(holdoutZoneController, origin, chargingRadiusSqr, body.corePosition))
                {
                    __result++;
                }
            }
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
