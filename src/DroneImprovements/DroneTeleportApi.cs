using System;
using System.Collections.Generic;
using RoR2;

namespace DroneImprovements
{
    /// <summary>
    /// Lets other plugins offer extra destinations for the drone's Teleport to Player skill. Plugins using it need a
    /// BepInDependency on <c>revor.DroneImprovements</c>; with a soft dependency, only touch this type when the mod is
    /// loaded.
    /// </summary>
    public static class DroneTeleportApi
    {
        /// <summary>
        /// Raised whenever the mod looks for the nearest teleport target for a drone. That happens on every machine
        /// whose HUD shows the drone's Teleport skill (the drone's owner and anyone spectating it) every frame, on
        /// the owner every tick while the skill's key is held, and when the teleport happens; not only on the host.
        /// Keep handlers cheap and free of allocations.
        /// <para>
        /// Arguments: the drone's body, and a list to add candidate bodies to. The list is the mod's own reused list
        /// and starts empty: only add to it, and don't keep it. Living, connected survivors on the drone's team are
        /// always candidates, so there is no need to add players.
        /// </para>
        /// <para>
        /// A candidate is skipped when it is null or destroyed, the drone itself, a Remote Operation drone, or dead
        /// (or without a health component). There is no team check. The candidate nearest to the drone wins.
        /// </para>
        /// </summary>
        public static event Action<CharacterBody, List<CharacterBody>> CollectAdditionalTargets;

        /// <summary>Raises <see cref="CollectAdditionalTargets"/>.</summary>
        internal static void CollectTargets(CharacterBody drone, List<CharacterBody> targets)
        {
            CollectAdditionalTargets?.Invoke(drone, targets);
        }
    }
}
