# Drone Improvements

- Allow drones to keep survivor actions: interact, buy, pick up items, and continue to gain gold *(toggleable settings as host)*
- Drone abilities: disconnect from the drone, or teleport to the nearest player
- Mithrix quality of life fixes: drones are no longer needed to start the cutscene

The idea is that when playing multiplayer, the run isn't completely doomed for a player who is forced to fall behind when dying, along with some small quality of life improvements.

## Details

### Survivor actions *(host setting, on by default)*
While controlling a drone you can open chests and barrels, use shrines, printers, scrappers and the teleporter, pay with your own money, and pick up items and equipment. The host's setting applies to everyone. With it off, drones are locked out of the environment like in the base game.

### Drone gold *(host setting)*
Drone players always keep the gold they had when they died. The host chooses how they earn more:
- **Full** *(default)*: they earn gold like survivors.
- **NoKillGold**: no gold from enemy kills, Ghor's Tome money packs, Brittle Crown or Roll of Pennies. Barrels, Shrine of Blood and Executive Card cashback still pay.
- **None**: no gold at all, and barrels and Shrine of Blood can't be used.

Kill gold a drone would have received goes to the living survivors instead.

### Drone abilities
Both show up in the normal skill bar with their own icons and cooldowns.
- **Disconnect** *(Utility)*: hold for 1 second to leave the drone and go back to spectating. The drone is removed, not killed, so it won't use up a Dio's Best Friend. You can pick a drone again from the spectator menu.
- **Teleport to Player** *(Special, R)*: jump next to the nearest living player, never into terrain. 30 second cooldown. It's greyed out when there's nobody to go to.

### Quality of life
- The Mithrix arena cutscene and the escape ship no longer wait for drone players. Drones left outside the sealed arena aren't killed, so they can teleport in.
- A drone inside the teleporter zone helps charge it. A drone outside never slows it down.
- The run still ends when every non-drone player is dead, and picking a drone works exactly as in the base game.

## Multiplayer
**Every player needs the mod installed.** Settings marked *(host)* are decided by the host.

## Configuration
Edit `BepInEx/config/revor.DroneImprovements.cfg`, use your mod manager's config editor, or change them in game with [Risk of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) installed.

| Setting | Default | |
|---|---|---|
| DroneSurvivorActions | true | *(host)* interact, buy, pick up items |
| DroneGold | Full | *(host)* Full / NoKillGold / None, see above |
| DronesCanChargeHoldoutZones | true | *(host)* drones inside the teleporter zone help charge it |
| IgnoreDronesForAllPlayerChecks | true | *(host)* Mithrix cutscene and escape ship don't wait for drones |
| SpareDronesFromArenaVoidKill | true | *(host)* drones outside the sealed Mithrix arena aren't killed |
| DisconnectEnabled | true | Disconnect ability |
| DisconnectHoldSeconds | 1 | how long to hold Disconnect |
| TeleportEnabled | true | Teleport to Player ability |
| TeleportCooldown | 30 | teleport cooldown in seconds |
