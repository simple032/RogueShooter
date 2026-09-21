namespace RogueShooter.Player
{
    /// <summary>
    /// Dodge / roll table. Producer draft — not locked. Swap this one file.
    /// Clip timing follows ACTION_SPEC_P1 (8 frames, OnFire stays on atk).
    /// I-frame window is the draft row, not the spec's 0.08–0.32 suggestion.
    /// </summary>
    public static class DodgeRules
    {
        /// <summary>Roll total. Matches ACTION_SPEC clip duration.</summary>
        public const float DurationSeconds = 0.40f;

        /// <summary>Invuln starts after startup (draft 0.04; spec suggested 0.08, unlocked).</summary>
        public const float IFrameStartSeconds = 0.04f;

        /// <summary>Invuln ends before recovery pose. Draft 0.28; change here only.</summary>
        public const float IFrameEndSeconds = 0.28f;

        public const float CooldownSeconds = 1.00f;

        /// <summary>Play move speed, for logs only. Displacement is its own table row.</summary>
        public const float MoveSpeedRef = 6f;

        /// <summary>Draft displacement 6u over DurationSeconds (not speed × duration).</summary>
        public const float Distance = 6f;

        /// <summary>Charge (and pending OnFire) can be cancelled into a roll.</summary>
        public const bool CancelCharge = true;

        /// <summary>Shot recovery / atk 后摇 can be cancelled into a roll.</summary>
        public const bool CancelShotRecovery = true;

        /// <summary>No fire / OnFire while rolling.</summary>
        public const bool BlockFireWhileRolling = true;

        /// <summary>No stamina row. Keep 0 until a stamina table exists.</summary>
        public const float StaminaCost = 0f;

        /// <summary>Invuln length (IFrameEnd − IFrameStart).</summary>
        public static float IFrameSeconds => IFrameEndSeconds - IFrameStartSeconds;

        public static float RollSpeed => DurationSeconds < 0.0001f ? 0f : Distance / DurationSeconds;

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
