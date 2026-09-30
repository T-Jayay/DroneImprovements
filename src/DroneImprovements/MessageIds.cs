namespace DroneImprovements
{
    /// <summary>
    /// The mod's network message ids, for the game's [NetworkMessageHandler]. UNet uses the ids below 48 and RoR2 uses
    /// 48 to 87 (RoR2.Networking.UmsgType); these sit far above both.
    /// </summary>
    internal static class MessageIds
    {
        /// <summary>Client to host: "remove my drone" (Disconnect). See <see cref="DroneNetworking"/>.</summary>
        public const short DisconnectRequest = 17392;

        /// <summary>Host to clients: the host's settings. See <see cref="HostSettings"/>.</summary>
        public const short HostSettingsUpdate = 17393;
    }
}
