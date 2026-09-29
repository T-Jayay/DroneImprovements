using BepInEx.Configuration;

namespace DroneImprovements
{
    public enum DroneGoldMode
    {
        /// <summary>Drone players earn gold like survivors.</summary>
        Full,
        /// <summary>No gold from kills/combat; barrels, Shrine of Blood and Executive Card still pay.</summary>
        NoKillGold,
        /// <summary>No gold at all, and barrels / Shrine of Blood can't be used.</summary>
        None
    }

    internal static class PluginConfig
    {
        // Host
        public static ConfigEntry<bool> DroneSurvivorActions;
        public static ConfigEntry<DroneGoldMode> DroneGold;

        // Objectives
        public static ConfigEntry<bool> DronesCanChargeHoldoutZones;
        public static ConfigEntry<bool> IgnoreDronesForAllPlayerChecks;
        public static ConfigEntry<bool> SpareDronesFromArenaVoidKill;

        // Abilities
        public static ConfigEntry<bool> DisconnectEnabled;
        public static ConfigEntry<float> DisconnectHoldSeconds;
        public static ConfigEntry<bool> TeleportEnabled;
        public static ConfigEntry<float> TeleportCooldown;

        public static void Init(ConfigFile config)
        {
            DroneSurvivorActions = config.Bind("Host", "DroneSurvivorActions", true,
                "(host) Drones can interact, buy and pick up items like survivors.");
            DroneGold = config.Bind("Host", "DroneGold", DroneGoldMode.Full,
                "(host) How drones earn gold. They keep the gold they had.\n" +
                "Full: like survivors\n" +
                "NoKillGold: no gold from combat, barrels and shrines still pay\n" +
                "None: no gold, can't use barrels or Shrine of Blood");

            DronesCanChargeHoldoutZones = config.Bind("Objectives", "DronesCanChargeHoldoutZones", true,
                "(host) Drones inside the teleporter zone help charge it. Drones outside never slow it down.");
            IgnoreDronesForAllPlayerChecks = config.Bind("Objectives", "IgnoreDronesForAllPlayerChecks", true,
                "(host) The Mithrix cutscene and escape ship don't wait for drones.");
            SpareDronesFromArenaVoidKill = config.Bind("Objectives", "SpareDronesFromArenaVoidKill", true,
                "(host) Drones outside the sealed Mithrix arena aren't killed.");

            DisconnectEnabled = config.Bind("Abilities", "DisconnectEnabled", true,
                "Drone Utility skill: hold to disconnect and return to spectating.");
            DisconnectHoldSeconds = config.Bind("Abilities", "DisconnectHoldSeconds", 1f,
                new ConfigDescription("Seconds to hold Disconnect. Needs a game restart.", new AcceptableValueRange<float>(0f, 5f)));
            TeleportEnabled = config.Bind("Abilities", "TeleportEnabled", true,
                "Drone Special skill (R): teleport to the nearest living player.");
            TeleportCooldown = config.Bind("Abilities", "TeleportCooldown", 30f,
                new ConfigDescription("Teleport cooldown in seconds. Needs a game restart.", new AcceptableValueRange<float>(0f, 600f)));
        }
    }
}
