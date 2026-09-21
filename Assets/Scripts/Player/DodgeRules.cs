namespace RogueShooter.Player
{
    /// <summary>
    /// Dodge / roll stub constants. Swap this one file when the balance table lands.
    /// Do not scatter replacements. Displacement is play move-speed × duration
    /// (existing MazeRules.PlayMoveSpeed) — not a new invented distance row.
    /// </summary>
    public static class DodgeRules
    {
        public const float DurationSeconds = 0.35f;
        public const float IFrameSeconds = 0.20f;
        public const float CooldownSeconds = 0.90f;
        public const float MoveSpeedRef = 6f;

        public static float Distance => MoveSpeedRef * DurationSeconds;

        public static bool IFrameActive(float now, float rollStart)
        {
            return now >= rollStart && now < rollStart + IFrameSeconds;
        }

        public static bool RollActive(float now, float rollStart)
        {
            return now >= rollStart && now < rollStart + DurationSeconds;
        }

        public static bool OnCooldown(float now, float rollStart)
        {
            return now < rollStart + CooldownSeconds;
        }
    }
}
