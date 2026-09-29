using System;
using System.Collections.Generic;
using System.Reflection;
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
        private static readonly object subscribersLock = new object();
        private static readonly HashSet<MethodInfo> failedHandlers = new HashSet<MethodInfo>();

        private static Action<CharacterBody, List<CharacterBody>> subscribers;

        // Snapshot of the subscribers, rebuilt when one is added or removed, so raising the event doesn't allocate.
        private static Delegate[] handlers = Array.Empty<Delegate>();

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
        /// <para>A handler that throws is logged once and doesn't stop the other handlers.</para>
        /// </summary>
        public static event Action<CharacterBody, List<CharacterBody>> CollectAdditionalTargets
        {
            add
            {
                lock (subscribersLock)
                {
                    subscribers += value;
                    handlers = subscribers == null ? Array.Empty<Delegate>() : subscribers.GetInvocationList();
                }
            }
            remove
            {
                lock (subscribersLock)
                {
                    subscribers -= value;
                    handlers = subscribers == null ? Array.Empty<Delegate>() : subscribers.GetInvocationList();
                }
            }
        }

        internal static bool HasHandlers => handlers.Length != 0;

        /// <summary>Calls every handler, each isolated from the others' exceptions.</summary>
        internal static void InvokeCollectAdditionalTargets(CharacterBody drone, List<CharacterBody> targets)
        {
            // A handler that subscribes or unsubscribes replaces the array, so this loop stays valid.
            Delegate[] current = handlers;
            for (int i = 0; i < current.Length; i++)
            {
                Action<CharacterBody, List<CharacterBody>> handler =
                    (Action<CharacterBody, List<CharacterBody>>)current[i];
                try
                {
                    handler(drone, targets);
                }
                catch (Exception e)
                {
                    if (failedHandlers.Add(handler.Method))
                    {
                        DroneImprovementsPlugin.Log.LogError($"A {nameof(CollectAdditionalTargets)} handler "
                            + $"({handler.Method.DeclaringType}.{handler.Method.Name}) threw. It is still called, but "
                            + $"its further errors aren't logged. {e}");
                    }
                }
            }
        }
    }
}
