using System;
using System.Collections.Generic;

namespace RogueShooter.Build
{
    public enum ShopSlotState
    {
        Buyable,
        NoGold,
        Sold
    }

    public enum ShopBuyResult
    {
        Bought,
        NoGold,
        Sold,
        Locked,
        Invalid
    }

    /// <summary>
    /// One shop instance (商店购买界面 v0.2 §3). Pure logic, no UI:
    /// shelves are rolled on the first open and kept for the rest of the run (no refresh, no restock);
    /// sold shelves stay sold across leave / re-enter; slot state is judged on the live gold every time.
    /// Confirm has no second step (P3) but is ignored for <see cref="OpenInputLockSeconds"/> after each open.
    /// </summary>
    public sealed class ShopSession
    {
        /// <summary>v0.2 §4.4 / §8.6 防误触: confirm ignored this long after the screen opens (建议值，程序调).</summary>
        public const float OpenInputLockSeconds = 0.2f;

        public string SiteId { get; private set; }
        public ShopShelf[] Shelves { get; private set; }
        public bool Generated { get; private set; }
        public bool IsOpen { get; private set; }
        public int OpenCount { get; private set; }
        public int GenerateCount { get; private set; }
        public float OpenedAt { get; private set; }

        /// <summary>Focus index: 0..Shelves.Length-1 = shelves, <see cref="LeaveIndex"/> = the 离开 button.</summary>
        public int Focus { get; set; }

        public ShopSession(string siteId)
        {
            SiteId = siteId ?? "";
            Shelves = new ShopShelf[0];
        }

        public int LeaveIndex
        {
            get { return Shelves != null ? Shelves.Length : 0; }
        }

        /// <summary>First open only: roll shelves. Later calls keep the same shelves (条目、价格、位置不变).</summary>
        public bool EnsureStock(Random rng, ShopBalance bal, IList<string> ownedRewards)
        {
            if (Generated)
                return false;
            RewardCatalog.BindPoolOwned(ownedRewards);
            Shelves = ShopStock.RollShelves(rng, bal);
            Generated = true;
            GenerateCount++;
            return true;
        }

        public void Open(float now, int gold)
        {
            IsOpen = true;
            OpenedAt = now;
            OpenCount++;
            Focus = DefaultFocus(gold);
        }

        public void Close()
        {
            IsOpen = false;
        }

        public bool InputLocked(float now)
        {
            // 1e-4s tolerance so a confirm exactly at +0.2s (float rounding) is accepted.
            return IsOpen && now - OpenedAt < OpenInputLockSeconds - 1e-4f;
        }

        public ShopSlotState StateOf(int index, int gold)
        {
            if (Shelves == null || index < 0 || index >= Shelves.Length)
                return ShopSlotState.Sold;
            ShopShelf s = Shelves[index];
            if (s.Sold || s.Empty)
                return ShopSlotState.Sold;
            return gold >= s.Price ? ShopSlotState.Buyable : ShopSlotState.NoGold;
        }

        /// <summary>§8.2 / §8.7: first buyable shelf; none buyable → 离开.</summary>
        public int DefaultFocus(int gold)
        {
            if (Shelves != null)
            {
                for (int i = 0; i < Shelves.Length; i++)
                {
                    if (StateOf(i, gold) == ShopSlotState.Buyable)
                        return i;
                }
            }

            return LeaveIndex;
        }

        /// <summary>§8.3: 6 shelves in a 2×3 grid then 离开, wrapping. dx/dy in {-1,0,1}.</summary>
        public int MoveFocus(int dx, int dy, int columns)
        {
            int n = LeaveIndex + 1;
            if (n <= 1)
            {
                Focus = 0;
                return Focus;
            }

            if (columns <= 0)
                columns = 3;
            int f = Focus;
            if (dx != 0)
                f = ((f + dx) % n + n) % n;
            if (dy != 0)
            {
                if (f == LeaveIndex)
                    f = dy > 0 ? 0 : Math.Max(0, LeaveIndex - columns);
                else
                {
                    int next = f + dy * columns;
                    if (next >= LeaveIndex)
                        f = LeaveIndex;
                    else if (next < 0)
                        f = LeaveIndex;
                    else
                        f = next;
                }
            }

            Focus = f;
            return Focus;
        }

        /// <summary>
        /// Single confirm = buy (P3, no second confirm). Locked during the open window.
        /// Success: gold -= price, reward applied, Build += table value (heal too), shelf sold. Focus stays put.
        /// NoGold / Sold: nothing changes.
        /// </summary>
        public ShopBuyResult TryBuy(int index, float now, RunBuildState build, out ShopShelf shelf)
        {
            shelf = default(ShopShelf);
            if (build == null || Shelves == null || index < 0 || index >= Shelves.Length)
                return ShopBuyResult.Invalid;
            shelf = Shelves[index];
            if (InputLocked(now))
                return ShopBuyResult.Locked;
            ShopSlotState state = StateOf(index, build.Gold);
            if (state == ShopSlotState.Sold)
                return ShopBuyResult.Sold;
            if (state == ShopSlotState.NoGold)
                return ShopBuyResult.NoGold;
            if (!build.TryShopBuy(shelf.Price, shelf.ContentRole, shelf.Id))
                return ShopBuyResult.NoGold;
            shelf.Sold = true;
            Shelves[index] = shelf;
            Focus = index;
            return ShopBuyResult.Bought;
        }

        /// <summary>Stack count of this reward family the player already owns (说明面板 当前层数).</summary>
        public static int OwnedStacks(IList<string> owned, string rewardId)
        {
            if (owned == null || string.IsNullOrEmpty(rewardId))
                return 0;
            if (!RewardCatalog.TryGet(rewardId, out RewardRow row))
                return 0;
            int n = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                if (RewardCatalog.TryGet(owned[i], out RewardRow o) && o.Stat == row.Stat)
                    n++;
            }

            return n;
        }

        /// <summary>乘算 / 加算 label from the catalog value_type (percent_add = 加算).</summary>
        public static string StackLabel(string rewardId)
        {
            if (!RewardCatalog.TryGet(rewardId, out RewardRow row))
                return "";
            if (ShopStock.IsHealRow(row))
                return "立即回复";
            if (string.Equals(row.ValueType, "percent_add", StringComparison.OrdinalIgnoreCase))
                return "加算";
            if (string.Equals(row.ValueType, "percent", StringComparison.OrdinalIgnoreCase))
                return "乘算";
            return "特殊"; // e.g. R3 鸿运 seconds (表 note: 特殊·非DPS)
        }

        public string FormatShelves()
        {
            return ShopStock.FormatShelves(Shelves);
        }
    }
}
