using RogueShooter.Balance;

namespace RogueShooter.Build
{
    /// <summary>
    /// N36 balance_gold_plan_箭骸.csv. Start gold 0. Chest 15/30 unchanged.
    /// Kill gold only in P1 (E1=0/E2=1/E3=2/E4=3); P2–P4 = 0. EmptySoft off (0–0).
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
            if (TimePressure.PhaseId(wallMinutes) != "P1")
                return 0;

            string k = string.IsNullOrEmpty(enemyKind) ? "E1" : enemyKind.ToUpperInvariant();
            switch (k)
            {
                case "E2": return 1;
                case "E3": return 2;
                case "E4": return 3;
                default: return 0;
            }
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
