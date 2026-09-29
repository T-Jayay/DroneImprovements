using HarmonyLib;
using RoR2;
using RoR2.Networking;
using UnityEngine.Networking;

namespace DroneImprovements
{
    /// <summary>
    /// Settings the host decides for everyone. The host sends its values to clients so their prompts and UI
    /// match what the server will actually allow, regardless of each client's own config.
    /// </summary>
    internal static class HostSettings
    {
        public const short SettingsMsgType = 17393;

        // What the host told us. Defaults to vanilla behaviour until we hear from a host running the mod.
        private static bool receivedSurvivorActions;
        private static DroneGoldMode receivedGoldMode = DroneGoldMode.Full;

        /// <summary>Drones can interact, buy and pick up items like a survivor.</summary>
        public static bool SurvivorActions => NetworkServer.active ? PluginConfig.DroneSurvivorActions.Value : receivedSurvivorActions;

        /// <summary>How drone players earn gold. Gold itself is paid on the host; clients need it for barrel/shrine prompts.</summary>
        public static DroneGoldMode GoldMode => NetworkServer.active ? PluginConfig.DroneGold.Value : receivedGoldMode;

        private class SettingsMessage : MessageBase
        {
            public bool survivorActions;
            public DroneGoldMode goldMode;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(survivorActions);
                writer.Write((byte)goldMode);
            }

            public override void Deserialize(NetworkReader reader)
            {
                survivorActions = reader.ReadBoolean();
                goldMode = (DroneGoldMode)reader.ReadByte();
            }
        }

        public static void Init()
        {
            PluginConfig.DroneSurvivorActions.SettingChanged += (sender, args) =>
            {
                if (NetworkServer.active)
                {
                    Broadcast();
                    ApplyToExistingDrones();
                }
            };
            PluginConfig.DroneGold.SettingChanged += (sender, args) =>
            {
                if (NetworkServer.active)
                {
                    Broadcast();
                }
            };
            Run.onRunStartGlobal += run =>
            {
                if (NetworkServer.active)
                {
                    Broadcast();
                }
            };
            Run.onRunDestroyGlobal += run =>
            {
                receivedSurvivorActions = false;
                receivedGoldMode = DroneGoldMode.Full;
            };
            // Players joining mid-run.
            NetworkUser.onPostNetworkUserStart += networkUser =>
            {
                if (NetworkServer.active)
                {
                    Broadcast();
                }
            };
        }

        private static void Broadcast()
        {
            NetworkServer.SendToAll(SettingsMsgType, new SettingsMessage
            {
                survivorActions = PluginConfig.DroneSurvivorActions.Value,
                goldMode = PluginConfig.DroneGold.Value
            });
        }

        private static void HandleSettings(NetworkMessage netMsg)
        {
            SettingsMessage message = netMsg.ReadMessage<SettingsMessage>();
            receivedSurvivorActions = message.survivorActions;
            receivedGoldMode = message.goldMode;
            ApplyToExistingDrones();
        }

        /// <summary>Drones that already exist pick up a changed setting immediately.</summary>
        private static void ApplyToExistingDrones()
        {
            foreach (CharacterBody body in CharacterBody.readOnlyInstancesList)
            {
                if (body && body.isRemoteOp && body.TryGetComponent(out Interactor interactor))
                {
                    interactor.isRemoteOp = !SurvivorActions;
                }
            }
        }

        [HarmonyPatch(typeof(NetworkMessageHandlerAttribute), nameof(NetworkMessageHandlerAttribute.RegisterClientMessages))]
        private static class RegisterClientMessagesPatch
        {
            private static void Postfix(NetworkClient client)
            {
                client.RegisterHandler(SettingsMsgType, HandleSettings);
            }
        }
    }
}
