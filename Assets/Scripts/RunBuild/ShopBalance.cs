using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueShooter.Balance;
using UnityEngine;

namespace RogueShooter.Build
{
    /// <summary>Price band for one shelf role (balance_shop_prices price_min / price_max / price_mid).</summary>
    public struct ShopPriceBand
    {
        public int Min;
        public int Max;
        public int Mid;

        public bool Contains(int price)
        {
            return price >= Min && price <= Max;
        }

        public override string ToString()
        {
            return Mid + "(" + Min + "-" + Max + ")";
        }
    }

    /// <summary>
    /// Shop numbers read from tables only (商店购买界面 v0.2 §2):
    /// - prices: StreamingAssets/BalanceCrit2/balance_shop_prices_箭骸.csv (fixed,tier_low/mid/high/heal)
    /// - flex weights + dedupe: StreamingAssets/BalanceCrit2/balance_offer_rules_箭骸.csv (shop,flex_w / dedupe,*)
    /// - shelf count / flex count / Build per buy: BalanceLock_v042/balance_shop_gold_locked.csv
    ///   (shelf_count, shelf_flex_count, shelf_flex_w, build_per_shop_buy, build_heal_buy, price_ref).
    /// A missing table falls back to the <see cref="ShopStock"/> mirror constants and is logged as a gap.
    /// Cross-table mismatches are logged (never silently "fixed").
    /// </summary>
    public sealed class ShopBalance
    {
        public const string FolderName = "BalanceCrit2";
        public const string PricesCsv = "balance_shop_prices_箭骸.csv";
        public const string OfferRulesCsv = "balance_offer_rules_箭骸.csv";

        public string Status { get; private set; }
        public readonly List<string> Notes = new List<string>();

        public int ShelfCount;
        public int FlexCount;
        public int FlexWLow;
        public int FlexWMid;
        public int FlexWHigh;
        public ShopPriceBand Low;
        public ShopPriceBand Mid;
        public ShopPriceBand High;
        public ShopPriceBand Heal;
        /// <summary>balance_shop_prices meta float_mode (how a price is taken inside the band is 数值's call).</summary>
        public string FloatMode = "";
        public int BuildPerBuy;
        public int BuildHealBuy;
        public int HealMax;
        public int RerollSameTier;
        public bool SameOfferUnique;
        public bool ShopNoSameX3;

        static ShopBalance _current;

        /// <summary>Loaded once from StreamingAssets (after BalanceLock if it is loaded).</summary>
        public static ShopBalance Current
        {
            get
            {
                if (_current == null)
                    _current = Load(BalanceLock.Current);
                return _current;
            }
            set { _current = value; }
        }

        public static string CatalogDirectory()
        {
            string sa = null;
            try
            {
                sa = Application.streamingAssetsPath;
            }
            catch (Exception)
            {
                sa = null;
            }

            if (!string.IsNullOrEmpty(sa))
                return Path.Combine(sa, FolderName);
            return Path.Combine("Assets", "StreamingAssets", FolderName);
        }

        /// <summary>Reload from disk and make it <see cref="Current"/>.</summary>
        public static ShopBalance Reload(BalanceLockData lockData)
        {
            _current = Load(lockData);
            return _current;
        }

        public static ShopBalance Load(BalanceLockData lockData)
        {
            string dir = CatalogDirectory();
            string prices = ReadOrNull(Path.Combine(dir, PricesCsv));
            string offer = ReadOrNull(Path.Combine(dir, OfferRulesCsv));
            ShopBalance bal = FromTables(prices, offer, lockData);
            if (prices == null)
                bal.Note("GAP missing " + Path.Combine(dir, PricesCsv) + " → ShopStock mirror prices");
            if (offer == null)
                bal.Note("GAP missing " + Path.Combine(dir, OfferRulesCsv) + " → ShopStock mirror flex_w");
            bal.LogSummary();
            return bal;
        }

