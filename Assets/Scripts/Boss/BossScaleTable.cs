using System;

namespace RogueShooter.Boss
{
    public struct BossScaleRow
    {
        public string Id;
        public int BuildMin;
        public int BuildMax;
        public int TimeTierMax;
        public float HpMul;
        public float DmgMul;
        public string Expect;
    }

    public struct BossScaleSnapshot
    {
        public string AnchorId;
        public int BuildCount;
        public float RarityScore;
        public float Power;
        public int TimeTier;
        public float HpMul;
        public float DmgMul;
        public float MaxHp;
        public float Bm;
        public float Tm;
        public bool UsedContinuous;
    }

    /// <summary>
    /// Final-boss scale: IDW over A–E on build and continuous pressure coordinate c(p).
    /// Enter HP uses locked base 1200 (design package wrote 1100). Damage weights are divided by 1.28.
    /// Enter locks MaxHP; live pressure only refreshes the outgoing multiplier.
    /// </summary>
    public static class BossScaleTable
    {
        public const float BaseHp = FinalBossRules.BaseHp;
        public const float DiffRefAttr = 1.25f;
        public const float PressureRef = FinalBossRules.PressureRef;
        public const float PowerBuildCoef = 0.45f;
        public const float PowerRarityCoef = 0.55f;
        public const float AnchorTol = 0.02f;

        // B1b from balance_boss_scale_箭骸.csv
        public static readonly BossScaleRow[] Anchors =
        {
            new BossScaleRow { Id = "A", BuildMin = 14, BuildMax = 14, TimeTierMax = 2, HpMul = 1.00f, DmgMul = 1.00f, Expect = "基准_含店购中位" },
            new BossScaleRow { Id = "B", BuildMin = 18, BuildMax = 18, TimeTierMax = 2, HpMul = 0.88f, DmgMul = 0.85f, Expect = "高Build偏轻松" },
            new BossScaleRow { Id = "C", BuildMin = 11, BuildMax = 11, TimeTierMax = 2, HpMul = 1.08f, DmgMul = 1.10f, Expect = "偏低Build" },
            new BossScaleRow { Id = "D", BuildMin = 8, BuildMax = 8, TimeTierMax = 1, HpMul = 1.15f, DmgMul = 1.18f, Expect = "早到_低Build" },
            new BossScaleRow { Id = "E", BuildMin = 14, BuildMax = 14, TimeTierMax = 3, HpMul = 1.00f, DmgMul = 1.28f, Expect = "晚到同Build更疼" },
        };

        public static int TimeTierFromMinutes(float wallMinutes)
        {
            if (wallMinutes < 4f) return 1;
            if (wallMinutes < 10f) return 2;
            return 3;
        }

        public static float ComputePower(int buildCount, float rarityScore)
        {
            return PowerBuildCoef * buildCount + PowerRarityCoef * rarityScore;
        }

        public static bool TryExact(int buildCount, int timeTier, out BossScaleRow row)
        {
            for (int i = 0; i < Anchors.Length; i++)
            {
                var a = Anchors[i];
                if (buildCount >= a.BuildMin && buildCount <= a.BuildMax && timeTier == a.TimeTierMax)
                {
                    row = a;
                    return true;
                }
            }

            row = default(BossScaleRow);
            return false;
        }

        /// <summary>Discrete tier helper. Time-tier integers match the anchor c values 1/2/3.</summary>
        public static void ContinuousMuls(int buildCount, int timeTier, out float hpMul, out float dmgMul)
        {
            ContinuousMuls(buildCount, (float)timeTier, out hpMul, out dmgMul);
        }

        /// <summary>
        /// IDW over A–E. Distance² = (B-anchorB)² + 4(c-anchorC)², weight = 1/(distance²+0.000001).
        /// Exact anchors return the anchor pair.
        /// </summary>
        public static void ContinuousMuls(int buildCount, float coordinate, out float hpMul, out float dmgMul)
        {
            BossScaleRow exact;
            if (TryExactCoordinate(buildCount, coordinate, out exact))
            {
                hpMul = exact.HpMul;
                dmgMul = exact.DmgMul;
                return;
            }

            double wSum = 0;
            double hpSum = 0;
            double dmgSum = 0;
            for (int i = 0; i < Anchors.Length; i++)
            {
                var a = Anchors[i];
                double db = buildCount - a.BuildMin;
                double dc = coordinate - a.TimeTierMax;
                double dist2 = db * db + 4.0 * dc * dc;
                double w = 1.0 / (dist2 + 0.000001);
                wSum += w;
                hpSum += w * a.HpMul;
                dmgSum += w * a.DmgMul;
            }

            hpMul = (float)(hpSum / wSum);
            dmgMul = (float)(dmgSum / wSum);
        }

        public static bool TryExactCoordinate(int buildCount, float coordinate, out BossScaleRow row)
        {
            for (int i = 0; i < Anchors.Length; i++)
            {
                var a = Anchors[i];
                if (buildCount >= a.BuildMin && buildCount <= a.BuildMax
                    && Math.Abs(coordinate - a.TimeTierMax) <= 0.0005f)
                {
                    row = a;
                    return true;
                }
            }

            row = default(BossScaleRow);
            return false;
        }

        public static BossScaleSnapshot Resolve(int buildCount, float wallMinutes, float tm, float bm)
        {
            return Resolve(buildCount, buildCount, wallMinutes, tm, bm);
        }

        public static BossScaleSnapshot Resolve(int buildCount, float rarityScore, float wallMinutes, float tm, float bm)
        {
            int tier = TimeTierFromMinutes(wallMinutes);
            float pressure = tm > 0.01f ? tm : FinalBossRules.PressureRef;
            float coordinate = FinalBossRules.PressureCoordinate(pressure);
            float hpMul, dmgMul;
            ContinuousMuls(buildCount, coordinate, out hpMul, out dmgMul);
            float power = ComputePower(buildCount, rarityScore);

            string anchorId = "cont";
            BossScaleRow row;
            if (TryExactCoordinate(buildCount, coordinate, out row))
                anchorId = row.Id;

            return new BossScaleSnapshot
            {
                AnchorId = anchorId,
                BuildCount = buildCount,
                RarityScore = rarityScore,
                Power = power,
                TimeTier = tier,
                HpMul = hpMul,
                DmgMul = FinalBossRules.OutgoingMultiplier(buildCount, pressure),
                MaxHp = FinalBossRules.EnterMaxHp(buildCount, pressure),
                Bm = bm,
                Tm = pressure,
                UsedContinuous = true
            };
        }

        public static float DmgMulFor(int buildCount, float pressure)
        {
            return FinalBossRules.OutgoingMultiplier(buildCount, pressure);
        }

        public static float DmgMulFor(int buildCount, float rarityScore, float wallMinutes)
        {
            return DmgMulFor(buildCount, wallMinutes);
        }

        /// <summary>Returns null if every A–E anchor is within AnchorTol of continuous.</summary>
        public static string VerifyAnchorsAe()
        {
            for (int i = 0; i < Anchors.Length; i++)
            {
                var a = Anchors[i];
                float hp, dmg;
                ContinuousMuls(a.BuildMin, a.TimeTierMax, out hp, out dmg);
                if (Math.Abs(hp - a.HpMul) > AnchorTol || Math.Abs(dmg - a.DmgMul) > AnchorTol)
                    return "anchor " + a.Id + " Δhp=" + Math.Abs(hp - a.HpMul).ToString("0.000")
                           + " Δdmg=" + Math.Abs(dmg - a.DmgMul).ToString("0.000");
            }

            return null;
        }
    }
}
