using System.Linq;
using BepInEx;
using BepInEx.Logging;
using DroneImprovements.Hooks;
using DroneImprovements.Skills;
using RoR2;

namespace DroneImprovements
{
    /// <summary>
    /// Improves the base game's Remote Operation (dead players piloting drones): survivor actions and gold for drone
    /// players, sprint and descend flight controls, Disconnect and Teleport to Player skills, and objectives that
    /// don't wait for drones.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(RiskOfOptionsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DroneImprovementsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "revor.DroneImprovements";
        public const string PluginName = "DroneImprovements";
        public const string PluginVersion = "1.2.0"; // tools/package.py checks this against the manifest

        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            // The mod adds skills and entity states to the game's catalogs and has its own network messages, so every
            // player needs the same version. The server refuses players whose list of network mods differs, and lobby
            // searches filter on it.
            NetworkModCompatibilityHelper.networkModList =
                NetworkModCompatibilityHelper.networkModList.Append(PluginGUID + ";" + PluginVersion);
            PluginConfig.Init(Config);
            HostSettings.Init();
            DroneSkills.Init();
            RoR2Application.onLoad += DroneBodyReport.OnGameLoaded;
            InteractionHooks.Init();
            GoldHooks.Init();
            FlightHooks.Init();
            HoldoutZoneHooks.Init();
            AllPlayersCheckHooks.Init();
            ArenaVoidKillHooks.Init();
            if (RiskOfOptionsCompat.IsInstalled)
            {
                RiskOfOptionsCompat.Init();
            }
        }
    }
}