        static string ReadOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Mirror constants only (tables unreadable). Same values as the tables at f1793ff.</summary>
        public static ShopBalance Fallback()
        {
            var b = new ShopBalance
            {
                Status = "FALLBACK",
                ShelfCount = ShopStock.ShelfCount,
                FlexCount = ShopStock.ElasticCount,
                FlexWLow = ShopStock.ElasticWLow,
                FlexWMid = ShopStock.ElasticWMid,
                FlexWHigh = ShopStock.ElasticWHigh,
                Low = Band(ShopStock.LowPriceMin, ShopStock.LowPriceMax, ShopStock.PriceBaseLow),
                Mid = Band(ShopStock.MidPriceMin, ShopStock.MidPriceMax, ShopStock.PriceBaseMid),
                High = Band(ShopStock.HighPriceMin, ShopStock.HighPriceMax, ShopStock.PriceBaseHigh),
                Heal = Band(ShopStock.MidPriceMin, ShopStock.MidPriceMax, ShopStock.PriceBaseHeal),
                BuildPerBuy = ShopStock.MirrorBuildPerBuy,
                BuildHealBuy = ShopStock.MirrorBuildPerBuy,
                HealMax = 1,
                RerollSameTier = 5,
                SameOfferUnique = true,
                ShopNoSameX3 = true,
            };
            return b;
        }

        public static ShopBalance FromTables(string pricesCsv, string offerRulesCsv, BalanceLockData lockData)
        {
            ShopBalance b = Fallback();
            bool pricesOk = false, offerOk = false, lockOk = false;

            if (!string.IsNullOrEmpty(pricesCsv))
                pricesOk = b.ApplyPrices(CsvTable.Parse(StripComments(pricesCsv)));
            if (!string.IsNullOrEmpty(offerRulesCsv))
                offerOk = b.ApplyOfferRules(CsvTable.Parse(StripComments(offerRulesCsv)));
            if (lockData != null && lockData.shopGold != null)
                lockOk = b.ApplyLock(lockData);
            else
                b.Note("GAP BalanceLock not loaded → shelf_count/build_* from ShopStock mirror");

            b.Status = pricesOk && offerOk && lockOk ? "LOADED"
                : pricesOk || offerOk || lockOk ? "PARTIAL"
                : "FALLBACK";
            return b;
        }

        bool ApplyPrices(CsvTable t)
        {
            bool low = false, mid = false, high = false, heal = false;
            string randomLow = "", randomMid = "", randomHigh = "", randomHeal = "", shelfMeta = "", randomCount = "";
            for (int i = 0; i < t.Rows.Count; i++)
            {
                string[] row = t.Rows[i];
                string slot = t.Get(row, "slot");
                string role = t.Get(row, "role");
                if (string.Equals(slot, "fixed", StringComparison.OrdinalIgnoreCase))
                {
                    ShopPriceBand band = Band(
                        CsvTable.ToInt(t.Get(row, "price_min")),
                        CsvTable.ToInt(t.Get(row, "price_max")),
                        CsvTable.ToInt(t.Get(row, "price_mid")));
                    if (band.Min <= 0 || band.Max < band.Min || !band.Contains(band.Mid))
                    {
                        Note("GAP shop price row " + role + " invalid band " + band);
                        continue;
                    }

                    switch (role)
                    {
                        case "tier_low": Low = band; low = true; break;
                        case "tier_mid": Mid = band; mid = true; break;
                        case "tier_high": High = band; high = true; break;
                        case "heal": Heal = band; heal = true; break;
                    }
                }
                else if (string.Equals(slot, "meta", StringComparison.OrdinalIgnoreCase))
                {
                    // meta rows: value sits in the price_min column.
                    string v = t.Get(row, "price_min");
                    switch (role)
                    {
                        case "float_mode": FloatMode = v; break;
                        case "shelf_count": shelfMeta = v; break;
                        case "random_count": randomCount = v; break;
                        case "random_p_low": randomLow = v; break;
                        case "random_p_mid": randomMid = v; break;
                        case "random_p_high": randomHigh = v; break;
                        case "random_p_heal": randomHeal = v; break;
                    }
                }
            }

            if (!(low && mid && high && heal))
                Note("GAP balance_shop_prices missing fixed rows low=" + low + " mid=" + mid + " high=" + high + " heal=" + heal);
            // Cross-check the prices table's own shelf meta against offer_rules later (see ApplyOfferRules / ApplyLock).
            _pricesShelfMeta = shelfMeta;
            _pricesRandomCount = randomCount;
            _pricesRandomP = randomLow.Length > 0
                ? randomLow + "/" + randomMid + "/" + randomHigh + "/" + randomHeal
                : "";
            return low && mid && high && heal;
        }

