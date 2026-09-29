using System;
using System.Reflection;
using HarmonyLib;

namespace DroneImprovements.Patches
{
    /// <summary>The features' names in the log. Each feature is patched, or left out, as a whole.</summary>
    internal static class FeatureNames
    {
        public const string SurvivorActions = "SurvivorActions";
        public const string DroneGold = "DroneGold";
        public const string HoldoutZones = "HoldoutZones";
        public const string MithrixArena = "MithrixArena";
        public const string AllPlayersTrigger = "AllPlayersTrigger";
        public const string EscapeShip = "EscapeShip";
        public const string EscapeShipPlayerCount = "EscapeShipPlayerCount";
        public const string ArenaVoidKill = "ArenaVoidKill";
        public const string Abilities = "Abilities";
    }

    /// <summary>
    /// Keeps the patches from breaking the game.
    /// <para>
    /// A hook (a Harmony prefix, postfix or finalizer) only hands over to the mod's logic, inside a try/catch. If that
    /// throws, the hook does what the base game would for that call and logs the first error, instead of breaking the
    /// patched game method on every call (most of them run every frame or on every hit). The logic sits in separate
    /// methods that are never inlined into the hook, so the JIT only compiles them when the hook calls them, inside
    /// its try: even a game member that a game update removed fails there.
    /// </para>
    /// <para>
    /// A game member that the logic calls directly, and that a game update could plausibly remove, is also checked in
    /// a patch class's Prepare(). If it is gone, the class isn't applied, so its whole feature is left out (see
    /// <see cref="DroneImprovementsPlugin"/>) instead of every call failing.
    /// </para>
    /// </summary>
    internal static class PatchSafety
    {
        /// <summary>Logs a hook's first unexpected error; it could otherwise repeat every frame.</summary>
        public static void ReportOnce(ref bool reported, string feature, string hook, Exception e)
        {
            if (reported)
            {
                return;
            }
            reported = true;
            DroneImprovementsPlugin.Log.LogError($"{feature}: unexpected error in the {hook} patch; that call worked "
                + $"as in the base game. Later errors there aren't logged. {e}");
        }

        /// <summary>For a Prepare(): whether the game still has this public instance method. Warns if not.</summary>
        public static bool GameHasMethod(Type type, string name, Type returnType, params Type[] parameterTypes)
        {
            MethodInfo method = AccessTools.Method(type, name, parameterTypes);
            if (method != null && method.IsPublic && !method.IsStatic && method.ReturnType == returnType)
            {
                return true;
            }
            DroneImprovementsPlugin.Log.LogWarning($"The game no longer has the method {type.Name}.{name}.");
            return false;
        }

        /// <summary>For a Prepare(): whether the game still has this public instance field. Warns if not.</summary>
        public static bool GameHasField(Type type, string name, Type fieldType)
        {
            FieldInfo field = AccessTools.Field(type, name);
            if (field != null && field.IsPublic && !field.IsStatic && field.FieldType == fieldType)
            {
                return true;
            }
            DroneImprovementsPlugin.Log.LogWarning($"The game no longer has the field {type.Name}.{name}.");
            return false;
        }

        /// <summary>Whether the game still has this public instance property getter. Warns if not.</summary>
        public static bool GameHasGetter(Type type, string name, Type propertyType)
        {
            MethodInfo getter = AccessTools.PropertyGetter(type, name);
            if (getter != null && getter.IsPublic && !getter.IsStatic && getter.ReturnType == propertyType)
            {
                return true;
            }
            DroneImprovementsPlugin.Log.LogWarning($"The game no longer has the property {type.Name}.{name}.");
            return false;
        }
    }
}
