using EntityStates;
using RoR2;
using UnityEngine;

namespace DroneImprovements.Skills
{
    /// <summary>Instant teleport. Runs on the drone owner's machine, which has authority over the drone's movement.</summary>
    public class DroneTeleportState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority && !DroneTeleport.TryTeleport(characterBody, out string failReason))
            {
                DroneImprovementsPlugin.Log.LogInfo("Teleport failed: " + failReason);
                activatorSkillSlot?.AddOneStock();
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority)
            {
                outer.SetNextStateToMain();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Any;
        }
    }

    /// <summary>
    /// Hold the Utility key to disconnect. While held, the skill's cooldown display counts down the hold time;
    /// letting go early refunds the skill.
    /// </summary>
    public class DroneDisconnectState : BaseSkillState
    {
        private float holdDuration;
        private bool requested;

        public override void OnEnter()
        {
            base.OnEnter();
            holdDuration = Mathf.Max(0f, PluginConfig.DisconnectHoldSeconds.Value);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority)
            {
                return;
            }

            if (!requested)
            {
                if (!IsDisconnectKeyHeld())
                {
                    outer.SetNextStateToMain();
                    return;
                }
                if (activatorSkillSlot)
                {
                    activatorSkillSlot.rechargeStopwatch = Mathf.Min(fixedAge, activatorSkillSlot.CalculateFinalRechargeInterval() - 0.01f);
                }
                if (fixedAge >= holdDuration)
                {
                    requested = true;
                    DroneNetworking.RequestDisconnect(characterBody);
                }
            }
            else if (fixedAge >= holdDuration + 3f)
            {
                // The host never removed the drone (e.g. host without the mod); give the skill back.
                outer.SetNextStateToMain();
            }
        }

        /// <summary>
        /// Reads the player's physical binding for this skill slot directly. The body's input bank can be
        /// cleared by movement/sprint handling (and by auto-sprint mods), which would cancel the hold while moving.
        /// </summary>
        private bool IsDisconnectKeyHeld()
        {
            CharacterMaster master = characterBody ? characterBody.master : null;
            PlayerCharacterMasterController pcmc = master ? master.playerCharacterMasterController : null;
            NetworkUser networkUser = pcmc ? pcmc.networkUser : null;
            LocalUser localUser = networkUser ? networkUser.localUser : null;
            if (localUser?.inputPlayer == null)
            {
                return IsKeyDownAuthority();
            }
            if (localUser.isUIFocused)
            {
                return false;
            }
            int actionId;
            switch (skillLocator ? skillLocator.FindSkillSlot(activatorSkillSlot) : SkillSlot.None)
            {
                case SkillSlot.Primary: actionId = 7; break;
                case SkillSlot.Secondary: actionId = 8; break;
                case SkillSlot.Utility: actionId = 9; break;
                case SkillSlot.Special: actionId = 10; break;
                default: return IsKeyDownAuthority();
            }
            return localUser.inputPlayer.GetButton(actionId);
        }

        public override void OnExit()
        {
            if (activatorSkillSlot)
            {
                activatorSkillSlot.stock = activatorSkillSlot.maxStock;
                activatorSkillSlot.rechargeStopwatch = 0f;
            }
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Any;
        }
    }
}
