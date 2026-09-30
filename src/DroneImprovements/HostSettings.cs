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
    /// interaction and pickup prompts (DroneSurvivorActions, and DroneGold for barrels and Shrine of Blood), whether
    /// a holdout zone's charge indicator and Lepton Daisy's effect on it count a drone inside as charging
    /// (DronesCanChargeHoldoutZones), and the Mithrix arena entrance's trigger (IgnoreDronesForAllPlayerChecks). The
    /// escape ship's counts run on clients too, but only the host acts on them. Until the host's values arrive, a
    /// client uses the mod's defaults: the host always runs the same version of the mod (see
    /// <see cref="DroneImprovementsPlugin"/>), and the defaults are what most hosts use.
    /// </summary>
    internal static class HostSettings
    {
        private static bool receivedSurvivorActions;
        private static DroneGoldMode receivedGoldMode;
        private static bool receivedDronesCanChargeHoldoutZones;
        private static bool receivedIgnoreDronesForAllPlayerChecks;
        private static bool receivedSpareDronesFromArenaVoidKill;

        /// <summary>Drone players can use interactables and pick up equipment like survivors.</summary>
        public static bool SurvivorActions =>
            NetworkServer.active ? PluginConfig.DroneSurvivorActions.Value : receivedSurvivorActions;

        /// <summary>How drone players earn gold. Gold is paid on the host; clients need it for the prompts.</summary>
        public static DroneGoldMode GoldMode =>
            NetworkServer.active ? PluginConfig.DroneGold.Value : receivedGoldMode;

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

        /// <summary>The host's values of every host setting.</summary>
        private sealed class SettingsMessage : MessageBase
        {
            public bool survivorActions;
            public DroneGoldMode goldMode;
            public bool dronesCanChargeHoldoutZones;
            public bool ignoreDronesForAllPlayerChecks;
            public bool spareDronesFromArenaVoidKill;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(survivorActions);
                writer.Write((byte)goldMode);
                writer.Write(dronesCanChargeHoldoutZones);
                writer.Write(ignoreDronesForAllPlayerChecks);
                writer.Write(spareDronesFromArenaVoidKill);
            }

            public override void Deserialize(NetworkReader reader)
            {
                survivorActions = reader.ReadBoolean();
                goldMode = (DroneGoldMode)reader.ReadByte();
                dronesCanChargeHoldoutZones = reader.ReadBoolean();
                ignoreDronesForAllPlayerChecks = reader.ReadBoolean();
                spareDronesFromArenaVoidKill = reader.ReadBoolean();
            }
        }

        public static void Init()
        {
            ResetReceivedSettings();
            PluginConfig.DroneSurvivorActions.SettingChanged += OnSurvivorActionsChanged;
            PluginConfig.DroneGold.SettingChanged += OnHostSettingChanged;
            PluginConfig.DronesCanChargeHoldoutZones.SettingChanged += OnHostSettingChanged;
            PluginConfig.IgnoreDronesForAllPlayerChecks.SettingChanged += OnHostSettingChanged;
            PluginConfig.SpareDronesFromArenaVoidKill.SettingChanged += OnHostSettingChanged;
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
                goldMode = PluginConfig.DroneGold.Value,
                dronesCanChargeHoldoutZones = PluginConfig.DronesCanChargeHoldoutZones.Value,
                ignoreDronesForAllPlayerChecks = PluginConfig.IgnoreDronesForAllPlayerChecks.Value,
                spareDronesFromArenaVoidKill = PluginConfig.SpareDronesFromArenaVoidKill.Value
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
            receivedGoldMode = message.goldMode;
            if (!Enum.IsDefined(typeof(DroneGoldMode), message.goldMode))
            {
                receivedGoldMode = (DroneGoldMode)PluginConfig.DroneGold.DefaultValue;
                DroneImprovementsPlugin.Log.LogWarning(
                    $"The host sent an unknown DroneGold value ({(int)message.goldMode}); using {receivedGoldMode}.");
            }
            receivedDronesCanChargeHoldoutZones = message.dronesCanChargeHoldoutZones;
            receivedIgnoreDronesForAllPlayerChecks = message.ignoreDronesForAllPlayerChecks;
            receivedSpareDronesFromArenaVoidKill = message.spareDronesFromArenaVoidKill;
            InteractionHooks.ApplySettingToExistingDrones();
        }

        private static void ResetReceivedSettings()
        {
            receivedSurvivorActions = (bool)PluginConfig.DroneSurvivorActions.DefaultValue;
            receivedGoldMode = (DroneGoldMode)PluginConfig.DroneGold.DefaultValue;
            receivedDronesCanChargeHoldoutZones = (bool)PluginConfig.DronesCanChargeHoldoutZones.DefaultValue;
            receivedIgnoreDronesForAllPlayerChecks = (bool)PluginConfig.IgnoreDronesForAllPlayerChecks.DefaultValue;
            receivedSpareDronesFromArenaVoidKill = (bool)PluginConfig.SpareDronesFromArenaVoidKill.DefaultValue;
        }
    }
}
