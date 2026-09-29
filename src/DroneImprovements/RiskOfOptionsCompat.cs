using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using DroneImprovements.Skills;
using RiskOfOptions;
using RiskOfOptions.Options;

namespace DroneImprovements
{
    /// <summary>In-game settings through Risk of Options, only when that mod is installed.</summary>
    internal static class RiskOfOptionsCompat
    {
        public const string Guid = "com.rune580.riskofoptions";

        public static bool Enabled => Chainloader.PluginInfos.ContainsKey(Guid);

        // Kept separate and non-inlined so RiskOfOptions types are only loaded when the mod is present.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init()
        {
            ModSettingsManager.SetModDescription(
                "Remote Operation improvements. Settings marked (host) are decided by the host for everyone.");
            if (DroneSkills.TeleportSkill && DroneSkills.TeleportSkill.icon)
            {
                ModSettingsManager.SetModIcon(DroneSkills.TeleportSkill.icon);
            }
            ModSettingsManager.AddOption(new CheckBoxOption(PluginConfig.DroneSurvivorActions));
            ModSettingsManager.AddOption(new ChoiceOption(PluginConfig.DroneGold));
            ModSettingsManager.AddOption(new CheckBoxOption(PluginConfig.DronesCanChargeHoldoutZones));
            ModSettingsManager.AddOption(new CheckBoxOption(PluginConfig.IgnoreDronesForAllPlayerChecks));
            ModSettingsManager.AddOption(new CheckBoxOption(PluginConfig.SpareDronesFromArenaVoidKill));
            ModSettingsManager.AddOption(new CheckBoxOption(PluginConfig.DisconnectEnabled));
            ModSettingsManager.AddOption(new CheckBoxOption(PluginConfig.TeleportEnabled));
        }
    }
}
