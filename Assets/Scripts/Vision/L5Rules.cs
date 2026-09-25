namespace RogueShooter.Vision
{
    /// <summary>
    /// L5 vision / spawn / aggro rules (策划+数值定稿). The only place these values live.
    /// Orthographic and isometric share the same logic-unit values: nothing here reads the
    /// camera size. Fields are static so a tuning PR or debug hotkey can change them at runtime;
    /// the <c>Default*</c> consts are the reviewed defaults (see balance_iso_camera_range_draft.csv).
    /// </summary>
    public static class L5Rules
    {
        // ---- aggro (logic u, independent of view) ----
        public const float DefaultAggroMeleeU = 8f;
        public const float DefaultAggroRangedU = 10f;
        /// <summary>Melee (normal / shield / dog) Patrol → Alert radius.</summary>
        public static float AggroMeleeU = DefaultAggroMeleeU;
        /// <summary>Ranged (cult mage / grand mage) Patrol → Alert radius.</summary>
        public static float AggroRangedU = DefaultAggroRangedU;

        // ---- mage orb ----
        public const float DefaultOrbRangeU = 12f;
        public const float DefaultOrbFlightSeconds = 1.0f;
        /// <summary>Max orb travel (replaces camera_width × 0.7).</summary>
        public static float OrbRangeU = DefaultOrbRangeU;
        /// <summary>Max orb flight time. Effective range = min(OrbRangeU, speed × OrbFlightSeconds).</summary>
        public static float OrbFlightSeconds = DefaultOrbFlightSeconds;
        /// <summary>
        /// On (default): an orb only ends on range, wall/door or player hit, and the caster waits for
        /// that before the next volley. Off: legacy camera-edge despawn as well.
        /// </summary>
        public static bool OrbRangeOrWallOnly = true;
        /// <summary>Hard rule: a ranged caster must be inside the screen view quad to fire.</summary>
        public static bool RangedFireRequiresInView = true;
        /// <summary>Retry delay after a fire was blocked by the view rule.</summary>
        public static float BlockedFireRetrySeconds = 0.25f;

        // ---- off-screen spawn ----
        /// <summary>On (default): Stage1 combat-room spots use the L5 off-screen rule + fallback.</summary>
        public static bool SpawnRuleEnabled = true;
        public const float DefaultSpawnMinU = 14f;
        public const float DefaultSpawnMaxU = 22f;
        public const float DefaultSpawnOutsideQuadU = 2f;
        public static float SpawnMinU = DefaultSpawnMinU;
        /// <summary>Upper bound 22 must not be lowered (logic-axis direction needs 18.7 + 2).</summary>
        public static float SpawnMaxU = DefaultSpawnMaxU;
        /// <summary>Spot must be outside the view quad by at least this distance.</summary>
        public static float SpawnOutsideQuadU = DefaultSpawnOutsideQuadU;

        // ---- fallback spawn warning ----
        public const float DefaultFallbackWarnSeconds = 1.0f;
        public const float DefaultFallbackWarnNearSeconds = 1.5f;
        /// <summary>Warning before a fallback (farthest walkable cell) spawn.</summary>
        public static float FallbackWarnSeconds = DefaultFallbackWarnSeconds;
        /// <summary>Warning when the fallback cell is closer than <see cref="SpawnMinU"/>.</summary>
        public static float FallbackWarnNearSeconds = DefaultFallbackWarnNearSeconds;

        // ---- stage-4 acceptance ----
        /// <summary>
        /// Stage 4: the nearest off-screen walkable cell must be at least this far from the player
        /// (iso 5.25, camera clamped). Replaces the old "player → screen edge" criterion.
        /// </summary>
        public const float Stage4MinOffscreenCellU = 12f;
        /// <summary>Ranged aggro fallback if stage 4 were short (reference only; aggro stays 10u).</summary>
        public const float Stage4RangedAggroIfShortU = 9f;

        public static float AggroFor(bool ranged)
        {
            return ranged ? AggroRangedU : AggroMeleeU;
        }

        public static float OrbMaxRange(float orbSpeed)
        {
            float byTime = orbSpeed > 0.01f && OrbFlightSeconds > 0.01f ? orbSpeed * OrbFlightSeconds : OrbRangeU;
            return byTime < OrbRangeU ? byTime : OrbRangeU;
        }

        public static float FallbackWarnFor(float distToPlayer)
        {
            return distToPlayer < SpawnMinU ? FallbackWarnNearSeconds : FallbackWarnSeconds;
        }

        public static void ResetDefaults()
        {
            AggroMeleeU = DefaultAggroMeleeU;
            AggroRangedU = DefaultAggroRangedU;
            OrbRangeU = DefaultOrbRangeU;
            OrbFlightSeconds = DefaultOrbFlightSeconds;
            OrbRangeOrWallOnly = true;
            RangedFireRequiresInView = true;
            BlockedFireRetrySeconds = 0.25f;
            SpawnRuleEnabled = true;
            SpawnMinU = DefaultSpawnMinU;
            SpawnMaxU = DefaultSpawnMaxU;
            SpawnOutsideQuadU = DefaultSpawnOutsideQuadU;
            FallbackWarnSeconds = DefaultFallbackWarnSeconds;
            FallbackWarnNearSeconds = DefaultFallbackWarnNearSeconds;
        }

        public static string Describe()
        {
            return "aggroMelee=" + AggroMeleeU + " aggroRanged=" + AggroRangedU
                + " orbRange=" + OrbRangeU + " orbFlight=" + OrbFlightSeconds + "s"
                + " orbRangeOrWall=" + (OrbRangeOrWallOnly ? 1 : 0)
                + " rangedFireInView=" + (RangedFireRequiresInView ? 1 : 0)
                + " spawn=" + (SpawnRuleEnabled ? 1 : 0) + " D=[" + SpawnMinU + "," + SpawnMaxU + "] M=" + SpawnOutsideQuadU
                + " warn=" + FallbackWarnSeconds + "/" + FallbackWarnNearSeconds + "s";
        }
    }
}
