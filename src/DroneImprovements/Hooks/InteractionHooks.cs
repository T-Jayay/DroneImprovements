using System.Collections.ObjectModel;
using RoR2;

namespace DroneImprovements.Hooks
{
    /// <summary>
    /// The host setting DroneSurvivorActions. Most interactables (chests, shrines, printers, scrappers, the
    /// teleporter, ...) refuse an <see cref="Interactor"/> whose isRemoteOp flag is set, which CharacterBody.Start
    /// copies from the body. Clearing the Interactor's flag lets a drone player use them like a survivor, while
    /// <see cref="CharacterBody.isRemoteOp"/> stays set for the flight controls and the rest of the game. Some
    /// interactables (barrels, for example) never check the flag, so drones can use those in the base game too.
    /// </summary>
    internal static class InteractionHooks
    {
        public static void Init()
        {
            On.RoR2.CharacterBody.Start += CharacterBody_Start;
            On.RoR2.GenericPickupController.BodyHasPickupPermission += GenericPickupController_BodyHasPickupPermission;
        }

        /// <summary>Applies the current setting to drones that already exist (after the host changes it).</summary>
        public static void ApplySettingToExistingDrones()
        {
            bool locked = !HostSettings.SurvivorActions;
            ReadOnlyCollection<CharacterBody> bodies = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < bodies.Count; i++)
            {
                CharacterBody body = bodies[i];
                if (body.isRemoteOp && body.TryGetComponent(out Interactor interactor))
                {
                    interactor.isRemoteOp = locked;
                }
            }
        }

        /// <summary>
        /// Clears the Interactor's remote-op flag right after CharacterBody.Start sets it. Runs on the host and on
        /// clients (with the host's setting), so the host's checks and the client's prompts agree.
        /// </summary>
        private static void CharacterBody_Start(On.RoR2.CharacterBody.orig_Start orig, CharacterBody self)
        {
            orig(self);
            if (self.isRemoteOp && HostSettings.SurvivorActions && self.TryGetComponent(out Interactor interactor))
            {
                interactor.isRemoteOp = false;
            }
        }

        /// <summary>
        /// Vanilla refuses Remote Operation drones every equipment pickup, and items tagged PowerShape or
        /// ObjectiveRelated (items that some stages' objectives need, such as keys). This lifts only the equipment
        /// block: those objectives expect survivors to carry the items (ConduitCanyonKeyItemDisabler, for example, only
        /// counts keys that survivors hold), so drones still can't pick them up.
        /// </summary>
        private static bool GenericPickupController_BodyHasPickupPermission(
            On.RoR2.GenericPickupController.orig_BodyHasPickupPermission orig, CharacterBody body,
            UniquePickup pickupState)
        {
            bool result = orig(body, pickupState);
            // Vanilla's other requirements: a player's body with an inventory.
            if (!result && DroneUtil.IsDronePlayer(body) && body.inventory && HostSettings.SurvivorActions)
            {
                result = !IsObjectiveItem(pickupState);
            }
            return result;
        }

        /// <summary>An item tagged PowerShape or ObjectiveRelated, which drones can't pick up.</summary>
        private static bool IsObjectiveItem(UniquePickup pickup)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickup.pickupIndex);
            if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            {
                return false;
            }
            ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
            return itemDef
                && (itemDef.ContainsTag(ItemTag.PowerShape) || itemDef.ContainsTag(ItemTag.ObjectiveRelated));
        }
    }
}
