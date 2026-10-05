using System;

namespace RogueShooter.Boss
{
    /// <summary>
    /// Runtime numbers for the 2026-10-05 final-boss redesign.
    /// Design package base HP was 1100; the producer lock replaces that constant with 1200.
    /// Design CSVs are not loaded at runtime.
    /// </summary>
    public struct BossSkill
    {
        public float Windup;
        public float Lock;
        public float Active;
        public float Recovery;
        public float Cooldown;
        public float Damage;
        public float Speed;
        public float Radius;
        public float Angle;
        public float Min;
        public float Max;
        public int Projectiles;
        public float HitRadius;
        public float ShotInterval;
    }

    public static class FinalBossRules
    {
        /// <summary>Locked base HP. The enter formula multiplies this, never 1100 or 3850.</summary>
        public const float BaseHp = 1200f;

        /// <summary>Same locked base. Reference enter (B14, pressure 1.55) resolves to this value.</summary>
        public const float MaxHp = BaseHp;

        public const float PressureRef = 1.55f;
        public const float DamageWeightRef = 1.28f;
        public const float IntroSeconds = 1f;
        public const float TransitionSeconds = 1.2f;
        public const float BodyRadius = 0.7f;
        public const float BodyScale = 1.7f;
        public const float PlayerRadius = 0.32f;
        public const float ShotRange = 12f;
        public const float RecoveryExtension = 0.5f;
        public const float WeakCooldown = 6f;
        public const float LeapAirSeconds = 0.85f;
        public const float LeapHeight = 0.9f;
        public const float LeapLandTime = 1.55f;
        public const float LeapHitEnd = 1.70f;
        public const float LogicStep = 0.05f;
        public const float PredictWindow = 0.20f;
        public const float ReferenceBuild = 14f;
        public const float FrontDamageMul = 0.5f;

        public static float MoveSpeed(BossPhase phase)
        {
            return phase == BossPhase.P2 ? 4.5f : 2.5f;
        }

        public static float TurnSpeed(BossPhase phase)
        {
            return phase == BossPhase.P2 ? 180f : 90f;
        }

        public static float Quiet(BossPhase phase)
        {
            return phase == BossPhase.P2 ? 0.35f : 1.1f;
        }

        public static BossSkill Skill(BossMoveId move)
        {
            switch (move)
            {
                case BossMoveId.StraightShot:
                    return SkillOf(1f, 0.5f, 0.05f, 1.65f, 4f, 18f, 8f, 0f, 0f, 3.5f, 10f, 1, 0.12f, 0f);
                case BossMoveId.BurstShot:
                    return SkillOf(1f, 0.65f, 1.05f, 1.85f, 4f, 12f, 8f, 0f, 0f, 3.5f, 10f, 3, 0.12f, 0.5f);
                case BossMoveId.WarningCharge:
                    return SkillOf(1.15f, 0.6f, 0.7f, 1.8f, 6f, 24f, 10f, 0.75f, 0f, 3.5f, 7f, 0, 0f, 0f);
                case BossMoveId.ShieldBash:
                    return SkillOf(0.85f, 0.3f, 0.15f, 1.4f, 4.8f, 20f, 0f, 2.6f, 120f, 0f, 2.8f, 0, 0f, 0f);
                case BossMoveId.TripleShot:
                    return SkillOf(0.9f, 0.4f, 0.05f, 1.3f, 3f, 16f, 8.5f, 0f, 15f, 3.5f, 10f, 3, 0.10f, 0f);
                case BossMoveId.LeapSlam:
                    return SkillOf(0.7f, 0.7f, 1f, 1.65f, 6f, 26f, 0f, 2f, 360f, 4f, 9f, 0, 0f, 0f);
                case BossMoveId.CrossbowBash:
                    return SkillOf(0.7f, 0.15f, 0.1f, 1f, 3f, 22f, 0f, 2.8f, 100f, 0f, 3f, 0, 0f, 0f);
                default:
                    return default(BossSkill);
            }
        }

        static BossSkill SkillOf(
            float windup, float lockAt, float active, float recovery, float cooldown, float damage,
            float speed, float radius, float angle, float min, float max, int projectiles, float hitRadius, float interval)
        {
            return new BossSkill
            {
                Windup = windup,
                Lock = lockAt,
                Active = active,
                Recovery = recovery,
                Cooldown = cooldown,
                Damage = damage,
                Speed = speed,
                Radius = radius,
                Angle = angle,
                Min = min,
                Max = max,
                Projectiles = projectiles,
                HitRadius = hitRadius,
                ShotInterval = interval
            };
        }

        public static bool IsP1Ranged(BossMoveId move)
        {
            return move == BossMoveId.StraightShot || move == BossMoveId.BurstShot;
        }

        public static bool IsMelee(BossMoveId move)
        {
            return move == BossMoveId.ShieldBash || move == BossMoveId.CrossbowBash;
        }

        public static bool IsHeavy(BossMoveId move)
        {
            return move == BossMoveId.WarningCharge || move == BossMoveId.LeapSlam;
        }

