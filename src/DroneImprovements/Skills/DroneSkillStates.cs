using EntityStates;
using RoR2;
using UnityEngine;

namespace DroneImprovements.Skills
{
    /// <summary>
    /// Teleport to Player: instant. Runs on the drone owner's machine, which has authority over the drone's movement.
    /// </summary>
    internal sealed class DroneTeleportState : BaseSkillState
    {
        // The sound the game plays when an interaction fails.
        private const string FailSound = "Play_UI_insufficient_funds";

        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority && !DroneTeleport.TryTeleport(characterBody))
            {
                // The player to go to disappeared between the key press and now: give the charge back.
                DroneImprovementsPlugin.Log.LogDebug("Teleport cancelled: nobody to teleport to.");
                if (activatorSkillSlot)
                {
                    activatorSkillSlot.AddOneStock();
                }
                Util.PlaySound(FailSound, RoR2Application.instance.gameObject);
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
    /// Disconnect: hold the Utility key to end remote operation. While it is held, the skill's cooldown display fills
    /// up with the hold time; letting go early, or the host not removing the drone, gives the skill back.
    /// </summary>
    internal sealed class DroneDisconnectState : BaseSkillState
    {
        // How long to wait for the host to remove the drone before giving the skill back.
        private const float HostResponseTimeout = 3f;

        // The cooldown display stops just short of full: a full display restocks the skill in the middle of the hold.
        private const float FullDisplayMargin = 0.01f;

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
                ShowHoldProgress();
                if (fixedAge >= holdDuration)
                {
                    requested = true;
                    DroneNetworking.RequestDisconnect(characterBody);
                }
            }
            else if (fixedAge >= holdDuration + HostResponseTimeout)
            {
                // The drone is still here: the request was lost or refused (for example during a stage change).
                outer.SetNextStateToMain();
            }
        }

        /// <summary>
        /// Fills the cooldown display in proportion to the hold time, so it stays in step with the hold even when
        /// cooldown items shorten the skill's recharge interval.
        /// </summary>
        private void ShowHoldProgress()
        {
            if (!activatorSkillSlot)
            {
                return;
            }
            float progress = holdDuration > 0f ? Mathf.Clamp01(fixedAge / holdDuration) : 1f;
            float fullDisplay = activatorSkillSlot.CalculateFinalRechargeInterval() - FullDisplayMargin;
            activatorSkillSlot.rechargeStopwatch = progress * fullDisplay;
        }

        /// <summary>
        /// Reads the player's own binding for the skill's slot rather than the body's input bank, which other mods
        /// may overwrite during the hold.
        /// </summary>
        private bool IsDisconnectKeyHeld()
        {
            CharacterMaster master = characterBody ? characterBody.master : null;
            PlayerCharacterMasterController player = master ? master.playerCharacterMasterController : null;
            NetworkUser networkUser = player ? player.networkUser : null;
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
                case SkillSlot.Primary:
                    actionId = RewiredConsts.Action.PrimarySkill;
                    break;
                case SkillSlot.Secondary:
                    actionId = RewiredConsts.Action.SecondarySkill;
                    break;
                case SkillSlot.Utility:
                    actionId = RewiredConsts.Action.UtilitySkill;
                    break;
                case SkillSlot.Special:
                    actionId = RewiredConsts.Action.SpecialSkill;
                    break;
                default:
                    return IsKeyDownAuthority();
            }
            return localUser.inputPlayer.GetButton(actionId);
        }

        public override void OnExit()
        {
            // Give the skill back: a successful Disconnect destroys the body, so this only matters otherwise.
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
