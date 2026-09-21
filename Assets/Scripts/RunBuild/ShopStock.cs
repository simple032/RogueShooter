using System;
using System.Collections.Generic;
using System.Text;

namespace RogueShooter.Build
{
    public enum ShopSlotRole
    {
        Low,
        Mid,
        High,
        Heal,
        Elastic
    }

    public struct ShopShelf
    {
        public string Id;
        public RewardTier Tier;
        public ShopSlotRole Role;
        public ShopSlotRole ContentRole;
        public int Price;
        public int PriceBase;
        public int LowAnchor;
        public bool Sold;
        public string Effect;
        public bool IsHeal;
        public bool IsElastic;
    }

    /// <summary>
    /// N38 shop: 6 shelves = 普/中/高/回血各1 + 弹性2. Never refresh.
    /// Locked mid prices 20/40/60/40 (ratio 1:2:3; high band 54–66). Elastic 56/32/12, no heal.
    /// </summary>
    public static class ShopStock
    {
        public const int ShelfCount = 6;
        public const int FixedCount = 4;
        public const int ElasticCount = 2;
        public const int PriceBaseLow = 20;
        public const int PriceBaseMid = 40;
        public const int PriceBaseHigh = 60;
        public const int PriceBaseHeal = 40;
        public const int LowPriceMin = 18;
        public const int LowPriceMax = 22;
        public const int MidPriceMin = 36;
        public const int MidPriceMax = 44;
        public const int HighPriceMin = 54;
        public const int HighPriceMax = 66;
        public const int ElasticWLow = 56;
        public const int ElasticWMid = 32;
        public const int ElasticWHigh = 12;
        public const int ElasticWHeal = 0;

        /// <summary>每次成功店购固定 +1 Build（制作人 2026-09-20 裁定，不用分层当量）。</summary>
        public static int BuildEquivFor(ShopSlotRole role)
        {
            _ = role;
            return 1;
        }

        public static int PriceBaseFor(ShopSlotRole role)
        {
            switch (role)
            {
                case ShopSlotRole.Mid: return PriceBaseMid;
                case ShopSlotRole.High: return PriceBaseHigh;
                case ShopSlotRole.Heal: return PriceBaseHeal;
                default: return PriceBaseLow;
            }
        }

        public static int PriceFor(RewardTier tier)
        {
            switch (tier)
            {
                case RewardTier.Mid: return PriceBaseMid;
                case RewardTier.High: return PriceBaseHigh;
                default: return PriceBaseLow;
            }
        }

        public static int RollLowAnchor(Random rng)
        {
            return PriceBaseLow;
        }

        public static int PriceFromLow(ShopSlotRole role, int lowAnchor)
        {
            int low = lowAnchor > 0 ? lowAnchor : PriceBaseLow;
            switch (role)
            {
                case ShopSlotRole.Mid:
                case ShopSlotRole.Heal:
                    return low * 2;
                case ShopSlotRole.High:
                    return low * 3;
                default:
                    return low;
            }
        }

        public static bool PriceMatchesRatio(int price, ShopSlotRole role, int lowAnchor)
        {
            return price == PriceFromLow(role, lowAnchor);
        }

        public static ShopShelf[] RollFive(Random rng)
        {
            return RollShelves(rng);
        }

        public static ShopShelf[] RollShelves(Random rng)
        {
            if (rng == null)
                rng = new Random();
            int low = RollLowAnchor(rng);
            var shelves = new ShopShelf[ShelfCount];
            var used = new HashSet<string>();
            shelves[0] = FillSlot(ShopSlotRole.Low, false, low, used, rng);
            shelves[1] = FillSlot(ShopSlotRole.Mid, false, low, used, rng);
            shelves[2] = FillSlot(ShopSlotRole.High, false, low, used, rng);
            shelves[3] = FillSlot(ShopSlotRole.Heal, false, low, used, rng);
            shelves[4] = FillSlot(RollElasticRole(rng), true, low, used, rng);
            shelves[5] = FillSlot(RollElasticRole(rng), true, low, used, rng);
            return shelves;
        }

