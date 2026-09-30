using System.Collections.Generic;
using System.Collections.ObjectModel;
using RoR2;
using RoR2.Navigation;
using UnityEngine;

namespace DroneImprovements
{
    /// <summary>
    /// Teleports a drone next to the nearest living survivor. Must run on the machine with authority over the drone
    /// (its owner): RoR2 ignores server teleports for bodies a client has authority over, and TeleportHelper
    /// replicates an authority-side teleport to everyone else.
    /// </summary>
    internal static class DroneTeleport
    {
        // Distances from the target to try, in order.
        private static readonly float[] ringDistances = { 4f, 6f, 2.5f, 9f };

        // Heights above the ground at a ring point to try for flying drones, in order.
        private static readonly float[] flierHeights = { 2.5f, 1.5f, 4f };

        // Heights above a ground node to try for flying drones before the standing height.
        private static readonly float[] flierNodeLifts = { 2f, 1f };

        private const int DirectionsPerRing = 12;
        private const float DirectionStepDegrees = 30f;
        private const float MinClearanceRadius = 0.5f;
        private const float ClearanceMargin = 0.25f;
        private const float GroundProbeStartHeight = 2f;
        private const float GroundProbeDistance = 6f;
        private const float StandingLift = 0.1f;
        private const float SolidGroundSearchDistance = 150f;
        private const float NodeSearchRadius = 30f;

        // Below these squared lengths a direction is treated as zero.
        private const float MinApproachSqrMagnitude = 0.01f;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        private static readonly List<CharacterBody> extraTargets = new List<CharacterBody>();

        /// <summary>Teleports the drone next to the nearest target. False when there is nobody to go to.</summary>
        public static bool TryTeleport(CharacterBody drone)
        {
            CharacterBody target = FindNearestTarget(drone);
            if (!target)
            {
                return false;
            }
            Vector3 destinationCore = FindDestination(drone, target);

            DroneUtil.SpawnTeleportEffect(drone.gameObject, drone.corePosition, drone.radius);
            // TeleportBody places the body's foot position; keep the drone's own offset from its core.
            Vector3 coreToFoot = drone.footPosition - drone.corePosition;
            TeleportHelper.TeleportBody(drone, destinationCore + coreToFoot, forceOutOfVehicle: false);
            DroneUtil.SpawnTeleportEffect(drone.gameObject, destinationCore, drone.radius);
            return true;
        }

        /// <summary>Whether there is anybody to go to. The skill's readiness check calls this every frame.</summary>
        public static bool HasTarget(CharacterBody drone)
        {
            return FindNearestTarget(drone);
        }

        /// <summary>
        /// The nearest living, connected survivor on the drone's team, or a nearer body offered through
        /// <see cref="DroneTeleportApi"/>.
        /// </summary>
        private static CharacterBody FindNearestTarget(CharacterBody drone)
        {
            CharacterBody best = null;
            float bestSqrDistance = float.PositiveInfinity;
            Vector3 origin = drone.corePosition;

            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(drone.teamComponent.teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                // A player who left keeps their body in the world, but there is nobody to follow there.
                if (DroneUtil.IsSurvivorPlayer(body) && IsConnected(body))
                {
                    ConsiderTarget(drone, origin, body, ref best, ref bestSqrDistance);
                }
            }

            extraTargets.Clear();
            DroneTeleportApi.CollectTargets(drone, extraTargets);
            for (int i = 0; i < extraTargets.Count; i++)
            {
                ConsiderTarget(drone, origin, extraTargets[i], ref best, ref bestSqrDistance);
            }
            return best;
        }

        private static bool IsConnected(CharacterBody body)
        {
            CharacterMaster master = body.master;
            PlayerCharacterMasterController player = master ? master.playerCharacterMasterController : null;
            return player && player.isConnected;
        }

        private static void ConsiderTarget(CharacterBody drone, Vector3 origin, CharacterBody body,
            ref CharacterBody best, ref float bestSqrDistance)
        {
            if (!body || body == drone || body.isRemoteOp || !body.healthComponent || !body.healthComponent.alive)
            {
                return;
            }
            float sqrDistance = (body.corePosition - origin).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                best = body;
            }
        }

