using System;

namespace RogueShooter.Combat
{
    /// <summary>
    /// Gameplay collision layers. Independent of Unity sorting layers.
    /// Mapped to Unity physics layers 8–12 for inspector readability.
    /// </summary>
    [Flags]
    public enum CollisionLayer
    {
        None = 0,
        Player = 1 << 0,
        Mob = 1 << 1,
        Wall = 1 << 2,
        Door = 1 << 3,
        Projectile = 1 << 4
    }
}