        string _pricesShelfMeta = "";
        string _pricesRandomCount = "";
        string _pricesRandomP = "";
        string _offerShelf = "";

        bool ApplyOfferRules(CsvTable t)
        {
            bool flex = false;
            for (int i = 0; i < t.Rows.Count; i++)
            {
                string[] row = t.Rows[i];
                string section = t.Get(row, "section");
                string item = t.Get(row, "item");
                string value = t.Get(row, "value");
                if (section == "shop" && item == "flex_w")
                {
                    int[] w = SplitInts(value);
                    if (w.Length == 3 && w[0] >= 0 && w[1] >= 0 && w[2] >= 0 && w[0] + w[1] + w[2] > 0)
                    {
                        FlexWLow = w[0];
                        FlexWMid = w[1];
                        FlexWHigh = w[2];
                        flex = true;
                    }
                    else
                        Note("GAP offer_rules shop.flex_w unreadable '" + value + "'");
                }
                else if (section == "shop" && item == "shelf")
                    _offerShelf = value;
                else if (section == "shop" && item.StartsWith("price_", StringComparison.Ordinal))
                    CrossCheckPrice(item, value);
                else if (section == "dedupe" && item == "heal_max")
                    HealMax = CsvTable.ToInt(value, HealMax);
                else if (section == "dedupe" && item == "reroll_same_tier")
                    RerollSameTier = CsvTable.ToInt(value, RerollSameTier);
                else if (section == "dedupe" && item == "same_offer_unique")
                    SameOfferUnique = !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
                else if (section == "dedupe" && item == "shop_no_same_x3")
                    ShopNoSameX3 = !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }

            if (_pricesRandomP.Length > 0)
            {
                string expect = Frac(FlexWLow) + "/" + Frac(FlexWMid) + "/" + Frac(FlexWHigh) + "/0.00";
                if (_pricesRandomP != expect)
                    Note("MISMATCH shop_prices random_p " + _pricesRandomP + " vs offer_rules flex_w " + FlexWLow + "/" + FlexWMid + "/" + FlexWHigh + " (offer_rules wins)");
            }

            return flex;
        }

        void CrossCheckPrice(string item, string value)
        {
            ShopPriceBand band;
            switch (item)
            {
                case "price_low": band = Low; break;
                case "price_mid": band = Mid; break;
                case "price_high": band = High; break;
                case "price_heal": band = Heal; break;
                default: return;
            }

            int[] r = SplitInts(value.Replace('-', '/'));
            if (r.Length == 2 && (r[0] != band.Min || r[1] != band.Max))
                Note("MISMATCH offer_rules shop." + item + "=" + value + " vs shop_prices " + band + " (shop_prices wins)");
        }

        bool ApplyLock(BalanceLockData d)
        {
            bool ok = true;
            if (d.shopShelfCount > 0)
                ShelfCount = d.shopShelfCount;
            else
            {
                ok = false;
                Note("GAP lock shelf_count missing → " + ShelfCount);
            }

            if (d.shopShelfFlexCount > 0)
                FlexCount = d.shopShelfFlexCount;
            else
            {
                ok = false;
                Note("GAP lock shelf_flex_count missing → " + FlexCount);
            }

            if (d.shopBuildPerShopBuy >= 0)
                BuildPerBuy = d.shopBuildPerShopBuy;
            else
            {
                ok = false;
                Note("GAP lock build_per_shop_buy missing → " + BuildPerBuy);
            }

            if (d.shopBuildHealBuy >= 0)
                BuildHealBuy = d.shopBuildHealBuy;
            else
            {
                ok = false;
                Note("GAP lock build_heal_buy missing/TBD → " + BuildHealBuy);
            }

            int[] w = SplitInts(d.shopShelfFlexW ?? "");
            if (w.Length == 3 && (w[0] != FlexWLow || w[1] != FlexWMid || w[2] != FlexWHigh))
                Note("MISMATCH lock shelf_flex_w=" + d.shopShelfFlexW + " vs offer_rules flex_w " + FlexWLow + "/" + FlexWMid + "/" + FlexWHigh + " (offer_rules wins)");
            if (!string.IsNullOrEmpty(d.shopPriceRef) && d.shopPriceRef.IndexOf(PricesCsv, StringComparison.Ordinal) < 0)
                Note("MISMATCH lock price_ref=" + d.shopPriceRef + " (loader reads " + PricesCsv + ")");

            int offerTotal, offerFlex;
            if (TryParseShelf(_offerShelf, out offerTotal, out offerFlex)
                && (offerTotal != ShelfCount || offerFlex != FlexCount))
                Note("MISMATCH offer_rules shop.shelf=" + _offerShelf + " vs lock shelf_count=" + ShelfCount + " flex=" + FlexCount);
            if (_pricesShelfMeta.Length > 0 && CsvTable.ToInt(_pricesShelfMeta) != ShelfCount)
                Note("MISMATCH shop_prices shelf_count=" + _pricesShelfMeta + " vs lock " + ShelfCount);
            if (_pricesRandomCount.Length > 0 && CsvTable.ToInt(_pricesRandomCount) != FlexCount)
                Note("MISMATCH shop_prices random_count=" + _pricesRandomCount + " vs lock flex " + FlexCount);
            if (ShelfCount != ShopStock.FixedCount + FlexCount)
                Note("MISMATCH shelf_count=" + ShelfCount + " != 4 fixed + flex " + FlexCount);
            return ok;
        }

