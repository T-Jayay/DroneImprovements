using System.Collections.Generic;
using RoR2;
using RoR2.Navigation;
using UnityEngine;

namespace DroneImprovements
{
    /// <summary>
    /// Teleports a drone to the nearest living survivor. Must run on the machine with authority over the drone
    /// (its owner): RoR2 ignores server teleports for bodies a client has authority over, and TeleportHelper
    /// replicates an authority-side teleport to everyone else.
    /// </summary>
    internal static class DroneTeleport
    {
        private static readonly List<CharacterBody> candidates = new List<CharacterBody>();
        private static readonly float[] ringDistances = { 4f, 6f, 2.5f, 9f };
        private static readonly float[] flierHeights = { 2.5f, 1.5f, 4f };
        private static readonly float[] groundHeights = { 0f };
        private static GameObject teleportEffectPrefab;

        public static bool TryTeleport(CharacterBody drone, out string failReason)
        {
            CharacterBody target = FindNearestTarget(drone);
            if (!target)
            {
                failReason = "No living player to teleport to.";
                return false;
            }
            if (!TryFindDestination(drone, target, out Vector3 destinationCore))
            {
                failReason = "Couldn't find a safe spot near " + GetName(target) + ".";
                return false;
            }

            SpawnEffect(drone.corePosition, drone.radius);
            Vector3 coreToFoot = drone.footPosition - drone.corePosition;
            TeleportHelper.TeleportBody(drone, destinationCore + coreToFoot, false);
            SpawnEffect(destinationCore, drone.radius);

            failReason = null;
            return true;
        }

        public static bool HasTarget(CharacterBody drone)
        {
            return FindNearestTarget(drone);
        }

        private static CharacterBody FindNearestTarget(CharacterBody drone)
        {
            candidates.Clear();
            foreach (PlayerCharacterMasterController pcmc in PlayerCharacterMasterController.instances)
            {
                if (pcmc && pcmc.master)
                {
                    candidates.Add(pcmc.master.GetBody());
                }
            }
            DroneTeleportApi.InvokeCollectAdditionalTargets(drone, candidates);

            CharacterBody best = null;
            float bestSqr = float.PositiveInfinity;
            Vector3 origin = drone.corePosition;
            foreach (CharacterBody body in candidates)
            {
                if (!body || body == drone || body.isRemoteOp || !body.healthComponent || !body.healthComponent.alive)
                {
                    continue;
                }
                float sqr = (body.corePosition - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = body;
                }
            }
            candidates.Clear();
            return best;
        }

        /// <summary>
        /// Finds a spot near the target that is not inside terrain. Candidates must be clear of world geometry,
        /// visible from the target (so never behind a wall or inside a closed-off pocket) and above solid ground.
        /// Falls back to the stage's navigation nodes, then to the target's own position.
        /// </summary>
        private static bool TryFindDestination(CharacterBody drone, CharacterBody target, out Vector3 destinationCore)
        {
            int worldMask = LayerIndex.world.mask;
            float clearance = Mathf.Max(drone.radius, 0.5f) + 0.25f;
            bool flier = !drone.characterMotor || drone.characterMotor.isFlying;
            float footToCore = drone.corePosition.y - drone.footPosition.y;

            Vector3 anchor = target.corePosition;
            Vector3 targetFoot = target.footPosition;

            // Try the side of the target the drone is coming from first.
            Vector3 approach = drone.corePosition - anchor;
            approach.y = 0f;
            if (approach.sqrMagnitude < 0.01f)
            {
                approach = -target.transform.forward;
                approach.y = 0f;
            }
            approach = approach.sqrMagnitude > 0.0001f ? approach.normalized : Vector3.forward;

            float[] heights = flier ? flierHeights : groundHeights;
            foreach (float distance in ringDistances)
            {
                for (int step = 0; step < 12; step++)
                {
                    // 0, +30, -30, +60, -60, ... 180
                    int sign = (step % 2 == 0) ? 1 : -1;
                    float angle = ((step + 1) / 2) * 30f * sign;
                    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * approach;
                    Vector3 ringPoint = targetFoot + direction * distance;

                    foreach (float height in heights)
                    {
                        Vector3 core;
                        if (flier)
                        {
                            core = ringPoint + Vector3.up * height;
                        }
                        else
                        {
                            Vector3 probe = ringPoint + Vector3.up * 2f;
                            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 6f, worldMask, QueryTriggerInteraction.Ignore))
                            {
                                continue;
                            }
                            core = ground.point + Vector3.up * (footToCore + 0.1f);
                        }

                        if (IsValidSpot(core, anchor, clearance, worldMask))
                        {
                            destinationCore = core;
                            return true;
                        }
                    }
                }
            }

            if (TryNodeGraphFallback(drone, target, flier, footToCore, clearance, worldMask, out destinationCore))
            {
                return true;
            }

            // Last resort: the target is standing there, so it is by definition not inside terrain.
            Vector3 above = anchor + Vector3.up * (target.radius + clearance);
            destinationCore = !Physics.CheckSphere(above, clearance, worldMask, QueryTriggerInteraction.Ignore) && !Physics.Linecast(anchor, above, worldMask, QueryTriggerInteraction.Ignore)
                ? above
                : anchor;
            return true;
        }

        private static bool IsValidSpot(Vector3 core, Vector3 anchor, float clearance, int worldMask)
        {
            if (Physics.CheckSphere(core, clearance, worldMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (Physics.Linecast(anchor, core, worldMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            // Not hovering over a bottomless pit / outside the map.
            return Physics.Raycast(core, Vector3.down, 150f, worldMask, QueryTriggerInteraction.Ignore);
        }

        private static bool TryNodeGraphFallback(CharacterBody drone, CharacterBody target, bool flier, float footToCore, float clearance, int worldMask, out Vector3 destinationCore)
        {
            destinationCore = default;
            SceneInfo sceneInfo = SceneInfo.instance;
            NodeGraph graph = sceneInfo ? sceneInfo.GetNodeGraph(MapNodeGroup.GraphType.Ground) : null;
            if (!graph)
            {
                return false;
            }
            NodeGraph.NodeIndex node = graph.FindClosestNode(target.footPosition, drone.hullClassification, 30f);
            if (node == NodeGraph.NodeIndex.invalid || !graph.GetNodePosition(node, out Vector3 nodePosition))
            {
                return false;
            }
            float[] lifts = flier ? new[] { 2f, 1f, footToCore + 0.1f } : new[] { footToCore + 0.1f };
            foreach (float lift in lifts)
            {
                Vector3 core = nodePosition + Vector3.up * lift;
                if (!Physics.CheckSphere(core, clearance, worldMask, QueryTriggerInteraction.Ignore))
                {
                    destinationCore = core;
                    return true;
                }
            }
            return false;
        }

        private static void SpawnEffect(Vector3 position, float radius)
        {
            if (!teleportEffectPrefab)
            {
                teleportEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/TeleportOutBoom");
            }
            if (teleportEffectPrefab)
            {
                EffectManager.SpawnEffect(teleportEffectPrefab, new EffectData
                {
                    origin = position,
                    rotation = Quaternion.identity,
                    scale = radius
                }, transmit: true);
            }
        }

        private static string GetName(CharacterBody body)
        {
            return body.GetUserName() ?? body.GetDisplayName();
        }
    }
}
