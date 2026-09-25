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
    /// v0.3 selection: every open starts with nothing selected (<see cref="NoSelection"/>); the first up/down/confirm
    /// only selects the first unsold row; up/down then clamp (no wrap) and do not skip sold rows; confirm = 购买.
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

        public const int NoSelection = -1;

        /// <summary>Selected row 0..Shelves.Length-1, or <see cref="NoSelection"/>. Buttons are not in the focus order (v0.3 §9 假设4).</summary>
        public int Focus { get; set; }

        public bool HasSelection
        {
            get { return Shelves != null && Focus >= 0 && Focus < Shelves.Length; }
        }

        public ShopSession(string siteId)
        {
            SiteId = siteId ?? "";
            Shelves = new ShopShelf[0];
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
            Focus = NoSelection; // v0.3 §5: open / re-enter = nothing selected, not remembered
            _ = gold;
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

        /// <summary>v0.3 §7.2: first row that is not sold out; all sold → row 0.</summary>
        public int FirstUnsold()
        {
            if (Shelves == null || Shelves.Length == 0)
                return NoSelection;
            for (int i = 0; i < Shelves.Length; i++)
            {
                if (!Shelves[i].Sold && !Shelves[i].Empty)
                    return i;
            }

            return 0;
        }

        /// <summary>Mouse click on a row: select it (never buys).</summary>
        public void Select(int index)
        {
            if (Shelves != null && index >= 0 && index < Shelves.Length)
                Focus = index;
        }

        /// <summary>
        /// Up/down (dy = -1 up, +1 down). Nothing selected → select <see cref="FirstUnsold"/>.
        /// Otherwise move one row, clamped at top/bottom (no wrap), sold rows are not skipped. Not time-locked (假设1).
        /// </summary>
        public int MoveSelection(int dy)
        {
            if (Shelves == null || Shelves.Length == 0)
                return Focus;
            if (!HasSelection)
            {
                Focus = FirstUnsold();
                return Focus;
            }

            if (dy == 0)
                return Focus;
            int f = Focus + (dy > 0 ? 1 : -1);
            if (f < 0) f = 0;
            if (f > Shelves.Length - 1) f = Shelves.Length - 1;
            Focus = f;
            return Focus;
        }

        /// <summary>
        /// Confirm key (A / Enter / Space). Inside the open lock window: fully ignored (假设1, also no auto-select).
        /// Nothing selected → only selects the first unsold row (returns <see cref="ShopBuyResult.Invalid"/>, no buy).
        /// Selected → presses 购买 for that row (§6 state rules).
        /// </summary>
        public ShopBuyResult ConfirmKey(float now, RunBuildState build, out ShopShelf shelf)
        {
            shelf = default(ShopShelf);
            if (InputLocked(now))
                return ShopBuyResult.Locked;
            if (!HasSelection)
            {
                Focus = FirstUnsold();
                return ShopBuyResult.Invalid;
            }

            return TryBuy(Focus, now, build, out shelf);
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
