using System;
using EntityStates;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;

namespace DroneImprovements.Hooks
{
    /// <summary>
    /// The flight controls of Remote Operation drones, which run PlayerFlyState: Space (Jump) flies up, the per-player
    /// DescendKey (Left Ctrl) flies down, and Sprint (Shift) flies faster by the host setting SprintSpeedMultiplier.
    /// In the base game holding Sprint flies down: for a Remote Operation flier, PlayerCharacterMasterController's
    /// PollButtonInput pushes the held Sprint key into InputBankTest.sprint, and PerformInputs reads that as "descend".
    /// At a multiplier of 1 Sprint still descends, as in the base game.
    /// <para>
    /// PerformInputs changes one condition (the sprint read that decides descending) and one value (the move vector
    /// it hands to the RigidbodyMotor) in the middle of a method that also runs the skills, so this is an IL hook:
    /// an On hook would have to replace the whole method. PerformInputs returns early unless the state has authority,
    /// so this only runs on the client that owns the drone, where the movement is decided; nothing networked
    /// (isSprinting included) is touched.
    /// </para>
    /// </summary>
    internal static class FlightHooks
    {
        /// <summary>At a multiplier of 1 the sprint speed feature is off and Sprint descends.</summary>
        private static bool SprintFliesFaster => HostSettings.SprintSpeedMultiplier > 1f;

        public static void Init()
        {
            IL.EntityStates.PlayerFlyState.PerformInputs += PlayerFlyState_PerformInputs;
        }

        /// <summary>
        /// Replaces the sprint read that makes the drone fly down (<see cref="IsDescending"/>) and scales the move
        /// vector right before it is stored (<see cref="ApplySprintSpeed"/>). If the game no longer has either, the
        /// method stays unchanged and the log says so.
        /// </summary>
        private static void PlayerFlyState_PerformInputs(ILContext il)
        {
            ILCursor descend = new ILCursor(il);
            ILCursor speed = new ILCursor(il);
            // if (base.inputBank.sprint.down) { ... moveVector.y = -1f; }
            if (!descend.TryGotoNext(MoveType.After,
                x => x.MatchLdflda(typeof(InputBankTest), nameof(InputBankTest.sprint)),
                x => x.MatchLdfld(typeof(InputBankTest.ButtonState), nameof(InputBankTest.ButtonState.down))))
            {
                DroneImprovementsPlugin.Log.LogError($"IL hook {il.Method.Name} failed: the sprint read that makes "
                    + "the drone fly down was not found.");
                return;
            }
            // base.rigidbodyMotor.moveVector = moveVector * base.characterBody.moveSpeed;
            if (!speed.TryGotoNext(MoveType.Before,
                x => x.MatchStfld(typeof(RigidbodyMotor), nameof(RigidbodyMotor.moveVector))))
            {
                DroneImprovementsPlugin.Log.LogError($"IL hook {il.Method.Name} failed: the store into the "
                    + "RigidbodyMotor's moveVector was not found.");
                return;
            }
            descend.Emit(OpCodes.Ldarg_0);
            descend.EmitDelegate<Func<bool, PlayerFlyState, bool>>(IsDescending);
            speed.Emit(OpCodes.Ldarg_0);
            speed.EmitDelegate<Func<Vector3, PlayerFlyState, Vector3>>(ApplySprintSpeed);
        }

        /// <summary>
        /// Whether the drone flies down. For a drone player that is the Descend key, or Sprint when the multiplier is
        /// 1; any other body keeps what the game read. The game's "jump while holding descend flies up" logic
        /// follows the result.
        /// </summary>
        private static bool IsDescending(bool sprintDown, PlayerFlyState state)
        {
            CharacterBody body = state.characterBody;
            if (!DroneUtil.IsDronePlayer(body))
            {
                return sprintDown;
            }
            return IsDescendKeyHeld(body) || (sprintDown && !SprintFliesFaster);
        }

        /// <summary>The move vector, scaled while a drone player holds Sprint and the multiplier is above 1.</summary>
        private static Vector3 ApplySprintSpeed(Vector3 moveVector, PlayerFlyState state)
        {
            if (SprintFliesFaster && state.inputBank.sprint.down && DroneUtil.IsDronePlayer(state.characterBody))
            {
                return moveVector * HostSettings.SprintSpeedMultiplier;
            }
            return moveVector;
        }

        /// <summary>
        /// The player holds their Descend key while their input goes to the game: not while they type in chat or have
        /// the console open (CanSendBodyInput is the game's own check for that). The drone's master is the player's own
        /// master, which Remote Operation respawns as the drone (CharacterMaster.SpawnRemoteOperationDrone). Only the
        /// main key is read: KeyboardShortcut.IsPressed returns false while any other key, such as a movement key, is
        /// held.
        /// </summary>
        private static bool IsDescendKeyHeld(CharacterBody body)
        {
            NetworkUser networkUser = body.master.playerCharacterMasterController.networkUser;
            return PlayerCharacterMasterController.CanSendBodyInput(networkUser, out _, out _, out _, out _)
                && Input.GetKey(PluginConfig.DescendKey.Value.MainKey);
        }
    }
}
