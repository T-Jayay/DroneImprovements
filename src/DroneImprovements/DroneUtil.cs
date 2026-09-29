using System.Collections.Generic;
using System.Collections.ObjectModel;
using RoR2;
using UnityEngine;

namespace DroneImprovements
{
    /// <summary>Telling drone players and survivors apart, and effects shared by the drone abilities.</summary>
    internal static class DroneUtil
    {
        /// <summary>A body a player is remote-operating: the base game's Remote Operation drone.</summary>
        public static bool IsDronePlayer(CharacterBody body)
        {
            return body && body.isPlayerControlled && body.isRemoteOp;
        }

        /// <summary>A player's own body: player-controlled and not a Remote Operation drone.</summary>
        public static bool IsSurvivorPlayer(CharacterBody body)
        {
            return body && body.isPlayerControlled && !body.isRemoteOp;
        }

        /// <summary>
        /// Run.livingPlayerCount without the drone players: the players whose current body is a survivor body. That
        /// is the count the checks this mod changes compare against, and like it, this includes bodies still playing
        /// their death animation and players who left the game (their body stays in the world). Player bodies are
        /// always on the player team.
        /// </summary>
        public static int CountSurvivorPlayers()
        {
            if (!Run.instance)
            {
                return 0;
            }
            int count = 0;
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(TeamIndex.Player);
            for (int i = 0; i < members.Count; i++)
            {
                CharacterBody body = members[i].body;
                if (IsSurvivorPlayer(body) && IsCurrentBody(body))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// The body is its master's current one, like the one body per player that Run.livingPlayerCount counts (not,
        /// for example, a body the game is still destroying after replacing it with a new one).
        /// </summary>
        private static bool IsCurrentBody(CharacterBody body)
        {
            CharacterMaster master = body.master;
            return master && master.GetBodyObject() == body.gameObject;
        }

        /// <summary>Drone players among the bodies of a team.</summary>
        public static int CountDronePlayers(TeamIndex teamIndex)
        {
            int count = 0;
            ReadOnlyCollection<TeamComponent> members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                if (IsDronePlayer(members[i].body))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Drone players in a list of bodies.</summary>
        public static int CountDronePlayers(List<CharacterBody> bodies)
        {
            int count = 0;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (IsDronePlayer(bodies[i]))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Plays, for everyone, the effect the game shows when it moves a body (Run.GetTeleportEffectPrefab, which
        /// game modes can override).
        /// </summary>
        public static void SpawnTeleportEffect(GameObject subject, Vector3 position, float scale)
        {
            GameObject prefab = Run.instance ? Run.instance.GetTeleportEffectPrefab(subject) : null;
            if (!prefab)
            {
                return;
            }
            EffectManager.SpawnEffect(prefab, new EffectData
            {
                origin = position,
                rotation = Quaternion.identity,
                scale = scale
            }, transmit: true);
        }
    }
}
