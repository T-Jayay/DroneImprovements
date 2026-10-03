using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DroneImprovements.DevTools
{
    /// <summary>
    /// TEST-ONLY: never packaged or shipped. Makes DroneImprovements' multiplayer-only drone flow testable in a solo
    /// run: keeps the run going after you die (PreventGameOver) and spawns invincible AI "stand-in players" that count
    /// as players for teleporting, spectating and holdout zone charging. Drone selection stays as in the base game
    /// (spectator menu, discovered drones, normal cost). PreventGameOver applies to everyone in the lobby when the
    /// host runs this plugin, so it warns whenever more than one player is present.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(DroneImprovementsPlugin.PluginGUID)]
    public sealed class DevToolsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "revor.DroneImprovements.DevTools";
        public const string PluginName = "DroneImprovements DevTools";
        public const string PluginVersion = "1.0.0";

        private const string StandInMasterName = "CommandoMonsterMaster";
        private const uint MoneyPerPress = 1000;
        private const float StandInSpawnDistance = 3f;
        private const float ActionMessageSeconds = 4f;

        // The panel sits at the left edge, below the money and lunar coin counters.
        private const float PanelLeft = 10f;
        private const float PanelTopScreenFraction = 0.22f;
        private const float PanelWidth = 380f;
        private const float HelpPanelHeight = 230f;
        private const float WarningPanelHeight = 60f;

        private static readonly List<CharacterBody> standIns = new List<CharacterBody>();
        private static readonly Predicate<CharacterBody> isDestroyed = body => !body;

        private ConfigEntry<bool> preventGameOver;
        private ConfigEntry<KeyCode> keyToggleHelp;
        private ConfigEntry<KeyCode> keyKillYourself;
        private ConfigEntry<KeyCode> keyGiveMoney;
        private ConfigEntry<KeyCode> keySpawnStandIn;
        private ConfigEntry<KeyCode> keyRemoveStandIns;
        private ConfigEntry<KeyCode> keyTogglePreventGameOver;

        private bool showHelp = true;
        private string lastAction = "";
        private float lastActionTime;
        private bool lastActionShown;
        private int shownStandInCount;
        private int shownPlayerCount;

        // The panel's text, rebuilt only when something on it changes (OnGUI runs several times a frame).
        private string helpText = "";
        private string warningText = "";
        private bool textIsStale = true;

        /// <summary>The stand-ins that haven't been destroyed yet (dead ones stay until their body is gone).</summary>
        internal static IReadOnlyList<CharacterBody> StandIns => standIns;

        private static CharacterMaster LocalMaster
        {
            get
            {
                LocalUser localUser = LocalUserManager.GetFirstLocalUser();
                return localUser != null ? localUser.cachedMaster : null;
            }
        }

        private void Awake()
        {
            preventGameOver = Config.Bind("DevTools", "PreventGameOver", true,
                "Keep a run going after every player has died, so you can remote-operate a drone solo. When you "
                + "host, this applies to everyone in the lobby.");
            keyToggleHelp = Config.Bind("Keys", "ToggleHelp", KeyCode.F5, "Show or hide the DevTools panel.");
            keyKillYourself = Config.Bind("Keys", "KillYourself", KeyCode.F6, "Kill your character (host only).");
            keyGiveMoney = Config.Bind("Keys", "GiveMoney", KeyCode.F7, $"Give yourself ${MoneyPerPress} (host only). "
                + "It is added to your money directly, so the gold settings don't affect it.");
            keySpawnStandIn = Config.Bind("Keys", "SpawnStandIn", KeyCode.F4,
                "Spawn an invincible AI stand-in player in front of you (host only). Spawn one before dying: solo, "
                + "only stand-ins can be spectated, and with nobody to spectate there is no Remote Operation menu. "
                + "Not F8, which overlays (NVIDIA, Overwolf, ...) often take before the game sees it.");
            keyRemoveStandIns = Config.Bind("Keys", "RemoveStandIns", KeyCode.F3,
                "Remove every stand-in player (host only), for example to test Teleport with nobody to go to.");
            keyTogglePreventGameOver = Config.Bind("Keys", "TogglePreventGameOver", KeyCode.F10,
                "Turn PreventGameOver on or off.");

            preventGameOver.SettingChanged += OnPreventGameOverChanged;
            Config.SettingChanged += OnAnySettingChanged;

            StandInHooks.Init();

            Run.onRunStartGlobal += OnRunStart;
            DroneTeleportApi.CollectAdditionalTargets += AddStandInTargets;
            Logger.LogWarning("DroneImprovements DevTools loaded: a testing plugin, never use it in real runs.");
        }

        private static void AddStandInTargets(CharacterBody drone, List<CharacterBody> targets)
        {
            targets.AddRange(standIns);
        }

        private void OnRunStart(Run run)
        {
            standIns.Clear();
            ApplyPreventGameOver(run);
        }

        private void OnPreventGameOverChanged(object sender, EventArgs e)
        {
            ApplyPreventGameOver(Run.instance);
        }

        private void OnAnySettingChanged(object sender, SettingChangedEventArgs e)
        {
            textIsStale = true;
        }

        private void ApplyPreventGameOver(Run run)
        {
            if (NetworkServer.active && run)
            {
                run.preventGameOver = preventGameOver.Value;
            }
        }

        private void Update()
        {
            standIns.RemoveAll(isDestroyed);
            if (!Run.instance)
            {
                return;
            }
            TrackPanelChanges();
            if (Util.IsChatWindowOpen())
            {
                return;
            }
            if (Input.GetKeyDown(keyToggleHelp.Value))
            {
                showHelp = !showHelp;
            }
            if (Input.GetKeyDown(keyKillYourself.Value))
            {
                KillYourself();
            }
            if (Input.GetKeyDown(keyGiveMoney.Value))
            {
                GiveMoney();
            }
            if (Input.GetKeyDown(keySpawnStandIn.Value))
            {
                SpawnStandIn();
            }
            if (Input.GetKeyDown(keyRemoveStandIns.Value))
            {
                RemoveStandIns();
            }
            if (Input.GetKeyDown(keyTogglePreventGameOver.Value))
            {
                preventGameOver.Value = !preventGameOver.Value;
                Report("Prevent game over: " + (preventGameOver.Value ? "ON" : "OFF"));
            }
        }

        /// <summary>Marks the panel's text as stale when what it shows changes; warns about other players.</summary>
        private void TrackPanelChanges()
        {
            int playerCount = NetworkUser.readOnlyInstancesList.Count;
            if (playerCount != shownPlayerCount)
            {
                if (playerCount > 1 && shownPlayerCount <= 1)
                {
                    Logger.LogWarning($"{playerCount} players are here. DevTools is for solo tests: when you host, "
                        + "PreventGameOver keeps the run going for everyone.");
                }
                shownPlayerCount = playerCount;
                textIsStale = true;
            }
            if (standIns.Count != shownStandInCount)
            {
                shownStandInCount = standIns.Count;
                textIsStale = true;
            }
            if (lastActionShown && Time.unscaledTime - lastActionTime >= ActionMessageSeconds)
            {
                lastActionShown = false;
                textIsStale = true;
            }
        }

        private bool RequireHost()
        {
            if (NetworkServer.active)
            {
                return true;
            }
            Report("Host only.");
            return false;
        }

        private void KillYourself()
        {
            if (!RequireHost())
            {
                return;
            }
            CharacterMaster master = LocalMaster;
            CharacterBody body = master ? master.GetBody() : null;
            if (body && body.healthComponent)
            {
                body.healthComponent.Suicide();
                Report("Killed " + body.GetDisplayName());
            }
        }

        private void GiveMoney()
        {
            if (!RequireHost())
            {
                return;
            }
            CharacterMaster master = LocalMaster;
            if (master)
            {
                master.money += MoneyPerPress;
                Report($"+${MoneyPerPress}");
            }
        }

        /// <summary>Spawns an invincible AI survivor on the player team that stands in for another player.</summary>
        private void SpawnStandIn()
        {
            if (!RequireHost())
            {
                return;
            }
            CharacterMaster localMaster = LocalMaster;
            if (!localMaster)
            {
                return;
            }
            GameObject masterPrefab = MasterCatalog.FindMasterPrefab(StandInMasterName);
            GameObject bodyPrefab = masterPrefab ? masterPrefab.GetComponent<CharacterMaster>().bodyPrefab : null;
            if (!bodyPrefab)
            {
                Report(StandInMasterName + " not found");
                return;
            }

            CharacterBody localBody = localMaster.GetBody();
            Vector3 origin = localBody && localBody.inputBank
                ? localBody.footPosition + localBody.inputBank.aimDirection * StandInSpawnDistance
                : localMaster.deathFootPosition;
            Vector3 position = TeleportHelper.FindSafeTeleportDestination(origin,
                bodyPrefab.GetComponent<CharacterBody>(), RoR2Application.rng) ?? origin;

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
                Report("Failed to spawn a stand-in");
                return;
            }
            standInBody.healthComponent.godMode = true;
            standIns.Add(standInBody);
            Report("Spawned a stand-in player");
        }

        /// <summary>Despawns the stand-ins (bodies and masters) without a death.</summary>
        private void RemoveStandIns()
        {
            if (!RequireHost())
            {
                return;
            }
            int removed = 0;
            foreach (CharacterBody body in standIns)
            {
                CharacterMaster master = body ? body.master : null;
                if (master)
                {
                    master.DestroyBody();
                    // Destroying a spawned object on the host despawns it for everyone (NetworkIdentity.OnDestroy).
                    Destroy(master.gameObject);
                    removed++;
                }
                else if (body)
                {
                    Destroy(body.gameObject);
                    removed++;
                }
            }
            standIns.Clear();
            Report($"Removed {removed} stand-in player(s)");
        }

        private void Report(string message)
        {
            lastAction = message;
            lastActionTime = Time.unscaledTime;
            lastActionShown = true;
            textIsStale = true;
            Logger.LogInfo(message);
        }

        private void RebuildText()
        {
            textIsStale = false;
            warningText = shownPlayerCount > 1
                ? $"<color=yellow>{shownPlayerCount} players here: DevTools is for solo tests, and PreventGameOver "
                    + "applies to all of them.</color>"
                : "";

            StringBuilder text = new StringBuilder();
            text.Append("<b>DroneImprovements DevTools</b>   (").Append(keyToggleHelp.Value).Append(": hide)\n");
            text.Append(keyKillYourself.Value).Append("   Kill yourself\n");
            text.Append(keyGiveMoney.Value).Append("   +$").Append(MoneyPerPress).Append('\n');
            text.Append(keySpawnStandIn.Value).Append("   Spawn a stand-in player (").Append(shownStandInCount)
                .Append(" alive)\n");
            text.Append(keyRemoveStandIns.Value).Append("   Remove the stand-in players\n");
            text.Append(keyTogglePreventGameOver.Value).Append("   Prevent game over: ")
                .Append(preventGameOver.Value ? "ON" : "OFF");
            if (warningText.Length != 0)
            {
                text.Append('\n').Append(warningText);
            }
            if (lastActionShown)
            {
                text.Append("\n> ").Append(lastAction);
            }
            helpText = text.ToString();
        }

        private void OnGUI()
        {
            if (!Run.instance || (!showHelp && shownPlayerCount <= 1))
            {
                return;
            }
            if (textIsStale)
            {
                RebuildText();
            }
            // Another OnGUI may have left a tint behind; don't leave ours behind either.
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            Rect area = new Rect(PanelLeft, Screen.height * PanelTopScreenFraction, PanelWidth,
                showHelp ? HelpPanelHeight : WarningPanelHeight);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(showHelp ? helpText : warningText);
            GUILayout.EndArea();
            GUI.color = previousColor;
        }
    }
}
