# Risk of Rain 2 modding guide

How DroneImprovements (this repo), [MarkAllSeen](https://github.com/T-Jayay/MarkAllSeen), [SprintImprovements](https://github.com/T-Jayay/SprintImprovements) and [AllyClarity](https://github.com/T-Jayay/AllyClarity) are set up, built, tested and published. Use it as the starting point for a new mod.

The mods follow the Risk of Rain 2 modding community's conventions. Read the [R2Wiki](https://risk-of-thunder.github.io/R2Wiki/) and its [ExamplePlugin](https://github.com/risk-of-thunder/ExamplePlugin) before designing anything, and use what the community uses (the publicized game assemblies from NuGet, R2API where it fits, the game's own attributes) instead of inventing something. Keep the code simple and let errors show (see Conventions).

## Environment

| Thing | Where / version |
|---|---|
| Game | 1.4.1 (Unity 2021.3.33), in `C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2` (code in `Risk of Rain 2_Data\Managed\RoR2.dll`). The build doesn't use it; decompiling does. |
| .NET SDK | 8.0, installed per user in `~/.dotnet` (the system `dotnet` only has runtimes). Set `DOTNET_ROOT=~/.dotnet` and put it first on `PATH`; `tools/package.py` finds it by itself. |
| NuGet | The build restores its references from nuget.org and the [BepInEx feed](https://nuget.bepinex.dev/), so the first build needs internet access. |
| Decompiler | `ilspycmd` 8.2, a global tool in `~/.dotnet/tools` |
| Mod manager | Thunderstore Mod Manager, profiles in `%APPDATA%\Thunderstore Mod Manager\DataFolder\RiskOfRain2\profiles\`. **`Test`** is the development profile. |
| Loader | BepInExPack 5.4.2122, RoR2BepInExPack and HookGenPatcher 1.2.9 (generates `MMHOOK_RoR2.dll` for the `On.`/`IL.` hooks). Risk of Options (the in-game settings menu) is installed in the Test profile. |
| Python | 3.14. The icon scripts need Pillow; `tools/package.py` needs no packages. UnityPy for asset extraction goes in a throwaway venv. |

## Project layout

```
<Mod>/
  Directory.Build.props      compiler settings, NuGet references, test profile  (identical in every mod:
  Directory.Build.targets    version, store icon, deploy                         copy them unchanged)
  <Mod>.sln
  README.md                  developer readme (the store page is thunderstore/<Mod>/README.md)
  src/<Mod>/<Mod>.csproj     just a header comment, plus any mod-specific items
  src/<Mod>/<Mod>Plugin.cs   the plugin class, with PluginVersion
  src/<Mod>/AssemblyInfo.cs  lets the plugin use the game's private members (see Build setup)
  thunderstore/<Mod>/        manifest.json, README.md (store page), CHANGELOG.md, icon.png (256x256)
  tools/package.py           checks, builds and writes dist/<Mod>-<version>.zip (identical apart from PACKAGES)
  tools/make_icon(s).py      optional, draws the icons (see Icons)
  LICENSE                    the Unlicense
  .gitignore, .gitattributes identical in every mod; text files are LF everywhere
```

## Build setup

- References come from NuGet, as in the R2Wiki's ExamplePlugin (`Directory.Build.props`): `BepInEx.Core` 5.4.21, `RiskOfRain2.GameLibs` 1.4.1-r.0 and `BepInEx.Analyzers` from the BepInEx feed (`RestoreAdditionalProjectSources`; nuget.org only has an old GameLibs), `UnityEngine.Modules` 2021.3.33 and `Rune580.Mods.RiskOfRain2.RiskOfOptions` 2.8.6 from nuget.org. Projects that hook also reference `MMHOOK.RoR2` in their own csproj (see Hooking). The versions are pinned: after a game update, bump GameLibs and MMHOOK together (and `UnityEngine.Modules` if Unity changed). A library build copies none of its references into its output, so no game or loader DLL is ever shipped.
- GameLibs is **publicized**: the game's private members are public at compile time, so the code uses them directly (`master.preventRespawnUntilNextStageServer = true`, `nameof(PlayerCharacterMasterController.PollButtonInput)`), without reflection or member names in strings. To use them, the R2Wiki's Assembly References page requires every project to have an `AssemblyInfo.cs` with `[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]`. Other mods' assemblies aren't publicized: use their public API.
- Every project targets `netstandard2.1` with C# 12 (`LangVersion` pinned).
- Builds are reproducible and record no local paths: `Deterministic`, a `PathMap` that writes every path in the DLL and PDB as `/_/...`, and no git queries (a clone and a plain copy build the same DLL, and the informational version has no `+commit`). `tools/package.py` refuses a DLL that contains your home folder's path.
- `Directory.Build.targets`:
  - takes `Version` from `thunderstore/<project name>/manifest.json`, so the assembly version always matches the package; the only other place with the version is `PluginVersion`, and `tools/package.py` checks the two match;
  - embeds `thunderstore/<project name>/icon.png` as the resource `<Mod>.icon.png`, for the Risk of Options mod icon;
  - after each build, copies the DLL and PDB into `<ProfileDir>\BepInEx\plugins\<assembly name>\` (`DeployToProfile`, on by default). `ProfileDir` is the Thunderstore Mod Manager profile `Test` unless you set it on the command line or as an environment variable, e.g. `-p:ProfileDir="C:\Users\<you>\AppData\Roaming\r2modmanPlus-local\RiskOfRain2\profiles\Dev"` for an r2modman profile named `Dev`. Without that profile folder, the build doesn't deploy.

## Build, run, check

```
dotnet build <Mod>.sln -c Release          # builds and deploys to the Test profile
```

- **The running game keeps plugin DLLs locked**: close it before building, or build with `-p:DeployToProfile=false`.
- Launch the Test profile directly (doorstop v4): `"Risk of Rain 2.exe" --doorstop-enabled true --doorstop-target-assembly "<profile>\BepInEx\core\BepInEx.Preloader.dll"`
- Log: `<profile>\BepInEx\LogOutput.log`. After every build, check that the plugin loaded and nothing from the mod logged an error (an IL hook that can't find its code logs an error naming the method). Errors about `PrinterItemIcon`, `StageReportPanel` and `InvalidKeyException` don't come from these mods.
- Game profiles (unlocks, logbook, settings) are saved in `<Steam>\userdata\<account id>\632360\remote\UserProfiles\<id>.xml`, shared by every mod manager profile. For tests that change a profile, create a separate game profile on the title screen instead of using your main one.

## Researching the game

```
ilspycmd -p -o <scratch>/ror2src "<game folder>/Risk of Rain 2_Data/Managed/RoR2.dll"
```

Then grep the decompiled source (decompile the game's own DLL: the GameLibs ones have no method bodies). Keep it in a scratch folder: **never commit decompiled game code**. Decompile Risk of Options the same way: `ilspycmd -p -o <scratch>/rooSrc "<profile>/BepInEx/plugins/Rune580-Risk_Of_Options/RiskOfOptions/RiskOfOptions.dll"`.

- Asset names and paths: `Risk of Rain 2_Data/StreamingAssets/aa/catalog.json` (grep for names like `RemoteOp`).
- Textures and prefabs in `aa/StandaloneWindows64/*.bundle` can be read with UnityPy (for reference only; draw your own art).
- The script execution order isn't in the code: Unity keeps it in the `MonoScript` entries of `Risk of Rain 2_Data/globalgamemanagers.assets` (see Game notes).
- Before hooking a method, check whether it has overloads (see Hooking).

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
        // Mod-specific setup, then the hooks (see Hooking).
        ExampleHooks.Init();
        if (RiskOfOptionsCompat.IsInstalled)
        {
            RiskOfOptionsCompat.Init();
        }
    }
}
```

- No `OnDestroy`: plugins live for the whole session, and a partial teardown would only mislead.
- A mod that adds content or network messages also adds itself to the network mod list in `Awake` (see Multiplayer).

## Conventions

- **Let errors show.** Write code that doesn't throw, and let unexpected exceptions reach the log, where BepInEx writes them with a stack trace for bug reports. No catch-all try/catch in hooks, no "log only the first error", no silent fallback to the base game: they make a mod look like it works while part of it doesn't. Catch only failures you expect and handle, and log the full exception.
- **Don't guard against game updates** that remove or rename members (member checks before hooking, per-feature groups of hooks, `NoInlining` helpers, try/catch around type lists). That is rare for RoR2 and fixed with a mod update; with the publicized GameLibs, such a change is a compile error once you bump the package.
- **Validate input** from outside the mod: network messages (the sender, and the object a request names) and config values other code relies on.
- **State around a game method** ("while this method runs") is set before calling `orig` and cleared in a `finally`, so it is also cleared when the method throws (DroneImprovements' `HoldoutZoneHooks`, the combat contexts in `GoldHooks`).

## Hooking

- Use a game event when one fits (`GlobalEventManager.onCharacterDeathGlobal`, `Run.onRunStartGlobal`, `RoR2Application.onLoad`, ...), as the R2Wiki's ExamplePlugin does. Otherwise hook with MonoMod's `On.` and `IL.` hooks from MMHOOK, which most RoR2 mods use (Hooking on the R2Wiki).
- Each hooks class subscribes its hooks in an `Init()` called from `Awake`, once; they are never removed (the R2Wiki's On Hook page: there is generally no reason to undo a hook). Name handlers as the IDE generates them: `On.RoR2.HoldoutZoneController.DoUpdate += HoldoutZoneController_DoUpdate;`. Read the game's private fields through `self` (publicized).
- **Call `orig`** unless the feature is to replace the method: skipping it also skips every hook other mods added before yours, so say why in a comment (DroneImprovements' `GoldHooks`). Change a return value with `var result = orig(self, ...)`; keep state for the duration of the call with try/finally around `orig`.
- **Overloads**: MMHOOK names their hooks after the parameter types, e.g. `On.RoR2.TeamManager.GiveTeamMoney_TeamIndex_uint`, `On.RoR2.CharacterMaster.TrueKill_GameObject_GameObject_DamageTypeCombo`. Check the decompiled source for overloads.
- **IL hooks**: `ILCursor.TryGotoNext` with `Match...` predicates for the exact instructions, operands through `nameof` (`AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.isSprinting))`), then `Emit`/`EmitDelegate`. When the match fails, log an error naming `il.Method.Name` and leave the method unchanged, as the R2Wiki's IL Hook page says: `GotoNext` throws, which would stop the rest of the mod's `Awake`. See SprintImprovements' `SprintHooks`.
- **References**: `<PackageReference Include="MMHOOK.RoR2" Version="2025.12.9" IncludeAssets="compile" NoWarn="NU1701" />` in the csproj of each project that hooks (2025.12.9 was published together with GameLibs 1.4.1-r.0; the package targets .NET Framework, hence NU1701). Players get `MMHOOK_RoR2.dll` from HookGenPatcher: add `RiskofThunder-HookGenPatcher-1.2.9` to the manifest's dependencies (a patcher, so no `BepInDependency`).
- `BepInEx.Analyzers` checks the plugin code; keep the build free of warnings.

## Multiplayer

- **Network mod list**: a mod that adds content to the game's catalogs or has its own network messages must be on every player in the same version. Add `PluginGUID + ";" + PluginVersion` to `NetworkModCompatibilityHelper.networkModList` in `Awake`; the game then refuses mismatched players (`ServerAuthManager`) and lobby searches filter on the list's hash. R2API only lists plugins that depend on it and only removes its own entries. Client-side mods (MarkAllSeen, SprintImprovements, AllyClarity) stay off the list, so players can join with or without them.
- **Client authority**: a player's body is moved by its owning client, which ignores server teleport messages for it. Teleport on the owner (`TeleportHelper.TeleportBody` then replicates it).
- **Work through the owner's input, not against the game's writes.** The owning client decides its body's state (sprint, movement, entity states) and the server passes it on; but some game code also runs on the server's copy of a client's body: `GenericSkill.FixedUpdate` (and so `SkillDef.OnFixedUpdate`, e.g. `forceSprintDuringState`) and entity-state code outside `isAuthority` checks. So a client-side mod that skips such a write on the owner leaves the server's copy out of step. Change what the player's input does where you can: SprintImprovements presses the sprint key for the player (`sprintInputPressReceived`), and the game's own input path changes sprint and tells the server, as auto-sprint mods do. Where a feature has to skip a game write (SprintImprovements' BlockForcedSprint), tell the server the owner's value once afterwards through the game's own command (`CallCmdUpdateSprint`): commands and entity-state changes both go on the reliable channel 0 (`QosChannelIndex.defaultReliable`), which delivers in order (EOS says so; Steam's P2P doesn't document it, but the game's own networking depends on it), so the server has left the skill by then. See SprintImprovements' `ServerSprintSync`.
- **Custom network messages**: mark a static `void Handle(NetworkMessage netMsg)` with the game's own `[NetworkMessageHandler(msgType = MessageIds.X, server = true)]` (or `client = true`), and add `[assembly: HG.Reflection.SearchableAttribute.OptIn]` to `AssemblyInfo.cs` so the game scans the assembly for it. The game collects these handlers once during startup and registers them on every server and client it starts, next to its own. Each message still needs its own id (only R2API's NetworkingAPI picks ids for you): keep them as constants in `MessageIds.cs`. RoR2 uses 48–87; the ids in use across these mods are 17392 (DroneImprovements: drone disconnect) and 17393 (DroneImprovements: host settings), so take new ones from 17394 up and add them here. Validate the sender by connection (`networkUser.connectionToClient == netMsg.conn`) and re-check the object a request names when it arrives.
- **Host-controlled settings**: the host sends its values to each joining player, to everyone at run start and on every change; clients use the mod's defaults until then and reset them on every new connection. Code that also runs on clients (interaction prompts, holdout zones' charge indicators and Lepton Daisy's effect on them) must read the host's values. See DroneImprovements' `HostSettings.cs`.

## Risk of Options

- **Soft dependency** when the mod works without it (DroneImprovements, SprintImprovements): `[BepInDependency(RiskOfOptionsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]`, not in the manifest. Check `Chainloader.PluginInfos.ContainsKey(guid)`, and keep every Risk of Options type inside the bodies of `[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]` methods of `RiskOfOptionsCompat` (never in signatures, fields or other classes), so its types only load when it is installed. This is the pattern of the R2Wiki's [Mod Compatibility: Soft Dependency](https://github.com/risk-of-thunder/R2Wiki/wiki/Mod-Creation_C%23-Programming_Mod-Compatibility:-Soft-Dependency) page. Call `Init` last in `Awake`, without a try/catch: if it throws, the log shows the error and the rest of the mod is already set up.
- **Hard dependency** when the mod *is* a Risk of Options option (MarkAllSeen's button): `[BepInDependency(guid)]` plus `Rune580-Risk_Of_Options-<version>` in the manifest's dependencies.
- It finds your mod with `Assembly.GetCallingAssembly()`, so call `ModSettingsManager` from your own assembly.
- `checkIfDisabled` greys an option out, e.g. host-only options on clients: `checkIfDisabled = () => NetworkClient.active && !NetworkServer.active`.
- After every change made in its menu it re-runs every `checkIfDisabled`, so an option can grey out depending on another (SprintImprovements greys its other settings out while `Enabled` is off). It doesn't redraw a control when code changes the value, so don't make settings change each other.
- Buttons: `new GenericButtonOption(name, category, description, buttonText, onPressed)`.
- Without `ModSettingsManager.SetModIcon(sprite)` the mod list shows a "?". Every mod shows its store icon, which the build embeds as `<Mod>.icon.png`: read the resource stream into a byte array, `Texture2D.LoadImage` it, set `wrapMode` to `TextureWrapMode.Clamp` (with the default `Repeat`, the edges of the scaled-down icon blend with the opposite edges), then `Sprite.Create`.

## Game notes

- **Remote Operation** is the base game's dead-player drone feature (`isRemoteOp`, `RemoteOperationDronePickerPanel`, `CharacterMaster.SpawnRemoteOperationDrone`). Find its bodies as the game does: `DroneDef.remoteOpBody` in `ContentManager.droneDefs`. The drones run `PlayerFlyState`, which activates a skill every tick while its key is held and ignores `SkillDef.mustKeyPress`; claim the key press yourself (`SkillKeyPress` in DroneImprovements' `Skills/DroneSkillDefs.cs`). `PlayerFlyState.PerformInputs` flies up on Jump and down while `inputBank.sprint` is down; for Remote Operation fliers `PollButtonInput` pushes the *held* Sprint key into it (no toggle, and `isSprinting` is never set). DroneImprovements' `FlightHooks` moves descending to its own key and makes Sprint a speed multiplier, on the owner only. `RigidbodyMotor` damages a flier that hits a wall at 4 times its move speed or faster.
- **Adding skills to a body**: register SkillDefs, SkillFamilies and EntityStates through an `IContentPackProvider` (`ContentManager.collectContentPackProviders`), and add the `GenericSkill` components in a hook on `BodyCatalog.SetBodyPrefabs`, before `orig`, so loadouts see them (an exception there stops the game from loading). Set the private `GenericSkill._skillFamily` directly. Give the skills their own `EntityStateMachine`. Skill name and description tokens can be plain text (a missing token displays as is). See DroneImprovements' `Skills/DroneSkills.cs`.
- **Input**: use the `RewiredConsts.Action` constants (`Jump` 4, `Interact` 5, `Equipment` 6, `PrimarySkill` 7 to `SpecialSkill` 10, `Sprint` 18, `Ping` 28) instead of raw ids, fully qualified where `Action` clashes with `System.Action`. Other mods (e.g. auto-sprint) can overwrite `InputBankTest` buttons mid-hold; read `localUser.inputPlayer.GetButton(id)` when a hold must be reliable.
- **Script execution order**: `PlayerCharacterMasterController` runs at -8, before `EntityStateMachine`, `GenericSkill`, `CharacterBody` and `InputBankTest` (0) and `CharacterMotor` (200). So the player's input poll sees what skills did in the previous tick.
- **Per-player state**: keep what should survive a new body (the next stage, a revive, a transformation) per player (`CharacterMaster` or its `PlayerCharacterMasterController`) in a `ConditionalWeakTable`, so the entries can go with the objects, and look up the current body's components again when the body changes (SprintImprovements' `SprintGuard`).
- **Sprint**: the game's sprint has no toggle state of its own; its only memory is `CharacterBody.isSprinting`. Every tick `PlayerCharacterMasterController.PollButtonInput` takes `body.isSprinting`, flips it if sprint was pressed, clears it unless you move within 60° of where you aim, and pushes it into `InputBankTest.sprint`; `GenericCharacterMain.HandleMovements` writes it back to the body (off at a move input of 0.5 or less). Anything else that writes `isSprinting` changes the player's toggle: skills with `forceSprintDuringState` or `cancelSprintingOnActivation`, and stuns. On a client the setter sends `CmdUpdateSprint` to the server. To start or stop sprint for the player, set `sprintInputPressReceived` in `PollButtonInput` after it reads `isSprinting` (an IL hook), as SprintImprovements' `SprintGuard.cs` and auto-sprint mods do; which entity states matter can be read from the `SkillCatalog` at startup (`SprintStates.cs`).
- **Gold**: kills, money packs and barrels all pay through `TeamManager.GiveTeamMoney`; classify by call context (see DroneImprovements' `Hooks/GoldHooks.cs`).
- **Outlines**: the game draws outlines per camera with `OutlineHighlight`. A handler on the static `OutlineHighlight.onPreRenderOutlineHighlight` adds renderers with `AddHighlight(renderer, color)` (the last color for a renderer wins), and nothing renders unless a `Highlight` is on or something called `AddPreRenderHighlightRequest`. Pings use it; Drone Tech puts its own `Highlight` on drones. See AllyClarity's `OutlineHooks`.
- **Recolouring an effect**: effects spawned through `EffectManager` are pooled per prefab (unless `VFXAttributes.DoNotPool`) and shared by everyone who spawns that prefab, so recolour an instance in a post-orig `EffectComponent.Reset` hook (it runs on every reuse, with the effect data) and restore it for the next user. Never edit shared materials or ramps, and never add effects to the `EffectCatalog` from a client-side mod (its indices go over the network). See AllyClarity's `NovaRecolor`.
- **"New!" markers and the profile**: every "New!" is a `ViewableTag` over a `ViewablesCatalog` node: `/Logbook/<category token>/<entry token>`, `/Loadout/Bodies/<body>/<skill family>.<skill>` and `.../Skins/<skin>`, `/Survivors/<survivor>`, `/Settings`, `/Signup`. `node.shouldShowUnviewed(profile)` decides, and the tags only check the first local user. Skill variants without an unlock count as unlocked (`StatSheet.HasUnlockable(null)` is true), so the loadouts of locked survivors, and of survivors from a DLC the player doesn't own, report "new" too. The logbook registers its nodes at sign-in (only for expansions a local user owns), and only the private `fullNameToNodeMap` lists every node. `UserProfile.MarkViewableAsViewed` only flags the profile as changed; it is written when the logbook closes, a run ends, on sign-out or on quit, so call `profile.RequestEventualSave()` after changing a profile (the save starts on the next frame, and the log shows `Saved file "<id>.xml"`). Viewed names are saved as one space-separated XML text: a name with a space or a carriage return doesn't load back, and a control character makes the whole profile load as "Corrupted Profile". See MarkAllSeen.

## Testing multiplayer features solo

Many features only happen in multiplayer (e.g. dead players). `src/DroneImprovements.DevTools` shows the pattern for a **test-only** plugin that is never packaged:

- Keep the run alive after dying: set `Run.preventGameOver` (private setter) on run start. When the host has DevTools this applies to the whole lobby, so it warns in the log and on screen whenever more than one player is present.
- Spawn an invincible AI survivor on the player team as a stand-in player (`MasterSummon` with `CommandoMonsterMaster`, `healthComponent.godMode = true`), and hook whatever should treat it as a player (spectating, holdout zones, ...).
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
