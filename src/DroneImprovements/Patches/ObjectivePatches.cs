using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace DroneImprovements.Patches
{
    // ---------------------------------------------------------------------------------------------
    // Holdout zones (teleporter, void cells, moon pillars, ...)
    //
    // Vanilla ignores drone players entirely, so a drone wandering off never slows charging. We keep
    // that guarantee and only ADD drones that are inside the zone, to both "players in radius" and
    // "living players". A drone outside the zone is never counted anywhere.
    // ---------------------------------------------------------------------------------------------

    [HarmonyPatch(typeof(HoldoutZoneController), "DoUpdate")]
    internal static class HoldoutZoneDoUpdatePatch
    {
        [ThreadStatic] internal static HoldoutZoneController currentZone;

        private static void Prefix(HoldoutZoneController __instance)
        {
            currentZone = __instance;
        }

        private static void Finalizer()
        {
            currentZone = null;
        }
    }

    [HarmonyPatch(typeof(HoldoutZoneController), "CountLivingPlayers")]
    internal static class HoldoutZoneCountLivingPlayersPatch
    {
        private static void Postfix(TeamIndex teamIndex, ref int __result)
        {
            HoldoutZoneController zone = HoldoutZoneDoUpdatePatch.currentZone;
            if (!zone || !PluginConfig.DronesCanChargeHoldoutZones.Value)
            {
                return;
            }
            float radius = zone.currentRadius;
            __result += HoldoutZoneHelpers.CountDronesInRadius(zone, zone.transform.position, radius * radius, teamIndex);
        }
    }

    [HarmonyPatch(typeof(HoldoutZoneController), nameof(HoldoutZoneController.CountPlayersInRadius))]
    internal static class HoldoutZoneCountPlayersInRadiusPatch
    {
        private static void Postfix(HoldoutZoneController holdoutZoneController, Vector3 origin, float chargingRadiusSqr, TeamIndex teamIndex, ref int __result)
        {
            if (!PluginConfig.DronesCanChargeHoldoutZones.Value)
            {
                return;
            }
            __result += HoldoutZoneHelpers.CountDronesInRadius(holdoutZoneController, origin, chargingRadiusSqr, teamIndex);
        }
    }

    internal static class HoldoutZoneHelpers
    {
        private static readonly Func<HoldoutZoneController, Vector3, float, CharacterBody, bool> isBodyInChargingRadius =
            AccessTools.MethodDelegate<Func<HoldoutZoneController, Vector3, float, CharacterBody, bool>>(
                AccessTools.Method(typeof(HoldoutZoneController), "IsBodyInChargingRadius",
                    new[] { typeof(HoldoutZoneController), typeof(Vector3), typeof(float), typeof(CharacterBody) }));

        public static int CountDronesInRadius(HoldoutZoneController zone, Vector3 origin, float chargingRadiusSqr, TeamIndex teamIndex)
        {
            int count = 0;
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                if (DroneUtil.IsDronePlayer(body) && isBodyInChargingRadius(zone, origin, chargingRadiusSqr, body))
                {
                    count++;
                }
            }
            return count;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // "All players must be present" triggers. Vanilla compares against Run.livingPlayerCount, which
    // includes drone players, so e.g. the Mithrix arena cutscene waits for drones. Only survivors count.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Mithrix arena entrance (and any other map that uses it).</summary>
    [HarmonyPatch(typeof(MultiBodyTrigger), "FixedUpdate")]
    internal static class MultiBodyTriggerFixedUpdatePatch
    {
        private static readonly Action<MultiBodyTrigger, List<CharacterBody>, int> setEncounteredBodies =
            AccessTools.MethodDelegate<Action<MultiBodyTrigger, List<CharacterBody>, int>>(
                AccessTools.Method(typeof(MultiBodyTrigger), "SetEncounteredBodies"));

        private static readonly List<CharacterBody> buffer = new List<CharacterBody>();

        private static bool Prefix(MultiBodyTrigger __instance, Queue<Collider> ___collisionsQueue, List<CharacterBody> ___encounteredBodies)
        {
            if (!PluginConfig.IgnoreDronesForAllPlayerChecks.Value || !__instance.playerControlledOnly)
            {
                return true;
            }
            if (___collisionsQueue.Count == 0 && ___encounteredBodies.Count == 0)
            {
                return false;
            }

            buffer.Clear();
            while (___collisionsQueue.Count > 0)
            {
                Collider collider = ___collisionsQueue.Dequeue();
                if (!collider)
                {
                    continue;
                }
                CharacterBody body = collider.GetComponent<CharacterBody>();
                if (body && body.isPlayerControlled && !body.isRemoteOp && !buffer.Contains(body))
                {
                    buffer.Add(body);
                }
            }
            int candidateCount = Run.instance ? DroneUtil.CountLivingNonDronePlayers() : 0;
            setEncounteredBodies(__instance, buffer, candidateCount);
            buffer.Clear();
            return false;
        }
    }

    /// <summary>
    /// When the arena seals, everyone outside is void-killed. A drone left outside would be TrueKilled, which
    /// also burns the player's Dio's Best Friend. Spare drones instead (they can teleport to an ally).
    /// </summary>
    [HarmonyPatch(typeof(MultiBodyTrigger), nameof(MultiBodyTrigger.KillAllOutsideWithVoidDeath))]
    internal static class MultiBodyTriggerVoidKillPatch
    {
        private static bool Prefix(List<CharacterBody> ___encounteredBodies)
        {
            if (!PluginConfig.SpareDronesFromArenaVoidKill.Value)
            {
                return true;
            }
            GameObject killer = BodyCatalog.FindBodyPrefab("BrotherBody");
            List<CharacterBody> bodies = new List<CharacterBody>(CharacterBody.readOnlyInstancesList);
            foreach (CharacterBody body in bodies)
            {
                if (!body || ___encounteredBodies.Contains(body) || DroneUtil.IsDronePlayer(body))
                {
                    continue;
                }
                CharacterMaster master = body.master;
                if (!master)
                {
                    continue;
                }
                try
                {
                    master.TrueKill(killer, null, DamageType.VoidDeath);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
            return false;
        }
    }

    /// <summary>Generic "all players inside" trigger.</summary>
    [HarmonyPatch(typeof(AllPlayersTrigger), "FixedUpdate")]
    internal static class AllPlayersTriggerFixedUpdatePatch
    {
        private static readonly List<CharacterBody> buffer = new List<CharacterBody>();

        private static bool Prefix(AllPlayersTrigger __instance, Queue<Collider> ___collisionQueueServer, ref bool ___triggerActiveServer)
        {
            if (!PluginConfig.IgnoreDronesForAllPlayerChecks.Value)
            {
                return true;
            }
            if (!Run.instance || ___collisionQueueServer == null)
            {
                return false;
            }

            buffer.Clear();
            while (___collisionQueueServer.Count > 0)
            {
                Collider collider = ___collisionQueueServer.Dequeue();
                if (!collider)
                {
                    continue;
                }
                CharacterBody body = collider.GetComponent<CharacterBody>();
                if (body && body.isPlayerControlled && !body.isRemoteOp && !buffer.Contains(body))
                {
                    buffer.Add(body);
                }
            }
            int inside = buffer.Count;
            buffer.Clear();

            bool active = inside != 0 && inside >= DroneUtil.CountLivingNonDronePlayers();
            if (___triggerActiveServer != active)
            {
                ___triggerActiveServer = active;
                (active ? __instance.onTriggerStart : __instance.onTriggerEnd)?.Invoke();
            }
            return false;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Escape ship at the end of Commencement: the ship only leaves once every living player is in the
    // extraction zone, and vanilla counts drones. Drones neither need to be there nor count when they are.
    // ---------------------------------------------------------------------------------------------

    [HarmonyPatch(typeof(EscapeSequenceExtractionZone), "CountLivingPlayers")]
    internal static class EscapeZoneCountLivingPlayersPatch
    {
        private static bool Prefix(TeamIndex teamIndex, ref int __result)
        {
            if (!PluginConfig.IgnoreDronesForAllPlayerChecks.Value)
            {
                return true;
            }
            int count = 0;
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                if (body && body.isPlayerControlled && !body.isRemoteOp)
                {
                    count++;
                }
            }
            __result = count;
            return false;
        }
    }

    [HarmonyPatch(typeof(EscapeSequenceExtractionZone), "GetPlayersInRadius")]
    internal static class EscapeZoneGetPlayersInRadiusPatch
    {
        private static void Postfix(List<CharacterBody> dest)
        {
            if (PluginConfig.IgnoreDronesForAllPlayerChecks.Value)
            {
                dest.RemoveAll(DroneUtil.IsDronePlayer);
            }
        }
    }

    [HarmonyPatch(typeof(EscapeSequenceExtractionZone), nameof(EscapeSequenceExtractionZone.CountPlayersInRadius))]
    internal static class EscapeZoneCountPlayersInRadiusPatch
    {
        private static void Postfix(Vector3 origin, float chargingRadiusSqr, TeamIndex teamIndex, ref int __result)
        {
            if (!PluginConfig.IgnoreDronesForAllPlayerChecks.Value)
            {
                return;
            }
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                if (DroneUtil.IsDronePlayer(body) && (body.corePosition - origin).sqrMagnitude <= chargingRadiusSqr)
                {
                    __result--;
                }
            }
        }
    }
}
