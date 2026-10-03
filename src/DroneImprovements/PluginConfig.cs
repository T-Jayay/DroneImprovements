using BepInEx.Configuration;
using UnityEngine;

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
        private const string FlightSection = "Flight";

        private const float DefaultSprintSpeedMultiplier = 2f;
        // At 1 the feature is off: holding Sprint flies down, as in the base game.
        private const float MinSprintSpeedMultiplier = 1f;
        // Below 4: RigidbodyMotor damages a drone that hits a wall at 4 times its move speed or faster.
        private const float MaxSprintSpeedMultiplier = 3f;

        private const float DefaultDisconnectHoldSeconds = 1f;
        private const float MinDisconnectHoldSeconds = 0f;
        private const float MaxDisconnectHoldSeconds = 5f;

        private const float DefaultTeleportCooldown = 30f;
        // At least a second: every teleport runs a physics search and sends two effects to every player.
        private const float MinTeleportCooldown = 1f;
        private const float MaxTeleportCooldown = 600f;

        public static ConfigEntry<bool> DroneSurvivorActions { get; private set; }
        public static ConfigEntry<bool> DroneCombatGold { get; private set; }
        public static ConfigEntry<bool> DroneBarrelGold { get; private set; }
        public static ConfigEntry<bool> DroneShrineOfBloodGold { get; private set; }
        public static ConfigEntry<bool> DronesCanChargeHoldoutZones { get; private set; }
        public static ConfigEntry<bool> IgnoreDronesForAllPlayerChecks { get; private set; }
        public static ConfigEntry<bool> SpareDronesFromArenaVoidKill { get; private set; }
        public static ConfigEntry<bool> DisconnectEnabled { get; private set; }
        public static ConfigEntry<float> DisconnectHoldSeconds { get; private set; }
        public static ConfigEntry<bool> TeleportEnabled { get; private set; }
        public static ConfigEntry<float> TeleportCooldown { get; private set; }
        public static ConfigEntry<float> SprintSpeedMultiplier { get; private set; }
        public static ConfigEntry<KeyboardShortcut> DescendKey { get; private set; }

        public static void Init(ConfigFile config)
        {
            DroneSurvivorActions = config.Bind(HostSection, "DroneSurvivorActions", true,
                "(host) Drone players can open chests, use shrines, printers, scrappers and the teleporter, buy with "
                + "their own gold, and pick up equipment. Off: they're locked out of those, as in the base game.");
            DroneCombatGold = config.Bind(HostSection, "DroneCombatGold", true,
                "(host) Drone players earn combat gold: kills, money packs, Brittle Crown, Roll of Pennies, ..., and "
                + "gold other mods pay during a kill, hit or damage. Off: they get none, and their share of the "
                + "shared combat gold goes to the survivors.");
            DroneBarrelGold = config.Bind(HostSection, "DroneBarrelGold", true,
                "(host) Drone players can open barrels and get their share of the gold. Off: they can't open "
                + "barrels, and the survivors get all the gold of the barrels they open.");
            DroneShrineOfBloodGold = config.Bind(HostSection, "DroneShrineOfBloodGold", true,
                "(host) Drone players can use Shrine of Blood, which trades health for gold. Off: they can't.");

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

            SprintSpeedMultiplier = config.Bind(FlightSection, "SprintSpeedMultiplier", DefaultSprintSpeedMultiplier,
                new ConfigDescription("(host) How many times faster drone players fly while holding Sprint. At 1 "
                    + "this is off, and Sprint flies down as in the base game.",
                    new AcceptableValueRange<float>(MinSprintSpeedMultiplier, MaxSprintSpeedMultiplier)));
            DescendKey = config.Bind(FlightSection, "DescendKey", new KeyboardShortcut(KeyCode.LeftControl),
                "(per player) Hold to fly your drone down (Jump flies up). It works whatever SprintSpeedMultiplier "
                + "is. Modifier keys are ignored: the main key alone counts, so it also works while you hold "
                + "movement keys.");
        }
    }
}
