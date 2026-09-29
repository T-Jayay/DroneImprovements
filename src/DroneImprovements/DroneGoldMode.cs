namespace DroneImprovements
{
    /// <summary>
    /// How drone players earn gold (host setting DroneGold). Whatever the mode, the mod never takes gold a drone
    /// player already has. Public because it is a config value type.
    /// </summary>
    public enum DroneGoldMode
    {
        /// <summary>Drone players earn gold like survivors.</summary>
        Full,

        /// <summary>
        /// No combat gold (enemy kills, money packs, Brittle Crown, Roll of Pennies, ...); barrels and Shrine of Blood
        /// still pay.
        /// </summary>
        NoKillGold,

        /// <summary>No gold at all, and barrels and Shrine of Blood can't be used.</summary>
        None
    }
}
