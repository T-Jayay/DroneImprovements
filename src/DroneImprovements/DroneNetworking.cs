using System.Reflection;
using HarmonyLib;
using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace DroneImprovements
{
    /// <summary>
    /// Disconnecting destroys the drone body, which only the host may do, so clients send a small request message.
    /// </summary>
    internal static class DroneNetworking
    {
        // Arbitrary id well clear of UNet's (0-47) and RoR2's (48-~100) message types.
        public const short DisconnectRequestMsgType = 17392;

        private class DisconnectRequestMessage : MessageBase
        {
            public GameObject networkUserObject;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(networkUserObject);
            }

            public override void Deserialize(NetworkReader reader)
            {
                networkUserObject = reader.ReadGameObject();
            }
        }

        private static readonly MethodInfo deathFootPositionSetter = AccessTools.PropertySetter(typeof(CharacterMaster), nameof(CharacterMaster.deathFootPosition));
        private static readonly MethodInfo lostBodyToDeathSetter = AccessTools.PropertySetter(typeof(CharacterMaster), nameof(CharacterMaster.lostBodyToDeath));
        private static readonly AccessTools.FieldRef<CharacterMaster, bool> preventRespawnUntilNextStageServer =
            AccessTools.FieldRefAccess<CharacterMaster, bool>("preventRespawnUntilNextStageServer");
        private static readonly MethodInfo restoreOriginalBodyPrefab = AccessTools.Method(typeof(CharacterMaster), "RestoreOriginalBodyPrefab");

        private static GameObject disconnectEffectPrefab;

        /// <summary>RoR2 (re)registers its server message handlers whenever a server starts; register ours alongside.</summary>
        [HarmonyPatch(typeof(NetworkMessageHandlerAttribute), nameof(NetworkMessageHandlerAttribute.RegisterServerMessages))]
        private static class RegisterServerMessagesPatch
        {
            private static void Postfix()
            {
                NetworkServer.RegisterHandler(DisconnectRequestMsgType, HandleDisconnectRequest);
            }
        }

        /// <summary>Called on the machine with authority over the drone (its owner).</summary>
        public static void RequestDisconnect(CharacterBody drone)
        {
            CharacterMaster master = drone ? drone.master : null;
            if (!master)
            {
                return;
            }
            if (NetworkServer.active)
            {
                ServerDisconnect(master);
                return;
            }
            NetworkUser networkUser = master.playerCharacterMasterController ? master.playerCharacterMasterController.networkUser : null;
            if (!networkUser)
            {
                return;
            }
            NetworkConnection connection = ClientScene.readyConnection;
            if (connection == null)
            {
                return;
            }
            connection.Send(DisconnectRequestMsgType, new DisconnectRequestMessage { networkUserObject = networkUser.gameObject });
        }

        private static void HandleDisconnectRequest(NetworkMessage netMsg)
        {
            DisconnectRequestMessage message = netMsg.ReadMessage<DisconnectRequestMessage>();
            NetworkUser networkUser = message.networkUserObject ? message.networkUserObject.GetComponent<NetworkUser>() : null;
            if (!networkUser)
            {
                return;
            }
            // Only the connection that owns this NetworkUser may disconnect it.
            NetworkConnection owner = networkUser.connectionToClient;
            if (owner == null || netMsg.conn == null || owner.connectionId != netMsg.conn.connectionId)
            {
                DroneImprovementsPlugin.Log.LogWarning($"Rejected drone disconnect request for {networkUser.userName} from a connection that does not own it.");
                return;
            }
            ServerDisconnect(networkUser.master);
        }

        /// <summary>
        /// Ends remote operation without killing the drone: no death, no Dio's Best Friend consumed, no kill feed.
        /// Leaves the master in exactly the state vanilla uses after a drone dies, so the player is back to
        /// spectating, can pick another drone, and still does not prevent a game over.
        /// </summary>
        public static void ServerDisconnect(CharacterMaster master)
        {
            if (!NetworkServer.active || !master)
            {
                return;
            }
            CharacterBody body = master.GetBody();
            if (!DroneUtil.IsDronePlayer(body) || !body.healthComponent || !body.healthComponent.alive)
            {
                return;
            }

            SpawnDisconnectEffect(body);

            // Same bookkeeping as CharacterMaster.OnBodyDeath for a drone that had no revive.
            deathFootPositionSetter.Invoke(master, new object[] { body.footPosition });
            lostBodyToDeathSetter.Invoke(master, new object[] { true });
            master.preventGameOver = false;
            preventRespawnUntilNextStageServer(master) = true;
            master.inventory.SetEquipmentDisabled(false);

            if (body.GetComponent<Inventory>())
            {
                // DestroyBody would copy this (drone-local) inventory over the player's; destroy it directly instead.
                Object.Destroy(body.gameObject);
                master.OnBodyDestroyed(body);
            }
            else
            {
                master.DestroyBody();
            }

            restoreOriginalBodyPrefab.Invoke(master, null);
            DroneImprovementsPlugin.Log.LogInfo($"{master.name} disconnected from their drone.");
        }

        private static void SpawnDisconnectEffect(CharacterBody body)
        {
            if (!disconnectEffectPrefab)
            {
                disconnectEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/TeleportOutBoom");
            }
            if (disconnectEffectPrefab)
            {
                EffectManager.SpawnEffect(disconnectEffectPrefab, new EffectData
                {
                    origin = body.corePosition,
                    rotation = Quaternion.identity,
                    scale = body.radius
                }, transmit: true);
            }
        }
    }
}
