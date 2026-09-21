using System;
using System.Collections.Generic;

namespace RogueShooter.Build
{
    public struct RewardRow
    {
        public string Id;
        public string Name;
        public string Stat;
        public float Value;
        public string ValueType;
        public string Desc;
        public RewardTier Tier;
        public int ShopPrice;
        public int BuildEquiv;
    }

    /// <summary>
    /// crit2 / balance_rewards_箭骸.csv mirror. Stack multiplicative on same stat.
    /// </summary>
    public static class RewardCatalog
    {
        /// <summary>Producer lock: high draws only these ids. Deleted R1H/R2H/R4H/R5H/R6H.</summary>
        public static readonly string[] HighPoolIds = { "R3", "R7H", "R8H", "R9H" };

        public static readonly string[] DeletedHighIds = { "R1H", "R2H", "R4H", "R5H", "R6H" };

        public static readonly RewardRow[] All =
        {
            Row("R1L", "锋矢", "damage", 0.10f, "percent", "伤害+10%", RewardTier.Low, 20, 1),
            Row("R1M", "锋矢", "damage", 0.20f, "percent", "伤害+20%", RewardTier.Mid, 40, 2),
            Row("R2L", "疾张", "charge_time", -0.0909f, "percent", "满蓄缩短9.09%", RewardTier.Low, 20, 1),
            Row("R2M", "疾张", "charge_time", -0.1667f, "percent", "满蓄缩短16.67%", RewardTier.Mid, 40, 2),
            Row("R3", "鸿运", "crit_window", 0.06f, "seconds", "幸运窗+0.06s", RewardTier.High, 60, 3),
            Row("R4L", "骨甲", "max_hp", 0.10f, "percent", "最大生命+10%", RewardTier.Low, 20, 1),
            Row("R4M", "骨甲", "max_hp", 0.20f, "percent", "最大生命+20%", RewardTier.Mid, 40, 2),
            Row("R5L", "残影", "move_speed", 0.10f, "percent", "移速+10%", RewardTier.Low, 20, 1),
            Row("R5M", "残影", "move_speed", 0.20f, "percent", "移速+20%", RewardTier.Mid, 40, 2),
            Row("R6L", "盗墓者", "gold_gain", 0.10f, "percent", "掉金+10%", RewardTier.Low, 20, 1),
            Row("R6M", "盗墓者", "gold_gain", 0.20f, "percent", "掉金+20%", RewardTier.Mid, 40, 2),
            Row("R7M", "瞬击预感", "crit_rate", 0.26f, "percent_add", "暴击率+26%", RewardTier.Mid, 40, 2),
            Row("R7H", "瞬击预感", "crit_rate", 0.39f, "percent_add", "暴击率+39%", RewardTier.High, 60, 3),
            Row("R8H", "破甲猛击", "crit_damage", 0.65f, "percent", "暴击伤害+65%", RewardTier.High, 60, 3),
            Row("R9H", "隙矢追猎", "weak_damage", 0.65f, "percent", "弱点攻击伤害+65%", RewardTier.High, 60, 3),
            Row("R10", "止血", "heal", 0.40f, "percent_max_hp", "回复40%最大生命", RewardTier.Mid, 40, 2),
            Row("R15_C", "震矢", "kb_dist_pct", 0.20f, "percent", "满蓄击退距离+20%", RewardTier.Low, 20, 1),
            Row("R15_R", "震矢", "kb_dist_pct", 0.40f, "percent", "满蓄击退距离+40%", RewardTier.Mid, 40, 2),
        };

        static RewardRow Row(string id, string name, string stat, float value, string valueType, string desc, RewardTier tier, int price, int equiv)
        {
            return new RewardRow
            {
                Id = id,
                Name = name,
                Stat = stat,
                Value = value,
                ValueType = valueType,
                Desc = desc,
                Tier = tier,
                ShopPrice = price,
                BuildEquiv = equiv
            };
        }

        public static string FamilyKey(string idOrName)
        {
            if (string.IsNullOrEmpty(idOrName))
                return "";
            if (TryGet(idOrName, out RewardRow row))
                return row.Name;
            return idOrName;
        }

        public static RewardTier ParseTier(string rarity)
        {
            if (string.IsNullOrEmpty(rarity))
                return RewardTier.Low;
            switch (rarity.Trim().ToLowerInvariant())
            {
                case "mid":
                case "中":
                    return RewardTier.Mid;
                case "high":
                case "高":
                    return RewardTier.High;
                default:
                    return RewardTier.Low;
            }
        }

        public static bool IsHighPoolId(string id)
        {
            if (string.IsNullOrEmpty(id) || HighPoolIds == null)
                return false;
            for (int i = 0; i < HighPoolIds.Length; i++)
            {
                if (HighPoolIds[i] == id)
                    return true;
            }

            return false;
        }