        /// <summary>Single price for a shelf. v0.2 §2.7: 取值方式由数值定 → band mid (price_mid) until 数值 defines float_mode sampling.</summary>
        public int PriceFor(ShopSlotRole role)
        {
            return BandFor(role).Mid;
        }

        public ShopPriceBand BandFor(ShopSlotRole role)
        {
            switch (role)
            {
                case ShopSlotRole.Mid: return Mid;
                case ShopSlotRole.High: return High;
                case ShopSlotRole.Heal: return Heal;
                default: return Low;
            }
        }

        public int BuildFor(ShopSlotRole contentRole)
        {
            return contentRole == ShopSlotRole.Heal ? BuildHealBuy : BuildPerBuy;
        }

        public string PriceLine()
        {
            return "low " + Low + " mid " + Mid + " high " + High + " heal " + Heal
                   + " flex " + FlexWLow + "/" + FlexWMid + "/" + FlexWHigh;
        }

        public string SummaryLine()
        {
            return "[ShopBalance] " + Status + " shelves=" + ShelfCount + " (4 fixed + " + FlexCount + " flex) "
                   + PriceLine() + " float_mode=" + (string.IsNullOrEmpty(FloatMode) ? "-" : FloatMode)
                   + " build/buy=" + BuildPerBuy + " build/heal=" + BuildHealBuy
                   + " heal_max=" + HealMax + " reroll_same_tier=" + RerollSameTier;
        }

        void LogSummary()
        {
            Debug.Log(SummaryLine());
            for (int i = 0; i < Notes.Count; i++)
                Debug.LogWarning("[ShopBalance] " + Notes[i]);
        }

        void Note(string line)
        {
            Notes.Add(line);
        }

        public static bool TryParseShelf(string shelf, out int total, out int flex)
        {
            total = 0;
            flex = 0;
            if (string.IsNullOrEmpty(shelf))
                return false;
            string[] parts = shelf.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                int n = 0, k = 0;
                while (k < p.Length && p[k] >= '0' && p[k] <= '9')
                {
                    n = n * 10 + (p[k] - '0');
                    k++;
                }

                if (k == 0)
                    return false;
                total += n;
                string label = p.Substring(k);
                if (label.IndexOf("弹性", StringComparison.Ordinal) >= 0 || label.IndexOf("灵活", StringComparison.Ordinal) >= 0)
                    flex += n;
            }

            return total > 0;
        }

        static int[] SplitInts(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return new int[0];
            string[] parts = raw.Split('/');
            var list = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                float v = CsvTable.ToFloat(parts[i], float.NaN);
                if (float.IsNaN(v))
                    return new int[0];
                list.Add((int)v);
            }

            return list.ToArray();
        }

        static string Frac(int w)
        {
            return (w / 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string StripComments(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            if (text[0] == '\uFEFF')
                text = text.Substring(1);
            var sb = new StringBuilder(text.Length);
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("#", StringComparison.Ordinal))
                    continue;
                sb.Append(lines[i]).Append('\n');
            }

            return sb.ToString();
        }

        static ShopPriceBand Band(int min, int max, int mid)
        {
            return new ShopPriceBand { Min = min, Max = max, Mid = mid };
        }
    }
}
