using System.Collections.Generic;

namespace RogueShooter.Build
{
    /// <summary>
    /// Per-run Build counters. Shop buy adds build_per_shop_buy (=1) per success, heal included
    /// (制作人 2026-09-20 / P2 2026-09-25 裁定，不用分层当量).
    /// Power = 0.45*B + 0.55*RS.
    /// </summary>
    public sealed class RunBuildState
    {
        public float PowerBuildCoef { get; private set; }
        public float PowerRarityCoef { get; private set; }

        public int BuildCount { get; private set; }
        public int RarityScore { get; private set; }
        public int Gold { get; private set; }
        public int ShopBuys { get; private set; }
        public string LastPick { get; private set; }
        readonly HashSet<string> _altarSizes = new HashSet<string>();
        readonly List<string> _owned = new List<string>();
        public RewardRegressionWeights Regression { get; private set; }

        public IList<string> OwnedRewardIds { get { return _owned; } }

        public RunBuildState(float powerBuildCoef, float powerRarityCoef, int startGold)
        {
            PowerBuildCoef = powerBuildCoef;
            PowerRarityCoef = powerRarityCoef;
            Gold = startGold < 0 ? 0 : startGold;
            LastPick = "";
            Regression = new RewardRegressionWeights();
        }

        public float Power
        {
            get { return PowerBuildCoef * BuildCount + PowerRarityCoef * RarityScore; }
        }

        public string PowerFormulaLine()
        {
            return PowerBuildCoef.ToString("0.00") + "*B+" + PowerRarityCoef.ToString("0.00") + "*RS";
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;
            Gold += amount;
        }

        public void ApplyReward(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (!RewardCatalog.TryGet(id, out _))
                return;
            _owned.Add(id);
        }

        /// <summary>Chest confirm. Small +1 Build, large +2 Build.</summary>
        public void GrantBuildPick(string id, string rarity, int score, int buildDelta = 1)
        {
            if (buildDelta < 1)
                buildDelta = 1;
            BuildCount += buildDelta;
            RarityScore += score;
            ApplyReward(id);
            LastPick = id + " " + rarity + " +" + score + " B+" + buildDelta;
        }

        public static int ChestBuildDelta(bool largeChest)
        {
            return largeChest ? 2 : 1;
        }

        public bool AltarSizeClaimed(AltarSize size)
        {
            if (size == AltarSize.None)
                return false;
            string key = AltarRewardRoll.SizeLabel(size);
            return _altarSizes.Contains(key);
        }

        /// <summary>First confirmed reward of this size. Build += 1/2/3.</summary>
        public bool ConfirmAltarSize(AltarSize size)
        {
            if (size == AltarSize.None)
                return false;
            string key = AltarRewardRoll.SizeLabel(size);
            if (_altarSizes.Contains(key))
                return false;
            int delta = AltarRewardRoll.BuildDelta(size);
            if (delta <= 0)
                return false;
            _altarSizes.Add(key);
            BuildCount += delta;
            LastPick = "altar " + key + " +" + delta;
            return true;
        }

        public bool ConfirmAltarPick(AltarSize size, string rewardId)
        {
            bool first = ConfirmAltarSize(size);
            ApplyReward(rewardId);
            if (!string.IsNullOrEmpty(rewardId))
                LastPick = (first ? "altar-first " : "altar ") + rewardId;
            return first;
        }

        /// <summary>Shop buy: gold + reward, Build always +1 per success.</summary>
        public bool TryShopBuy(int price, int buildEquiv, string rewardId)
        {
            if (price < 0 || Gold < price)
                return false;
            Gold -= price;
            ShopBuys++;
            int add = ShopStock.BuildEquivFor(ShopSlotRole.Low);
            _ = buildEquiv;
            BuildCount += add;
            ApplyReward(rewardId);
            LastPick = "SHOP +" + add + " " + rewardId + " gold-" + price;
            return true;
        }

        /// <summary>
        /// 商店购买界面 v0.2 §6: every successful buy adds Build from the table
        /// (build_per_shop_buy; heal shelf build_heal_buy — both 1 per 制作人 P2). Failure: no gold, no Build.
        /// </summary>
        public bool TryShopBuy(int price, ShopSlotRole contentRole, string rewardId)
        {
            if (price < 0 || Gold < price)
                return false;
            Gold -= price;
            ShopBuys++;
            int add = ShopStock.BuildEquivFor(contentRole);
            BuildCount += add;
            ApplyReward(rewardId);
            LastPick = "SHOP +" + add + " " + rewardId + " gold-" + price;
            return true;
        }

        /// <summary>Legacy overload: price only; success still +1 Build.</summary>
        public bool TryShopBuy(int price)
        {
            return TryShopBuy(price, ShopStock.BuildEquivFor(ShopSlotRole.Low), "");
        }

        public void Reset(int startGold)
        {
            BuildCount = 0;
            RarityScore = 0;
            ShopBuys = 0;
            Gold = startGold < 0 ? 0 : startGold;
            LastPick = "";
            _altarSizes.Clear();
            _owned.Clear();
            if (Regression != null)
                Regression.Reset();
            else
                Regression = new RewardRegressionWeights();
        }
    }
}
