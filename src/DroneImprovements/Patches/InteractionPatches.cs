using HarmonyLib;
using RoR2;

namespace DroneImprovements.Patches
{
    // All of these follow the host's "DroneSurvivorActions" setting (see HostSettings).

    /// <summary>
    /// Every interactable in the game checks <see cref="Interactor.isRemoteOp"/> to lock drone players out
    /// (chests, shrines, teleporter, printers, ...). CharacterBody.Start copies the body's remote-op flag onto
    /// its Interactor, so clearing it right afterwards lets the drone interact like a survivor while keeping
    /// <see cref="CharacterBody.isRemoteOp"/> intact for flight controls and the rest of the game.
    /// Runs on the host and on clients, so the server-side check and the local prompt agree.
    /// </summary>
    [HarmonyPatch(typeof(CharacterBody), "Start")]
    internal static class CharacterBodyStartPatch
    {
        private static void Postfix(CharacterBody __instance)
        {
            if (!__instance.isRemoteOp || !HostSettings.SurvivorActions)
            {
                return;
            }
            if (__instance.TryGetComponent(out Interactor interactor))
            {
                interactor.isRemoteOp = false;
            }
        }
    }

    /// <summary>
    /// Vanilla blocks drone players from picking up equipment and objective items.
    /// </summary>
    [HarmonyPatch(typeof(GenericPickupController), "BodyHasPickupPermission")]
    internal static class PickupPermissionPatch
    {
        private static void Postfix(CharacterBody body, ref bool __result)
        {
            if (__result || !HostSettings.SurvivorActions || !body || !body.isRemoteOp)
            {
                return;
            }
            __result = body.masterObject && body.masterObject.GetComponent<PlayerCharacterMasterController>() && body.inventory;
        }
    }
}
