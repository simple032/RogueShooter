namespace RogueShooter.Boss
{
    public enum FinalBossPhase
    {
        P1,
        P2
    }

    public enum FinalBossMoveId
    {
        None,
        NailBow,
        ShieldCharge,
        TripleArrow,
        RingBolt,
        Melee
    }

    public enum FinalBossStep
    {
        Idle,
        Windup,
        Active,
        Recovery
    }

    public enum FinalBossOpenChoice
    {
        Melee,
        Ranged,
        Hold
    }

    /// <summary>
    /// 终局 Boss 开招规则。近战数字来自 balance_boss_melee_draft.csv，草案不锁。
    /// 远程四招的伤害和时序不在这张近战表里改。
    /// </summary>
    public static class FinalBossRules
    {
        public const string DraftCsv = "balance_boss_melee_draft.csv";

        /// <summary>终局 Boss 血量。不改。</summary>
        public const float MaxHp = 1200f;

        /// <summary>钉骨弩伤害。不改。</summary>
        public const float NailBowDamage = 20f;

        /// <summary>锁向盾冲伤害。不改。</summary>
        public const float ShieldChargeDamage = 25f;

        /// <summary>裂冠三矢伤害。不改。</summary>
        public const float TripleArrowDamage = 18f;

        /// <summary>断仪环 / 环弹伤害。不改。</summary>
        public const float RingBoltDamage = 15f;

        /// <summary>钉骨弩一发。近战草案不改颗数。</summary>
        public const int NailBowVolley = 1;

        /// <summary>盾冲是冲锋拍，不另生成弹体。时序仍走原表。</summary>
        public const int ShieldChargeVolley = 0;

        /// <summary>裂冠三矢三发。近战草案不改颗数。</summary>
        public const int TripleArrowVolley = 3;

        /// <summary>
        /// 环弹会生成弹体。颗数不在近战草案里，不把这个数当成已锁伤害。
        /// 近战替换只保证这一拍不生成弹体。
        /// </summary>
        public const int RingBoltVolley = 1;

        /// <summary>原远程开招门槛。不放宽。近战不受这条限制。</summary>
        public const float RangedMinOpenDistance = 3f;

        /// <summary>
        /// 草案不锁（balance_boss_melee_draft.csv trigger_dist）。
        /// 接受区间 [3.0, 5.0]，不得小于 3u。等数值锁表后再填。
        /// </summary>
        public const float MeleeTriggerDistance = 4.0f;

        /// <summary>
        /// 草案不锁（windup）。接受区间 [0.60, 0.85]。等数值锁表后再填。
        /// </summary>
        public const float MeleeWindupSeconds = 0.70f;

        /// <summary>
        /// 草案不锁（hit_time）。出手在前摇结束，不绑帧率。等数值锁表后再填。
        /// </summary>
        public const float MeleeHitTimeSeconds = 0.70f;

        /// <summary>
        /// 草案不锁（hit_active）。接受区间 [0.03, 0.08]。等数值锁表后再填。
        /// </summary>
        public const float MeleeActiveSeconds = 0.05f;

        /// <summary>
        /// 草案不锁（recovery）。接受区间 [0.70, 1.00]。
        /// 只约束近战这一招，不插入另外四招的时序。等数值锁表后再填。
        /// </summary>
        public const float MeleeRecoverySeconds = 0.80f;

        /// <summary>
        /// 草案不锁（base_damage）。接受区间 [26, 33]。等数值锁表后再填。
        /// </summary>
        public const float MeleeDamage = 30f;

        /// <summary>草案不锁（hits）。单次判定，无多段。等数值锁表后再填。</summary>
        public const int MeleeHits = 1;

        public static float RangedDamage(FinalBossMoveId move)
        {
            switch (move)
            {
                case FinalBossMoveId.NailBow: return NailBowDamage;
                case FinalBossMoveId.ShieldCharge: return ShieldChargeDamage;
                case FinalBossMoveId.TripleArrow: return TripleArrowDamage;
                case FinalBossMoveId.RingBolt: return RingBoltDamage;
                default: return 0f;
            }
        }

        public static int ProjectileVolley(FinalBossMoveId move)
        {
            switch (move)
            {
                case FinalBossMoveId.NailBow: return NailBowVolley;
                case FinalBossMoveId.ShieldCharge: return ShieldChargeVolley;
                case FinalBossMoveId.TripleArrow: return TripleArrowVolley;
                case FinalBossMoveId.RingBolt: return RingBoltVolley;
                default: return 0;
            }
        }

        public static bool TryMapRanged(FinalBossMoveId move, out BossMoveId id)
        {
            switch (move)
            {
                case FinalBossMoveId.NailBow:
                    id = BossMoveId.StraightShot;
                    return true;
                case FinalBossMoveId.ShieldCharge:
                    id = BossMoveId.WarningCharge;
                    return true;
                case FinalBossMoveId.TripleArrow:
                    id = BossMoveId.TripleShot;
                    return true;
                case FinalBossMoveId.RingBolt:
                    id = BossMoveId.RingBurst;
                    return true;
                default:
                    id = BossMoveId.None;
                    return false;
            }
        }

        /// <summary>
        /// 近战读草案三段。远程四招读 <see cref="BossBrain.TimingsFor"/>，不在这里改秒数。
        /// </summary>
        public static void Timing(FinalBossMoveId move, out float windup, out float active, out float recovery)
        {
            if (move == FinalBossMoveId.Melee)
            {
                windup = MeleeWindupSeconds;
                active = MeleeActiveSeconds;
                recovery = MeleeRecoverySeconds;
                return;
            }

            BossMoveId id;
            if (!TryMapRanged(move, out id))
            {
                windup = 0f;
                active = 0f;
                recovery = 0f;
                return;
            }

            BossBrain.TimingsFor(id, out windup, out active, out recovery);
        }

        /// <summary>
        /// 开新招那一帧的选择。≤ 触发距离走近战（正好等于也是近战）；
        /// 否则远程仍要 ≥ 3u 且完整身形和冠在视野内；否则本拍不开。
        /// </summary>
        public static FinalBossOpenChoice Choose(float distance, bool bodyAndCrownInView)
        {
            if (distance <= MeleeTriggerDistance)
                return FinalBossOpenChoice.Melee;
            if (distance >= RangedMinOpenDistance && bodyAndCrownInView)
                return FinalBossOpenChoice.Ranged;
            return FinalBossOpenChoice.Hold;
        }
    }
}
