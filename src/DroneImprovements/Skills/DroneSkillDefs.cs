using RoR2;
using RoR2.Skills;

namespace DroneImprovements.Skills
{
    /// <summary>Teleport to Player (Special slot). Only ready while there is a living player to go to.</summary>
    internal sealed class DroneTeleportSkillDef : SkillDef
    {
        public override bool IsReady(GenericSkill skillSlot)
        {
            // Cheapest checks first: the target search runs every frame the skill is shown.
            return base.IsReady(skillSlot) && PluginConfig.TeleportEnabled.Value && skillSlot.characterBody
                && DroneTeleport.HasTarget(skillSlot.characterBody);
        }

        public override bool CanExecute(GenericSkill skillSlot)
        {
            return !SkillKeyPress.IsClaimed(skillSlot) && base.CanExecute(skillSlot);
        }

        public override void OnExecute(GenericSkill skillSlot)
        {
            base.OnExecute(skillSlot);
            SkillKeyPress.Claim(skillSlot);
        }
    }

    /// <summary>
    /// Disconnect (Utility slot): hold to end remote operation. See <see cref="DroneDisconnectState"/>.
    /// </summary>
    internal sealed class DroneDisconnectSkillDef : SkillDef
    {
        public override bool IsReady(GenericSkill skillSlot)
        {
            return base.IsReady(skillSlot) && PluginConfig.DisconnectEnabled.Value;
        }

        public override bool CanExecute(GenericSkill skillSlot)
        {
            return !SkillKeyPress.IsClaimed(skillSlot) && base.CanExecute(skillSlot);
        }

        public override void OnExecute(GenericSkill skillSlot)
        {
            base.OnExecute(skillSlot);
            SkillKeyPress.Claim(skillSlot);
        }
    }

    /// <summary>
    /// Makes a held key activate these skills only once. Remote Operation drones run PlayerFlyState, which activates
    /// a skill every tick while its key is down and ignores SkillDef.mustKeyPress. So the skills claim the key press
    /// when they activate, as GenericCharacterMain does, and refuse to activate while it is claimed; the claim clears
    /// when the key is released (InputBankTest.ButtonState.PushState).
    /// </summary>
    internal static class SkillKeyPress
    {
        public static bool IsClaimed(GenericSkill skillSlot)
        {
            CharacterBody body = skillSlot.characterBody;
            InputBankTest inputBank = body ? body.inputBank : null;
            if (!inputBank || !body.skillLocator)
            {
                return false;
            }
            switch (body.skillLocator.FindSkillSlot(skillSlot))
            {
                case SkillSlot.Primary:
                    return inputBank.skill1.hasPressBeenClaimed;
                case SkillSlot.Secondary:
                    return inputBank.skill2.hasPressBeenClaimed;
                case SkillSlot.Utility:
                    return inputBank.skill3.hasPressBeenClaimed;
                case SkillSlot.Special:
                    return inputBank.skill4.hasPressBeenClaimed;
                default:
                    return false;
            }
        }

        public static void Claim(GenericSkill skillSlot)
        {
            CharacterBody body = skillSlot.characterBody;
            InputBankTest inputBank = body ? body.inputBank : null;
            if (!inputBank || !body.skillLocator)
            {
                return;
            }
            switch (body.skillLocator.FindSkillSlot(skillSlot))
            {
                case SkillSlot.Primary:
                    inputBank.skill1.hasPressBeenClaimed = true;
                    break;
                case SkillSlot.Secondary:
                    inputBank.skill2.hasPressBeenClaimed = true;
                    break;
                case SkillSlot.Utility:
                    inputBank.skill3.hasPressBeenClaimed = true;
                    break;
                case SkillSlot.Special:
                    inputBank.skill4.hasPressBeenClaimed = true;
                    break;
            }
        }
    }
}
