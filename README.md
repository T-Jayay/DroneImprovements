# DroneImprovements

A Risk of Rain 2 (BepInEx) mod that improves Remote Operation, the base game's feature that lets dead players pilot drones: drone players get survivor actions and gold, sprint and descend flight controls, Disconnect and Teleport to Player skills, and objectives that don't wait for them.

- Store page: [revoreverse/DroneImprovements](https://thunderstore.io/c/riskofrain2/p/revoreverse/DroneImprovements/). Its source, [thunderstore/DroneImprovements/README.md](thunderstore/DroneImprovements/README.md), describes every feature and setting for players; this file is for development.
- [docs/MODDING_GUIDE.md](docs/MODDING_GUIDE.md): how this mod, [MarkAllSeen](https://github.com/T-Jayay/MarkAllSeen) and [SprintImprovements](https://github.com/T-Jayay/SprintImprovements) are set up, built, tested and published.
- License: [the Unlicense](LICENSE) (public domain).

## Building

You need the [.NET SDK](https://dotnet.microsoft.com/download) 8 or later. The game, BepInEx, Risk of Options and the MonoMod hook types (`MMHOOK.RoR2`) are referenced from NuGet (the game's publicized reference assemblies, `RiskOfRain2.GameLibs`, come from the [BepInEx feed](https://nuget.bepinex.dev/)), so the first build needs internet access but not the game.

```
dotnet build DroneImprovements.sln -c Release
```

Each build copies `DroneImprovements.dll` and the test-only `DroneImprovements.DevTools.dll` (see below), with their PDBs, into a mod manager profile for testing, as `<ProfileDir>\BepInEx\plugins\<name>\`, if that profile exists; build `src/DroneImprovements/DroneImprovements.csproj` alone to leave DevTools out. The test profile needs BepInExPack and HookGenPatcher (which generates `MMHOOK_RoR2.dll` when the game starts), and Risk of Options for the in-game settings (players can use the mod without it). Set these properties on the command line or as environment variables:

| Property | Default | Example |
|---|---|---|
| `ProfileDir` | Thunderstore Mod Manager's `Test` profile: `%APPDATA%\Thunderstore Mod Manager\DataFolder\RiskOfRain2\profiles\Test` | an r2modman profile named `Dev`: `-p:ProfileDir="C:\Users\<you>\AppData\Roaming\r2modmanPlus-local\RiskOfRain2\profiles\Dev"` |
| `DeployToProfile` | `true` | `-p:DeployToProfile=false` builds without deploying |

Close the game before building: it keeps the plugins locked, so the copy fails. The build takes the version from `thunderstore/DroneImprovements/manifest.json` and embeds `thunderstore/DroneImprovements/icon.png` for the Risk of Options mod list.

## Releasing

1. Set the new version in `thunderstore/DroneImprovements/manifest.json` (`version_number`) and in `PluginVersion` in `src/DroneImprovements/DroneImprovementsPlugin.cs`, and list the changes under `## <version>` in `thunderstore/DroneImprovements/CHANGELOG.md`. An uploaded version can't be changed, so any change, even to the manifest alone, needs a new version.
2. Commit.
3. `python tools/package.py` (Python 3.8 or later) checks the package against Thunderstore's rules and the version against `PluginVersion` and the changelog, builds the mod and writes `dist/DroneImprovements-<version>.zip`. It refuses to package uncommitted changes or to overwrite an existing zip; `--force` skips both checks, for test builds only.
4. Upload the zip at https://thunderstore.io/package/create/ with the team **revoreverse** and the community Risk of Rain 2, and tick the **AI Generated** category.

The full checklist is under Publishing in the [modding guide](docs/MODDING_GUIDE.md#publishing).

## How it works

- `DroneImprovementsPlugin.cs` sets everything up and adds the MonoMod hooks (`On.` and `IL.` hooks) in `Hooks/` and `Skills/`. Players get `MMHOOK_RoR2.dll`, which the hooks need, from HookGenPatcher, a dependency in the manifest.
- Every player needs the same version of the mod: it adds `revor.DroneImprovements;<version>` to the game's network mod list, so the game refuses players with another version or without the mod. The mod adds skills and entity states to the game's catalogs and has its own network messages, so a mismatch would break the session.
- Settings marked *host* in the store README are decided by the host, which sends them to the clients (`HostSettings.cs`). The parts of the mod that also run on clients then follow the host: the interaction prompts (`DroneSurvivorActions`, and `DroneBarrelGold` and `DroneShrineOfBloodGold` for barrels and the Shrine of Blood), whether a holdout zone's charge indicator and Lepton Daisy's effect on it count a drone in the zone as charging (`DronesCanChargeHoldoutZones`), and the flight speed, which the client that owns the drone computes (`SprintSpeedMultiplier`).
- Network messages, handled through the game's `[NetworkMessageHandler]`, with their ids in `MessageIds.cs`: 17392, a client asking the host to remove its drone (Disconnect, `DroneNetworking.cs`); 17393, the host settings (`HostSettings.cs`).
- `Skills/` adds the Disconnect (Utility) and Teleport to Player (Special) skills to every Remote Operation body. `DroneTeleport.cs` finds a safe spot next to a player; other mods can add destinations through the public `DroneTeleportApi`.
- `Hooks/` has one file per host setting: `InteractionHooks.cs` (`DroneSurvivorActions`), `GoldHooks.cs` (`DroneCombatGold`, `DroneBarrelGold` and `DroneShrineOfBloodGold`), `FlightHooks.cs` (`SprintSpeedMultiplier`, and the per-player `DescendKey`: one IL hook on `PlayerFlyState.PerformInputs`), `HoldoutZoneHooks.cs` (`DronesCanChargeHoldoutZones`), `AllPlayersCheckHooks.cs` (`IgnoreDronesForAllPlayerChecks`: the Mithrix arena entrance, all-players triggers and the escape ship) and `ArenaVoidKillHooks.cs` (`SpareDronesFromArenaVoidKill`).
- Risk of Options is optional (a soft dependency): `RiskOfOptionsCompat.cs` is only used when it is installed.

## Testing solo (DevTools)

`src/DroneImprovements.DevTools` is a test-only plugin that is never packaged. It makes the multiplayer-only drone flow testable in a solo run. Launch the test profile and start a run; a panel on the left lists the keys, which you can change in `BepInEx/config/revor.DroneImprovements.DevTools.cfg`:

| Key | Action |
|---|---|
| F5 | Show or hide the panel |
| F6 | Kill yourself |
| F7 | +$1000, added to your money directly, so the gold settings don't affect it |
| F4 | Spawn an invincible AI stand-in player: a teleport target that also counts as a living player for holdout zones, and can be spectated. Spawn one before dying: with nobody to spectate there is no Remote Operation menu. |
| F3 | Remove every stand-in, for example to see Teleport greyed out with nobody to go to |
| F10 | Turn "prevent game over" on or off (on by default, so a solo run survives your death) |

Picking a drone works as in the base game (spectator menu, discovered drones only, normal cost). DevTools is for solo tests: when the host has it, prevent game over applies to the whole lobby, so it warns in the log and on screen whenever more than one player is present.

Solo testing doesn't cover everything. Before a release, check these in a real lobby with another player:
- Disconnect from a **client** (the client → host request naming the drone, message 17392).
- The host settings reaching clients (message 17393): the interaction prompts follow the host, and so do a holdout zone's charge indicator and Lepton Daisy's effect on it while only a drone is in the zone; host settings are greyed out in the client's Risk of Options.
- Flying a drone as a **client**: Shift flies faster by the host's `SprintSpeedMultiplier` (also after the host changes it), Left Ctrl flies down, Space flies up, and the Descend key does nothing while typing in chat.
- A player with a different version of the mod, or without it, being refused.
- The Mithrix arena cutscene and the escape ship not waiting for drones; drones inside the arena and aboard the ship surviving; drones outside the arena not being killed.
- The run ending when every non-drone player is dead.

## Icons

`tools/make_icons.py` (needs Pillow) draws the two skill icons into `src/DroneImprovements/Assets/`, which the build embeds as `DroneImprovements.Assets.<file>`, and the store icon `thunderstore/DroneImprovements/icon.png`. With Pillow 12.3.0 it reproduces the committed PNGs byte for byte.
