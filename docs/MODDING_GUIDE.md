# Risk of Rain 2 modding guide

How DroneImprovements (this repo), [MarkAllSeen](https://github.com/T-Jayay/MarkAllSeen) and [SprintImprovements](https://github.com/T-Jayay/SprintImprovements) are set up, built, tested and published. Use it as the starting point for a new mod.

## Environment

| Thing | Where / version |
|---|---|
| Game | `C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2` (code in `Risk of Rain 2_Data\Managed\RoR2.dll`) |
| .NET SDK | 8.0, installed per user in `~/.dotnet` (the system `dotnet` only has runtimes). Set `DOTNET_ROOT=~/.dotnet` and put it first on `PATH`; `tools/package.py` finds it by itself. |
| Decompiler | `ilspycmd` 8.2, a global tool in `~/.dotnet/tools` |
| Mod manager | Thunderstore Mod Manager, profiles in `%APPDATA%\Thunderstore Mod Manager\DataFolder\RiskOfRain2\profiles\`. **`Test`** is the development profile. |
| Loader | BepInExPack 5.4.2122 (HarmonyX included) and RoR2BepInExPack. Risk of Options (the in-game settings menu) is installed in the Test profile. |
| Python | 3.14. The icon scripts need Pillow; `tools/package.py` needs no packages. UnityPy for asset extraction goes in a throwaway venv. |

## Project layout

```
<Mod>/
  Directory.Build.props      machine paths, compiler settings, references  (identical in every mod:
  Directory.Build.targets    version, store icon, path checks, deploy        copy them unchanged)
  <Mod>.sln
  README.md                  developer readme (the store page is thunderstore/<Mod>/README.md)
  src/<Mod>/<Mod>.csproj     just a header comment, plus any mod-specific items
  src/<Mod>/<Mod>Plugin.cs   the plugin class, with PluginVersion
  thunderstore/<Mod>/        manifest.json, README.md (store page), CHANGELOG.md, icon.png (256x256)
  tools/package.py           checks, builds and writes dist/<Mod>-<version>.zip (identical apart from PACKAGES)
  tools/make_icon(s).py      optional, draws the icons (see Icons)
  LICENSE                    the Unlicense
  .gitignore, .gitattributes identical in every mod; text files are LF everywhere
```

## Build setup

- `Directory.Build.props` holds the machine-specific paths: `GameDir` (default: the Steam folder above), `ProfileDir` (default: the Test profile) and `DeployToProfile` (default `true`). Override them on the command line or as environment variables, e.g. `-p:GameDir="D:\SteamLibrary\steamapps\common\Risk of Rain 2"`, `-p:ProfileDir="C:\Users\<you>\AppData\Roaming\r2modmanPlus-local\RiskOfRain2\profiles\Dev"` (an r2modman profile named `Dev`) or `-p:DeployToProfile=false`. A path that doesn't match stops the build with one error naming the property to set.
- Every project targets `netstandard2.1` with C# 12 (`LangVersion` pinned) and references the game's DLLs from `GameDir`, and BepInEx, Harmony and Risk of Options from `ProfileDir`, all with `Private=false`, so no game or loader DLL is ever shipped. Risk of Options must be installed in the profile to build, even for mods that only use it when it's there.
- Builds are reproducible and record no local paths: `Deterministic`, a `PathMap` that writes every path in the DLL and PDB as `/_/...`, and no git queries (a clone and a plain copy build the same DLL, and the informational version has no `+commit`). `tools/package.py` refuses a DLL that contains your home folder's path.
- `Directory.Build.targets`:
  - takes `Version` from `thunderstore/<project name>/manifest.json`, so the assembly version always matches the package; the only other place with the version is `PluginVersion`, and `tools/package.py` checks the two match;
  - embeds `thunderstore/<project name>/icon.png` as the resource `<Mod>.icon.png`, for the Risk of Options mod icon;
  - after each build, copies the DLL and PDB into `<ProfileDir>\BepInEx\plugins\<assembly name>\` (`DeployToProfile`). If the game has the plugin locked, it fails straight away and says so.

## Build, run, check

```
dotnet build <Mod>.sln -c Release          # builds and deploys to the Test profile
```

- **The running game keeps plugin DLLs locked**: close it before building, or build with `-p:DeployToProfile=false`.
- Launch the Test profile directly (doorstop v4): `"Risk of Rain 2.exe" --doorstop-enabled true --doorstop-target-assembly "<profile>\BepInEx\core\BepInEx.Preloader.dll"`
- Log: `<profile>\BepInEx\LogOutput.log`. After every build, check that the plugin loaded, each Harmony patch was applied (RoR2BepInExPack logs them) and nothing from the mod logged an error. Errors about `PrinterItemIcon`, `StageReportPanel` and `InvalidKeyException` don't come from these mods.
- Game profiles (unlocks, logbook, settings) are saved in `<Steam>\userdata\<account id>\632360\remote\UserProfiles\<id>.xml`, shared by every mod manager profile. For tests that change a profile, create a separate game profile on the title screen instead of using your main one.

## Researching the game

```
ilspycmd -p -o <scratch>/ror2src "<GameDir>/Risk of Rain 2_Data/Managed/RoR2.dll"
```

Then grep the decompiled source. Keep it in a scratch folder: **never commit decompiled game code**. Decompile Risk of Options the same way: `ilspycmd -p -o <scratch>/rooSrc "<profile>/BepInEx/plugins/Rune580-Risk_Of_Options/RiskOfOptions/RiskOfOptions.dll"`.

- Asset names and paths: `Risk of Rain 2_Data/StreamingAssets/aa/catalog.json` (grep for names like `RemoteOp`).
- Textures and prefabs in `aa/StandaloneWindows64/*.bundle` can be read with UnityPy (for reference only; draw your own art).
- The script execution order isn't in the code: Unity keeps it in the `MonoScript` entries of `Risk of Rain 2_Data/globalgamemanagers.assets` (see Game notes).
- Before patching a method, check whether it has overloads (see Patching).

## Plugin structure

The plugin classes follow the same pattern, in `src/<Mod>/<Mod>Plugin.cs` (MarkAllSeen has no settings and a hard dependency on Risk of Options, which it calls directly):

```csharp
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(RiskOfOptionsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class <Mod>Plugin : BaseUnityPlugin
{
    public const string PluginGUID = "revor.<Mod>";
    public const string PluginName = "<Mod>";
    public const string PluginVersion = "1.0.0"; // tools/package.py checks this against the manifest

    internal static ManualLogSource Log { get; private set; }

    private void Awake()
    {
        Log = Logger;
        PluginConfig.Init(Config);
        // Mod-specific setup, then the patches (see Patching).
        if (RiskOfOptionsCompat.IsInstalled)
        {
            try
            {
                RiskOfOptionsCompat.Init();
            }
            catch (Exception e)
            {
                Log.LogError($"Risk of Options settings failed to load; the config file still works. {e}");
            }
        }
    }
}
```

- No `OnDestroy`: plugins live for the whole session, and a partial teardown would only mislead.
- A mod that adds content or network messages also adds itself to the network mod list in `Awake` (see Multiplayer).

## Patching

- **Feature by feature** when features are independent (DroneImprovements): give each feature its own Harmony instance (`new Harmony(PluginGUID + "." + feature)`), apply its patch classes one by one with `harmony.CreateClassProcessor(type).Patch()` in a try/catch, and `UnpatchSelf()` that instance if one fails. A broken target then turns off one feature, named in the log, instead of the whole mod: `PatchAll` stops at the first class that fails. `Patch()` returns an empty list when the class's `Prepare()` returned false, which DroneImprovements also treats as a failure. See `DroneImprovementsPlugin.ApplyPatches`.
- **All or nothing** when the patches only work together (SprintImprovements): `PatchAll` in a try/catch, `UnpatchSelf()` if it throws, and the patches do nothing until a `Patched` flag is set, in case the unpatch fails too. See `SprintImprovementsPlugin.Patch`.
- **Keep hooks thin and exception-safe**: a patch method only calls the mod's logic, in a try/catch. On an exception it does what the game would (a prefix returns true, a replacement returns the game's value) and logs the first error only. An exception escaping a hook on a per-tick method breaks that method every tick, for example all of a player's input in `PlayerCharacterMasterController.PollButtonInput`. Put the logic in a separate `[MethodImpl(MethodImplOptions.NoInlining)]` method: the JIT then compiles it only when the hook calls it, inside the try, so even a game member that a game update removed is caught. Keep enter/exit state balanced with `__state` and a finalizer, and in a replacement don't consume the game's state (e.g. a queue) before the last step that can throw, so the original can still run. See DroneImprovements' `Patches/PatchSafety.cs`.
- **Reflection**: resolve private members once, with parameter types, into delegates (`AccessTools.MethodDelegate`, `FieldRefAccess`), but never in a static field initializer: a member a game update removed then throws `TypeInitializationException` from the patched method every frame. Resolve in the patch class's `Prepare()` (return false to skip the class) or in explicit init code that disables just that feature. An assembly publicizer would turn renamed members into compile errors instead. Prefer public members that give the same result (e.g. `HoldoutZoneController.IsBodyInChargingRadius(body)`), but check those in the patch class's `Prepare()` too (`AccessTools.Method(type, name, new[] { typeof(CharacterBody) })`): a public member that a game update removed only fails when the code calling it is compiled, not at patch time. Harmony does check injected `___field` parameters at patch time, so prefer them to reading fields through `__instance`.
- The same goes for lists of game types (`typeof(...)`): a removed type throws when the method that names it is compiled. Build such a list in a separate `[MethodImpl(MethodImplOptions.NoInlining)]` method called from init code in a try/catch, never in a static initializer of a class that hot code uses (SprintImprovements' `SprintSensitiveStates`).
- **Overloaded methods**: `AccessTools.Method(type, "Name")` and `[HarmonyPatch(type, "Name")]` throw `AmbiguousMatchException` when there is more than one overload (this once broke teleporter charging, throwing every frame). Check the decompiled source, and pass the parameter types when a target has or could get overloads: `AccessTools.Method(type, "Name", new[] { typeof(A) })`, `[HarmonyPatch(type, "Name", typeof(A))]`. For no parameters use `new Type[0]` in attributes: `Type.EmptyTypes` isn't a constant, so attributes can't use it.
- **Transpilers**: match the exact instruction (`ci.Calls(AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.isSprinting)))`) and count the matches. If the count isn't what you expect, log an error and return the original code, so a game update disables the feature instead of breaking the method. When inserting before a matched instruction, move its labels and exception blocks to the first new instruction (`MoveLabelsTo`, `MoveBlocksTo`). Pass a private field by reference with `ldarg.0; ldflda <field>`, and get the helper's `MethodInfo` from a delegate (`new HelperCall(Helper).Method`) rather than by name. Log one line on success, so the log check shows it applied. See SprintImprovements' `SprintPatches.PollButtonInputPatch`.

## Multiplayer

- **Network mod list**: a mod that adds content to the game's catalogs or has its own network messages must be on every player in the same version. Add `PluginGUID + ";" + PluginVersion` to `NetworkModCompatibilityHelper.networkModList` in `Awake`; the game then refuses mismatched players (`ServerAuthManager`) and lobby searches filter on the list's hash. R2API only lists plugins that depend on it and only removes its own entries. Client-side mods (MarkAllSeen, SprintImprovements) stay off the list, so players can join with or without them.
- **Client authority**: a player's body is moved by its owning client, which ignores server teleport messages for it. Teleport on the owner (`TeleportHelper.TeleportBody` then replicates it).
- **The server runs skill code on its copy of a client's body too** (`GenericSkill.FixedUpdate` isn't limited to the owner), and the owner ignores the server's sprint value for its own body. So when a client blocks a forced sprint, the server's copy may still be sprinting; once blocking stops, tell the server (`CallCmdUpdateSprint(false)`, see SprintImprovements' `ServerSprintSync`).
- **Custom network messages**: register server handlers in `NetworkManagerSystem.onStartServerGlobal` and client handlers in `NetworkManagerSystem.onStartClientGlobal` (both run after RoR2 has registered its own). Keep the ids in `MessageIds.cs` and log an error if another mod already registered one (UNet silently replaces handlers). RoR2 uses 48–87; the ids in use across these mods are 17392 (DroneImprovements: drone disconnect) and 17393 (DroneImprovements: host settings), so take new ones from 17394 up and add them here. Validate the sender by connection (`networkUser.connectionToClient == netMsg.conn`) and re-check the object a request names when it arrives.
- **Host-controlled settings**: the host sends its values to each joining player, to everyone at run start and on every change; clients use the mod's defaults until then and reset them on every new connection. Code that also runs on clients (interaction prompts, holdout zones' charge indicators and Lepton Daisy's effect on them) must read the host's values. See DroneImprovements' `HostSettings.cs`.

## Risk of Options

- **Soft dependency** when the mod works without it (DroneImprovements, SprintImprovements): `[BepInDependency(RiskOfOptionsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]`, not in the manifest. Check `Chainloader.PluginInfos.ContainsKey(guid)`, and keep every Risk of Options type inside the bodies of `[MethodImpl(MethodImplOptions.NoInlining)]` methods of `RiskOfOptionsCompat` (never in signatures, fields or other classes), so its types only load when it is installed. Call `Init` in a try/catch (see Plugin structure), so a broken settings page can't stop the rest of the mod.
- **Hard dependency** when the mod *is* a Risk of Options option (MarkAllSeen's button): `[BepInDependency(guid)]` plus `Rune580-Risk_Of_Options-<version>` in the manifest's dependencies.
- It finds your mod with `Assembly.GetCallingAssembly()`, so call `ModSettingsManager` from your own assembly.
- `checkIfDisabled` greys an option out, e.g. host-only options on clients: `checkIfDisabled = () => NetworkClient.active && !NetworkServer.active`.
- After a change made in its UI it re-runs every `checkIfDisabled`, but it only redraws a control when it is clicked or its panel opens, and it doesn't listen to `SettingChanged`. When code or a reloaded config file changes a value (for example linked settings), redraw the open controls yourself: SprintImprovements calls the protected `UpdateControls` of each open `ModSettingsBool` (`RiskOfOptionsCompat.RefreshCheckboxes`).
- Buttons: `new GenericButtonOption(name, category, description, buttonText, onPressed)`.
- Without `ModSettingsManager.SetModIcon(sprite)` the mod list shows a "?". Every mod shows its store icon, which the build embeds as `<Mod>.icon.png`: read the resource stream into a byte array, `Texture2D.LoadImage` it (check the result), set `wrapMode` to `TextureWrapMode.Clamp` (with the default `Repeat`, the edges of the scaled-down icon blend with the opposite edges), then `Sprite.Create`, and only log a warning if that fails.

## Game notes

- **Remote Operation** is the base game's dead-player drone feature (`isRemoteOp`, `RemoteOperationDronePickerPanel`, `CharacterMaster.SpawnRemoteOperationDrone`). Find its bodies as the game does: `DroneDef.remoteOpBody` in `ContentManager.droneDefs`. The drones run `PlayerFlyState`, which activates a skill every tick while its key is held and ignores `SkillDef.mustKeyPress`; claim the key press yourself (`SkillKeyPress` in DroneImprovements' `Skills/DroneSkillDefs.cs`).
- **Adding skills to a body**: register SkillDefs, SkillFamilies and EntityStates through an `IContentPackProvider` (`ContentManager.collectContentPackProviders`), and add the `GenericSkill` components in a prefix on `BodyCatalog.SetBodyPrefabs` so loadouts see them. Keep that prefix in a try/catch: an exception there stops the game from loading. `GenericSkill._skillFamily` is private. Give the skills their own `EntityStateMachine`. Skill name and description tokens can be plain text (a missing token displays as is). See DroneImprovements' `Skills/DroneSkills.cs`.
- **Input**: use the `RewiredConsts.Action` constants (`Jump` 4, `Interact` 5, `Equipment` 6, `PrimarySkill` 7 to `SpecialSkill` 10, `Sprint` 18, `Ping` 28) instead of raw ids, fully qualified where `Action` clashes with `System.Action`. Other mods (e.g. auto-sprint) can overwrite `InputBankTest` buttons mid-hold; read `localUser.inputPlayer.GetButton(id)` when a hold must be reliable.
- **Script execution order**: `PlayerCharacterMasterController` runs at -8, before `EntityStateMachine`, `GenericSkill`, `CharacterBody` and `InputBankTest` (0) and `CharacterMotor` (200). So the player's input poll sees what skills did in the previous tick.
- **Per-player and per-body state**: keep what should survive a new body (the next stage, a revive, a transformation) per `CharacterMaster`, and the rest per `CharacterBody`, in `ConditionalWeakTable`s so the entries can go with the objects (SprintImprovements' `SprintGuard`).
- **Sprint**: the game's sprint has no toggle state of its own; its only memory is `CharacterBody.isSprinting`. Every tick `PlayerCharacterMasterController.PollButtonInput` takes `body.isSprinting`, flips it if sprint was pressed, clears it unless you move within 60° of where you aim, and pushes it into `InputBankTest.sprint`; `GenericCharacterMain.HandleMovements` writes it back to the body (off at a move input of 0.5 or less). Anything else that writes `isSprinting` changes the player's toggle: skills with `forceSprintDuringState` or `cancelSprintingOnActivation`, and stuns. On a client the setter sends `CmdUpdateSprint` to the server. See SprintImprovements' `SprintGuard.cs`.
- **Gold**: kills, money packs and barrels all pay through `TeamManager.GiveTeamMoney`; classify by call context (see DroneImprovements' `Patches/GoldPatches.cs`).
- **"New!" markers and the profile**: every "New!" is a `ViewableTag` over a `ViewablesCatalog` node: `/Logbook/<category token>/<entry token>`, `/Loadout/Bodies/<body>/<skill family>.<skill>` and `.../Skins/<skin>`, `/Survivors/<survivor>`, `/Settings`, `/Signup`. `node.shouldShowUnviewed(profile)` decides, and the tags only check the first local user. Skill variants without an unlock count as unlocked (`StatSheet.HasUnlockable(null)` is true), so the loadouts of locked survivors, and of survivors from a DLC the player doesn't own, report "new" too. The logbook registers its nodes at sign-in (only for expansions a local user owns), and only the private `fullNameToNodeMap` lists every node. `UserProfile.MarkViewableAsViewed` only flags the profile as changed; it is written when the logbook closes, a run ends, on sign-out or on quit, so call `profile.RequestEventualSave()` after changing a profile (the save starts on the next frame, and the log shows `Saved file "<id>.xml"`). Viewed names are saved as one space-separated XML text: a name with a space or a carriage return doesn't load back, and a control character makes the whole profile load as "Corrupted Profile". See MarkAllSeen.

## Testing multiplayer features solo

Many features only happen in multiplayer (e.g. dead players). `src/DroneImprovements.DevTools` shows the pattern for a **test-only** plugin that is never packaged:

- Keep the run alive after dying: set `Run.preventGameOver` (private setter) on run start. When the host has DevTools this applies to the whole lobby, so it warns in the log and on screen whenever more than one player is present.
- Spawn an invincible AI survivor on the player team as a stand-in player (`MasterSummon` with `CommandoMonsterMaster`, `healthComponent.godMode = true`), and patch whatever should treat it as a player (spectating, holdout zones, ...).
- Every hotkey is a configurable `KeyCode`, including the panel's show/hide key. **Avoid F8**: overlays (NVIDIA, Overwolf, ...) often take it before the game sees it.
- Solo testing does **not** cover client→host networking; test those paths in a real lobby before a release.

## Icons

`tools/make_icons.py` draws icons in the style of the base game's drone skill icons (dark glitch background, glowing corner brackets, a glowing one-colour glyph) with Pillow: write a function that draws the glyph as a white-on-black mask and pass it to `compose(glyph, colour, seed)`. SprintImprovements' `tools/make_icon.py` keeps identical copies of the helpers and draws its store icon; MarkAllSeen's icon has no script. The output depends only on the Pillow version: 12.3.0 reproduces the committed PNGs byte for byte.

## Publishing

1. Bump `version_number` in `thunderstore/<Mod>/manifest.json` **and** `PluginVersion`, and add a `## <version>` entry to the changelog. Uploaded versions can never be changed, so even a manifest-only change needs a new version.
2. Commit. `website_url` in the manifest is the store page's link to the GitHub repo (`https://github.com/T-Jayay/<Mod>`).
3. The first time: create the public repo `T-Jayay/<Mod>`. Before any first push, check every commit's author and committer with `git log --format='%h %an <%ae> / %cn <%ce>'`. GitHub keeps commits reachable by their SHA even after a force-push replaced them, and lists them in the repo's activity; only deleting the repo, or GitHub Support, removes them. Then push `main`, so `website_url` works.
4. `python tools/package.py` checks the package against Thunderstore's rules (name, Major.Minor.Patch version, description of at most 250 characters, `website_url`, dependency strings, a 256x256 PNG icon, README, a `## <version>` changelog entry, `PluginVersion`), builds the mod and writes `dist/<Mod>-<version>.zip` with `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png` and the plugin DLL (never a PDB or a test-only plugin). It refuses uncommitted changes in `src/`, `thunderstore/` or the `Directory.Build` files, and an existing zip; `--force` skips both, for test builds only. Pass package names to build only some.
5. Test the zip in a clean profile with the mod manager's **Import local mod**.
6. Upload it at https://thunderstore.io/package/create/: team **revoreverse**, community Risk of Rain 2, and tick the **AI Generated** category. New uploads take a while to appear in mod managers.
7. Check that the package page shows the new version.
8. Tag the release commit (`git tag v<version>`) and push the branch and the tag: `git push origin main v<version>`.
