using System.Collections;
using System.IO;
using System.Reflection;
using EntityStates;
using HarmonyLib;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Skills;
using UnityEngine;

namespace DroneImprovements.Skills
{
    /// <summary>
    /// Adds two real skills to every remote-op drone body so they show up in the HUD skill bar like any ability:
    ///   Utility slot: Disconnect (hold)      Special slot (R): Teleport to nearest player
    /// None of the vanilla remote-op drones use those two slots.
    /// </summary>
    internal static class DroneSkills
    {
        public const string StateMachineName = "DroneImprovementsAbility";

        public static DroneTeleportSkillDef TeleportSkill { get; private set; }
        public static DroneDisconnectSkillDef DisconnectSkill { get; private set; }
        public static SkillFamily TeleportFamily { get; private set; }
        public static SkillFamily DisconnectFamily { get; private set; }

        private static readonly FieldInfo skillFamilyField = AccessTools.Field(typeof(GenericSkill), "_skillFamily");

        public static void Init()
        {
            TeleportSkill = ScriptableObject.CreateInstance<DroneTeleportSkillDef>();
            ((ScriptableObject)TeleportSkill).name = "DroneImprovementsTeleport";
            TeleportSkill.skillName = "DroneImprovementsTeleport";
            TeleportSkill.skillNameToken = "Teleport to Player";
            TeleportSkill.skillDescriptionToken = $"Teleport next to the nearest living player. <style=cIsUtility>{PluginConfig.TeleportCooldown.Value:0.#}s cooldown</style>.";
            TeleportSkill.icon = LoadIcon("texDroneTeleportIcon.png");
            TeleportSkill.activationStateMachineName = StateMachineName;
            TeleportSkill.activationState = new SerializableEntityStateType(typeof(DroneTeleportState));
            TeleportSkill.interruptPriority = InterruptPriority.Skill;
            TeleportSkill.baseRechargeInterval = PluginConfig.TeleportCooldown.Value;
            TeleportSkill.baseMaxStock = 1;
            TeleportSkill.rechargeStock = 1;
            TeleportSkill.requiredStock = 1;
            TeleportSkill.stockToConsume = 1;
            TeleportSkill.fullRestockOnAssign = true;
            TeleportSkill.isCombatSkill = false;
            TeleportSkill.cancelSprintingOnActivation = false;
            TeleportSkill.canceledFromSprinting = false;
            TeleportSkill.mustKeyPress = true;

            DisconnectSkill = ScriptableObject.CreateInstance<DroneDisconnectSkillDef>();
            ((ScriptableObject)DisconnectSkill).name = "DroneImprovementsDisconnect";
            DisconnectSkill.skillName = "DroneImprovementsDisconnect";
            DisconnectSkill.skillNameToken = "Disconnect";
            DisconnectSkill.skillDescriptionToken = "<style=cIsUtility>Hold</style> to end remote operation and return to spectating.";
            DisconnectSkill.icon = LoadIcon("texDroneDisconnectIcon.png");
            DisconnectSkill.activationStateMachineName = StateMachineName;
            DisconnectSkill.activationState = new SerializableEntityStateType(typeof(DroneDisconnectState));
            DisconnectSkill.interruptPriority = InterruptPriority.Any;
            // The "cooldown" is only used to display the hold progress; it is refunded when the hold is released.
            DisconnectSkill.baseRechargeInterval = Mathf.Max(0.1f, PluginConfig.DisconnectHoldSeconds.Value);
            DisconnectSkill.beginSkillCooldownOnSkillEnd = true;
            DisconnectSkill.baseMaxStock = 1;
            DisconnectSkill.rechargeStock = 1;
            DisconnectSkill.requiredStock = 1;
            DisconnectSkill.stockToConsume = 1;
            DisconnectSkill.fullRestockOnAssign = true;
            DisconnectSkill.isCombatSkill = false;
            DisconnectSkill.cancelSprintingOnActivation = false;
            DisconnectSkill.canceledFromSprinting = false;
            DisconnectSkill.mustKeyPress = true;

            TeleportFamily = CreateFamily("DroneImprovementsTeleportFamily", TeleportSkill);
            DisconnectFamily = CreateFamily("DroneImprovementsDisconnectFamily", DisconnectSkill);

            ContentManager.collectContentPackProviders += add => add(new DroneContentPackProvider());
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

        /// <summary>
        /// Called for every remote-op body prefab before BodyCatalog records each body's skill slots, so loadouts
        /// and networking see the new slots everywhere. The slots are always added (independent of config) so that
        /// players with different settings still agree on the body layout; the config only enables/disables the skill.
        /// </summary>
        public static void AddToBody(GameObject prefab)
        {
            if (EntityStateMachine.FindByCustomName(prefab, StateMachineName))
            {
                return;
            }
            SkillLocator locator = prefab.GetComponent<SkillLocator>();
            if (!locator)
            {
                DroneImprovementsPlugin.Log.LogWarning($"{prefab.name} has no SkillLocator; drone abilities not added.");
                return;
            }

            EntityStateMachine machine = prefab.AddComponent<EntityStateMachine>();
            machine.customName = StateMachineName;
            machine.initialStateType = new SerializableEntityStateType(typeof(Idle));
            machine.mainStateType = new SerializableEntityStateType(typeof(Idle));

            if (!locator.utility)
            {
                locator.utility = AddSkill(prefab, DisconnectFamily, "DroneDisconnect");
            }
            else
            {
                DroneImprovementsPlugin.Log.LogWarning($"{prefab.name} already has a utility skill; Disconnect not added.");
            }
            if (!locator.special)
            {
                locator.special = AddSkill(prefab, TeleportFamily, "DroneTeleport");
            }
            else
            {
                DroneImprovementsPlugin.Log.LogWarning($"{prefab.name} already has a special skill; Teleport not added.");
            }
        }

        private static GenericSkill AddSkill(GameObject prefab, SkillFamily family, string slotName)
        {
            GenericSkill skill = prefab.AddComponent<GenericSkill>();
            skill.skillName = slotName;
            skill.hideInCharacterSelect = true;
            skillFamilyField.SetValue(skill, family);
            return skill;
        }

        private static Sprite LoadIcon(string fileName)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            using (Stream stream = typeof(DroneSkills).Assembly.GetManifestResourceStream("DroneImprovements.Assets." + fileName))
            {
                if (stream == null)
                {
                    DroneImprovementsPlugin.Log.LogError($"Missing embedded icon {fileName}");
                    return null;
                }
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                texture.LoadImage(bytes);
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = System.IO.Path.GetFileNameWithoutExtension(fileName);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        [HarmonyPatch(typeof(BodyCatalog), "SetBodyPrefabs")]
        private static class BodyCatalogSetBodyPrefabsPatch
        {
            private static void Prefix(GameObject[] newBodyPrefabs)
            {
                foreach (GameObject prefab in newBodyPrefabs)
                {
                    if (prefab && prefab.name.EndsWith("BodyRemoteOp"))
                    {
                        AddToBody(prefab);
                    }
                }
            }
        }

        private class DroneContentPackProvider : IContentPackProvider
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

    /// <summary>Only usable while there is a living player to go to (greyed out otherwise).</summary>
    public class DroneTeleportSkillDef : SkillDef
    {
        public override bool IsReady(GenericSkill skillSlot)
        {
            return base.IsReady(skillSlot) && IsAvailable(skillSlot);
        }

        public override bool CanExecute(GenericSkill skillSlot)
        {
            return base.CanExecute(skillSlot) && IsAvailable(skillSlot);
        }

        private static bool IsAvailable(GenericSkill skillSlot)
        {
            return PluginConfig.TeleportEnabled.Value && skillSlot.characterBody && DroneTeleport.HasTarget(skillSlot.characterBody);
        }
    }

    public class DroneDisconnectSkillDef : SkillDef
    {
        public override bool IsReady(GenericSkill skillSlot)
        {
            return base.IsReady(skillSlot) && PluginConfig.DisconnectEnabled.Value;
        }

        public override bool CanExecute(GenericSkill skillSlot)
        {
            return base.CanExecute(skillSlot) && PluginConfig.DisconnectEnabled.Value;
        }
    }
}
