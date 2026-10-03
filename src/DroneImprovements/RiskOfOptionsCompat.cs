using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine.Networking;

namespace DroneImprovements
{
    /// <summary>
    /// In-game settings through Risk of Options, a soft dependency. Its types appear only inside the bodies of the
    /// non-inlined methods here, so they are only loaded when it is installed.
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        public const string Guid = "com.rune580.riskofoptions";

        // The store icon, embedded from thunderstore/DroneImprovements/icon.png by Directory.Build.targets.
        private const string IconResource = "DroneImprovements.icon.png";

        private const float DisconnectHoldStep = 0.1f;
        private const float TeleportCooldownStep = 1f;
        private const float SprintSpeedMultiplierStep = 0.1f;

        public static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(Guid);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init()
        {
            ModSettingsManager.SetModDescription("Makes Remote Operation drones useful for dead players: survivor "
                + "actions, flight controls, Disconnect and Teleport skills, and objectives that don't wait for "
                + "drones. Settings marked (host) are decided by the host for everyone and are greyed out while "
                + "you're a client; the others only affect your own drone.");
            ModSettingsManager.SetModIcon(EmbeddedSprites.Load(IconResource));

            AddCheckBox(PluginConfig.DroneSurvivorActions, hostOnly: true);
            AddCheckBox(PluginConfig.DroneCombatGold, hostOnly: true);
            AddCheckBox(PluginConfig.DroneBarrelGold, hostOnly: true);
            AddCheckBox(PluginConfig.DroneShrineOfBloodGold, hostOnly: true);
            AddCheckBox(PluginConfig.DronesCanChargeHoldoutZones, hostOnly: true);
            AddCheckBox(PluginConfig.IgnoreDronesForAllPlayerChecks, hostOnly: true);
            AddCheckBox(PluginConfig.SpareDronesFromArenaVoidKill, hostOnly: true);
            AddCheckBox(PluginConfig.DisconnectEnabled, hostOnly: false);
            AddStepSlider(PluginConfig.DisconnectHoldSeconds, DisconnectHoldStep, "{0:0.0}s", hostOnly: false);
            AddCheckBox(PluginConfig.TeleportEnabled, hostOnly: false);
            AddStepSlider(PluginConfig.TeleportCooldown, TeleportCooldownStep, "{0:0}s", hostOnly: false);
            AddStepSlider(PluginConfig.SprintSpeedMultiplier, SprintSpeedMultiplierStep, "{0:0.0}x", hostOnly: true);
            AddKeyBind(PluginConfig.DescendKey);
        }

        /// <summary>Host settings only change anything on the host, so they are greyed out on a client.</summary>
        private static bool IsClientOfAnotherHost()
        {
            return NetworkClient.active && !NetworkServer.active;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void AddCheckBox(ConfigEntry<bool> entry, bool hostOnly)
        {
            CheckBoxConfig config = new CheckBoxConfig();
            if (hostOnly)
            {
                config.checkIfDisabled = IsClientOfAnotherHost;
            }
            ModSettingsManager.AddOption(new CheckBoxOption(entry, config));
        }

        /// <summary>A slider over the entry's acceptable range.</summary>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void AddStepSlider(ConfigEntry<float> entry, float step, string format, bool hostOnly)
        {
            AcceptableValueRange<float> range = (AcceptableValueRange<float>)entry.Description.AcceptableValues;
            StepSliderConfig config = new StepSliderConfig
            {
                min = range.MinValue,
                max = range.MaxValue,
                increment = step,
                FormatString = format
            };
            if (hostOnly)
            {
                config.checkIfDisabled = IsClientOfAnotherHost;
            }
            ModSettingsManager.AddOption(new StepSliderOption(entry, config));
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void AddKeyBind(ConfigEntry<KeyboardShortcut> entry)
        {
            ModSettingsManager.AddOption(new KeyBindOption(entry));
        }
    }
}
