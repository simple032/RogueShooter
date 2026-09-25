using System.Collections.Generic;
using RogueShooter.Build;

namespace RogueShooter.Player
{
    public enum ChargeShotKind
    {
        None,
        Weak,
        Full,
        Crit // talent weak-spot window (命中弱点)
    }

    /// <summary>
    /// Current full-charge time + weak-spot window for one loadout. 疾张 (charge_time, multiplicative)
    /// shortens Full; the window and the normal-damage minimum follow Full by percentage; 鸿运
    /// (crit_window, seconds) widens the window's upper bound. Recovery is never scaled.
    /// </summary>
    public struct ChargeProfile
    {
        public float Full;
        public float WindowBonus;

        public float WeakMax => Full * ChargeShotRules.NormalMinPct;
        public float WindowEnter => Full * ChargeShotRules.WeakSpotEnterPct;
        public float WindowExit => Full * ChargeShotRules.WeakSpotExitPct + WindowBonus;

        public static ChargeProfile Base => new ChargeProfile { Full = ChargeShotRules.RingFillSeconds, WindowBonus = 0f };

        public static ChargeProfile Make(float chargeMul, float windowBonusSeconds)
        {
            if (chargeMul < ChargeShotRules.MinChargeMul)
                chargeMul = ChargeShotRules.MinChargeMul;
            return new ChargeProfile
            {
                Full = ChargeShotRules.RingFillSeconds * chargeMul,
                WindowBonus = windowBonusSeconds > 0f ? windowBonusSeconds : 0f
            };
        }

        /// <summary>From owned reward ids: ∏(1+charge_time) and Σ crit_window.</summary>
        public static ChargeProfile FromOwned(IList<string> owned)
        {
            return Make(
                RewardStatHooks.ProductMul(owned, ChargeShotRules.StatChargeTime),
                RewardStatHooks.SumAdd(owned, ChargeShotRules.StatCritWindow));
        }

        public float Progress(float heldSeconds)
        {
            if (heldSeconds <= 0f || Full <= 0f)
                return 0f;
            float p = heldSeconds / Full;
            return p > 1f ? 1f : p;
        }

        public bool InWindow(float heldSeconds)
        {
            return heldSeconds + ChargeShotRules.EdgeEpsilon >= WindowEnter
                && heldSeconds - ChargeShotRules.EdgeEpsilon <= WindowExit;
        }

        public ChargeShotKind Resolve(float heldSeconds)
        {
            if (heldSeconds <= ChargeShotRules.MinChargeSeconds)
                return ChargeShotKind.None;
            if (InWindow(heldSeconds))
                return ChargeShotKind.Crit;
            if (heldSeconds + ChargeShotRules.EdgeEpsilon < WeakMax)
                return ChargeShotKind.Weak;
            return ChargeShotKind.Full;
        }

        /// <summary>Ring full (held ≥ Full).</summary>
        public bool Reached(float heldSeconds)
        {
            return heldSeconds + ChargeShotRules.EdgeEpsilon >= Full;
        }
    }

    /// <summary>
    /// Charge bow. Full charge (ring full) 0.70s at 0B. Weak-spot window = 76%–84% of the
    /// current full charge (0.532–0.588s at 0.70) + 鸿运 seconds on the upper bound. Normal
    /// damage needs ≥60% of full (0.42s at 0.70); shorter is weak ×0.50. Fire gate 0.20s.
    /// Recover 0.20s after fire (not scaled by 疾张). Values: 数值 2026-09-25 (PM approved).
    /// </summary>
    public static class ChargeShotRules
    {
        public const float RingFillSeconds = 0.70f;
        public const float ChargeSeconds = RingFillSeconds;
        /// <summary>Fire gate (unchanged). 凝神窥机 raises its own gate to GuaranteedCritActive.FocusMinChargeSeconds.</summary>
        public const float MinChargeSeconds = 0.20f;
        /// <summary>Normal (×1.0) damage needs held ≥ this share of the current full charge.</summary>
        public const float NormalMinPct = 0.60f;
        public const float WeakSpotEnterPct = 0.76f;
        public const float WeakSpotExitPct = 0.84f;
        public const float BaseDamage = 10f;
        public const float WeakMul = 0.50f;
        public const float FullMul = 1.00f;
        public const float CritMul = 2.00f;
        public const float RecoverSeconds = 0.20f;
        public const float WeakSpotStaggerSeconds = 0.50f;
        /// <summary>Float tolerance on window / threshold edges (0.70×0.76 is not exact in float).</summary>
        public const float EdgeEpsilon = 0.0005f;
        /// <summary>Floor on the stacked 疾张 multiplier.</summary>
        public const float MinChargeMul = 0.20f;
        public const string StatChargeTime = "charge_time";
        public const string StatCritWindow = "crit_window";

        /// <summary>DPS reference cycle at 0B (数值: 1.0s per shot); non-charge overhead = cycle − full, not scaled.</summary>
        public const float BaseShotCycleSeconds = 1.0f;
        public static float ShotCycleOverheadSeconds => BaseShotCycleSeconds - RingFillSeconds;
        /// <summary>DPS-alignment weak-spot hit rate (crit2 口径; gameplay start crit rate = 0).</summary>
        public const float AssumedWeakSpotRate = 0.30f;

        /// <summary>0B values (derived from the percentages; kept as names for existing callers).</summary>
        public static float WeakMaxSeconds => ChargeProfile.Base.WeakMax;
        public static float GreenEnterSeconds => ChargeProfile.Base.WindowEnter;
        public static float GreenExitSeconds => ChargeProfile.Base.WindowExit;
        public static float GreenEnter => GreenEnterSeconds;
        public static float GreenExit => GreenExitSeconds;

        public static float Progress(float heldSeconds)
        {
            return ChargeProfile.Base.Progress(heldSeconds);
        }

        public static ChargeShotKind Resolve(float heldSeconds)
        {
            return ChargeProfile.Base.Resolve(heldSeconds);
        }

        public static float Damage(ChargeShotKind kind)
        {
            switch (kind)
            {
                case ChargeShotKind.Weak: return BaseDamage * WeakMul;
                case ChargeShotKind.Full: return BaseDamage * FullMul;
                case ChargeShotKind.Crit: return BaseDamage * CritMul;
                default: return 0f;
            }
        }

        /// <summary>Apply loadout damage product on top of shot mul.</summary>
        public static float DamageWithBuild(ChargeShotKind kind, float damageProduct)
        {
            float baseShot = Damage(kind);
            if (damageProduct < 0.01f)
                damageProduct = 1f;
            return baseShot * damageProduct;
        }

        /// <summary>
        /// Wooden-dummy DPS (crit rate 0): BaseDamage × (w×CritMul + (1−w)×FullMul) / cycle,
        /// cycle = profile.Full + ShotCycleOverheadSeconds. 0B → 13.0 on the 1.0s cycle.
        /// </summary>
        public static float DummyDps(ChargeProfile profile)
        {
            float perShot = BaseDamage * (AssumedWeakSpotRate * CritMul + (1f - AssumedWeakSpotRate) * FullMul);
            float cycle = profile.Full + ShotCycleOverheadSeconds;
            return cycle > 0.01f ? perShot / cycle : 0f;
        }
    }
}
