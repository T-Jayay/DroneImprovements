using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HG;
using RoR2;
using UnityEngine;
using UnityEngine.Events;

namespace DroneImprovements.Patches
{
    // The host setting IgnoreDronesForAllPlayerChecks: checks that wait until every living player is somewhere only
    // wait for the survivors. The game counts drone players as living players (Run.livingPlayerCount), so without
    // this the Mithrix arena and the escape ship wait for every drone. Drone players who are there anyway still count
    // as there, as in the base game.

    /// <summary>
    /// The Mithrix arena entrance (a player-only MultiBodyTrigger). FixedUpdate collects the players inside,
    /// drone players included, and passes Run.livingPlayerCount as the number to wait for; this lowers that number
    /// to the living survivors plus the drone players already inside. Drone players inside still get the trigger's
    /// enter and exit events and, being on its list, survive the arena's void kill, as in vanilla.
    /// </summary>
    [HarmonyPatch(typeof(MultiBodyTrigger), "SetEncounteredBodies", typeof(List<CharacterBody>), typeof(int))]
    internal static class MultiBodyTriggerCandidatesPatch
    {
        private static bool failed;

        private static void Prefix(List<CharacterBody> newEncounteredBodies, ref int candidateCount,
            bool ___playerControlledOnly)
        {
            try
            {
                candidateCount = SurvivorsOnlyChecks.ArenaCandidateCount(newEncounteredBodies, candidateCount,
                    ___playerControlledOnly);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.MithrixArena, "MultiBodyTrigger.SetEncounteredBodies",
                    e);
            }
        }
    }

    /// <summary>
    /// AllPlayersTrigger (a trigger that fires while every living player is inside it). This replaces its
    /// FixedUpdate, which runs only on the host, with a copy that counts survivors only: vanilla counts every
    /// player-controlled body inside and compares that with Run.livingPlayerCount, both within that one method, so
    /// there is no narrower place to change.
    /// </summary>
    [HarmonyPatch(typeof(AllPlayersTrigger), "FixedUpdate")]
    internal static class AllPlayersTriggerPatch
    {
        private static bool failed;

        private static bool Prefix(Queue<Collider> ___collisionQueueServer, ref bool ___triggerActiveServer,
            UnityEvent ___onTriggerStart, UnityEvent ___onTriggerEnd)
        {
            bool active;
            try
            {
                if (!SurvivorsOnlyChecks.TryCheckTrigger(___collisionQueueServer, ___triggerActiveServer, out active))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                // The check leaves the queue alone until it is done, so the original still has this tick's colliders.
                PatchSafety.ReportOnce(ref failed, FeatureNames.AllPlayersTrigger, "AllPlayersTrigger.FixedUpdate", e);
                return true;
            }
            // As in vanilla: an exception from the trigger's own events propagates.
            if (___triggerActiveServer != active)
            {
                ___triggerActiveServer = active;
                (active ? ___onTriggerStart : ___onTriggerEnd)?.Invoke();
            }
            return false;
        }
    }

    // --- The escape ship (EscapeSequenceExtractionZone, at the end of Commencement) ------------------------------
    //
    // Each FixedUpdate counts the living players and fills its playersInRadius list with the players in the zone; once
    // the list holds as many players as are alive, the ship leaves: KillAllStragglers TrueKills every master whose body
    // isn't on that list, then the run ends. These three patches work together: the ship waits for survivors only,
    // and drone players aboard still leave with it.

    /// <summary>The living players the ship waits for: survivors only.</summary>
    [HarmonyPatch(typeof(EscapeSequenceExtractionZone), "CountLivingPlayers", typeof(TeamIndex))]
    internal static class EscapeZoneLivingPlayersPatch
    {
        private static bool failed;

        private static void Postfix(TeamIndex teamIndex, ref int __result)
        {
            try
            {
                __result -= SurvivorsOnlyChecks.EscapeDronePlayers(teamIndex);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.EscapeShip,
                    "EscapeSequenceExtractionZone.CountLivingPlayers", e);
            }
        }
    }

    /// <summary>The players aboard, compared with the living players: survivors only.</summary>
    [HarmonyPatch(typeof(EscapeSequenceExtractionZone), "GetPlayersInRadius", typeof(Vector3), typeof(float),
        typeof(TeamIndex), typeof(List<CharacterBody>))]
    internal static class EscapeZonePlayersAboardPatch
    {
        private static bool failed;

        private static void Postfix(List<CharacterBody> dest)
        {
            try
            {
                SurvivorsOnlyChecks.RemoveEscapeDronePlayers(dest);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.EscapeShip,
                    "EscapeSequenceExtractionZone.GetPlayersInRadius", e);
            }
        }
    }

    /// <summary>
    /// When the ship leaves, puts the drone players aboard back on the list KillAllStragglers spares, so they leave
    /// with the ship instead of being killed (a TrueKill also uses up a Dio's Best Friend). This can't change whether
    /// the run ends: afterwards the list is only checked for being non-empty, and the next FixedUpdate rebuilds it.
    /// Its Prepare() checks that the game still has the method that tells who is aboard.
    /// </summary>
    [HarmonyPatch(typeof(EscapeSequenceExtractionZone), nameof(EscapeSequenceExtractionZone.KillAllStragglers))]
    internal static class EscapeZoneSpareDronesAboardPatch
    {
        private static bool failed;

        private static bool Prepare()
        {
            return PatchSafety.GameHasMethod(typeof(EscapeSequenceExtractionZone),
                nameof(EscapeSequenceExtractionZone.IsBodyInRadius), typeof(bool), typeof(CharacterBody));
        }

        private static void Prefix(EscapeSequenceExtractionZone __instance, List<CharacterBody> ___playersInRadius,
            TeamIndex ___teamIndex)
        {
            try
            {
                SurvivorsOnlyChecks.AddDronePlayersAboard(__instance, ___playersInRadius, ___teamIndex);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.EscapeShip,
                    "EscapeSequenceExtractionZone.KillAllStragglers", e);
            }
        }
    }

    /// <summary>
    /// EscapeSequenceExtractionZone.CountPlayersInRadius without drone players, like the checks above. Nothing in the
    /// game calls this public method; this is for other mods that do.
    /// </summary>
    [HarmonyPatch(typeof(EscapeSequenceExtractionZone), nameof(EscapeSequenceExtractionZone.CountPlayersInRadius),
        typeof(Vector3), typeof(float), typeof(TeamIndex))]
    internal static class EscapeZoneCountPlayersInRadiusPatch
    {
        private static bool failed;

        private static void Postfix(Vector3 origin, float chargingRadiusSqr, TeamIndex teamIndex, ref int __result)
        {
            try
            {
                __result -= SurvivorsOnlyChecks.EscapeDronePlayersInRadius(origin, chargingRadiusSqr, teamIndex);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.EscapeShipPlayerCount,
                    "EscapeSequenceExtractionZone.CountPlayersInRadius", e);
            }
        }
    }

    /// <summary>The logic of the patches above (see <see cref="PatchSafety"/>).</summary>
    internal static class SurvivorsOnlyChecks
    {
        private static readonly Predicate<CharacterBody> isDronePlayer = DroneUtil.IsDronePlayer;

        /// <summary>The number of players the Mithrix arena entrance waits for.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ArenaCandidateCount(List<CharacterBody> bodiesInside, int candidateCount,
            bool playerControlledOnly)
        {
            // OnDisable passes 0 (nobody to wait for), as does FixedUpdate for triggers that aren't player-only.
            if (candidateCount <= 0 || !playerControlledOnly || !HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                return candidateCount;
            }
            int survivors = DroneUtil.CountSurvivorPlayers();
            // With no living survivor there is nobody to wait for, and the trigger doesn't fire.
            return survivors > 0 ? survivors + DroneUtil.CountDronePlayers(bodiesInside) : 0;
        }

        /// <summary>
        /// AllPlayersTrigger's check with survivors only: whether every living survivor is among the colliders the
        /// trigger collected this tick. False when the original should run instead; otherwise
        /// <paramref name="active"/> is the trigger's new state. The queue is only emptied at the end, when nothing
        /// can fail any more, so the original can still run if anything before that throws.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryCheckTrigger(Queue<Collider> collisionQueue, bool wasActive, out bool active)
        {
            active = wasActive;
            if (!HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                return false;
            }
            // Vanilla does nothing without a run, and has no queue on clients (where the component disables itself)
            // and would throw without one.
            if (!Run.instance || collisionQueue == null)
            {
                return true;
            }

            int survivors = DroneUtil.CountSurvivorPlayers();
            List<CharacterBody> survivorsInside = CollectionPool<CharacterBody, List<CharacterBody>>.RentCollection();
            foreach (Collider collider in collisionQueue)
            {
                CharacterBody body = collider ? collider.GetComponent<CharacterBody>() : null;
                if (DroneUtil.IsSurvivorPlayer(body) && !survivorsInside.Contains(body))
                {
                    survivorsInside.Add(body);
                }
            }
            int inside = survivorsInside.Count;
            CollectionPool<CharacterBody, List<CharacterBody>>.ReturnCollection(survivorsInside);

            collisionQueue.Clear();
            active = inside != 0 && inside == survivors;
            return true;
        }

        /// <summary>Drone players to take off the escape ship's living players (vanilla counts every player).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int EscapeDronePlayers(TeamIndex teamIndex)
        {
            return HostSettings.IgnoreDronesForAllPlayerChecks ? DroneUtil.CountDronePlayers(teamIndex) : 0;
        }

        /// <summary>Takes the drone players off the escape ship's list of players aboard.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RemoveEscapeDronePlayers(List<CharacterBody> playersAboard)
        {
            if (HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                playersAboard.RemoveAll(isDronePlayer);
            }
        }

        /// <summary>Puts the drone players aboard back on the list of players KillAllStragglers spares.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddDronePlayersAboard(EscapeSequenceExtractionZone zone, List<CharacterBody> playersAboard,
            TeamIndex teamIndex)
        {
            if (!HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                return;
            }
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                if (DroneUtil.IsDronePlayer(body) && zone.IsBodyInRadius(body) && !playersAboard.Contains(body))
                {
                    playersAboard.Add(body);
                }
            }
        }

        /// <summary>Drone players to take off EscapeSequenceExtractionZone.CountPlayersInRadius.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int EscapeDronePlayersInRadius(Vector3 origin, float radiusSqr, TeamIndex teamIndex)
        {
            if (!HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                return 0;
            }
            int count = 0;
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                // The same test as the zone's private IsBodyInRadius: the core position within the radius.
                if (DroneUtil.IsDronePlayer(body) && (body.corePosition - origin).sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
