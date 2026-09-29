using System;
using System.Reflection;
using HarmonyLib;
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
        private static readonly NetworkMessageDelegate disconnectRequestHandler = HandleDisconnectRequest;

        // The private CharacterMaster members that CharacterMaster.OnBodyDeath uses; resolved once in Init.
        private static Action<CharacterMaster, Vector3> setDeathFootPosition;
        private static Action<CharacterMaster, bool> setLostBodyToDeath;
        private static AccessTools.FieldRef<CharacterMaster, bool> preventRespawnUntilNextStageServer;
        private static Action<CharacterMaster> restoreOriginalBodyPrefab;

        /// <summary>
        /// False when a game update removed a member Disconnect needs; the skill then stays greyed out and the host
        /// refuses requests.
        /// </summary>
        public static bool IsDisconnectAvailable { get; private set; }

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

        public static void Init()
        {
            // The handler is registered even when Disconnect is unavailable, so a request is refused with a log line
            // instead of arriving as an unknown message (which makes UNet drop the rest of the packet).
            NetworkManagerSystem.onStartServerGlobal += OnStartServer;
            IsDisconnectAvailable = ResolveMasterMembers();
        }

        private static bool ResolveMasterMembers()
        {
            MethodInfo footSetter =
                AccessTools.PropertySetter(typeof(CharacterMaster), nameof(CharacterMaster.deathFootPosition));
            if (footSetter == null)
            {
                return ReportMissing("CharacterMaster.deathFootPosition setter");
            }
            MethodInfo lostBodySetter =
                AccessTools.PropertySetter(typeof(CharacterMaster), nameof(CharacterMaster.lostBodyToDeath));
            if (lostBodySetter == null)
            {
                return ReportMissing("CharacterMaster.lostBodyToDeath setter");
            }
            FieldInfo preventRespawnField =
                AccessTools.Field(typeof(CharacterMaster), "preventRespawnUntilNextStageServer");
            if (preventRespawnField == null || preventRespawnField.FieldType != typeof(bool))
            {
                return ReportMissing("CharacterMaster.preventRespawnUntilNextStageServer");
            }
            MethodInfo restoreMethod =
                AccessTools.Method(typeof(CharacterMaster), "RestoreOriginalBodyPrefab", Type.EmptyTypes);
            if (restoreMethod == null)
            {
                return ReportMissing("CharacterMaster.RestoreOriginalBodyPrefab()");
            }
            try
            {
                setDeathFootPosition = AccessTools.MethodDelegate<Action<CharacterMaster, Vector3>>(footSetter);
                setLostBodyToDeath = AccessTools.MethodDelegate<Action<CharacterMaster, bool>>(lostBodySetter);
                preventRespawnUntilNextStageServer =
                    AccessTools.FieldRefAccess<CharacterMaster, bool>(preventRespawnField);
                restoreOriginalBodyPrefab = AccessTools.MethodDelegate<Action<CharacterMaster>>(restoreMethod);
            }
            catch (Exception e)
            {
                DroneImprovementsPlugin.Log.LogError($"Disconnect is disabled: CharacterMaster changed. {e}");
                return false;
            }
            return true;
        }

        private static bool ReportMissing(string member)
        {
            DroneImprovementsPlugin.Log.LogError($"Disconnect is disabled: the game no longer has {member}.");
            return false;
        }

        /// <summary>A server is starting (the game has just registered its own handlers).</summary>
        private static void OnStartServer()
        {
            try
            {
                MessageIds.RegisterServerHandler(MessageIds.DisconnectRequest, disconnectRequestHandler);
            }
            catch (Exception e)
            {
                DroneImprovementsPlugin.Log.LogError($"Clients won't be able to Disconnect from their drones. {e}");
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
            if (!NetworkServer.active || !DroneUtil.IsDronePlayer(drone))
            {
                return;
            }
            if (!IsDisconnectAvailable)
            {
                DroneImprovementsPlugin.Log.LogWarning("Refused a drone disconnect: Disconnect is disabled.");
                return;
            }
            CharacterMaster master = drone.master;
            // The request may arrive late: only remove the drone the player is still controlling.
            if (!master || master.GetBodyObject() != drone.gameObject
                || !drone.healthComponent || !drone.healthComponent.alive)
            {
                return;
            }

            DroneUtil.SpawnTeleportEffect(drone.gameObject, drone.corePosition, drone.radius);

            setDeathFootPosition(master, drone.footPosition);
            setLostBodyToDeath(master, true);
            master.preventGameOver = false;
            preventRespawnUntilNextStageServer(master) = true;
            master.inventory.SetEquipmentDisabled(false);

            if (drone.GetComponent<Inventory>())
            {
                // DestroyBody would copy this body's own inventory over the player's, so destroy the body directly.
                // Its OnDestroy tells the master, as DestroyBody would.
                UnityEngine.Object.Destroy(drone.gameObject);
            }
            else
            {
                master.DestroyBody();
            }

            restoreOriginalBodyPrefab(master);
            DroneImprovementsPlugin.Log.LogInfo($"{Util.GetBestMasterName(master)} disconnected from their drone.");
        }
    }
}
