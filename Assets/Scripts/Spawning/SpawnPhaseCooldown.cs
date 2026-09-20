namespace RogueShooter.Spawning
{
    /// <summary>
    /// Map-phase cooldown for a spawn point: after a spawn in phase N,
    /// phases N and N+1 are blocked; N+2 and later may spawn again.
    /// Phases are Z1 → Z2 → Z3. Does not wire T0–T3 pressure.
    /// </summary>
    public static class SpawnPhaseCooldown
    {
        public static int PhaseIndex(string bandId)
        {
            switch (bandId)
            {
                case "Z1": return 0;
                case "Z2": return 1;
                case "Z3": return 2;
                default: return -1;
            }
        }

        public static bool IsBlocked(int lastPhaseIndex, string currentBandId)
        {
            if (lastPhaseIndex < 0)
                return false;
            int cur = PhaseIndex(currentBandId);
            if (cur < 0)
                return true;
            return cur - lastPhaseIndex <= 1;
        }
    }
}
