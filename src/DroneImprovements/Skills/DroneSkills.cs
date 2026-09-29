using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using EntityStates;
using HarmonyLib;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Skills;
using UnityEngine;

namespace DroneImprovements.Skills
{
    /// <summary>
    /// Adds two skills to every Remote Operation drone body, so they show in the HUD skill bar like any ability:
    /// Disconnect in the Utility slot and Teleport to Player in the Special slot (R). No base-game Remote Operation
    /// drone uses those two slots. The name and description tokens are plain text, which the game shows as is.
    /// </summary>
    internal static class DroneSkills
    {
        public const string StateMachineName = "DroneImprovementsAbility";

        private const string TeleportIconResource = "DroneImprovements.Assets.texDroneTeleportIcon.png";
        private const string DisconnectIconResource = "DroneImprovements.Assets.texDroneDisconnectIcon.png";

        // A zero recharge interval would make the game restock Disconnect in the middle of the hold.
        private const float MinDisconnectRechargeInterval = 0.1f;

        private static AccessTools.FieldRef<GenericSkill, SkillFamily> skillFamily;

        public static DroneTeleportSkillDef TeleportSkill { get; private set; }
        public static DroneDisconnectSkillDef DisconnectSkill { get; private set; }
        public static SkillFamily TeleportFamily { get; private set; }
        public static SkillFamily DisconnectFamily { get; private set; }

        /// <summary>True once the skills are created and registered; they are only added to bodies then.</summary>
        public static bool IsInitialized { get; private set; }

        public static void Init()
        {
            // GenericSkill has no public way to set its skill family.
            FieldInfo familyField = AccessTools.Field(typeof(GenericSkill), "_skillFamily");
            if (familyField == null || familyField.FieldType != typeof(SkillFamily))
            {
                DroneImprovementsPlugin.Log.LogError(
                    "Drone abilities are disabled: the game no longer has GenericSkill._skillFamily.");
                return;
            }
            skillFamily = AccessTools.FieldRefAccess<GenericSkill, SkillFamily>(familyField);

            TeleportSkill = CreateSkillDef<DroneTeleportSkillDef>("DroneImprovementsTeleport", "Teleport to Player",
                TeleportIconResource, typeof(DroneTeleportState), InterruptPriority.Skill);

            DisconnectSkill = CreateSkillDef<DroneDisconnectSkillDef>("DroneImprovementsDisconnect", "Disconnect",
                DisconnectIconResource, typeof(DroneDisconnectState), InterruptPriority.Any);
            DisconnectSkill.skillDescriptionToken =
                "<style=cIsUtility>Hold</style> to end remote operation and return to spectating.";
            // The recharge interval only drives the hold progress display (see DroneDisconnectState), and the skill
            // is given back when the state ends.
            DisconnectSkill.beginSkillCooldownOnSkillEnd = true;

            ApplyRechargeSettings();
            PluginConfig.TeleportCooldown.SettingChanged += OnRechargeSettingChanged;
            PluginConfig.DisconnectHoldSeconds.SettingChanged += OnRechargeSettingChanged;

            TeleportFamily = CreateFamily("DroneImprovementsTeleportFamily", TeleportSkill);
            DisconnectFamily = CreateFamily("DroneImprovementsDisconnectFamily", DisconnectSkill);

            ContentManager.collectContentPackProviders += AddContentPackProvider;
            IsInitialized = true;
        }

