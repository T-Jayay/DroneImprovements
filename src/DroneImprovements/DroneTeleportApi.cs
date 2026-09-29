using System;
using System.Collections.Generic;
using RoR2;

namespace DroneImprovements
{
    /// <summary>Lets other plugins offer extra teleport destinations for drone players.</summary>
    public static class DroneTeleportApi
    {
        /// <summary>
        /// Raised on the drone owner's machine when looking for a teleport target.
        /// Args: the drone body, and the candidate list to add bodies to.
        /// </summary>
        public static event Action<CharacterBody, List<CharacterBody>> CollectAdditionalTargets;

        internal static void InvokeCollectAdditionalTargets(CharacterBody drone, List<CharacterBody> targets)
        {
            CollectAdditionalTargets?.Invoke(drone, targets);
        }
    }
}