        /// <summary>
        /// Finds a spot near the target that isn't inside terrain: clear of world geometry, visible from the target
        /// (so never behind a wall or in a closed-off pocket) and above solid ground. Tries rings around the target,
        /// starting on the side the drone comes from, then the stage's ground node graph, then just above the target,
        /// and finally the target's own position.
        /// </summary>
        private static Vector3 FindDestination(CharacterBody drone, CharacterBody target)
        {
            int worldMask = LayerIndex.world.mask;
            float clearance = Mathf.Max(drone.radius, MinClearanceRadius) + ClearanceMargin;
            bool flier = !drone.characterMotor || drone.characterMotor.isFlying;
            float footToCore = drone.corePosition.y - drone.footPosition.y;

            Vector3 anchor = target.corePosition;
            Vector3 targetFoot = target.footPosition;

            Vector3 approach = drone.corePosition - anchor;
            approach.y = 0f;
            if (approach.sqrMagnitude < MinApproachSqrMagnitude)
            {
                approach = -target.transform.forward;
                approach.y = 0f;
            }
            approach = approach.sqrMagnitude > MinDirectionSqrMagnitude ? approach.normalized : Vector3.forward;

            foreach (float distance in ringDistances)
            {
                for (int step = 0; step < DirectionsPerRing; step++)
                {
                    // Alternate sides of the approach direction, widening: 0, -30, +30, -60, +60, ..., -180 degrees.
                    int side = step % 2 == 0 ? 1 : -1;
                    float angle = ((step + 1) / 2) * DirectionStepDegrees * side;
                    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * approach;
                    Vector3 ringPoint = targetFoot + direction * distance;

                    if (flier)
                    {
                        foreach (float height in flierHeights)
                        {
                            Vector3 core = ringPoint + Vector3.up * height;
                            if (IsValidSpot(core, anchor, clearance, worldMask))
                            {
                                return core;
                            }
                        }
                    }
                    else if (Physics.Raycast(ringPoint + Vector3.up * GroundProbeStartHeight, Vector3.down,
                        out RaycastHit ground, GroundProbeDistance, worldMask, QueryTriggerInteraction.Ignore))
                    {
                        Vector3 core = ground.point + Vector3.up * (footToCore + StandingLift);
                        if (IsValidSpot(core, anchor, clearance, worldMask))
                        {
                            return core;
                        }
                    }
                }
            }

            if (TryNodeGraphSpot(drone, target, flier, footToCore, clearance, worldMask, out Vector3 nodeCore))
            {
                return nodeCore;
            }

            // Last resort: the target is standing there, so its position isn't inside terrain.
            Vector3 above = anchor + Vector3.up * (target.radius + clearance);
            bool aboveIsClear = !Physics.CheckSphere(above, clearance, worldMask, QueryTriggerInteraction.Ignore)
                && !Physics.Linecast(anchor, above, worldMask, QueryTriggerInteraction.Ignore);
            return aboveIsClear ? above : anchor;
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
            // Not over a bottomless pit or outside the map.
            return Physics.Raycast(core, Vector3.down, SolidGroundSearchDistance, worldMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>A clear spot at the ground node nearest to the target.</summary>
        private static bool TryNodeGraphSpot(CharacterBody drone, CharacterBody target, bool flier, float footToCore,
            float clearance, int worldMask, out Vector3 destinationCore)
        {
            destinationCore = default;
            SceneInfo sceneInfo = SceneInfo.instance;
            NodeGraph graph = sceneInfo ? sceneInfo.GetNodeGraph(MapNodeGroup.GraphType.Ground) : null;
            if (!graph)
            {
                return false;
            }
            NodeGraph.NodeIndex node = graph.FindClosestNode(target.footPosition, drone.hullClassification,
                NodeSearchRadius);
            if (node == NodeGraph.NodeIndex.invalid || !graph.GetNodePosition(node, out Vector3 nodePosition))
            {
                return false;
            }
            if (flier)
            {
                foreach (float lift in flierNodeLifts)
                {
                    if (IsClear(nodePosition + Vector3.up * lift, clearance, worldMask, out destinationCore))
                    {
                        return true;
                    }
                }
            }
            return IsClear(nodePosition + Vector3.up * (footToCore + StandingLift), clearance, worldMask,
                out destinationCore);
        }

        private static bool IsClear(Vector3 core, float clearance, int worldMask, out Vector3 destinationCore)
        {
            destinationCore = core;
            return !Physics.CheckSphere(core, clearance, worldMask, QueryTriggerInteraction.Ignore);
        }
    }
}