        public static ShopSlotRole RollElasticRole(Random rng)
        {
            if (rng == null)
                rng = new Random();
            int roll = rng.Next(100);
            if (roll < ElasticWLow)
                return ShopSlotRole.Low;
            if (roll < ElasticWLow + ElasticWMid)
                return ShopSlotRole.Mid;
            return ShopSlotRole.High;
        }

        public static string FormatShelves(ShopShelf[] shelves)
        {
            if (shelves == null || shelves.Length == 0)
                return "";
            var sb = new StringBuilder();
            for (int i = 0; i < shelves.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                var s = shelves[i];
                sb.Append(s.Id)
                    .Append(' ')
                    .Append(s.IsElastic ? "弹:" : "")
                    .Append(RoleLabel(s.ContentRole))
                    .Append(' ')
                    .Append(s.Price)
                    .Append("g(L=")
                    .Append(s.LowAnchor)
                    .Append(") ")
                    .Append(s.Effect);
            }

            return sb.ToString();
        }

        public static string RoleLabel(ShopSlotRole role)
        {
            switch (role)
            {
                case ShopSlotRole.Mid: return "中";
                case ShopSlotRole.High: return "高";
                case ShopSlotRole.Heal: return "回血";
                case ShopSlotRole.Elastic: return "弹";
                default: return "普";
            }
        }

        static ShopShelf FillSlot(ShopSlotRole role, bool elastic, int lowAnchor, HashSet<string> used, Random rng)
        {
            RewardRow row;
            if (role == ShopSlotRole.Heal)
            {
                if (!TryPickHeal(used, rng, out row))
                    RewardCatalog.TryPickEqual(RewardTier.Mid, used, rng, out row);
            }
            else
            {
                RewardTier tier = role == ShopSlotRole.High ? RewardTier.High
                    : role == ShopSlotRole.Mid ? RewardTier.Mid
                    : RewardTier.Low;
                if (!RewardCatalog.TryPickEqual(tier, used, rng, out row))
                {
                    for (int r = 0; r < RewardCatalog.All.Length; r++)
                    {
                        RewardRow cand = RewardCatalog.All[r];
                        if (used.Contains(cand.Id))
                            continue;
                        if (tier == RewardTier.High && !RewardCatalog.IsHighPoolId(cand.Id))
                            continue;
                        if (RewardCatalog.IsDeletedHighId(cand.Id))
                            continue;
                        if (cand.Tier != tier)
                            continue;
                        row = cand;
                        break;
                    }
                }
            }

            used.Add(row.Id);
            return new ShopShelf
            {
                Id = row.Id,
                Tier = row.Tier,
                Role = elastic ? ShopSlotRole.Elastic : role,
                ContentRole = role,
                Price = PriceFromLow(role, lowAnchor),
                PriceBase = PriceBaseFor(role),
                LowAnchor = lowAnchor,
                Sold = false,
                Effect = row.Desc,
                IsHeal = role == ShopSlotRole.Heal || string.Equals(row.Stat, "heal", StringComparison.OrdinalIgnoreCase),
                IsElastic = elastic
            };
        }

        static bool TryPickHeal(HashSet<string> usedIds, Random rng, out RewardRow row)
        {
            int count = 0;
            for (int i = 0; i < RewardCatalog.All.Length; i++)
            {
                var r = RewardCatalog.All[i];
                if (!string.Equals(r.Stat, "heal", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (usedIds != null && usedIds.Contains(r.Id))
                    continue;
                count++;
            }

            row = default(RewardRow);
            if (count <= 0 || rng == null)
                return false;
            int choice = rng.Next(count);
            for (int i = 0; i < RewardCatalog.All.Length; i++)
            {
                var r = RewardCatalog.All[i];
                if (!string.Equals(r.Stat, "heal", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (usedIds != null && usedIds.Contains(r.Id))
                    continue;
                if (choice == 0)
                {
                    row = r;
                    return true;
                }

                choice--;
            }

            return false;
        }
    }
}
