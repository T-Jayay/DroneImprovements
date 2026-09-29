using System.Collections.Generic;
using UnityEngine.Networking;

namespace DroneImprovements
{
    /// <summary>
    /// The mod's custom UNet message types, and handler registration that reports a clash. UNet uses the ids below 48
    /// and RoR2 uses 48 to 87 (RoR2.Networking.UmsgType); these sit far above both. UNet silently replaces a handler
    /// when another one is registered for the same id, so a mod using the same id would break one of the two mods.
    /// </summary>
    internal static class MessageIds
    {
        /// <summary>Client to host: "remove my drone" (Disconnect). See <see cref="DroneNetworking"/>.</summary>
        public const short DisconnectRequest = 17392;

        /// <summary>Host to clients: the host's settings. See <see cref="HostSettings"/>.</summary>
        public const short HostSettingsUpdate = 17393;

        /// <summary>Registers a handler on the server, logging an error if another mod already uses the id.</summary>
        public static void RegisterServerHandler(short msgType, NetworkMessageDelegate handler)
        {
            ReportClash(NetworkServer.handlers, msgType, handler, "host");
            NetworkServer.RegisterHandler(msgType, handler);
        }

        /// <summary>Registers a handler on a client, logging an error if another mod already uses the id.</summary>
        public static void RegisterClientHandler(NetworkClient client, short msgType, NetworkMessageDelegate handler)
        {
            ReportClash(client.handlers, msgType, handler, "client");
            client.RegisterHandler(msgType, handler);
        }

        private static void ReportClash(Dictionary<short, NetworkMessageDelegate> handlers, short msgType,
            NetworkMessageDelegate handler, string side)
        {
            // Finding our own handler isn't a clash: with Epic Online Services the game raises onStartServerGlobal
            // twice for one server start (the base OnStartServer, then NetworkManagerSystemEOS.OnStartServer).
            if (handlers == null || !handlers.TryGetValue(msgType, out NetworkMessageDelegate existing)
                || existing == null || existing.Method == handler.Method)
            {
                return;
            }
            DroneImprovementsPlugin.Log.LogError($"Network message id {msgType} is already used on the {side} by "
                + $"{existing.Method.DeclaringType}.{existing.Method.Name} (another mod). DroneImprovements replaces "
                + "it, so one of the two mods won't work in multiplayer. Please report this conflict.");
        }
    }
}
