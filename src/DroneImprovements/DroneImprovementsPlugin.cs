using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using DroneImprovements.Patches;
using DroneImprovements.Skills;
using HarmonyLib;
using RoR2;

namespace DroneImprovements
{
    /// <summary>
    /// Improves the base game's Remote Operation (dead players piloting drones): survivor actions and gold for drone
    /// players, Disconnect and Teleport to Player skills, and objectives that don't wait for drones. Each feature is
    /// patched on its own, so a game update that breaks one leaves the others working.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(RiskOfOptionsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DroneImprovementsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "revor.DroneImprovements";
        public const string PluginName = "DroneImprovements";
        public const string PluginVersion = "1.1.1"; // tools/package.py checks this against the manifest

        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            RequireSameModOnEveryPlayer();
            PluginConfig.Init(Config);
            TryInit("Host settings for clients", HostSettings.Init);
            TryInit("Disconnect", DroneNetworking.Init);
            TryInit("Drone abilities", DroneSkills.Init);
            RoR2Application.onLoad += DroneBodyReport.OnGameLoaded;
            ApplyPatches();
            if (RiskOfOptionsCompat.IsInstalled)
            {
                try
                {
                    RiskOfOptionsCompat.Init();
                }
                catch (Exception e)
                {
                    Log.LogError($"Risk of Options settings failed to load; the config file still works. {e}");
                }
            }
        }

        /// <summary>
        /// Adds the mod to the game's list of network mods, whose hash players must share to join each other (the
        /// server kicks mismatches, and lobby searches filter on it). The mod adds skills and entity states to the
        /// game's catalogs and has its own network messages, so a player with another version, or without it, would
        /// get a broken session instead. R2API adds its own entries to this list and only ever removes those.
        /// </summary>
        private static void RequireSameModOnEveryPlayer()
        {
            try
            {
                string entry = PluginGUID + ";" + PluginVersion;
                IEnumerable<string> networkMods = NetworkModCompatibilityHelper.networkModList;
                if (!networkMods.Contains(entry))
                {
                    NetworkModCompatibilityHelper.networkModList = networkMods.Append(entry);
                    Log.LogDebug($"Added {entry} to the network mod list.");
                }
            }
            catch (Exception e)
            {
                Log.LogError("Couldn't add the mod to the network mod list, so players with another version (or "
                    + $"without the mod) can join and get a broken session. {e}");
            }
        }

        /// <summary>Runs one feature's setup; a failure is logged and leaves the rest of the mod working.</summary>
        private static void TryInit(string feature, Action init)
        {
            try
            {
                init();
            }
            catch (Exception e)
            {
                Log.LogError($"{feature}: setup failed, so this part of the mod doesn't work. {e}");
            }
        }

        /// <summary>
        /// Patches feature by feature. The patch classes of one feature only work together (for example the escape
        /// ship's three patches), so each feature is applied, or left out, as a whole.
        /// </summary>
        private static void ApplyPatches()
        {
            // Every feature tells drone players apart by this flag: without it, none of them can work.
            if (!PatchSafety.GameHasGetter(typeof(CharacterBody), nameof(CharacterBody.isRemoteOp), typeof(bool)))
            {
                Log.LogError("The mod is disabled: the game no longer marks Remote Operation drones as it expects "
                    + "(CharacterBody.isRemoteOp), so everything works as in the base game.");
                return;
            }

            List<string> disabled = new List<string>();
            DroneInteractors.IsPatched = PatchFeature(disabled, FeatureNames.SurvivorActions,
                "DroneSurvivorActions has no effect: drone players are locked out as in the base game",
                typeof(InteractorRemoteOpPatch), typeof(PickupPermissionPatch));
            PatchFeature(disabled, FeatureNames.DroneGold,
                "DroneGold has no effect: drone players earn gold as in the base game",
                typeof(DeathRewardsContextPatch), typeof(MoneyPickupContextPatch), typeof(HitEnemyContextPatch),
                typeof(TakeDamageContextPatch), typeof(GiveTeamMoneyPatch), typeof(AddOrbPatch),
                typeof(GoldOrbArrivalPatch), typeof(GiveMoneyPatch), typeof(BarrelInteractabilityPatch),
                typeof(BloodShrineInteractabilityPatch));
            PatchFeature(disabled, FeatureNames.HoldoutZones,
                "DronesCanChargeHoldoutZones has no effect: drones don't help charge holdout zones",
                typeof(HoldoutZoneUpdatePatch), typeof(HoldoutZoneLivingPlayersPatch),
                typeof(HoldoutZonePlayersInRadiusPatch));
            PatchFeature(disabled, FeatureNames.MithrixArena,
                "the Mithrix arena entrance waits for drone players as in the base game",
                typeof(MultiBodyTriggerCandidatesPatch));
            PatchFeature(disabled, FeatureNames.AllPlayersTrigger,
                "all-players triggers wait for drone players as in the base game",
                typeof(AllPlayersTriggerPatch));
            PatchFeature(disabled, FeatureNames.EscapeShip,
                "the escape ship waits for drone players as in the base game",
                typeof(EscapeZoneLivingPlayersPatch), typeof(EscapeZonePlayersAboardPatch),
                typeof(EscapeZoneSpareDronesAboardPatch));
            PatchFeature(disabled, FeatureNames.EscapeShipPlayerCount,
                "EscapeSequenceExtractionZone.CountPlayersInRadius counts drone players, for other mods",
                typeof(EscapeZoneCountPlayersInRadiusPatch));
            PatchFeature(disabled, FeatureNames.ArenaVoidKill,
                "SpareDronesFromArenaVoidKill has no effect: drones outside the sealed Mithrix arena are killed",
                typeof(ArenaVoidKillScopePatch), typeof(ArenaVoidKillSpareDronesPatch));
            if (DroneSkills.IsInitialized)
            {
                PatchFeature(disabled, FeatureNames.Abilities, "drones get no Disconnect or Teleport to Player skill",
                    typeof(BodyCatalogSetBodyPrefabsPatch));
            }
            else
            {
                disabled.Add(FeatureNames.Abilities);
            }

            if (disabled.Count == 0)
            {
                Log.LogInfo("All features patched.");
            }
            else
            {
                Log.LogWarning($"Working as in the base game: {string.Join(", ", disabled)} (see the errors above).");
            }
        }

        /// <summary>
        /// Applies the patch classes of one feature with their own Harmony instance. If any class fails (for example
        /// because a game update renamed a patched method, or a Prepare() found a game member missing), every patch
        /// of the feature is removed again, so that part of the game runs unmodified rather than half patched.
        /// </summary>
        private static bool PatchFeature(List<string> disabled, string feature, string fallback,
            params Type[] patchClasses)
        {
            Harmony harmony = new Harmony(PluginGUID + "." + feature);
            try
            {
                foreach (Type patchClass in patchClasses)
                {
                    List<MethodInfo> patched = harmony.CreateClassProcessor(patchClass).Patch();
                    // Harmony returns an empty list when a patch class's Prepare() says no (it logged why).
                    if (patched == null || patched.Count == 0)
                    {
                        throw new InvalidOperationException($"{patchClass.Name} patched nothing.");
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                disabled.Add(feature);
                Log.LogError($"{feature}: {fallback}. A game update or another mod changed game code it relies on. "
                    + $"{e}");
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception unpatchError)
                {
                    Log.LogError($"{feature}: couldn't remove the patches that did apply. {unpatchError}");
                }
                return false;
            }
        }
    }
}
