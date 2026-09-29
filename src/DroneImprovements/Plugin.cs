using BepInEx;
using BepInEx.Logging;
using DroneImprovements.Skills;
using HarmonyLib;
using RoR2;

namespace DroneImprovements
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(RiskOfOptionsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public class DroneImprovementsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "revor.DroneImprovements";
        public const string PluginName = "DroneImprovements";
        public const string PluginVersion = "1.1.1";

        internal static ManualLogSource Log;

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            PluginConfig.Init(Config);
            DroneSkills.Init();
            HostSettings.Init();
            if (RiskOfOptionsCompat.Enabled)
            {
                RiskOfOptionsCompat.Init();
            }

            harmony = new Harmony(PluginGUID);
            harmony.PatchAll(typeof(DroneImprovementsPlugin).Assembly);

            RoR2Application.onLoad += DroneBodySetup.OnGameLoaded;
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
