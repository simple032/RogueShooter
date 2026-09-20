namespace RogueShooter.Balance
{
    /// <summary>
    /// N34 move speeds from balance_move_speeds_箭骸.csv (Player +12% vs L=22).
    /// </summary>
    public static class MoveSpeeds
    {
        public const float Player = 0.10267f;
        public const float ChargeMul = 0.5f;
        public const float E1 = 0.06673f;
        public const float E2 = 0.11294f;
        public const float E3 = 0.05647f;
        public const float E4 = 0.05133f;

        public static float ForKind(string kindId)
        {
            if (string.IsNullOrEmpty(kindId))
                return E1;
            switch (kindId.ToUpperInvariant())
            {
                case "E2": return E2;
                case "E3": return E3;
                case "E4": return E4;
                default: return E1;
            }
        }

        public static float PlayerWhileCharging => Player * ChargeMul;
    }
}