        /// <summary>A skill with the settings both share: one charge, no combat skill, sprinting unaffected.</summary>
        private static T CreateSkillDef<T>(string name, string nameToken, string iconResource, Type stateType,
            InterruptPriority interruptPriority) where T : SkillDef
        {
            T skill = ScriptableObject.CreateInstance<T>();
            // SkillDef hides ScriptableObject.name with a property that returns null.
            ((ScriptableObject)skill).name = name;
            skill.skillName = name;
            skill.skillNameToken = nameToken;
            skill.icon = EmbeddedSprites.Load(iconResource);
            skill.activationStateMachineName = StateMachineName;
            skill.activationState = new SerializableEntityStateType(stateType);
            skill.interruptPriority = interruptPriority;
            skill.baseMaxStock = 1;
            skill.rechargeStock = 1;
            skill.requiredStock = 1;
            skill.stockToConsume = 1;
            skill.fullRestockOnAssign = true;
            // Always a single charge: bonus charges from items (Hardlight Afterburner, Lysate Cell) don't apply.
            skill.dontAllowPastMaxStocks = true;
            skill.hideStockCount = true;
            skill.isCombatSkill = false;
            skill.cancelSprintingOnActivation = false;
            skill.canceledFromSprinting = false;
            // Honoured on bodies running GenericCharacterMain; see SkillKeyPress for the flying drones.
            skill.mustKeyPress = true;
            return skill;
        }

