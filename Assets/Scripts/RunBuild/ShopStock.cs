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
        /// <summary>True when no eligible item was left for this slot (shown as 售罄, never buyable).</summary>
        public bool Empty;
    }

    /// <summary>
    /// 商店购买界面 v0.2: 6 shelves = 普/中/高/回复各1 + 灵活2, generated once per shop instance, never refreshed.
    /// Prices, flex weights, shelf counts and Build per buy come from <see cref="ShopBalance"/> (tables).
    /// The constants below only mirror the tables at f1793ff and are used when the tables are unreadable
    /// (ShopBalance.Fallback) and by the legacy slice checks.
    /// Flex shelves roll only 普/中/高 (heal weight forced 0) and never duplicate an item on the same shelf set.
    /// Heal items (stat=heal, e.g. R10 which is tier mid) only ever appear on the heal shelf (dedupe,heal_max=1).
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
        /// <summary>Mirror of balance_shop_gold_locked build_per_shop_buy / build_heal_buy (both 1).</summary>
        public const int MirrorBuildPerBuy = 1;

        /// <summary>Build per successful shop buy: build_per_shop_buy, heal shelf build_heal_buy (制作人 P2: 1).</summary>
        public static int BuildEquivFor(ShopSlotRole role)
        {
            return ShopBalance.Current.BuildFor(role);
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
            return RollShelves(rng, ShopBalance.Current);
        }

        /// <summary>
        /// Fixed 普/中/高/回复 then <see cref="ShopBalance.FlexCount"/> flex shelves (each rolls its tier on flex_w).
        /// Call <see cref="RewardCatalog.BindPoolOwned"/> first so capped rewards (pierce Σ≥100%) stay out.
        /// </summary>
        public static ShopShelf[] RollShelves(Random rng, ShopBalance bal)
        {
            if (rng == null)
                rng = new Random();
            if (bal == null)
                bal = ShopBalance.Fallback();
            int flexCount = bal.FlexCount > 0 ? bal.FlexCount : ElasticCount;
            var shelves = new ShopShelf[FixedCount + flexCount];
            var used = new HashSet<string>();
            shelves[0] = FillSlot(ShopSlotRole.Low, false, bal, used, rng);
            shelves[1] = FillSlot(ShopSlotRole.Mid, false, bal, used, rng);
            shelves[2] = FillSlot(ShopSlotRole.High, false, bal, used, rng);
            shelves[3] = FillSlot(ShopSlotRole.Heal, false, bal, used, rng);
            for (int i = 0; i < flexCount; i++)
                shelves[FixedCount + i] = FillSlot(RollElasticRole(rng, bal), true, bal, used, rng);
            return shelves;
        }

        public static ShopSlotRole RollElasticRole(Random rng)
        {
            return RollElasticRole(rng, ShopBalance.Current);
        }

        /// <summary>offer_rules shop.flex_w 普/中/高. Heal is never a flex outcome.</summary>
        public static ShopSlotRole RollElasticRole(Random rng, ShopBalance bal)
        {
            if (rng == null)
                rng = new Random();
            int wl = bal != null ? bal.FlexWLow : ElasticWLow;
            int wm = bal != null ? bal.FlexWMid : ElasticWMid;
            int wh = bal != null ? bal.FlexWHigh : ElasticWHigh;
            int total = wl + wm + wh;
            if (total <= 0)
                return ShopSlotRole.Low;
            int roll = rng.Next(total);
            if (roll < wl)
                return ShopSlotRole.Low;
            if (roll < wl + wm)
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

        public static bool IsHealRow(RewardRow row)
        {
            return string.Equals(row.Stat, "heal", StringComparison.OrdinalIgnoreCase);
        }

        public static ShopSlotRole RoleForTier(RewardTier tier)
        {
            switch (tier)
            {
                case RewardTier.Mid: return ShopSlotRole.Mid;
                case RewardTier.High: return ShopSlotRole.High;
                default: return ShopSlotRole.Low;
            }
        }

        static RewardTier TierForRole(ShopSlotRole role)
        {
            return role == ShopSlotRole.High ? RewardTier.High
                : role == ShopSlotRole.Mid ? RewardTier.Mid
                : RewardTier.Low;
        }

        static ShopShelf FillSlot(ShopSlotRole role, bool elastic, ShopBalance bal, HashSet<string> used, Random rng)
        {
            RewardRow row;
            bool ok;
            ShopSlotRole content = role;
            if (role == ShopSlotRole.Heal)
            {
                ok = TryPickHeal(used, rng, out row);
            }
            else
            {
                ok = TryPickTier(TierForRole(role), used, bal.RerollSameTier, rng, out row);
                if (!ok)
                {
                    // Tier pool exhausted on this shelf set: 从未出现池 = any unused non-heal item.
                    ok = TryPickAnyUnused(used, rng, out row);
                }

                if (ok)
                    content = RoleForTier(row.Tier);
            }

            if (!ok)
            {
                return new ShopShelf
                {
                    Id = "",
                    Tier = TierForRole(role == ShopSlotRole.Heal ? ShopSlotRole.Mid : role),
                    Role = elastic ? ShopSlotRole.Elastic : role,
                    ContentRole = role,
                    Price = 0,
                    PriceBase = 0,
                    LowAnchor = bal.Low.Mid,
                    Sold = true,
                    Effect = "",
                    IsHeal = role == ShopSlotRole.Heal,
                    IsElastic = elastic,
                    Empty = true
                };
            }

            used.Add(row.Id);
            int price = bal.PriceFor(content);
            return new ShopShelf
            {
                Id = row.Id,
                Tier = row.Tier,
                Role = elastic ? ShopSlotRole.Elastic : role,
                ContentRole = content,
                Price = price,
                PriceBase = price,
                LowAnchor = bal.Low.Mid,
                Sold = false,
                Effect = row.Desc,
                IsHeal = content == ShopSlotRole.Heal,
                IsElastic = elastic,
                Empty = false
            };
        }

        static bool ShopEligible(RewardRow row, RewardTier tier)
        {
            return !IsHealRow(row) && RewardCatalog.CanOffer(row, tier);
        }

        /// <summary>
        /// dedupe,reroll_same_tier: draw from the tier pool; a duplicate is re-drawn in the same tier up to N times,
        /// then taken from the not-yet-shown part of that tier.
        /// </summary>
        static bool TryPickTier(RewardTier tier, HashSet<string> used, int rerolls, Random rng, out RewardRow row)
        {
            row = default(RewardRow);
            var pool = new List<RewardRow>();
            var fresh = new List<RewardRow>();
            for (int i = 0; i < RewardCatalog.All.Length; i++)
            {
                RewardRow r = RewardCatalog.All[i];
                if (!ShopEligible(r, tier))
                    continue;
                pool.Add(r);
                if (!used.Contains(r.Id))
                    fresh.Add(r);
            }

            if (fresh.Count == 0)
                return false;
            int attempts = 1 + (rerolls > 0 ? rerolls : 0);
            for (int a = 0; a < attempts; a++)
            {
                RewardRow pick = pool[rng.Next(pool.Count)];
                if (!used.Contains(pick.Id))
                {
                    row = pick;
                    return true;
                }
            }

            row = fresh[rng.Next(fresh.Count)];
            return true;
        }

        static bool TryPickAnyUnused(HashSet<string> used, Random rng, out RewardRow row)
        {
            row = default(RewardRow);
            var fresh = new List<RewardRow>();
            for (int i = 0; i < RewardCatalog.All.Length; i++)
            {
                RewardRow r = RewardCatalog.All[i];
                if (used.Contains(r.Id))
                    continue;
                if (!ShopEligible(r, r.Tier))
                    continue;
                fresh.Add(r);
            }

            if (fresh.Count == 0)
                return false;
            row = fresh[rng.Next(fresh.Count)];
            return true;
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
