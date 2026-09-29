# Risk of Rain 2 modding guide

How the mods in this repo (and [MarkAllSeen](https://github.com/T-Jayay/MarkAllSeen)) are set up, built, tested and published. Use it as the starting point for a new mod.

## Environment

| Thing | Where / version |
|---|---|
| Game | `C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2` (code in `Risk of Rain 2_Data\Managed\RoR2.dll`) |
| .NET SDK | 8.0, installed per-user in `~/.dotnet` (the system `dotnet` only has a runtime). Set `DOTNET_ROOT=~/.dotnet` and put it first on `PATH`. |
| Decompiler | `ilspycmd` 8.2 global tool in `~/.dotnet/tools` |
| Mod manager | Thunderstore Mod Manager, profiles in `%APPDATA%\Thunderstore Mod Manager\DataFolder\RiskOfRain2\profiles\`. **`Test`** is the development profile. |
| Loader | BepInExPack 5.4.2122 (HarmonyX included), plus RoR2BepInExPack. Risk of Options (in-game settings menu) is in the Test profile. |
| Python | 3.14 with Pillow (icons, packaging). UnityPy for asset extraction goes in a throwaway venv. |

## Project layout

```
<Mod>/
  Directory.Build.props      shared paths + references (copy from this repo)
  <Mod>.sln
  src/<Mod>/<Mod>.csproj     plugin; DeployToProfile target copies the DLL into the Test profile
  thunderstore/<Mod>/        manifest.json, README.md (store page), CHANGELOG.md, icon.png (256x256)
  tools/package.py           builds + validates dist/<Mod>-<version>.zip
  LICENSE                    The Unlicense
```

- Target `netstandard2.1`; every game/BepInEx reference has `<Private>false</Private>` (never ship game DLLs).
- `Directory.Build.props` holds `GameDir`, `ProfileDir` (Test profile) and `DeployToProfile` (default true). Override on the command line, e.g. `-p:DeployToProfile=false`.
- Plugin GUID convention: `revor.<ModName>`. Patch with `new Harmony(GUID).PatchAll(assembly)`.
- Optional dependency (e.g. Risk of Options): `[BepInDependency(guid, SoftDependency)]`, check `Chainloader.PluginInfos.ContainsKey(guid)`, and keep the calls in a separate class with `[MethodImpl(NoInlining)]` so its types only load when present (see `RiskOfOptionsCompat.cs`).

## Researching the game

```
ilspycmd -p -o <scratch>/ror2src "<GameDir>/Risk of Rain 2_Data/Managed/RoR2.dll"
```

Then grep the decompiled source. Keep it in a scratch folder: **never commit decompiled game code**.

- Asset names/paths: `Risk of Rain 2_Data/StreamingAssets/aa/catalog.json` (grep for names like `RemoteOp`).
- Textures/prefabs from bundles in `aa/StandaloneWindows64/*.bundle` can be read with UnityPy (reference only; draw your own art).
- Before patching a method, check it isn't overloaded (see gotchas).

## Build, run, check

```
dotnet build <Mod>.sln -c Release          # builds and deploys to the Test profile
```

- **The running game locks plugin DLLs**: close it before deploying (or build with `-p:DeployToProfile=false`).
- Launch the Test profile directly (doorstop v4):
  `"Risk of Rain 2.exe" --doorstop-enabled true --doorstop-target-assembly "<profile>\BepInEx\core\BepInEx.Preloader.dll"`
- Log: `<profile>\BepInEx\LogOutput.log`. After every build confirm the plugin loaded, each Harmony patch was applied (RoR2BepInExPack logs them), and there are no exceptions from the mod. Errors about `PrinterItemIcon`, `StageReportPanel` and `InvalidKeyException` come from other mods / the game.

## Testing multiplayer features solo

Many features only happen in multiplayer (e.g. dead players). `src/DroneImprovements.DevTools` shows the pattern for a **test-only** plugin (never packaged):

- Keep the run alive after dying: set `Run.preventGameOver` (private setter) on run start.
- Spawn an invincible AI survivor on the player team as a stand-in player (`MasterSummon` with `CommandoMonsterMaster`, `healthComponent.godMode = true`), and patch whatever should treat it as a player (spectating, holdout zones, ...).
- Hotkeys via configurable `KeyCode`s. **Avoid F8**: overlays (NVIDIA, Overwolf, ...) often grab it before the game.
- Solo testing does **not** cover client→host networking; test those paths in a real lobby before release.

## Patterns and gotchas

- **Overloaded private methods**: `AccessTools.Method(type, "Name")` throws `AmbiguousMatchException` when there is more than one overload (this broke teleporter charging once and threw every frame). Always pass the parameter types.
- Cache reflection (`AccessTools.FieldRefAccess`, `MethodDelegate`) in static fields; prefer Harmony `___field` injection in patches. An assembly publicizer would turn renamed members into compile errors instead of runtime failures.
- **Client authority**: a player's body is moved by its owning client, which ignores server teleport messages for it. Teleport on the owner (`TeleportHelper.TeleportBody` then replicates it).
- **Custom network messages**: register server handlers in a postfix on `NetworkMessageHandlerAttribute.RegisterServerMessages`, client handlers on `RegisterClientMessages(NetworkClient)`. Message ids in use: 17392 (drone disconnect), 17393 (host settings). Validate the sender (`networkUser.connectionToClient`).
- **Host-controlled settings**: the host broadcasts its values to clients (on run start, player join and change) so client UI matches what the server allows. See `HostSettings.cs`.
- **Adding skills to a body**: register SkillDefs/SkillFamilies/EntityStates through an `IContentPackProvider` (`ContentManager.collectContentPackProviders`), and add the `GenericSkill` components in a prefix on `BodyCatalog.SetBodyPrefabs` so loadouts see them. `GenericSkill._skillFamily` is private. Give the skills their own `EntityStateMachine`. Skill name/description tokens can be plain text (missing tokens display as-is). See `Skills/DroneSkills.cs`.
- **Input**: Rewired action ids: 5 interact, 6 equipment, 7 primary, 8 secondary, 9 utility, 10 special, 18 sprint. Other mods (e.g. auto-sprint) can clear `InputBankTest` buttons mid-hold; read `localUser.inputPlayer.GetButton(id)` when a hold must be reliable.
- **Gold**: kills, money packs and barrels all pay through `TeamManager.GiveTeamMoney`; classify by call context (see `Patches/GoldPatches.cs`).
- The vanilla dead-player drone feature is "Remote Operation" (`isRemoteOp`, `RemoteOperationDronePickerPanel`, `CharacterMaster.SpawnRemoteOperationDrone`).

## Icons

`tools/make_icons.py` draws icons in the style of the vanilla drone skill icons (dark glitch background, glowing corner brackets, one-colour glyph) with Pillow. Add a glyph function and call `compose(glyph, colour, seed)`.

## Publishing

1. Bump `version_number` in `thunderstore/<Mod>/manifest.json` **and** the plugin version in the source, and add a changelog entry. Uploaded versions can never be changed, so even a manifest-only change needs a new version.
2. `python tools/package.py` validates (name `[A-Za-z0-9_]`, description ≤ 250 chars, 256×256 icon, versions match) and writes `dist/<Mod>-<version>.zip`.
3. Upload at https://thunderstore.io/c/riskofrain2/create/ under the team. Tick the **AI Generated** category. New uploads take a while to appear in mod managers.
4. `website_url` in the manifest is the page's link to the GitHub repo.
5. Test the zip in a clean profile with the manager's **Import local mod** before announcing it.
