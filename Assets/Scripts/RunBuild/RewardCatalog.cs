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
        public static readonly string[] HighPoolIds =
            { "R3", "R7H", "R8H", "R9H", "R12H", "R13H", "R14H" };

        public static readonly string[] DeletedHighIds = { "R1H", "R2H", "R4H", "R5H", "R6H" };

        static IList<string> _poolOwned;
        public static IList<string> PoolOwned => _poolOwned;
        static RogueShooter.Spawning.StageId _poolStage=RogueShooter.Spawning.StageId.S1;
        public static bool IsUniqueId(string id)=>id=="R16M"||id=="R17M"||id=="R18H";
        public static void BindPoolContext(RogueShooter.Spawning.StageId stage,IList<string> owned){_poolStage=stage;_poolOwned=owned;}

        public static void BindPoolOwned(IList<string> owned)
        {
            _poolOwned = owned;
            _poolStage=RogueShooter.Spawning.StageId.S1;
        }

        public static readonly RewardRow[] All =
        {
            Row("R1L", "锋矢", "damage", 0.10f, "percent", "伤害+10%", RewardTier.Low, 40, 1),
            Row("R1M", "锋矢", "damage", 0.20f, "percent", "伤害+20%", RewardTier.Mid, 80, 2),
            Row("R2L", "疾张", "charge_time", -0.0909f, "percent", "满蓄缩短9.09%", RewardTier.Low, 40, 1),
            Row("R2M", "疾张", "charge_time", -0.1667f, "percent", "满蓄缩短16.67%", RewardTier.Mid, 80, 2),
            Row("R3", "鸿运", "crit_window", 0.06f, "seconds", "弱点窗额外+0.09s", RewardTier.High, 120, 3),
            Row("R4L", "骨甲", "max_hp", 0.10f, "percent", "最大生命+10%", RewardTier.Low, 40, 1),
            Row("R4M", "骨甲", "max_hp", 0.20f, "percent", "最大生命+20%", RewardTier.Mid, 80, 2),
            Row("R5L", "残影", "move_speed", 0.10f, "percent", "移速+10%", RewardTier.Low, 40, 1),
            Row("R5M", "残影", "move_speed", 0.20f, "percent", "移速+20%", RewardTier.Mid, 80, 2),
            Row("R6L", "盗墓者", "gold_gain", 0.10f, "percent", "掉金+10%", RewardTier.Low, 40, 1),
            Row("R6M", "盗墓者", "gold_gain", 0.20f, "percent", "掉金+20%", RewardTier.Mid, 80, 2),
            Row("R7M", "瞬击预感", "crit_rate", 0.26f, "percent_add", "暴击率+26%", RewardTier.Mid, 80, 2),
            Row("R7H", "瞬击预感", "crit_rate", 0.39f, "percent_add", "暴击率+39%", RewardTier.High, 120, 3),
            Row("R8H", "破甲猛击", "crit_damage", 0.65f, "percent", "暴击伤害+65%", RewardTier.High, 120, 3),
            Row("R9H", "隙矢追猎", "weak_damage", 0.65f, "percent", "弱点攻击伤害+65%", RewardTier.High, 120, 3),
            Row("R10", "止血", "heal", 0.40f, "percent_max_hp", "回复40%最大生命", RewardTier.Mid, 60, 2),
            Row("R11L", "满血额外伤害", "dmg_vs_fullhp", 0.20f, "percent", "满血目标伤害+20%", RewardTier.Low, 40, 1),
            Row("R11M", "满血额外伤害", "dmg_vs_fullhp", 0.40f, "percent", "满血目标伤害+40%", RewardTier.Mid, 80, 2),
            Row("R12M", "进房间5s内加攻", "atk_enter_room_5s", 0.30f, "percent", "进房5s内攻击+30%", RewardTier.Mid, 80, 2),
            Row("R12H", "进房间5s内加攻", "atk_enter_room_5s", 0.45f, "percent", "进房5s内攻击+45%", RewardTier.High, 120, 3),
            Row("R13H", "攻击吸血", "lifesteal", 0.15f, "percent", "攻击吸血15%", RewardTier.High, 120, 3),
            Row("R14H", "穿透后排", "pierce_back", 0.40f, "percent_add", "穿透后排伤害+40%", RewardTier.High, 120, 3),
            Row("R15_C", "震矢", "kb_dist_pct", 0.20f, "percent", "满蓄击退距离+20%", RewardTier.Low, 40, 1),
            Row("R15_R", "震矢", "kb_dist_pct", 0.40f, "percent", "满蓄击退距离+40%", RewardTier.Mid, 80, 2),
            Row("R16M", "回身箭", "return_arrow", .4f, "conditional", "翻滚后首箭更强", RewardTier.Mid,80,2),
            Row("R17M", "守势蓄发", "guarded_charge", .2f, "conditional", "避伤后满蓄更强", RewardTier.Mid,80,2),
            Row("R18H", "凝神余响", "focus_echo", 3f, "seconds_cap", "弱点箭延续凝神", RewardTier.High,120,3),
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

        /// <summary>Exclude every rarity ID sharing a visible name within one offer.</summary>
        public static void ExcludeFamily(System.Collections.Generic.HashSet<string> used, string id)
        {
            if (used == null) return;
            string family = FamilyKey(id);
            foreach (var row in All) if (row.Name == family) used.Add(row.Id);
            used.Add(id);
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
            if(IsUniqueId(row.Id))
            {
                int unlock=row.Id=="R18H"?3:2;
                return (int)_poolStage>=unlock&&(_poolOwned==null||!_poolOwned.Contains(row.Id));
            }
            if (tier == RewardTier.High && !IsHighPoolId(row.Id))
                return false;
            if (RewardStatHooks.IsPoolExcluded(row, _poolOwned))
                return false;
            return true;
        }

        public static bool CanOffer(RewardRow row, RewardTier tier)
        {
            return EligibleForPool(row, tier);
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

    /// <summary>v0.57: chosen family has 10% weight until three other confirmed choices; viewing does not advance.</summary>
    public sealed class RewardRegressionWeights
    {
        readonly Dictionary<string, int> _missStreak = new Dictionary<string, int>();

        public const int SuppressedWeight = 10;
        public const int RecoveryChoices = 3;
        public int WeightFor(RewardRow row)
        {
            return _missStreak.TryGetValue(row.Name, out int choices) && choices < RecoveryChoices
                ? SuppressedWeight : 100;
        }

        public void NotifyChosen(string rewardId)
        {
            if (!RewardCatalog.TryGet(rewardId, out RewardRow row))
                return;
            var tracked = new List<string>(_missStreak.Keys);
            foreach (string key in tracked)
                _missStreak[key] = Math.Min(RecoveryChoices, _missStreak[key] + 1);
            _missStreak[row.Name] = 0;
        }

        /// <summary>Only confirmed selections advance the window, including families not shown.</summary>
        public void NotifyOfferResolved(IList<string> offeredIds, string chosenId)
        {
            if (offeredIds != null && !offeredIds.Contains(chosenId)) return;
            NotifyChosen(chosenId);
        }

        public void Reset()
        {
            _missStreak.Clear();
        }
    }
}
