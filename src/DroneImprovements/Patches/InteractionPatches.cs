using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoR2;

namespace DroneImprovements.Patches
{
    /// <summary>
    /// The host setting DroneSurvivorActions. Most interactables (chests, shrines, printers, scrappers, the
    /// teleporter, ...) refuse an <see cref="Interactor"/> whose isRemoteOp flag is set, which CharacterBody.Start
    /// copies from the body. Clearing the Interactor's flag lets a drone player use them like a survivor, while
    /// <see cref="CharacterBody.isRemoteOp"/> stays set for the flight controls and the rest of the game. Some
    /// interactables (barrels, for example) never check the flag, so drones can use those in the base game too.
    /// </summary>
    internal static class DroneInteractors
    {
        /// <summary>Whether the patches below are applied; if not, drones are locked out as in the base game.</summary>
        internal static bool IsPatched { get; set; }

        /// <summary>Applies the current setting to drones that already exist (after the host changes it).</summary>
        public static void ApplySettingToExistingDrones()
        {
            if (IsPatched)
            {
                SetExistingDronesLocked(!HostSettings.SurvivorActions);
            }
        }

        // Only called once the patches are applied, whose Prepare() checked the Interactor field this uses; the JIT
        // compiles it on its own, so a game without that field never compiles it.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetExistingDronesLocked(bool locked)
        {
            ReadOnlyCollection<CharacterBody> bodies = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < bodies.Count; i++)
            {
                CharacterBody body = bodies[i];
                if (body && body.isRemoteOp && body.TryGetComponent(out Interactor interactor))
                {
                    interactor.isRemoteOp = locked;
                }
            }
        }

        /// <summary>A body has just started: unlocks its Interactor if it is a drone and the setting is on.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OnBodyStart(CharacterBody body)
        {
            if (body.isRemoteOp && HostSettings.SurvivorActions && body.TryGetComponent(out Interactor interactor))
            {
                interactor.isRemoteOp = false;
            }
        }

        /// <summary>
        /// GenericPickupController.BodyHasPickupPermission for drone players: equipment too, but still no items that
        /// a stage's objective needs (see <see cref="PickupPermissionPatch"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HasPickupPermission(CharacterBody body, UniquePickup pickupState, bool permitted)
        {
            // Vanilla's other requirements: a player's body with an inventory.
            if (permitted || !DroneUtil.IsDronePlayer(body) || !body.inventory || !HostSettings.SurvivorActions)
            {
                return permitted;
            }
            return !IsObjectiveItem(pickupState);
        }

        private static bool IsObjectiveItem(UniquePickup pickupState)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupState.pickupIndex);
            if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            {
                return false;
            }
            ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
            return itemDef
                && (itemDef.ContainsTag(ItemTag.PowerShape) || itemDef.ContainsTag(ItemTag.ObjectiveRelated));
        }
    }

    /// <summary>
    /// Clears the Interactor's remote-op flag right after CharacterBody.Start sets it. Runs on the host and on
    /// clients (with the host's setting), so the host's checks and the client's prompts agree. Its Prepare() checks
    /// that the game still has that flag.
    /// </summary>
    [HarmonyPatch(typeof(CharacterBody), "Start")]
    internal static class InteractorRemoteOpPatch
    {
        private static bool failed;

        private static bool Prepare()
        {
            return PatchSafety.GameHasField(typeof(Interactor), nameof(Interactor.isRemoteOp), typeof(bool));
        }

        private static void Postfix(CharacterBody __instance)
        {
            try
            {
                DroneInteractors.OnBodyStart(__instance);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.SurvivorActions, "CharacterBody.Start", e);
            }
        }
    }

    /// <summary>
    /// Vanilla refuses Remote Operation drones every equipment pickup, and items tagged PowerShape or
    /// ObjectiveRelated (items that some stages' objectives need, such as keys). This lifts only the equipment block:
    /// those objectives expect survivors to carry the items (ConduitCanyonKeyItemDisabler, for example, only counts
    /// keys that survivors hold), so drones still can't pick them up.
    /// </summary>
    [HarmonyPatch(typeof(GenericPickupController), "BodyHasPickupPermission", typeof(CharacterBody),
        typeof(UniquePickup))]
    internal static class PickupPermissionPatch
    {
        private static bool failed;

        private static void Postfix(CharacterBody body, UniquePickup pickupState, ref bool __result)
        {
            try
            {
                __result = DroneInteractors.HasPickupPermission(body, pickupState, __result);
            }
            catch (Exception e)
            {
                PatchSafety.ReportOnce(ref failed, FeatureNames.SurvivorActions,
                    "GenericPickupController.BodyHasPickupPermission", e);
            }
        }
    }
}