        private static SkillFamily CreateFamily(string name, SkillDef skill)
        {
            SkillFamily family = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)family).name = name;
            family.variants = new[]
            {
                new SkillFamily.Variant
                {
                    skillDef = skill,
                    viewableNode = new ViewablesCatalog.Node(skill.skillName, false, null)
                }
            };
            return family;
        }

        /// <summary>Copies the per-player cooldown settings into the skills.</summary>
        private static void ApplyRechargeSettings()
        {
            float cooldown = PluginConfig.TeleportCooldown.Value;
            TeleportSkill.baseRechargeInterval = cooldown;
            TeleportSkill.skillDescriptionToken = "Teleport next to the nearest living player. "
                + $"<style=cIsUtility>{cooldown:0.#}s base cooldown</style>.";
            DisconnectSkill.baseRechargeInterval =
                Mathf.Max(MinDisconnectRechargeInterval, PluginConfig.DisconnectHoldSeconds.Value);
        }

        /// <summary>A cooldown setting changed: update the skills, including those of existing drones.</summary>
        private static void OnRechargeSettingChanged(object sender, EventArgs e)
        {
            ApplyRechargeSettings();
            ReadOnlyCollection<CharacterBody> bodies = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < bodies.Count; i++)
            {
                SkillLocator locator = bodies[i] ? bodies[i].skillLocator : null;
                if (locator)
                {
                    RefreshRechargeInterval(locator.utility);
                    RefreshRechargeInterval(locator.special);
                }
            }
        }

        private static void RefreshRechargeInterval(GenericSkill skill)
        {
            // GenericSkill caches its final recharge interval.
            if (skill && (skill.skillDef == TeleportSkill || skill.skillDef == DisconnectSkill))
            {
                skill.RecalculateValues();
            }
        }

        private static void AddContentPackProvider(ContentManager.AddContentPackProviderDelegate addProvider)
        {
            addProvider(new DroneContentPackProvider());
        }

        /// <summary>
        /// Adds the skills to the Remote Operation bodies among the given prefabs: the remoteOpBody of each DroneDef,
        /// the same test CharacterBody.Start uses to set isRemoteOp. Called from a hook, inside its try (see
        /// <see cref="Patches.PatchSafety"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddToRemoteOpBodies(GameObject[] bodyPrefabs)
        {
            HashSet<GameObject> remoteOpBodies = new HashSet<GameObject>();
            foreach (DroneDef droneDef in ContentManager.droneDefs ?? Array.Empty<DroneDef>())
            {
                if (droneDef && droneDef.remoteOpBody)
                {
                    remoteOpBodies.Add(droneDef.remoteOpBody);
                }
            }
            foreach (GameObject prefab in bodyPrefabs)
            {
                if (!prefab || !remoteOpBodies.Contains(prefab))
                {
                    continue;
                }
                try
                {
                    AddToBody(prefab);
                }
                catch (Exception e)
                {
                    DroneImprovementsPlugin.Log.LogError($"Couldn't add the drone abilities to {prefab.name}. {e}");
                }
            }
        }

        /// <summary>
        /// Adds the skills to one body prefab. The slots are added whatever the config, so players with different
        /// settings agree on the body layout; the config only makes each skill usable or not.
        /// </summary>
        private static void AddToBody(GameObject prefab)
        {
            // Already done (the catalog can be rebuilt with the body_reload_all console commands).
            if (EntityStateMachine.FindByCustomName(prefab, StateMachineName))
            {
                return;
            }
            SkillLocator locator = prefab.GetComponent<SkillLocator>();
            if (!locator)
            {
                DroneImprovementsPlugin.Log.LogWarning($"{prefab.name} has no SkillLocator; no drone abilities added.");
                return;
            }
            bool addDisconnect = !locator.utility;
            bool addTeleport = !locator.special;
            if (!addDisconnect)
            {
                DroneImprovementsPlugin.Log.LogWarning($"{prefab.name} already has a Utility skill; no Disconnect.");
            }
            if (!addTeleport)
            {
                DroneImprovementsPlugin.Log.LogWarning($"{prefab.name} already has a Special skill; no Teleport.");
            }
            if (!addDisconnect && !addTeleport)
            {
                return;
            }

            // The skills' own state machine. It isn't added to the body's NetworkStateMachine: the states only act
            // on the drone owner's machine (the teleport and the disconnect request), so there is nothing to sync.
            EntityStateMachine machine = prefab.AddComponent<EntityStateMachine>();
            machine.customName = StateMachineName;
            machine.initialStateType = new SerializableEntityStateType(typeof(Idle));
            machine.mainStateType = new SerializableEntityStateType(typeof(Idle));

            if (addDisconnect)
            {
                locator.utility = AddSkill(prefab, DisconnectFamily, "DroneDisconnect");
            }
            if (addTeleport)
            {
                locator.special = AddSkill(prefab, TeleportFamily, "DroneTeleport");
            }
        }

        private static GenericSkill AddSkill(GameObject prefab, SkillFamily family, string slotName)
        {
            GenericSkill skill = prefab.AddComponent<GenericSkill>();
            skill.skillName = slotName;
            skill.hideInCharacterSelect = true;
            skillFamily(skill) = family;
            return skill;
        }

        /// <summary>Registers the skills, families and states with the game's content catalogs.</summary>
        private sealed class DroneContentPackProvider : IContentPackProvider
        {
            private readonly ContentPack contentPack = new ContentPack();

            public string identifier => DroneImprovementsPlugin.PluginGUID;

            public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
            {
                contentPack.skillDefs.Add(new SkillDef[] { TeleportSkill, DisconnectSkill });
                contentPack.skillFamilies.Add(new[] { TeleportFamily, DisconnectFamily });
                contentPack.entityStateTypes.Add(new[] { typeof(DroneTeleportState), typeof(DroneDisconnectState) });
                args.ReportProgress(1f);
                yield break;
            }

            public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
            {
                ContentPack.Copy(contentPack, args.output);
                args.ReportProgress(1f);
                yield break;
            }

            public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
            {
                args.ReportProgress(1f);
                yield break;
            }
        }
    }

    /// <summary>
    /// Adds the drone skills to the Remote Operation bodies before BodyCatalog records each body's components and
    /// skill slots, so loadouts and networking see the new slots on every machine.
    /// </summary>
    [HarmonyPatch(typeof(BodyCatalog), "SetBodyPrefabs", typeof(GameObject[]))]
    internal static class BodyCatalogSetBodyPrefabsPatch
    {
        private static void Prefix(GameObject[] newBodyPrefabs)
        {
            // An exception here would escape into BodyCatalog.Init and stop the game from loading.
            try
            {
                DroneSkills.AddToRemoteOpBodies(newBodyPrefabs);
            }
            catch (Exception e)
            {
                DroneImprovementsPlugin.Log.LogError($"Couldn't add the drone abilities. {e}");
            }
        }
    }
}
