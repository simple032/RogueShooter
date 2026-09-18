using System;
using System.Collections.Generic;
using RogueShooter.Balance;

namespace RogueShooter.Build
{
    public struct RewardOption
    {
        public string Id;
        public string Rarity;
        public string Tag;
        public int Score;
    }

    /// <summary>
    /// Same reward ID pool; chest vs altar use different rarity weight tables + tag bias.
    /// </summary>
    public static class RewardOffer
    {
        public static RewardOption[] RollUnique(BalanceLockData data, string source, Random rng)
        {
            int n = data != null && data.offerCount > 0 ? data.offerCount : 3;
            var result = new List<RewardOption>(n);
            if (data == null || data.rewardPool == null || data.rewardPool.Length == 0)
                return result.ToArray();
            if (rng == null)
                rng = new Random();

            RarityWeight[] table = SourceTable(data, source);
            var used = new HashSet<string>();
            for (int i = 0; i < n; i++)
            {
                string rarity = RollRarity(table, rng);
                RewardDef pick = PickFromPool(data, source, rarity, used, rng);
                if (pick == null)
                    pick = PickFromPool(data, source, null, used, rng);
                if (pick == null)
                    break;
                used.Add(pick.id);
                result.Add(new RewardOption
                {
                    Id = pick.id,
                    Rarity = pick.rarity,
                    Tag = pick.tag,
                    Score = data.ScoreForRarity(pick.rarity)
                });
            }

            return result.ToArray();
        }

        public static string RollRarity(RarityWeight[] table, Random rng)
        {
            if (table == null || table.Length == 0)
                return "C";
            float sum = 0f;
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] != null && table[i].weight > 0f)
                    sum += table[i].weight;
            }

            if (sum <= 0f)
                return table[0].rarity;

            float r = (float)rng.NextDouble() * sum;
            float acc = 0f;
            for (int i = 0; i < table.Length; i++)
            {
                RarityWeight w = table[i];
                if (w == null || w.weight <= 0f)
                    continue;
                acc += w.weight;
                if (r <= acc)
                    return w.rarity;
            }

            return table[table.Length - 1].rarity;
        }

        static RarityWeight[] SourceTable(BalanceLockData data, string source)
        {
            if (string.Equals(source, "altar", StringComparison.OrdinalIgnoreCase))
                return data.altarRarity;
            return data.chestRarity;
        }

        static RewardDef PickFromPool(BalanceLockData data, string source, string rarity, HashSet<string> used, Random rng)
        {
            float sum = 0f;
            for (int i = 0; i < data.rewardPool.Length; i++)
            {
                RewardDef d = data.rewardPool[i];
                if (!Eligible(d, rarity, used))
                    continue;
                sum += Math.Max(0.01f, data.TagBiasFor(source, d.tag));
            }

            if (sum <= 0f)
                return null;

            float r = (float)rng.NextDouble() * sum;
            float acc = 0f;
            for (int i = 0; i < data.rewardPool.Length; i++)
            {
                RewardDef d = data.rewardPool[i];
                if (!Eligible(d, rarity, used))
                    continue;
                acc += Math.Max(0.01f, data.TagBiasFor(source, d.tag));
                if (r <= acc)
                    return d;
            }

            return null;
        }

        static bool Eligible(RewardDef d, string rarity, HashSet<string> used)
        {
            if (d == null || string.IsNullOrEmpty(d.id))
                return false;
            if (used != null && used.Contains(d.id))
                return false;
            if (!string.IsNullOrEmpty(rarity) && d.rarity != rarity)
                return false;
            return true;
        }
    }
}
