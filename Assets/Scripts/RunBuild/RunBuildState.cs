namespace RogueShooter.Build
{
    /// <summary>
    /// Per-run Build counters. Shop purchases must not touch buildCount or rarityScore.
    /// Power = powerBuildCoef * B + powerRarityCoef * RS (LOCK: 0.45 / 0.55).
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

        public RunBuildState(float powerBuildCoef, float powerRarityCoef, int startGold)
        {
            PowerBuildCoef = powerBuildCoef;
            PowerRarityCoef = powerRarityCoef;
            Gold = startGold < 0 ? 0 : startGold;
            LastPick = "";
        }

        public float Power
        {
            get { return PowerBuildCoef * BuildCount + PowerRarityCoef * RarityScore; }
        }

        public string PowerFormulaLine()
        {
            return PowerBuildCoef.ToString("0.00") + "*B+" + PowerRarityCoef.ToString("0.00") + "*RS";
        }

        public void GrantBuildPick(string id, string rarity, int score)
        {
            BuildCount++;
            RarityScore += score;
            LastPick = id + " " + rarity + " +" + score;
        }

        /// <summary>Shop / gold spend. Explicitly does not change Build.</summary>
        public bool TryShopBuy(int price)
        {
            if (price < 0 || Gold < price)
                return false;
            Gold -= price;
            ShopBuys++;
            LastPick = "SHOP gold-" + price;
            return true;
        }

        public void Reset(int startGold)
        {
            BuildCount = 0;
            RarityScore = 0;
            ShopBuys = 0;
            Gold = startGold < 0 ? 0 : startGold;
            LastPick = "";
        }
    }
}
