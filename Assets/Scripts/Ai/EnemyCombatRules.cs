using RogueShooter.Player;
using RogueShooter.Spawning;

namespace RogueShooter.Ai
{
    /// <summary>
    /// Spec v0.5 enemy combat constants. Damage/HP numbers marked STUB are
    /// TTK-anchor placeholders — not lock-CSV rows (数值重填).
    /// </summary>
    public static class EnemyCombatRules
    {
        public const float PlayOrthoSize = 6f; // producer retune; keep in sync with CameraViewService
        public const float DefaultAspect = 16f / 9f;
        public const float OrbSpeedWalkMul = 2f;
        public const float OrbRangeCameraWidthFrac = 0.7f;
        public const float ShieldRaiseDelaySeconds = 1f;
        public const float ShieldMoveMul = 0.30f; // −70%
        public const float ShieldFrontDamageMul = 0.50f;
        public const float ShieldWeakSpotStaggerSeconds = 1.00f;
        public const float GrandOrbSpreadDegrees = 15f;
        public const int CultOrbCount = 1;
        public const int GrandOrbCount = 3;
        public const float BangVisibleSeconds = 0.05f; // keep mark up for whole windup

        // STUB lunge geometry. Damage is DRAFT absolute [30,40] on easy (stats CSV).
        public const float LungeRangeStub = 2.80f;
        public const float LungeDistanceStub = 2.20f;
        public const float LungeSpeedStub = 14.00f;
        public const float LungeCooldownStub = 1.60f;
        public const float LungeContactRadiusStub = 0.45f;
        public const int LungeDamageMinEasyStub = 30;
        public const int LungeDamageMaxEasyStub = 40;

        public const float MeleeHitRadius = 0.70f;
        public const float OrbHitRadiusStub = 0.40f;

        // STUB play-scale walk (player play ≈ 6). Not MoveSpeeds lock CSV.
        public const float WalkNormalStub = 2.40f;
        public const float WalkDogStub = 3.60f;
        public const float WalkMageStub = 2.00f;
        public const float WalkShieldStub = 1.80f;
        public const float WalkGrandStub = 1.80f;

        public static float CameraWidth(float orthographicSize, float aspect)
        {
            if (orthographicSize < 0.01f)
                orthographicSize = PlayOrthoSize;
            if (aspect < 0.01f)
                aspect = DefaultAspect;
            return 2f * orthographicSize * aspect;
        }

        public static float OrbMaxRange(float orthographicSize, float aspect)
        {
            return CameraWidth(orthographicSize, aspect) * OrbRangeCameraWidthFrac;
        }

        public static float OrbSpeed(float walkSpeed)
        {
            return walkSpeed * OrbSpeedWalkMul;
        }

        public static void RotateDeg(float x, float y, float degrees, out float ox, out float oy)
        {
            double rad = degrees * 3.14159265358979323846 / 180.0;
            double c = System.Math.Cos(rad);
            double s = System.Math.Sin(rad);
            ox = (float)(x * c - y * s);
            oy = (float)(x * s + y * c);
        }

        /// <summary>
        /// Front = attacker is in the shield-facing hemisphere.
        /// Weak-spot shots ignore the front mul (Spec: 弱点射击伤害不变).
        /// </summary>
        public static float IncomingDamageMul(bool shieldRaised, bool hitFromFront, bool weakSpot)
        {
            if (!shieldRaised || weakSpot || !hitFromFront)
                return 1f;
            return ShieldFrontDamageMul;
        }

        public static bool HitFromFront(float facingX, float facingY, float attackerX, float attackerY, float selfX, float selfY)
        {
            float dx = attackerX - selfX;
            float dy = attackerY - selfY;
            float mag = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (mag < 0.001f)
                return true;
            dx /= mag;
            dy /= mag;
            float fmag = (float)System.Math.Sqrt(facingX * facingX + facingY * facingY);
            if (fmag < 0.001f)
                return true;
            facingX /= fmag;
            facingY /= fmag;
            return facingX * dx + facingY * dy > 0f;
        }

        public static float WeakSpotStaggerSeconds(string kindId)
        {
            if (kindId == EnemyKindIds.Shield)
                return ShieldWeakSpotStaggerSeconds;
            return ChargeShotRules.WeakSpotStaggerSeconds;
        }

        public static bool UsesOrbs(string kindId)
        {
            return kindId == EnemyKindIds.CultMage || kindId == EnemyKindIds.GrandMage;
        }

        public static int OrbCount(string kindId)
        {
            if (kindId == EnemyKindIds.GrandMage)
                return GrandOrbCount;
            if (kindId == EnemyKindIds.CultMage)
                return CultOrbCount;
            return 0;
        }

        public static bool CanLunge(string kindId, StageId stage)
        {
            return kindId == EnemyKindIds.Normal && StageIdUtil.GrantsLunge(stage);
        }
    }
}
