using System;
using DroneImprovements.Hooks;
using RoR2;
using RoR2.Networking;
using UnityEngine.Networking;

namespace DroneImprovements
{
    /// <summary>
    /// The settings the host decides for everyone. The host uses its own config and sends the values to each player
    /// who joins, to everyone when a run starts (a resync) and to everyone whenever one of them changes. Clients use
    /// the host's values, so the hooked game code that also runs on their machine matches what the host does: the
    /// interaction and pickup prompts (DroneSurvivorActions, and DroneBarrelGold and DroneShrineOfBloodGold for barrels
    /// and Shrine of Blood), whether a holdout zone's charge indicator and Lepton Daisy's effect on it count a drone
    /// inside as charging (DronesCanChargeHoldoutZones), the Mithrix arena entrance's trigger
    /// (IgnoreDronesForAllPlayerChecks), and the flight speed (SprintSpeedMultiplier: a drone is moved by the client
    /// that owns it). The escape ship's counts run on clients too, but only the host acts on them. Until the host's
    /// values arrive, a client uses the mod's defaults: the host always runs the same version of the mod (see
    /// <see cref="DroneImprovementsPlugin"/>), and the defaults are what most hosts use.
    /// </summary>
    internal static class HostSettings
    {
        private static bool receivedSurvivorActions;
        private static bool receivedCombatGold;
        private static bool receivedBarrelGold;
        private static bool receivedShrineOfBloodGold;
        private static bool receivedDronesCanChargeHoldoutZones;
        private static bool receivedIgnoreDronesForAllPlayerChecks;
        private static bool receivedSpareDronesFromArenaVoidKill;
        private static float receivedSprintSpeedMultiplier;

        /// <summary>Drone players can use interactables and pick up equipment like survivors.</summary>
        public static bool SurvivorActions =>
            NetworkServer.active ? PluginConfig.DroneSurvivorActions.Value : receivedSurvivorActions;

        /// <summary>Drone players earn combat gold. Gold is paid on the host.</summary>
        public static bool CombatGold =>
            NetworkServer.active ? PluginConfig.DroneCombatGold.Value : receivedCombatGold;

        /// <summary>Drone players can open barrels and get their share of the gold. Clients need it for prompts.</summary>
        public static bool BarrelGold =>
            NetworkServer.active ? PluginConfig.DroneBarrelGold.Value : receivedBarrelGold;

        /// <summary>Drone players can use Shrine of Blood. Clients need it for prompts.</summary>
        public static bool ShrineOfBloodGold =>
            NetworkServer.active ? PluginConfig.DroneShrineOfBloodGold.Value : receivedShrineOfBloodGold;

        /// <summary>Drone players inside a holdout zone help charge it.</summary>
        public static bool DronesCanChargeHoldoutZones =>
            NetworkServer.active ? PluginConfig.DronesCanChargeHoldoutZones.Value : receivedDronesCanChargeHoldoutZones;

        /// <summary>Checks that wait for every player only wait for survivors.</summary>
        public static bool IgnoreDronesForAllPlayerChecks =>
            NetworkServer.active
                ? PluginConfig.IgnoreDronesForAllPlayerChecks.Value
                : receivedIgnoreDronesForAllPlayerChecks;

        /// <summary>Drone players outside the Mithrix arena aren't killed when it seals.</summary>
        public static bool SpareDronesFromArenaVoidKill =>
            NetworkServer.active
                ? PluginConfig.SpareDronesFromArenaVoidKill.Value
                : receivedSpareDronesFromArenaVoidKill;

        /// <summary>How many times faster drone players fly while holding Sprint. Owners move their drones.</summary>
        public static float SprintSpeedMultiplier =>
            NetworkServer.active ? PluginConfig.SprintSpeedMultiplier.Value : receivedSprintSpeedMultiplier;

        /// <summary>The host's values of every host setting.</summary>
        private sealed class SettingsMessage : MessageBase
        {
            public bool survivorActions;
            public bool combatGold;
            public bool barrelGold;
            public bool shrineOfBloodGold;
            public bool dronesCanChargeHoldoutZones;
            public bool ignoreDronesForAllPlayerChecks;
            public bool spareDronesFromArenaVoidKill;
            public float sprintSpeedMultiplier;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(survivorActions);
                writer.Write(combatGold);
                writer.Write(barrelGold);
                writer.Write(shrineOfBloodGold);
                writer.Write(dronesCanChargeHoldoutZones);
                writer.Write(ignoreDronesForAllPlayerChecks);
                writer.Write(spareDronesFromArenaVoidKill);
                writer.Write(sprintSpeedMultiplier);
            }

            public override void Deserialize(NetworkReader reader)
            {
                survivorActions = reader.ReadBoolean();
                combatGold = reader.ReadBoolean();
                barrelGold = reader.ReadBoolean();
                shrineOfBloodGold = reader.ReadBoolean();
                dronesCanChargeHoldoutZones = reader.ReadBoolean();
                ignoreDronesForAllPlayerChecks = reader.ReadBoolean();
                spareDronesFromArenaVoidKill = reader.ReadBoolean();
                sprintSpeedMultiplier = reader.ReadSingle();
            }
        }

