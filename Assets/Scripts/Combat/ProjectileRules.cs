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
        public const float OrbSpeed = EnemyCombatRules.OrbSpeedAbsStub;
        public const float OrbHitRadius = EnemyCombatRules.OrbHitRadiusStub;

        public static ChargeShotKind ResolveShot(float heldSeconds)
        {
            return ChargeShotRules.Resolve(heldSeconds);
        }
    }
}
