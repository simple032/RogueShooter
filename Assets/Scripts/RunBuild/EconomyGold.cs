using RogueShooter.Balance;

namespace RogueShooter.Build
{
    /// <summary>
    /// N36 balance_gold_plan_箭骸.csv. Start gold 0. Chest 15/30 unchanged.
    /// Kill gold is 1 in every stage; gold_gain is applied on pickup. EmptySoft off (0–0).
    /// Death inherit = min(floor(prevHeld × 0.50), 30).
    /// </summary>
    public static class EconomyGold
    {
        public const int StartGold = 0;
        public const float DeathInheritRate = 0.50f;
        public const int DeathInheritCap = 30;
        public const int ChestSmall = 15;
        public const int ChestLarge = 30;
        public const int EmptySoftMin = 0;
        public const int EmptySoftMax = 0;
        public const int ArriveShopP50 = 75;
        public const int ArriveShopP80 = 90;
        public const int ArriveShopMin = 60;
        public const int ArriveShopMax = 90;

        public static int ChestGold(bool large, float wallMinutes)
        {
            return large ? ChestLarge : ChestSmall;
        }

        public static int KillGold(string enemyKind, float wallMinutes)
        {
            return 1; // Every stage: one base coin; bonus is settled on pickup.

        }

        /// <summary>min(floor(previous held × 0.50), 30).</summary>
        public static int DeathInherit(int previousHeld)
        {
            if (previousHeld <= 0)
                return 0;
            int granted = (int)System.Math.Floor(previousHeld * DeathInheritRate);
            if (granted > DeathInheritCap)
                granted = DeathInheritCap;
            return granted < 0 ? 0 : granted;
        }
    }
}
