namespace RogueShooter.RoomCombat
{
    /// <summary>一阶段战斗房型（GDD §4.5）。走廊不刷不锁；BOSS 不走本节。</summary>
    public enum RoomCombatKind
    {
        Corridor = 0,
        Normal = 1,
        SmallChest = 2,
        Altar = 3,
        Boss = 4
    }
}
