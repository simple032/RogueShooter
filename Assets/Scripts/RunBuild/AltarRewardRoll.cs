using System;
using System.Collections.Generic;

namespace RogueShooter.Build
{
    public enum AltarSize
    {
        None,
        Small,
        Mid,
        Large
    }

    public enum RewardTier
    {
        Low,
        Mid,
        High
    }

    public struct AltarPick
    {
        public string Id;
        public string Effect;
        public RewardTier Tier;
    }

    /// <summary>
    /// GDD §6.6 weights unchanged. N16: roll tier → equal pick from RewardCatalog tier pool.
    /// Small 72/28/0, Mid 53/32/15, Large 16/54/30.
    /// </summary>
    public static class AltarRewardRoll
    {
        public static string SizeLabel(AltarSize size)
        {
            switch (size)
            {
                case AltarSize.Small: return "小";
                case AltarSize.Mid: return "中";
                case AltarSize.Large: return "大";
                default: return "";
            }
        }

        public static string TierLabel(RewardTier tier)
        {
            switch (tier)
            {
                case RewardTier.Low: return "低";
                case RewardTier.Mid: return "中";
                case RewardTier.High: return "高";
                default: return "";
            }
        }

        public static int BuildDelta(AltarSize size)
        {
            switch (size)
            {
                case AltarSize.Small: return 1;
                case AltarSize.Mid: return 2;
                case AltarSize.Large: return 3;
                default: return 0;
            }
        }

        public static void GetWeights(AltarSize size, out int low, out int mid, out int high)
        {
            switch (size)
            {
                case AltarSize.Small:
                    low = 72;
                    mid = 28;
                    high = 0;
                    return;
                case AltarSize.Mid:
                    low = 53;
                    mid = 32;
                    high = 15;
                    return;
                case AltarSize.Large:
                    low = 16;
                    mid = 54;
                    high = 30;
                    return;
                default:
                    low = mid = high = 0;
                    return;
            }
        }

        /// <summary>Chest §6.6: small 67/28/5, large 53/32/15.</summary>
        public static void GetChestWeights(bool large, out int low, out int mid, out int high)
        {
            if (large)
            {
                low = 53;
                mid = 32;
                high = 15;
            }
            else
            {
                low = 67;
                mid = 28;
                high = 5;
            }
        }

        public static AltarSize SizeForHookId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return AltarSize.None;
            switch (id)
            {
                case "A_Shared":
                    return AltarSize.Small;
                case "A1":
                case "A2":
                case "A4":
                    return AltarSize.Mid;
                case "A3":
                case "A5":
                case "A6":
                    return AltarSize.Large;
                default:
                    return AltarSize.None;
            }
        }

        public static AltarPick[] RollThree(AltarSize size, Random rng)
        {
            if (size == AltarSize.None)
                return Array.Empty<AltarPick>();
            if (rng == null)
                rng = new Random();

            var used = new HashSet<string>();
            var list = new List<AltarPick>(3);
            for (int i = 0; i < 3; i++)
            {
                RewardTier tier = RollTier(size, rng);
                if (!TryPick(tier, used, rng, out AltarPick pick))
                    break;
                used.Add(pick.Id);
                list.Add(pick);
            }

            list.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            return list.ToArray();
        }

        public static AltarPick[] RollThreeChest(bool large, Random rng)
        {
            if (rng == null)
                rng = new Random();
            var used = new HashSet<string>();
            var list = new List<AltarPick>(3);
            for (int i = 0; i < 3; i++)
            {
                RewardTier tier = RollChestTier(large, rng);
                if (!TryPick(tier, used, rng, out AltarPick pick))
                    break;
                used.Add(pick.Id);
                list.Add(pick);
            }

            list.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            return list.ToArray();
        }

        public static RewardTier RollTier(AltarSize size, Random rng)
        {
            GetWeights(size, out int low, out int mid, out int high);
            return RollTierWeighted(low, mid, high, rng);
        }

        public static RewardTier RollChestTier(bool large, Random rng)
        {
            GetChestWeights(large, out int low, out int mid, out int high);
            return RollTierWeighted(low, mid, high, rng);
        }

        static RewardTier RollTierWeighted(int low, int mid, int high, Random rng)
        {
            int total = low + mid + high;
            if (total <= 0 || rng == null)
                return RewardTier.Low;
            int roll = rng.Next(total);
            if (roll < low)
                return RewardTier.Low;
            if (roll < low + mid)
                return RewardTier.Mid;
            return RewardTier.High;
        }

        static bool TryPick(RewardTier tier, HashSet<string> used, Random rng, out AltarPick pick)
        {
            pick = default;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (!RewardCatalog.TryPickWeighted(tier, used, null, rng, out RewardRow row))
                    return false;
                if (used != null && used.Contains(row.Id))
                    continue;
                pick = new AltarPick { Id = row.Id, Effect = row.Desc, Tier = row.Tier };
                return true;
            }

            if (!RewardCatalog.TryPickWeighted(tier, used, null, rng, out RewardRow fallback))
                return false;
            pick = new AltarPick { Id = fallback.Id, Effect = fallback.Desc, Tier = fallback.Tier };
            return true;
        }
    }
}