        public static bool IsDeletedHighId(string id)
        {
            if (string.IsNullOrEmpty(id) || DeletedHighIds == null)
                return false;
            for (int i = 0; i < DeletedHighIds.Length; i++)
            {
                if (DeletedHighIds[i] == id)
                    return true;
            }

            return false;
        }

        static bool EligibleForPool(RewardRow row, RewardTier tier)
        {
            if (row.Tier != tier)
                return false;
            if (IsDeletedHighId(row.Id))
                return false;
            if (tier == RewardTier.High && !IsHighPoolId(row.Id))
                return false;
            return true;
        }

        public static int CountInTier(RewardTier tier)
        {
            int n = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (EligibleForPool(All[i], tier))
                    n++;
            }

            return n;
        }

        public static bool TryGet(string id, out RewardRow row)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    row = All[i];
                    return true;
                }
            }

            row = default(RewardRow);
            return false;
        }

        /// <summary>Equal-weight pick within tier; skip used ids. Optional regression weights by family.</summary>
        public static bool TryPickEqual(RewardTier tier, HashSet<string> usedIds, Random rng, out RewardRow row)
        {
            return TryPickWeighted(tier, usedIds, null, rng, out row);
        }

        public static bool TryPickWeighted(
            RewardTier tier,
            HashSet<string> usedIds,
            RewardRegressionWeights weights,
            Random rng,
            out RewardRow row)
        {
            row = default(RewardRow);
            if (rng == null)
                return false;

            int total = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (!EligibleForPool(All[i], tier))
                    continue;
                if (usedIds != null && usedIds.Contains(All[i].Id))
                    continue;
                int w = weights != null ? weights.WeightFor(All[i]) : 100;
                if (w > 0)
                    total += w;
            }

            if (total <= 0)
                return false;

            int roll = rng.Next(total);
            int acc = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (!EligibleForPool(All[i], tier))
                    continue;
                if (usedIds != null && usedIds.Contains(All[i].Id))
                    continue;
                int w = weights != null ? weights.WeightFor(All[i]) : 100;
                if (w <= 0)
                    continue;
                acc += w;
                if (roll < acc)
                {
                    row = All[i];
                    return true;
                }
            }

            return false;
        }

        public static string FormatAttackTiers()
        {
            return "R1L/M damage +10%/+20% (no R1H)";
        }

        public static string FormatSpecialTiers()
        {
            RewardRow r3, r10;
            TryGet("R3", out r3);
            TryGet("R10", out r10);
            return "R3=" + AltarRewardRoll.TierLabel(r3.Tier) + " R10=" + AltarRewardRoll.TierLabel(r10.Tier);
        }
    }

    /// <summary>crit2 §2.8 unpicked regression: after pick weight=1; miss1→33; miss2→67; else 100.</summary>
    public sealed class RewardRegressionWeights
    {
        readonly Dictionary<string, int> _missStreak = new Dictionary<string, int>();

        public int WeightFor(RewardRow row)
        {
            if (string.Equals(row.Stat, "heal", StringComparison.OrdinalIgnoreCase))
                return 100; // HP-scaled heal handled by caller; default full
            string key = row.Name;
            int streak;
            if (!_missStreak.TryGetValue(key, out streak))
                return 100;
            if (streak <= 0)
                return 1;
            if (streak == 1)
                return 33;
            if (streak == 2)
                return 67;
            return 100;
        }

        public void NotifyChosen(string rewardId)
        {
            if (!RewardCatalog.TryGet(rewardId, out RewardRow row))
                return;
            _missStreak[row.Name] = 0;
        }

        /// <summary>Call after player picks one of the offered three.</summary>
        public void NotifyOfferResolved(IList<string> offeredIds, string chosenId)
        {
            NotifyChosen(chosenId);
            if (offeredIds == null)
                return;
            string chosenFamily = RewardCatalog.TryGet(chosenId, out RewardRow c) ? c.Name : "";
            for (int i = 0; i < offeredIds.Count; i++)
            {
                string id = offeredIds[i];
                if (string.IsNullOrEmpty(id) || id == chosenId)
                    continue;
                if (!RewardCatalog.TryGet(id, out RewardRow row))
                    continue;
                if (row.Name == chosenFamily)
                    continue;
                int streak;
                if (!_missStreak.TryGetValue(row.Name, out streak))
                    streak = 0;
                else if (streak == 0)
                    streak = 0; // was recently chosen → first miss becomes 1 below
                _missStreak[row.Name] = streak + 1;
            }
        }

        public void Reset()
        {
            _missStreak.Clear();
        }
    }
}
