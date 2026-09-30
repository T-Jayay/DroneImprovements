using System.Collections.Generic;
using System.Collections.ObjectModel;
using HG;
using RoR2;
using UnityEngine;

namespace DroneImprovements.Hooks
{
    /// <summary>
    /// The host setting IgnoreDronesForAllPlayerChecks: checks that wait until every living player is somewhere only
    /// wait for the survivors. The game counts drone players as living players (Run.livingPlayerCount), so without
    /// this the Mithrix arena and the escape ship wait for every drone. Drone players who are there anyway still count
    /// as there, as in the base game.
    /// </summary>
    internal static class AllPlayersCheckHooks
    {
        public static void Init()
        {
            On.RoR2.MultiBodyTrigger.SetEncounteredBodies += MultiBodyTrigger_SetEncounteredBodies;
            On.RoR2.AllPlayersTrigger.FixedUpdate += AllPlayersTrigger_FixedUpdate;
            On.RoR2.EscapeSequenceExtractionZone.CountLivingPlayers += EscapeSequenceExtractionZone_CountLivingPlayers;
            On.RoR2.EscapeSequenceExtractionZone.GetPlayersInRadius += EscapeSequenceExtractionZone_GetPlayersInRadius;
            On.RoR2.EscapeSequenceExtractionZone.KillAllStragglers += EscapeSequenceExtractionZone_KillAllStragglers;
        }

        /// <summary>
        /// The Mithrix arena entrance (a player-only MultiBodyTrigger). FixedUpdate collects the players inside,
        /// drone players included, and passes Run.livingPlayerCount as the number to wait for; this lowers that number
        /// to the living survivors plus the drone players already inside. Drone players inside still get the trigger's
        /// enter and exit events and, being on its list, survive the arena's void kill, as in vanilla.
        /// </summary>
        private static void MultiBodyTrigger_SetEncounteredBodies(
            On.RoR2.MultiBodyTrigger.orig_SetEncounteredBodies orig, MultiBodyTrigger self,
            List<CharacterBody> newEncounteredBodies, int candidateCount)
        {
            // OnDisable passes 0 (nobody to wait for), as does FixedUpdate for triggers that aren't player-only.
            if (candidateCount > 0 && self.playerControlledOnly && HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                int survivors = DroneUtil.CountSurvivorPlayers();
                // With no living survivor there is nobody to wait for, and the trigger doesn't fire.
                candidateCount = survivors > 0 ? survivors + DroneUtil.CountDronePlayers(newEncounteredBodies) : 0;
            }
            orig(self, newEncounteredBodies, candidateCount);
        }

        /// <summary>
        /// AllPlayersTrigger (a trigger that fires while every living player is inside it). When the setting is on,
        /// this replaces its FixedUpdate, which runs only on the host, with a copy that counts survivors only: vanilla
        /// counts every player-controlled body inside and compares that with Run.livingPlayerCount, both within that
        /// one method, so there is no narrower place to change.
        /// </summary>
        private static void AllPlayersTrigger_FixedUpdate(On.RoR2.AllPlayersTrigger.orig_FixedUpdate orig,
            AllPlayersTrigger self)
        {
            if (!HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                orig(self);
                return;
            }
            // The replacement: orig isn't called, which also skips the hooks other mods added to this method before
            // this one.
            if (!Run.instance)
            {
                return;
            }
            int survivorsInside = 0;
            List<CharacterBody> bodiesInside = CollectionPool<CharacterBody, List<CharacterBody>>.RentCollection();
            Queue<Collider> collisionQueue = self.collisionQueueServer;
            while (collisionQueue.Count > 0)
            {
                Collider collider = collisionQueue.Dequeue();
                CharacterBody body = collider ? collider.GetComponent<CharacterBody>() : null;
                if (DroneUtil.IsSurvivorPlayer(body) && !bodiesInside.Contains(body))
                {
                    bodiesInside.Add(body);
                    survivorsInside++;
                }
            }
            CollectionPool<CharacterBody, List<CharacterBody>>.ReturnCollection(bodiesInside);

            bool active = survivorsInside == DroneUtil.CountSurvivorPlayers() && survivorsInside != 0;
            if (self.triggerActiveServer != active)
            {
                self.triggerActiveServer = active;
                (active ? self.onTriggerStart : self.onTriggerEnd)?.Invoke();
            }
        }

        // --- The escape ship (EscapeSequenceExtractionZone, at the end of Commencement) ------------------------------
        //
        // Each FixedUpdate counts the living players and fills its playersInRadius list with the players in the zone;
        // once the list holds as many players as are alive, the ship leaves: KillAllStragglers TrueKills every master
        // whose body isn't on that list, then the run ends. These three hooks work together: the ship waits for
        // survivors only, and drone players aboard still leave with it.

        /// <summary>The living players the ship waits for: survivors only.</summary>
        private static int EscapeSequenceExtractionZone_CountLivingPlayers(
            On.RoR2.EscapeSequenceExtractionZone.orig_CountLivingPlayers orig, TeamIndex teamIndex)
        {
            int result = orig(teamIndex);
            if (HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                result -= DroneUtil.CountDronePlayers(teamIndex);
            }
            return result;
        }

        /// <summary>The players aboard, compared with the living players: survivors only.</summary>
        private static int EscapeSequenceExtractionZone_GetPlayersInRadius(
            On.RoR2.EscapeSequenceExtractionZone.orig_GetPlayersInRadius orig, EscapeSequenceExtractionZone self,
            Vector3 origin, float chargingRadiusSqr, TeamIndex teamIndex, List<CharacterBody> dest)
        {
            int result = orig(self, origin, chargingRadiusSqr, teamIndex, dest);
            if (HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                dest.RemoveAll(DroneUtil.IsDronePlayer);
            }
            return result;
        }

        /// <summary>
        /// When the ship leaves, puts the drone players aboard back on the list KillAllStragglers spares, so they
        /// leave with the ship instead of being killed (a TrueKill also uses up a Dio's Best Friend). This can't change
        /// whether the run ends: afterwards the list is only checked for being non-empty, and the next FixedUpdate
        /// rebuilds it.
        /// </summary>
        private static void EscapeSequenceExtractionZone_KillAllStragglers(
            On.RoR2.EscapeSequenceExtractionZone.orig_KillAllStragglers orig, EscapeSequenceExtractionZone self)
        {
            if (HostSettings.IgnoreDronesForAllPlayerChecks)
            {
                List<CharacterBody> playersAboard = self.playersInRadius;
                ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(self.teamIndex);
                for (int i = 0; i < members.Count; i++)
                {
                    CharacterBody body = members[i].body;
                    if (DroneUtil.IsDronePlayer(body) && self.IsBodyInRadius(body) && !playersAboard.Contains(body))
                    {
                        playersAboard.Add(body);
                    }
                }
            }
            orig(self);
        }
    }
}
