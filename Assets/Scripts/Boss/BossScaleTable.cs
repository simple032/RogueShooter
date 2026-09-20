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
    /// W3-04 continuous BOSS scale: IDW over A–E control points (CSV 点验偏差≤0.02；锚点处精确命中).
    /// Also exposes Power = 0.45B+0.55RS for evidence / Bm·Tm 口径.
    /// Enter locks MaxHP; cross-seg only refreshes dmg mul.
    /// </summary>
    public static class BossScaleTable
    {
        public const float BaseHp = 3850f;
        public const float DiffRefAttr = 1.25f;
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

        /// <summary>Continuous muls via inverse-distance weighting over A–E (exact at anchors).</summary>
        public static void ContinuousMuls(int buildCount, int timeTier, out float hpMul, out float dmgMul)
        {
            BossScaleRow exact;
            if (TryExact(buildCount, timeTier, out exact))
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
                double dt = timeTier - a.TimeTierMax;
                double dist2 = db * db + dt * dt * 4.0; // tier weighted
                double w = 1.0 / (dist2 + 1e-6);
                wSum += w;
                hpSum += w * a.HpMul;
                dmgSum += w * a.DmgMul;
            }

            hpMul = (float)(hpSum / wSum);
            dmgMul = (float)(dmgSum / wSum);
        }

        public static BossScaleSnapshot Resolve(int buildCount, float wallMinutes, float tm, float bm)
        {
            return Resolve(buildCount, buildCount, wallMinutes, tm, bm);
        }

        public static BossScaleSnapshot Resolve(int buildCount, float rarityScore, float wallMinutes, float tm, float bm)
        {
            int tier = TimeTierFromMinutes(wallMinutes);
            float hpMul, dmgMul;
            ContinuousMuls(buildCount, tier, out hpMul, out dmgMul);
            float power = ComputePower(buildCount, rarityScore);

            string anchorId = "cont";
            BossScaleRow row;
            if (TryExact(buildCount, tier, out row))
                anchorId = row.Id;

            float attr = tm > 0.01f ? tm : DiffRefAttr;
            float attrScale = attr / DiffRefAttr;
            return new BossScaleSnapshot
            {
                AnchorId = anchorId,
                BuildCount = buildCount,
                RarityScore = rarityScore,
                Power = power,
                TimeTier = tier,
                HpMul = hpMul,
                DmgMul = dmgMul * attrScale,
                MaxHp = BaseHp * hpMul * attrScale,
                Bm = bm,
                Tm = tm,
                UsedContinuous = true
            };
        }

        public static float DmgMulFor(int buildCount, float wallMinutes)
        {
            int tier = TimeTierFromMinutes(wallMinutes);
            float hpMul, dmgMul;
            ContinuousMuls(buildCount, tier, out hpMul, out dmgMul);
            return dmgMul;
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
