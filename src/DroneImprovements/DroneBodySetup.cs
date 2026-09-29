using System.Text;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace DroneImprovements
{
    /// <summary>Makes sure every remote-op drone body can interact, and logs its layout for troubleshooting.</summary>
    internal static class DroneBodySetup
    {
        public static void OnGameLoaded()
        {
            int count = 0;
            foreach (DroneDef droneDef in DroneCatalog.allDroneDefs)
            {
                if (!droneDef || !droneDef.remoteOpBody)
                {
                    continue;
                }
                count++;
                GameObject prefab = droneDef.remoteOpBody;
                StringBuilder log = new StringBuilder(prefab.name);

                if (!prefab.GetComponent<Interactor>())
                {
                    Interactor interactor = prefab.AddComponent<Interactor>();
                    interactor.maxInteractionDistance = 3f;
                    log.Append(" [added Interactor]");
                }
                if (!prefab.GetComponent<InteractionDriver>())
                {
                    if (prefab.GetComponent<InputBankTest>())
                    {
                        prefab.AddComponent<InteractionDriver>();
                        log.Append(" [added InteractionDriver]");
                    }
                    else
                    {
                        log.Append(" [no InputBankTest, cannot interact]");
                    }
                }

                SkillLocator skills = prefab.GetComponent<SkillLocator>();
                if (skills)
                {
                    log.Append(" skills:");
                    AppendSkill(log, "primary", skills.primary);
                    AppendSkill(log, "secondary", skills.secondary);
                    AppendSkill(log, "utility", skills.utility);
                    AppendSkill(log, "special", skills.special);
                }
                DroneImprovementsPlugin.Log.LogInfo(log.ToString());
            }
            DroneImprovementsPlugin.Log.LogInfo($"Prepared {count} remote-op drone bodies.");
        }

        private static void AppendSkill(StringBuilder log, string slot, GenericSkill skill)
        {
            SkillDef def = skill ? skill.skillFamily?.defaultSkillDef : null;
            if (def)
            {
                log.Append(' ').Append(slot).Append('=').Append(def.skillName ?? ((ScriptableObject)def).name);
            }
        }
    }
}
