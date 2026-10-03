# Drone Improvements

When you die in multiplayer and take over a drone with Remote Operation, the run isn't over for you: your drone can use the world like a survivor, flies faster when you sprint, gets Disconnect and Teleport to Player skills, and the Mithrix arena and the escape ship don't wait for it.

## Survivor actions *(host setting)*
While controlling a drone you can open chests, use shrines, printers, scrappers and the teleporter, pay with your own gold, and pick up equipment. Items that a stage's objective needs (keys and the like) stay off-limits, as in the base game. With `DroneSurvivorActions` off, drones are locked out of all that as in the base game; they can still pick up items and open barrels (unless `DroneBarrelGold` is off), which the base game allows. Using interactables needs a drone that can interact: every base-game drone can, but a drone added by another mod might not.

## Drone gold *(host settings)*
The mod never takes gold a drone player already has (the base game still can, for example Brittle Crown when they're hit). Three settings decide how they earn more, one for each source of gold. All are on by default:
- `DroneCombatGold`: gold from combat: enemy kills, money packs (Ghor's Tome, Cleanup Drone, gilded enemies), Brittle Crown, Roll of Pennies and other gold from hitting or getting hit. Off: they get none.
- `DroneBarrelGold`: opening barrels and getting their share of the gold. Off: they can't open barrels.
- `DroneShrineOfBloodGold`: using Shrine of Blood. Off: they can't use it.

Shared gold a drone player doesn't get (combat gold, barrels) is split between the living survivors instead.

Gold from other mods follows the same rules when it comes through the game's own payouts, and gold a mod pays straight to a drone player during a kill, a hit or damage counts as combat gold. Mods that give every player the same wallet, such as ShareSuite's shared money, decide drone players' gold themselves.

## Flight controls
Drones fly with the base game's controls, except that Sprint now makes you faster instead of flying down:
- **Space** (Jump) flies up.
- **Left Ctrl** (`DescendKey`, per player, you can rebind it) flies down. Only the key itself counts, not modifier keys, so it also works while you hold movement keys. Pressing Jump while you hold it flies up, as in the base game.
- **Shift** (Sprint) flies `SprintSpeedMultiplier` times faster *(host setting)*: 2 by default, 1 to 3. At 1 this is off, and Sprint flies down as in the base game.

## Drone abilities
Both show in the skill bar with their own icons, like any skill. Holding the key doesn't repeat them: press it again.
- **Disconnect** *(Utility)*: hold it (1 second by default) to leave the drone and go back to spectating. The drone is removed, not killed, so nothing that happens on death triggers: a Dio's Best Friend is neither used up nor revives you. You can pick a drone again from the spectator menu.
- **Teleport to Player** *(Special, R)*: jump next to the nearest living player, never into terrain. One charge, with a 30 second base cooldown by default (cooldown items can shorten it). It's greyed out while there's nobody to go to.

## Objectives *(host settings)*
- A drone inside a holdout zone (the teleporter, pillars, Void Fields cells, ...) helps charge it. A drone outside never slows it down.
- The Mithrix arena and the escape ship only wait for survivors, not for drone players. Drones that are there anyway still count: they're safe when the arena seals and leave with the ship.
- Drones left outside the sealed Mithrix arena aren't killed (which would also use up a Dio's Best Friend), so they can teleport in.
- The run still ends when every survivor is dead, and picking a drone works exactly as in the base game.

## Settings
In `BepInEx/config/revor.DroneImprovements.cfg`, or in game under Settings → Mod Options → **DroneImprovements** when [Risk of Options](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) is installed. Changes apply immediately.
- `DroneSurvivorActions` (default `true`, host): drone players can interact, buy and pick up equipment.
- `DroneCombatGold` (default `true`, host): drone players earn combat gold.
- `DroneBarrelGold` (default `true`, host): drone players can open barrels and get their share of the gold.
- `DroneShrineOfBloodGold` (default `true`, host): drone players can use Shrine of Blood.
- `DronesCanChargeHoldoutZones` (default `true`, host): drones inside a holdout zone help charge it.
- `IgnoreDronesForAllPlayerChecks` (default `true`, host): the Mithrix arena and the escape ship don't wait for drones.
- `SpareDronesFromArenaVoidKill` (default `true`, host): drones outside the sealed Mithrix arena aren't killed.
- `DisconnectEnabled` (default `true`): your drone's Disconnect skill.
- `DisconnectHoldSeconds` (default `1`, 0 to 5): how long to hold Disconnect.
- `TeleportEnabled` (default `true`): your drone's Teleport to Player skill.
- `TeleportCooldown` (default `30`, 1 to 600): Teleport's base cooldown in seconds.
- `SprintSpeedMultiplier` (default `2`, 1 to 3, host): how many times faster drones fly while holding Sprint; 1 is off.
- `DescendKey` (default `LeftControl`): hold to fly your drone down.

## Multiplayer
Every player needs the mod, in the same version: the game doesn't let players with a different version, or without the mod, join each other. Settings marked *host* are decided by the host for everyone (in Risk of Options they're greyed out while you're a client); the others only change your own drone.
