namespace RogueShooter.Spawning
{
    /// <summary>
    /// Spec v0.5 enemy kind ids. E1/E2/E3 reuse existing catalog ids.
    /// SHIELD / GRAND are new stubs (not lock-CSV rows).
    /// </summary>
    public static class EnemyKindIds
    {
        public const string Normal = "E1";
        public const string Dog = "E2";
        public const string CultMage = "E3";
        public const string Shield = "SHIELD";
        public const string GrandMage = "GRAND";
    }
}
