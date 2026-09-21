namespace RogueShooter.Player
{
    /// <summary>
    /// Dodge / roll table. Swap this one file when the balance table lands.
    /// ACTION_SPEC_P1 marks the i-frame window as 建议 / 不锁.
    /// Displacement is play move-speed × duration — not a new invented distance row.
    /// </summary>
    public static class DodgeRules
    {
        /// <summary>Spec roll total 0.40s (still inside the 0.3–0.4 stub band).</summary>
        public const float DurationSeconds = 0.40f;

        /// <summary>Startup is hittable. Spec suggested 0.08s.</summary>
        public const float IFrameStartSeconds = 0.08f;

        /// <summary>
        /// End of suggested invuln. Length stays 0.20s (stub ≈0.2s).
        /// Spec text suggested 0.08–0.32; not locked — change here only.
        /// </summary>
        public const float IFrameEndSeconds = 0.28f;

        public const float CooldownSeconds = 0.90f;
        public const float MoveSpeedRef = 6f;

        /// <summary>Invuln length (IFrameEnd − IFrameStart). Kept for logs / older callers.</summary>
        public static float IFrameSeconds => IFrameEndSeconds - IFrameStartSeconds;

        public static float Distance => MoveSpeedRef * DurationSeconds;

        public static bool IFrameActive(float now, float rollStart)
        {
            float t = now - rollStart;
            return t >= IFrameStartSeconds && t < IFrameEndSeconds;
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
