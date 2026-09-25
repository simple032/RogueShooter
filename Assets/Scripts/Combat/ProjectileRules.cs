using RogueShooter.Ai;
using RogueShooter.Player;

namespace RogueShooter.Combat
{
    /// <summary>
    /// Flying shot numbers reuse existing projectile / charge rows.
    /// Arrow speed = orb speed stub (player_move×2). Range = charge hitRange 8.
    /// </summary>
    public static class ProjectileRules
    {
        public const float ArrowSpeed = EnemyCombatRules.OrbSpeedAbsStub;
        public const float ArrowMaxRange = 8f;
        public const float ArrowHitRadius = EnemyCombatRules.OrbHitRadiusStub;
        /// <summary>
        /// Arrow vs wall/door: the shaft, not the 0.40 hit disc. With 0.40 an arrow fired sideways
        /// from a doorway grazed the closed door strip beside it (PR#19 retest, N1).
        /// Mobs still use ArrowHitRadius against their collision volume.
        /// </summary>
        public const float ArrowBlockRadius = 0.08f;
        public const float OrbSpeed = EnemyCombatRules.OrbSpeedAbsStub;
        public const float OrbHitRadius = EnemyCombatRules.OrbHitRadiusStub;

        public static ChargeShotKind ResolveShot(float heldSeconds)
        {
            return ChargeShotRules.Resolve(heldSeconds);
        }
    }
}