        public static void Init()
        {
            ResetReceivedSettings();
            PluginConfig.DroneSurvivorActions.SettingChanged += OnSurvivorActionsChanged;
            PluginConfig.DroneCombatGold.SettingChanged += OnHostSettingChanged;
            PluginConfig.DroneBarrelGold.SettingChanged += OnHostSettingChanged;
            PluginConfig.DroneShrineOfBloodGold.SettingChanged += OnHostSettingChanged;
            PluginConfig.DronesCanChargeHoldoutZones.SettingChanged += OnHostSettingChanged;
            PluginConfig.IgnoreDronesForAllPlayerChecks.SettingChanged += OnHostSettingChanged;
            PluginConfig.SpareDronesFromArenaVoidKill.SettingChanged += OnHostSettingChanged;
            PluginConfig.SprintSpeedMultiplier.SettingChanged += OnHostSettingChanged;
            NetworkManagerSystem.onStartClientGlobal += OnStartClient;
            NetworkUser.onPostNetworkUserStart += OnNetworkUserStart;
            Run.onRunStartGlobal += OnRunStart;
        }

        private static void OnSurvivorActionsChanged(object sender, EventArgs e)
        {
            if (NetworkServer.active)
            {
                Broadcast();
                InteractionHooks.ApplySettingToExistingDrones();
            }
        }

        private static void OnHostSettingChanged(object sender, EventArgs e)
        {
            if (NetworkServer.active)
            {
                Broadcast();
            }
        }

        /// <summary>A client is starting a connection: don't carry an earlier host's values over into it.</summary>
        private static void OnStartClient(NetworkClient client)
        {
            ResetReceivedSettings();
        }

        /// <summary>On the host: a player joined (their NetworkUser started), so send them the host's values.</summary>
        private static void OnNetworkUserStart(NetworkUser networkUser)
        {
            // The host's own players read the config directly.
            if (!NetworkServer.active || networkUser.isLocalPlayer)
            {
                return;
            }
            networkUser.connectionToClient.Send(MessageIds.HostSettingsUpdate, CreateMessage());
        }

        private static void OnRunStart(Run run)
        {
            if (NetworkServer.active)
            {
                Broadcast();
            }
        }

        private static void Broadcast()
        {
            NetworkServer.SendToAll(MessageIds.HostSettingsUpdate, CreateMessage());
        }

        private static SettingsMessage CreateMessage()
        {
            return new SettingsMessage
            {
                survivorActions = PluginConfig.DroneSurvivorActions.Value,
                combatGold = PluginConfig.DroneCombatGold.Value,
                barrelGold = PluginConfig.DroneBarrelGold.Value,
                shrineOfBloodGold = PluginConfig.DroneShrineOfBloodGold.Value,
                dronesCanChargeHoldoutZones = PluginConfig.DronesCanChargeHoldoutZones.Value,
                ignoreDronesForAllPlayerChecks = PluginConfig.IgnoreDronesForAllPlayerChecks.Value,
                spareDronesFromArenaVoidKill = PluginConfig.SpareDronesFromArenaVoidKill.Value,
                sprintSpeedMultiplier = PluginConfig.SprintSpeedMultiplier.Value
            };
        }

        [NetworkMessageHandler(msgType = MessageIds.HostSettingsUpdate, client = true)]
        private static void HandleSettings(NetworkMessage netMsg)
        {
            // The host's own client receives the broadcasts too; the host reads its config directly.
            if (NetworkServer.active)
            {
                return;
            }
            SettingsMessage message = netMsg.ReadMessage<SettingsMessage>();
            receivedSurvivorActions = message.survivorActions;
            receivedCombatGold = message.combatGold;
            receivedBarrelGold = message.barrelGold;
            receivedShrineOfBloodGold = message.shrineOfBloodGold;
            receivedDronesCanChargeHoldoutZones = message.dronesCanChargeHoldoutZones;
            receivedIgnoreDronesForAllPlayerChecks = message.ignoreDronesForAllPlayerChecks;
            receivedSpareDronesFromArenaVoidKill = message.spareDronesFromArenaVoidKill;
            // Network input is validated: keep the host's value within the setting's range.
            receivedSprintSpeedMultiplier = (float)PluginConfig.SprintSpeedMultiplier.Description.AcceptableValues
                .Clamp(message.sprintSpeedMultiplier);
            InteractionHooks.ApplySettingToExistingDrones();
        }

        private static void ResetReceivedSettings()
        {
            receivedSurvivorActions = (bool)PluginConfig.DroneSurvivorActions.DefaultValue;
            receivedCombatGold = (bool)PluginConfig.DroneCombatGold.DefaultValue;
            receivedBarrelGold = (bool)PluginConfig.DroneBarrelGold.DefaultValue;
            receivedShrineOfBloodGold = (bool)PluginConfig.DroneShrineOfBloodGold.DefaultValue;
            receivedDronesCanChargeHoldoutZones = (bool)PluginConfig.DronesCanChargeHoldoutZones.DefaultValue;
            receivedIgnoreDronesForAllPlayerChecks = (bool)PluginConfig.IgnoreDronesForAllPlayerChecks.DefaultValue;
            receivedSpareDronesFromArenaVoidKill = (bool)PluginConfig.SpareDronesFromArenaVoidKill.DefaultValue;
            receivedSprintSpeedMultiplier = (float)PluginConfig.SprintSpeedMultiplier.DefaultValue;
        }
    }
}
