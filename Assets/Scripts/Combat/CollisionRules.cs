using RogueShooter.Ai;
using RogueShooter.Maze;

namespace RogueShooter.Combat
{
    /// <summary>
    /// Collision volume sizes. Body halves reuse the existing orb-hit radius (0.40);
    /// wall thickness matches Stage-1 maze wall quads. Not a combat balance table.
    /// </summary>
    public static class CollisionRules
    {
        public const float WallThickness = 0.45f;
        public const float BodyHalf = EnemyCombatRules.OrbHitRadiusStub;
        public const float PlayerHalfX = BodyHalf;
        public const float PlayerHalfY = BodyHalf;
        public const float MobHalfX = BodyHalf;
        public const float MobHalfY = BodyHalf;
        public const float ProjectileRadius = EnemyCombatRules.OrbHitRadiusStub;

        public const int UnityLayerPlayer = 8;
        public const int UnityLayerMob = 9;
        public const int UnityLayerWall = 10;
        public const int UnityLayerDoor = 11;
        public const int UnityLayerProjectile = 12;

        public static CollisionLayer SolidMask => CollisionLayer.Wall | CollisionLayer.Door;

        public static float DoorOpening => MazeRules.CorridorWidth;

        public static int UnityLayer(CollisionLayer layer)
        {
            if ((layer & CollisionLayer.Player) != 0)
                return UnityLayerPlayer;
            if ((layer & CollisionLayer.Mob) != 0)
                return UnityLayerMob;
            if ((layer & CollisionLayer.Wall) != 0)
                return UnityLayerWall;
            if ((layer & CollisionLayer.Door) != 0)
                return UnityLayerDoor;
            if ((layer & CollisionLayer.Projectile) != 0)
                return UnityLayerProjectile;
            return 0;
        }
    }
}
