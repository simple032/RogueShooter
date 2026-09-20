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
        public static readonly RewardRow[] All =
        {
            Row("R1L", "锋矢", "damage", 0.15f, "percent", "伤害+15%", RewardTier.Low, 20, 1),
            Row("R1M", "锋矢", "damage", 0.225f, "percent", "伤害+22.5%", RewardTier.Mid, 40, 2),
            Row("R1H", "锋矢", "damage", 0.3375f, "percent", "伤害+33.75%", RewardTier.High, 80, 4),
            Row("R2L", "疾张", "charge_time", -0.1304f, "percent", "满蓄缩短13.04%", RewardTier.Low, 20, 1),
            Row("R2M", "疾张", "charge_time", -0.1837f, "percent", "满蓄缩短18.37%", RewardTier.Mid, 40, 2),
            Row("R2H", "疾张", "charge_time", -0.2523f, "percent", "满蓄缩短25.23%", RewardTier.High, 80, 4),
            Row("R3", "鸿运", "crit_window", 0.06f, "seconds", "幸运窗+0.06s", RewardTier.High, 80, 4),
            Row("R4L", "骨甲", "max_hp", 0.15f, "percent", "最大生命+15%", RewardTier.Low, 20, 1),
            Row("R4M", "骨甲", "max_hp", 0.225f, "percent", "最大生命+22.5%", RewardTier.Mid, 40, 2),
            Row("R4H", "骨甲", "max_hp", 0.3375f, "percent", "最大生命+33.75%", RewardTier.High, 80, 4),
            Row("R5L", "残影", "move_speed", 0.15f, "percent", "移速+15%", RewardTier.Low, 20, 1),
            Row("R5M", "残影", "move_speed", 0.225f, "percent", "移速+22.5%", RewardTier.Mid, 40, 2),
            Row("R5H", "残影", "move_speed", 0.3375f, "percent", "移速+33.75%", RewardTier.High, 80, 4),
            Row("R6L", "盗墓者", "gold_gain", 0.15f, "percent", "掉金+15%", RewardTier.Low, 20, 1),
            Row("R6M", "盗墓者", "gold_gain", 0.225f, "percent", "掉金+22.5%", RewardTier.Mid, 40, 2),
            Row("R6H", "盗墓者", "gold_gain", 0.3375f, "percent", "掉金+33.75%", RewardTier.High, 80, 4),
            Row("R7M", "瞬击预感", "crit_rate", 0.20f, "percent_add", "暴击率+20%", RewardTier.Mid, 40, 2),
            Row("R7H", "瞬击预感", "crit_rate", 0.40f, "percent_add", "暴击率+40%", RewardTier.High, 80, 4),
            Row("R8H", "破甲猛击", "crit_damage", 0.60f, "percent", "暴击伤害+60%", RewardTier.High, 80, 4),
            Row("R9H", "隙矢追猎", "weak_damage", 0.50f, "percent", "弱点攻击伤害+50%", RewardTier.High, 80, 4),
            Row("R10", "止血", "heal", 0.40f, "percent_max_hp", "回复40%最大生命", RewardTier.Mid, 40, 2),
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

        public static int CountInTier(RewardTier tier)
        {
            int n = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Tier == tier)
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
                if (All[i].Tier != tier)
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
                if (All[i].Tier != tier)
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
            return "R1L/M/H damage +15%/+22.5%/+33.75%";
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
