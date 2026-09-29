using System;
using System.Text;
using DroneImprovements.Skills;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace DroneImprovements
{
    /// <summary>
    /// Logs the Remote Operation drone bodies once the game has loaded, for troubleshooting: a summary at Info and
    /// each body's skills at Debug.
    /// </summary>
    internal static class DroneBodyReport
    {
        public static void OnGameLoaded()
        {
            // RoR2Application.onLoad stops calling later handlers when one throws.
            try
            {
                LogDroneBodies();
            }
            catch (Exception e)
            {
                DroneImprovementsPlugin.Log.LogError($"Couldn't list the Remote Operation drones. {e}");
            }
        }

        private static void LogDroneBodies()
        {
            int bodies = 0;
            int withDisconnect = 0;
            int withTeleport = 0;
            StringBuilder line = new StringBuilder();
            foreach (DroneDef droneDef in DroneCatalog.allDroneDefs)
            {
                GameObject prefab = droneDef ? droneDef.remoteOpBody : null;
                if (!prefab)
                {
                    continue;
                }
                bodies++;
                if (!prefab.GetComponent<Interactor>())
                {
                    DroneImprovementsPlugin.Log.LogWarning($"{prefab.name} has no Interactor, so it can't use "
                        + "interactables even with DroneSurvivorActions.");
                }

                line.Clear().Append(prefab.name).Append(':');
                SkillLocator locator = prefab.GetComponent<SkillLocator>();
                if (locator)
                {
                    AppendSkill(line, "primary", locator.primary);
                    AppendSkill(line, "secondary", locator.secondary);
                    AppendSkill(line, "utility", locator.utility);
                    AppendSkill(line, "special", locator.special);
                    if (HasFamily(locator.utility, DroneSkills.DisconnectFamily))
                    {
                        withDisconnect++;
                    }
                    if (HasFamily(locator.special, DroneSkills.TeleportFamily))
                    {
                        withTeleport++;
                    }
                }
                DroneImprovementsPlugin.Log.LogDebug(line.ToString());
            }
            DroneImprovementsPlugin.Log.LogInfo($"{bodies} Remote Operation drones: {withDisconnect} with Disconnect, "
                + $"{withTeleport} with Teleport to Player.");
        }

        private static bool HasFamily(GenericSkill skill, SkillFamily family)
        {
            return skill && family && skill.skillFamily == family;
        }

        private static void AppendSkill(StringBuilder line, string slot, GenericSkill skill)
        {
            SkillFamily family = skill ? skill.skillFamily : null;
            SkillDef def = family ? family.defaultSkillDef : null;
            if (!def)
            {
                return;
            }
            // SkillDef hides ScriptableObject.name with a property that returns null.
            string name = string.IsNullOrEmpty(def.skillName) ? ((ScriptableObject)def).name : def.skillName;
            line.Append(' ').Append(slot).Append('=').Append(name);
        }
    }
}
