# Changelog

## 1.1.1

- Every player needs the same version of the mod: the game refuses players with a different version, or without the mod, instead of letting them into a broken session.
- Drone players on the escape ship leave with it instead of being killed when it takes off.
- Drone players inside the Mithrix arena count as inside, as in the base game, so they're never killed when it seals, even with `SpareDronesFromArenaVoidKill` off. The arena still doesn't wait for drones outside.
- Clients use the host's `DronesCanChargeHoldoutZones`, `IgnoreDronesForAllPlayerChecks` and `SpareDronesFromArenaVoidKill` too, not their own, so for example a holdout zone's charge indicator and Lepton Daisy's effect on it agree with the host on whether a drone inside is charging it.
- Drone players can't pick up items that a stage's objective needs (keys and the like) any more, as in the base game. Equipment still works.
- Drones no longer reveal the teleporter just by flying near it, as in the base game.
- Holding the Teleport or Disconnect key no longer repeats the skill; press it again.
- Teleport to Player always has a single charge (Lysate Cell doesn't add any). Its 30 second cooldown is a base value that cooldown items can shorten.
- Disconnect's hold bar matches the hold time with cooldown items, and never shows a charge counter.
- `TeleportCooldown` is at least 1 second.
- `DisconnectHoldSeconds` and `TeleportCooldown` can be changed in Risk of Options, and both apply without a restart.
- Risk of Options greys out the host settings while you're a client, and shows the mod's icon.
- Players who left the game are no longer Teleport targets.
- A Teleport whose target disappears at the last moment plays the "can't do that" sound (the charge is refunded).
- Remote Operation drones added by other mods get Disconnect and Teleport to Player whatever their body is called. To use chests, shrines and other interactables they need to be able to interact, as every base-game drone can; the rest of the mod works for them either way.
- If a game update breaks part of the mod, only that part stops working (it works as in the base game, with an error in the log) and the rest keeps working.
- Corrected the descriptions: drone players never get Executive Card cashback (Remote Operation disables your equipment), and drones could always open barrels, whatever `DroneSurvivorActions` says.
- Added a link to the source code: https://github.com/T-Jayay/DroneImprovements
- For other mods: `DroneTeleportApi` handlers get their own empty list to add targets to, and an error in one handler no longer stops the others.

## 1.1.0

- New host setting **DroneGold**, how drone players earn gold (the mod never takes the gold they have):
  - **Full** (default): like survivors.
  - **NoKillGold**: no gold from kills and combat (enemy kills, Ghor's Tome, Brittle Crown, Roll of Pennies); barrels and Shrine of Blood still pay.
  - **None**: no gold at all, and barrels and Shrine of Blood can't be used.
  - Kill gold a drone would have received goes to the living survivors.
- **DroneSurvivorActions** now only controls interacting, buying and picking up items; gold is controlled by DroneGold.

## 1.0.0

- Initial release.
- Drones keep survivor actions: interact, buy, pick up items and earn gold (host toggle, synced to all players; optional in-game toggle with Risk of Options).
- Drone abilities: Disconnect (Utility) and Teleport to Player (Special).
- Mithrix quality of life: drones are no longer needed to start the arena cutscene or for the escape ship to leave, and aren't void-killed outside the arena.
- Drones inside the teleporter zone help charge it; drones outside never slow it down.