        public static bool IsRetired(BossMoveId move)
        {
            return move == BossMoveId.RingBurst;
        }

        public static BossMoveId CooldownKey(BossMoveId move)
        {
            return IsP1Ranged(move) ? BossMoveId.StraightShot : move;
        }

        /// <summary>c(p): p&lt;=1.25 → 1+(p-1)/0.25, else 2+(p-1.25)/0.30.</summary>
        public static float PressureCoordinate(float pressure)
        {
            if (pressure < 0f)
                pressure = 0f;
            if (pressure <= 1.25f)
                return 1f + (pressure - 1f) / 0.25f;
            return 2f + (pressure - 1.25f) / 0.30f;
        }

        /// <summary>Enter HP = 1200 * hpWeight(B, c(p)) * p / 1.55. Locked once at enter.</summary>
        public static float EnterMaxHp(int build, float pressure)
        {
            float hp;
            float dmg;
            Weights(build, pressure, out hp, out dmg);
            if (pressure < 0f)
                pressure = 0f;
            return BaseHp * hp * pressure / PressureRef;
        }

        /// <summary>Table damage multiplier = dmgWeight / 1.28 * p / 1.55. Refreshes with pressure.</summary>
        public static float OutgoingMultiplier(int build, float pressure)
        {
            float hp;
            float dmg;
            Weights(build, pressure, out hp, out dmg);
            if (pressure < 0f)
                pressure = 0f;
            return dmg / DamageWeightRef * pressure / PressureRef;
        }

        public static void Weights(int build, float pressure, out float hpWeight, out float dmgWeight)
        {
            float c = PressureCoordinate(pressure);
            BossScaleTable.ContinuousMuls(build, c, out hpWeight, out dmgWeight);
        }

        /// <summary>
        /// P1 front non-weak hits take 50% (half-plane, boundary included).
        /// Weak hits and P2 hits are full. A zero direction is not frontal.
        /// </summary>
        public static float IncomingMultiplier(BossPhase phase, float faceX, float faceY, float inX, float inY, bool weak)
        {
            if (phase == BossPhase.P2 || weak)
                return 1f;
            float inMag = inX * inX + inY * inY;
            float faceMag = faceX * faceX + faceY * faceY;
            if (inMag <= 0.000001f || faceMag <= 0.000001f)
                return 1f;
            float invIn = 1f / (float)Math.Sqrt(inMag);
            float invFace = 1f / (float)Math.Sqrt(faceMag);
            float dot = (faceX * invFace) * (-inX * invIn) + (faceY * invFace) * (-inY * invIn);
            if (dot >= -0.000001f)
                return FrontDamageMul;
            return 1f;
        }

        public static bool InSector(
            float cx, float cy, float faceX, float faceY, float px, float py,
            float radius, float angleDeg, float bodyRadius)
        {
            float dx = px - cx;
            float dy = py - cy;
            float dist2 = dx * dx + dy * dy;
            float body2 = bodyRadius * bodyRadius;
            if (dist2 <= body2)
                return true;

            float dist = (float)Math.Sqrt(dist2);
            float faceMag = (float)Math.Sqrt(faceX * faceX + faceY * faceY);
            float nx = faceMag > 0.000001f ? faceX / faceMag : 1f;
            float ny = faceMag > 0.000001f ? faceY / faceMag : 0f;
            float dot = (nx * dx + ny * dy) / dist;
            if (dot > 1f)
                dot = 1f;
            if (dot < -1f)
                dot = -1f;
            float ang = (float)(Math.Acos(dot) * (180.0 / Math.PI));
            if (ang <= angleDeg * 0.5f && dist <= radius + bodyRadius)
                return true;

            float facing = (float)Math.Atan2(ny, nx);
            for (int side = -1; side <= 1; side += 2)
            {
                float edge = facing + side * angleDeg * 0.5f * (float)(Math.PI / 180.0);
                float ex = (float)Math.Cos(edge) * radius;
                float ey = (float)Math.Sin(edge) * radius;
                float edge2 = ex * ex + ey * ey;
                if (edge2 < 0.0000001f)
                    continue;
                float t = (dx * ex + dy * ey) / edge2;
                if (t < 0f)
                    t = 0f;
                if (t > 1f)
                    t = 1f;
                float qx = dx - ex * t;
                float qy = dy - ey * t;
                if (qx * qx + qy * qy <= body2)
                    return true;
            }

            return false;
        }

        public static void PredictPoint(
            float playerX, float playerY, float bossX, float bossY, float vx, float vy,
            out float outX, out float outY)
        {
            float dx = playerX - bossX;
            float dy = playerY - bossY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float lead = dist / 8f + 0.35f;
            if (lead > 0.75f)
                lead = 0.75f;
            float ox = vx * lead * 0.65f;
            float oy = vy * lead * 0.65f;
            float mag = (float)Math.Sqrt(ox * ox + oy * oy);
            if (mag > 3f)
            {
                ox *= 3f / mag;
                oy *= 3f / mag;
            }

            outX = playerX + ox;
            outY = playerY + oy;
        }
    }
}
