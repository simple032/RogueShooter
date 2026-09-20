using System;
using System.Collections.Generic;

namespace RogueShooter.Build
{
    /// <summary>
    /// crit2 expected DPS from owned rewards (not ×1.15^B).
    /// DPS = baseAtk × ∏(1+damage) × weakFactor × critFactor / chargeTime
    /// </summary>
    public static class Crit2Dps
    {
        public const float BaseAtk = 100f;
        public const float BaseChargeSeconds = 1f;
        public const float AssumedWeakRate = 0.30f;
        public const float AssumedCritRate = 0.30f; // DPS-align assume; gameplay start crit=0
        public const float DefaultWeakMul = 2f;
        public const float DefaultCritMul = 2f;

        public struct Snapshot
        {
            public float DamageProduct;
            public float ChargeSeconds;
            public float CritRate;
            public float CritMul;
            public float WeakMul;
            public float WindowBonus;
            public float Dps;
            public int OwnedCount;
        }

        public static Snapshot FromIds(IList<string> ownedIds, bool useAssumedCritRate)
        {
            float dmgProd = 1f;
            float charge = BaseChargeSeconds;
            float critRate = useAssumedCritRate ? AssumedCritRate : 0f;
            float critMul = DefaultCritMul;
            float weakMul = DefaultWeakMul;
            float window = 0f;
            int n = 0;
            if (ownedIds != null)
            {
                for (int i = 0; i < ownedIds.Count; i++)
                {
                    if (!RewardCatalog.TryGet(ownedIds[i], out RewardRow row))
                        continue;
                    n++;
                    switch (row.Stat)
                    {
                        case "damage":
                            dmgProd *= (1f + row.Value);
                            break;
                        case "charge_time":
                            charge *= (1f + row.Value);
                            break;
                        case "crit_rate":
                            critRate += row.Value;
                            break;
                        case "crit_damage":
                            critMul = DefaultCritMul * (1f + row.Value);
                            break;
                        case "weak_damage":
                            weakMul = DefaultWeakMul * (1f + row.Value);
                            break;
                        case "crit_window":
                            window += row.Value;
                            break;
                    }
                }
            }

            if (critRate > 1f)
                critRate = 1f;
            if (charge < 0.05f)
                charge = 0.05f;

            float weakFactor = AssumedWeakRate * weakMul + (1f - AssumedWeakRate) * 1f;
            float critFactor = critRate * critMul + (1f - critRate) * 1f;
            float dps = BaseAtk * dmgProd * weakFactor * critFactor / charge;

            return new Snapshot
            {
                DamageProduct = dmgProd,
                ChargeSeconds = charge,
                CritRate = critRate,
                CritMul = critMul,
                WeakMul = weakMul,
                WindowBonus = window,
                Dps = dps,
                OwnedCount = n
            };
        }

        public static Snapshot FromState(RunBuildState state, bool useAssumedCritRate)
        {
            return FromIds(state != null ? state.OwnedRewardIds : null, useAssumedCritRate);
        }

        /// <summary>Baseline with no rewards, assumed rates → ~169 DPS₀.</summary>
        public static float Dps0()
        {
            return FromIds(null, true).Dps;
        }
    }
}
