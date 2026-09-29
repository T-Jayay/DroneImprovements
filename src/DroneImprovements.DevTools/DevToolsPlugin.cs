using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DroneImprovements.DevTools
{
    /// <summary>
    /// Makes the multiplayer-only drone flow testable in a solo run: keeps the run alive after you die and spawns
    /// invincible AI "stand-in players" that count as players for teleporting and holdout zone charging.
    /// Drone selection is left completely vanilla (spectator menu, discovered drones, normal cost).
    /// Only for the test profile.
    /// </summary>
    [BepInPlugin(PluginGUID, "DroneImprovements DevTools", "1.0.0")]
    [BepInDependency(DroneImprovementsPlugin.PluginGUID)]
    public class DevToolsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "revor.DroneImprovements.DevTools";

        private static readonly MethodInfo runPreventGameOverSetter = AccessTools.PropertySetter(typeof(Run), nameof(Run.preventGameOver));
        internal static readonly List<CharacterBody> standIns = new List<CharacterBody>();

        private ConfigEntry<bool> preventGameOver;
        private ConfigEntry<KeyCode> keyKill;
        private ConfigEntry<KeyCode> keyMoney;
        private ConfigEntry<KeyCode> keyStandIn;
        private ConfigEntry<KeyCode> keyGameOver;
        private bool showHelp = true;
        private string lastAction = "";
        private float lastActionTime;
        private Harmony harmony;

        private void Awake()
        {
            preventGameOver = Config.Bind("DevTools", "PreventGameOver", true,
                "Keep solo runs going after you die so you can remote-operate a drone.");

            keyKill = Config.Bind("Keys", "KillYourself", KeyCode.F6, "");
            keyMoney = Config.Bind("Keys", "GiveMoney", KeyCode.F7, "");
            // F8 is commonly grabbed by overlays (NVIDIA, Overwolf, ...) before the game sees it.
            keyStandIn = Config.Bind("Keys", "SpawnStandIn", KeyCode.F4, "");
            keyGameOver = Config.Bind("Keys", "TogglePreventGameOver", KeyCode.F10, "");

            harmony = new Harmony(PluginGUID);
            harmony.PatchAll(typeof(DevToolsPlugin).Assembly);

            Run.onRunStartGlobal += OnRunStart;
            DroneTeleportApi.CollectAdditionalTargets += (drone, targets) => targets.AddRange(standIns);
            Logger.LogWarning("DroneImprovements DevTools loaded: this is a testing plugin, do not use it in real runs.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private void OnRunStart(Run run)
        {
            standIns.Clear();
            ApplyPreventGameOver();
        }

        private void ApplyPreventGameOver()
        {
            if (NetworkServer.active && Run.instance)
            {
                runPreventGameOverSetter.Invoke(Run.instance, new object[] { preventGameOver.Value });
            }
        }

        private void Update()
        {
            standIns.RemoveAll(b => !b);
            if (!Run.instance)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                showHelp = !showHelp;
            }
            if (Util.IsChatWindowOpen())
            {
                return;
            }
            if (Input.GetKeyDown(keyKill.Value)) KillSelf();
            if (Input.GetKeyDown(keyMoney.Value)) GiveMoney();
            if (Input.GetKeyDown(keyStandIn.Value)) SpawnStandIn();
            if (Input.GetKeyDown(keyGameOver.Value))
            {
                preventGameOver.Value = !preventGameOver.Value;
                ApplyPreventGameOver();
                Report("Prevent game over: " + (preventGameOver.Value ? "ON" : "OFF"));
            }
        }

        private static CharacterMaster LocalMaster => LocalUserManager.GetFirstLocalUser()?.cachedMaster;

        private bool RequireHost()
        {
            if (NetworkServer.active)
            {
                return true;
            }
            Report("Host only.");
            return false;
        }

        private void KillSelf()
        {
            if (!RequireHost()) return;
            CharacterBody body = LocalMaster ? LocalMaster.GetBody() : null;
            if (body && body.healthComponent)
            {
                body.healthComponent.Suicide();
                Report("Killed " + body.GetDisplayName());
            }
        }

        private void GiveMoney()
        {
            if (!RequireHost()) return;
            CharacterMaster master = LocalMaster;
            if (master)
            {
                master.GiveMoney(1000);
                Report("+$1000");
            }
        }

        /// <summary>Spawns an invincible AI survivor on the player team that stands in for another player.</summary>
        private void SpawnStandIn()
        {
            if (!RequireHost()) return;
            CharacterMaster localMaster = LocalMaster;
            if (!localMaster) return;

            GameObject masterPrefab = MasterCatalog.FindMasterPrefab("CommandoMonsterMaster");
            if (!masterPrefab)
            {
                Report("CommandoMonsterMaster not found");
                return;
            }

            CharacterBody localBody = localMaster.GetBody();
            Vector3 origin = localBody ? localBody.footPosition + localBody.inputBank.aimDirection * 3f : localMaster.deathFootPosition;
            CharacterBody bodyPrefab = masterPrefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>();
            Vector3 position = TeleportHelper.FindSafeTeleportDestination(origin, bodyPrefab, RoR2Application.rng) ?? origin;

            CharacterMaster standIn = new MasterSummon
            {
                masterPrefab = masterPrefab,
                position = position,
                rotation = Quaternion.identity,
                teamIndexOverride = TeamIndex.Player,
                ignoreTeamMemberLimit = true
            }.Perform();
            CharacterBody standInBody = standIn ? standIn.GetBody() : null;
            if (!standInBody)
            {
                Report("Failed to spawn stand-in");
                return;
            }
            standInBody.healthComponent.godMode = true;
            standIns.Add(standInBody);
            Report("Spawned stand-in player");
        }

        private void Report(string message)
        {
            lastAction = message;
            lastActionTime = Time.unscaledTime;
            Logger.LogInfo(message);
        }

        private void OnGUI()
        {
            if (!Run.instance || !showHelp)
            {
                return;
            }
            GUI.color = Color.white;
            // Below the money / lunar coin counters.
            GUILayout.BeginArea(new Rect(10, Screen.height * 0.22f, 340, 170), GUI.skin.box);
            GUILayout.Label("<b>DroneImprovements DevTools</b>  (F5 hide)");
            GUILayout.Label($"{keyKill.Value}  Kill yourself");
            GUILayout.Label($"{keyMoney.Value}  +$1000");
            GUILayout.Label($"{keyStandIn.Value}  Spawn stand-in player ({standIns.Count} alive)");
            GUILayout.Label($"{keyGameOver.Value} Prevent game over: " + (preventGameOver.Value ? "ON" : "OFF"));
            if (!string.IsNullOrEmpty(lastAction) && Time.unscaledTime - lastActionTime < 4f)
            {
                GUILayout.Label("> " + lastAction);
            }
            GUILayout.EndArea();
        }
    }

    /// <summary>
    /// Spectating only targets bodies owned by a NetworkUser, so in solo there is nothing to spectate after dying,
    /// which also means no spectator HUD and no Remote Operation menu. Let the stand-ins be spectated.
    /// </summary>
    [HarmonyPatch(typeof(CameraRigControllerSpectateControls), "CanUserSpectateBody")]
    internal static class StandInSpectatePatch
    {
        private static void Postfix(CharacterBody body, ref bool __result)
        {
            if (!__result && body && DevToolsPlugin.standIns.Contains(body))
            {
                __result = true;
            }
        }
    }

    // Stand-ins are AI, which the game ignores for holdout zones. Count them as players so the
    // teleporter behaves like it would with another (living, non-drone) player in the game.

    [HarmonyPatch(typeof(HoldoutZoneController), "CountLivingPlayers")]
    internal static class StandInLivingPlayersPatch
    {
        private static void Postfix(TeamIndex teamIndex, ref int __result)
        {
            foreach (CharacterBody body in DevToolsPlugin.standIns)
            {
                if (body && body.teamComponent.teamIndex == teamIndex && body.healthComponent.alive)
                {
                    __result++;
                }
            }
        }
    }

    [HarmonyPatch(typeof(HoldoutZoneController), nameof(HoldoutZoneController.CountPlayersInRadius))]
    internal static class StandInPlayersInRadiusPatch
    {
        private static readonly Func<HoldoutZoneController, Vector3, float, Vector3, bool> isPointInChargingRadius =
            AccessTools.MethodDelegate<Func<HoldoutZoneController, Vector3, float, Vector3, bool>>(
                AccessTools.Method(typeof(HoldoutZoneController), "IsPointInChargingRadius",
                    new[] { typeof(HoldoutZoneController), typeof(Vector3), typeof(float), typeof(Vector3) }));

        private static void Postfix(HoldoutZoneController holdoutZoneController, Vector3 origin, float chargingRadiusSqr, TeamIndex teamIndex, ref int __result)
        {
            foreach (CharacterBody body in DevToolsPlugin.standIns)
            {
                if (body && body.teamComponent.teamIndex == teamIndex && body.healthComponent.alive
                    && isPointInChargingRadius(holdoutZoneController, origin, chargingRadiusSqr, body.corePosition))
                {
                    __result++;
                }
            }
        }
    }
}
