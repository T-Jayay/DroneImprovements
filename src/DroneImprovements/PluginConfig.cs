using BepInEx.Configuration;

namespace DroneImprovements
{
    /// <summary>
    /// The settings in BepInEx/config/revor.DroneImprovements.cfg. "(host)" settings are decided by the host for
    /// everyone (see <see cref="HostSettings"/>); "(per player)" settings only change the player's own drone. All of
    /// them apply immediately. Section and key names are stored in players' config files: renaming or moving one
    /// resets that setting for everyone, so they must never change.
    /// </summary>
    internal static class PluginConfig
    {
        private const string HostSection = "Host";
        private const string ObjectivesSection = "Objectives";
        private const string AbilitiesSection = "Abilities";

        private const float DefaultDisconnectHoldSeconds = 1f;
        private const float MinDisconnectHoldSeconds = 0f;
        private const float MaxDisconnectHoldSeconds = 5f;

        private const float DefaultTeleportCooldown = 30f;
        // At least a second: every teleport runs a physics search and sends two effects to every player.
        private const float MinTeleportCooldown = 1f;
        private const float MaxTeleportCooldown = 600f;

        public static ConfigEntry<bool> DroneSurvivorActions { get; private set; }
        public static ConfigEntry<DroneGoldMode> DroneGold { get; private set; }
        public static ConfigEntry<bool> DronesCanChargeHoldoutZones { get; private set; }
        public static ConfigEntry<bool> IgnoreDronesForAllPlayerChecks { get; private set; }
        public static ConfigEntry<bool> SpareDronesFromArenaVoidKill { get; private set; }
        public static ConfigEntry<bool> DisconnectEnabled { get; private set; }
        public static ConfigEntry<float> DisconnectHoldSeconds { get; private set; }
        public static ConfigEntry<bool> TeleportEnabled { get; private set; }
        public static ConfigEntry<float> TeleportCooldown { get; private set; }

        public static void Init(ConfigFile config)
        {
            DroneSurvivorActions = config.Bind(HostSection, "DroneSurvivorActions", true,
                "(host) Drone players can open chests, use shrines, printers, scrappers and the teleporter, buy with "
                + "their own gold, and pick up equipment. Off: they're locked out of those, as in the base game.");
            DroneGold = config.Bind(HostSection, "DroneGold", DroneGoldMode.Full,
                "(host) How drone players earn gold. The mod never takes gold they already have.\n"
                + "Full: like survivors.\n"
                + "NoKillGold: no gold from combat (kills, money packs, Brittle Crown, Roll of Pennies, ...); "
                + "barrels and Shrine of Blood still pay.\n"
                + "None: no gold at all, and they can't use barrels or Shrine of Blood.\n"
                + "Shared gold (kills, money packs, barrels) that drone players don't get goes to the survivors.");

            DronesCanChargeHoldoutZones = config.Bind(ObjectivesSection, "DronesCanChargeHoldoutZones", true,
                "(host) Drone players inside a holdout zone (teleporter, pillars, Void Fields cells, ...) help charge "
                + "it. Drones outside never slow it down.");
            IgnoreDronesForAllPlayerChecks = config.Bind(ObjectivesSection, "IgnoreDronesForAllPlayerChecks", true,
                "(host) Checks that wait for every player (the Mithrix arena entrance, the escape ship, ...) only wait "
                + "for survivors. Drone players who are there anyway still count as there: they're safe when the "
                + "arena seals and leave with the ship.");
            SpareDronesFromArenaVoidKill = config.Bind(ObjectivesSection, "SpareDronesFromArenaVoidKill", true,
                "(host) Drone players outside the Mithrix arena aren't killed when it seals (that would also use up "
                + "a Dio's Best Friend). Drone players inside are never killed.");

            DisconnectEnabled = config.Bind(AbilitiesSection, "DisconnectEnabled", true,
                "(per player) Disconnect, your drone's Utility skill: hold it to leave the drone and go back to "
                + "spectating.");
            DisconnectHoldSeconds = config.Bind(AbilitiesSection, "DisconnectHoldSeconds",
                DefaultDisconnectHoldSeconds,
                new ConfigDescription("(per player) How long to hold Disconnect, in seconds.",
                    new AcceptableValueRange<float>(MinDisconnectHoldSeconds, MaxDisconnectHoldSeconds)));
            TeleportEnabled = config.Bind(AbilitiesSection, "TeleportEnabled", true,
                "(per player) Teleport to Player, your drone's Special skill (R): jump next to the nearest living "
                + "player.");
            TeleportCooldown = config.Bind(AbilitiesSection, "TeleportCooldown", DefaultTeleportCooldown,
                new ConfigDescription("(per player) Base cooldown of Teleport to Player, in seconds. Cooldown items "
                    + "can shorten it.",
                    new AcceptableValueRange<float>(MinTeleportCooldown, MaxTeleportCooldown)));
        }
    }
}
