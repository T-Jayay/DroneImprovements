using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace DroneImprovements
{
    /// <summary>
    /// The Disconnect ability's host side. Ending remote operation destroys the drone body, which only the host may
    /// do, so a client sends the host a request naming its drone.
    /// </summary>
    internal static class DroneNetworking
    {
        /// <summary>The request a client sends: the drone it wants removed.</summary>
        private sealed class DisconnectRequestMessage : MessageBase
        {
            public GameObject droneObject;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(droneObject);
            }

            public override void Deserialize(NetworkReader reader)
            {
                droneObject = reader.ReadGameObject();
            }
        }

        /// <summary>Asks the host to end remote operation of this drone. Called on the drone owner's machine.</summary>
        public static void RequestDisconnect(CharacterBody drone)
        {
            if (!DroneUtil.IsDronePlayer(drone))
            {
                return;
            }
            if (NetworkServer.active)
            {
                ServerDisconnect(drone);
                return;
            }
            NetworkConnection connection = ClientScene.readyConnection;
            if (connection == null)
            {
                return;
            }
            connection.Send(MessageIds.DisconnectRequest,
                new DisconnectRequestMessage { droneObject = drone.gameObject });
        }

        [NetworkMessageHandler(msgType = MessageIds.DisconnectRequest, server = true)]
        private static void HandleDisconnectRequest(NetworkMessage netMsg)
        {
            DisconnectRequestMessage message = netMsg.ReadMessage<DisconnectRequestMessage>();
            CharacterBody drone = message.droneObject ? message.droneObject.GetComponent<CharacterBody>() : null;
            CharacterMaster master = drone ? drone.master : null;
            PlayerCharacterMasterController player = master ? master.playerCharacterMasterController : null;
            NetworkUser networkUser = player ? player.networkUser : null;
            if (!networkUser)
            {
                // Not a player's body, or it's already gone.
                return;
            }
            // Only the connection that owns this player may disconnect their drone.
            if (networkUser.connectionToClient != netMsg.conn)
            {
                DroneImprovementsPlugin.Log.LogWarning($"Refused to disconnect {networkUser.userName}'s drone: the "
                    + "request came from another player's connection.");
                return;
            }
            ServerDisconnect(drone);
        }

        /// <summary>
        /// Ends remote operation without a death. It does the bookkeeping CharacterMaster.OnBodyDeath does for a drone
        /// player who has no revive (death position, lostBodyToDeath, no game-over protection, no respawn until the
        /// next stage, equipment enabled again, the survivor body prefab restored), so the player is back to
        /// spectating and can pick another drone. Everything else about dying is skipped: no death event or kill feed,
        /// no revive (a Dio's Best Friend is neither used nor revives the player), and the life stopwatch isn't reset.
        /// </summary>
        private static void ServerDisconnect(CharacterBody drone)
        {
            // A request names any body; only a player's drone may be removed.
            if (!DroneUtil.IsDronePlayer(drone))
            {
                return;
            }
            CharacterMaster master = drone.master;
            // The request may arrive late: only remove the drone the player is still controlling.
            if (master.GetBodyObject() != drone.gameObject || !drone.healthComponent || !drone.healthComponent.alive)
            {
                return;
            }

            DroneUtil.SpawnTeleportEffect(drone.gameObject, drone.corePosition, drone.radius);

            master.deathFootPosition = drone.footPosition;
            master.lostBodyToDeath = true;
            master.preventGameOver = false;
            master.preventRespawnUntilNextStageServer = true;
            master.inventory.SetEquipmentDisabled(false);

            if (drone.GetComponent<Inventory>())
            {
                // DestroyBody would copy this body's own inventory over the player's, so destroy the body directly.
                // Its OnDestroy tells the master, as DestroyBody would.
                Object.Destroy(drone.gameObject);
            }
            else
            {
                master.DestroyBody();
            }

            master.RestoreOriginalBodyPrefab();
            DroneImprovementsPlugin.Log.LogInfo($"{Util.GetBestMasterName(master)} disconnected from their drone.");
        }
    }
}
