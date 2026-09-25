using System;
using System.Collections.Generic;
using System.Text;
using RogueShooter.Build;
using RogueShooter.Player;

namespace RogueShooter.Combat
{
    /// <summary>
    /// 数值 2026-09-25 charge + 凝神窥机 acceptance. Everything is derived from ChargeShotRules /
    /// GuaranteedCritActive constants and the reward table (RewardCatalog), so a later retune only
    /// changes constants. Targets in comments are the values at the current constants.
    /// </summary>
    public static class ChargeAcceptanceChecks
    {
        /// <summary>数值: full charge 0.70 ± 0.02.</summary>
        public const float FullChargeTolerance = 0.02f;
        /// <summary>Dummy DPS tolerance (±5%).</summary>
        public const float DpsTolerancePct = 0.05f;
        /// <summary>数值 dummy targets on the 1.0s cycle: 0B 13, 1×R2L ≈13.9, 1×R2M ≈14.7.</summary>
        public const float Dps0Target = 13.0f;
        public const float DpsR2LTarget = 13.9f;
        public const float DpsR2MTarget = 14.7f;
        /// <summary>数值 stack sample: full 0.530s, DPS ≈ 15.7 (±5%). With the table values
        /// (R2L −0.0909, R2M −0.1667, multiplicative) this is R2L+R2M (0.70×0.9091×0.8333);
        /// 2×R2L would be 0.70×0.9091² = 0.579s / 14.8. Checked as R2L+R2M; see PR note.</summary>
        public const float StackFullTarget = 0.530f;
        public const float StackDpsTarget = 15.7f;

        const float Step = 0.002f;

        public static string Run()
        {
            string e = CheckFullCharge();
            if (e != null) return e;
            e = CheckWindow(ChargeProfile.Base, "0B");
            if (e != null) return e;
            e = CheckWeakMax(ChargeProfile.Base, "0B");
            if (e != null) return e;
            e = CheckR2Scaling();
            if (e != null) return e;
            e = CheckR3();
            if (e != null) return e;
            e = CheckDummyDps();
            if (e != null) return e;
            e = CheckFocusSkill();
            if (e != null) return e;
            return null;
        }

        static string CheckFullCharge()
        {
            ChargeProfile b = ChargeProfile.Base;
            if (Math.Abs(b.Full - ChargeShotRules.RingFillSeconds) > 0.0001f)
                return "0B full must be RingFillSeconds";
            if (b.Progress(b.Full) < 0.999f || b.Progress(b.Full - FullChargeTolerance) >= 0.999f)
                return "ring must fill exactly at full charge (±" + FullChargeTolerance + ")";
            if (!b.Reached(b.Full) || b.Reached(b.Full - FullChargeTolerance))
                return "full-charge reached edge";
            return null;
        }

        /// <summary>Scan held time: Crit exactly inside [Full×Enter, Full×Exit + bonus].</summary>
        static string CheckWindow(ChargeProfile p, string tag)
        {
            float lo, hi;
            if (!ScanCrit(p, out lo, out hi))
                return tag + " no weak-spot hit found";
            float wantLo = p.Full * ChargeShotRules.WeakSpotEnterPct;
            float wantHi = p.Full * ChargeShotRules.WeakSpotExitPct + p.WindowBonus;
            if (Math.Abs(lo - wantLo) > Step + ChargeShotRules.EdgeEpsilon || Math.Abs(hi - wantHi) > Step + ChargeShotRules.EdgeEpsilon)
                return tag + " weak-spot window scan [" + lo.ToString("0.000") + "," + hi.ToString("0.000")
                    + "] != [" + wantLo.ToString("0.000") + "," + wantHi.ToString("0.000") + "]";
            return null;
        }

        static string CheckWeakMax(ChargeProfile p, string tag)
        {
            float wm = p.Full * ChargeShotRules.NormalMinPct;
            if (p.Resolve(wm - 0.01f) != ChargeShotKind.Weak)
                return tag + " held < " + ChargeShotRules.NormalMinPct.ToString("0%") + " full must be weak";
            if (p.Resolve(wm + 0.001f) != ChargeShotKind.Full)
                return tag + " held ≥ " + ChargeShotRules.NormalMinPct.ToString("0%") + " full must be normal";
            if (p.Resolve(ChargeShotRules.MinChargeSeconds) != ChargeShotKind.None
                || p.Resolve(ChargeShotRules.MinChargeSeconds + 0.01f) != ChargeShotKind.Weak)
                return tag + " fire gate MinChargeSeconds";
            if (Math.Abs(ChargeShotRules.Damage(ChargeShotKind.Weak) - ChargeShotRules.BaseDamage * ChargeShotRules.WeakMul) > 0.001f)
                return "weak damage ×WeakMul";
            return null;
        }

        static string CheckR2Scaling()
        {
            foreach (string id in new[] { "R2L", "R2M" })
            {
                RewardRow row;
                if (!RewardCatalog.TryGet(id, out row) || row.Stat != ChargeShotRules.StatChargeTime)
                    return id + " must be charge_time";
                ChargeProfile p = ChargeProfile.FromOwned(new List<string> { id });
                float want = ChargeShotRules.RingFillSeconds * (1f + row.Value);
                if (Math.Abs(p.Full - want) > 0.0005f)
                    return id + " full " + p.Full.ToString("0.000") + " != " + want.ToString("0.000");
                string e = CheckWindow(p, id);
                if (e != null) return e;
                e = CheckWeakMax(p, id);
                if (e != null) return e;
                if (Math.Abs(p.WindowEnter / p.Full - ChargeShotRules.WeakSpotEnterPct) > 0.0005f
                    || Math.Abs(p.WindowExit / p.Full - ChargeShotRules.WeakSpotExitPct) > 0.0005f)
                    return id + " window must scale with full (same %)";
            }

            // Stacking is multiplicative.
            ChargeProfile both = ChargeProfile.FromOwned(new List<string> { "R2L", "R2M" });
            RewardRow l, m;
            RewardCatalog.TryGet("R2L", out l);
            RewardCatalog.TryGet("R2M", out m);
            if (Math.Abs(both.Full - ChargeShotRules.RingFillSeconds * (1f + l.Value) * (1f + m.Value)) > 0.0005f)
                return "R2L+R2M must stack multiplicatively";
            if (Math.Abs(both.Full - StackFullTarget) > 0.002f)
                return "R2L+R2M full " + both.Full.ToString("0.000") + " != " + StackFullTarget;
            float dStack = ChargeShotRules.DummyDps(both);
            if (!Near(dStack, StackDpsTarget))
                return "R2L+R2M dummy DPS " + dStack.ToString("0.00") + " !≈ " + StackDpsTarget;
            ChargeProfile twoL = ChargeProfile.FromOwned(new List<string> { "R2L", "R2L" });
            if (Math.Abs(twoL.Full - ChargeShotRules.RingFillSeconds * (1f + l.Value) * (1f + l.Value)) > 0.0005f)
                return "2×R2L must stack multiplicatively";
            string e2 = CheckWindow(twoL, "2xR2L");
            if (e2 != null) return e2;
            return null;
        }

        static string CheckR3()
        {
            RewardRow r3;
            if (!RewardCatalog.TryGet("R3", out r3) || r3.Stat != ChargeShotRules.StatCritWindow)
                return "R3 must be crit_window";
            ChargeProfile p = ChargeProfile.FromOwned(new List<string> { "R3" });
            if (Math.Abs(p.WindowBonus - r3.Value) > 0.0001f)
                return "R3 window bonus";
            if (Math.Abs(p.WindowEnter - ChargeProfile.Base.WindowEnter) > 0.0001f)
                return "R3 must not move the window start";
            return CheckWindow(p, "R3");
        }

        static string CheckDummyDps()
        {
            float d0 = ChargeShotRules.DummyDps(ChargeProfile.Base);
            if (!Near(d0, Dps0Target)) return "0B dummy DPS " + d0.ToString("0.00") + " !≈ " + Dps0Target;
            float dl = ChargeShotRules.DummyDps(ChargeProfile.FromOwned(new List<string> { "R2L" }));
            if (!Near(dl, DpsR2LTarget)) return "R2L dummy DPS " + dl.ToString("0.00") + " !≈ " + DpsR2LTarget;
            float dm = ChargeShotRules.DummyDps(ChargeProfile.FromOwned(new List<string> { "R2M" }));
            if (!Near(dm, DpsR2MTarget)) return "R2M dummy DPS " + dm.ToString("0.00") + " !≈ " + DpsR2MTarget;
            if (Math.Abs(ChargeShotRules.ShotCycleOverheadSeconds - (ChargeShotRules.BaseShotCycleSeconds - ChargeShotRules.RingFillSeconds)) > 0.0001f)
                return "cycle overhead = 1.0s − full (recover etc. not scaled)";
            return null;
        }

        static string CheckFocusSkill()
        {
            var s = new FocusSkillState();
            if (!s.IsReady || s.IsActive) return "focus starts ready";
            ChargeProfile b = ChargeProfile.Base;
            // Inactive: base kinds untouched.
            float mid = (b.WeakMax + b.WindowEnter) * 0.5f;
            if (s.ResolveShot(mid, b.Resolve(mid)) != ChargeShotKind.Full) return "focus inactive must not change kind";
            if (!s.TryActivate() || !s.IsActive) return "focus activate";
            if (s.TryActivate()) return "focus cannot re-activate while active";
            // ≥ min charge → weak-spot (Crit) whatever the base kind; window itself unchanged.
            float gate = GuaranteedCritActive.FocusMinChargeSeconds;
            foreach (float h in new[] { gate, gate + 0.01f, b.WeakMax - 0.01f, mid, b.Full, b.Full + 0.3f })
            {
                if (s.ResolveShot(h, b.Resolve(h)) != ChargeShotKind.Crit)
                    return "focus held " + h.ToString("0.000") + " must hit weak spot";
            }
            // < 0.30 while active → no shot (GDD §5.1 不足不发射; §10-5 silent).
            foreach (float h in new[] { ChargeShotRules.MinChargeSeconds + 0.01f, gate - 0.01f })
            {
                if (s.ResolveShot(h, b.Resolve(h)) != ChargeShotKind.None)
                    return "focus held " + h.ToString("0.000") + " < " + gate + "s must not fire";
            }
            if (b.InWindow(mid) || b.Resolve(mid) != ChargeShotKind.Full)
                return "focus must not auto-fill / move the window";
            if (Math.Abs(ChargeShotRules.RecoverSeconds - 0.20f) > 0.0001f) return "recover 0.20s unchanged";
            // Paused: frozen.
            s.Tick(5f, true);
            if (Math.Abs(s.BuffLeft - GuaranteedCritActive.DurationSeconds) > 0.0001f) return "focus timer must freeze while paused";
            // Duration then cooldown (from activation).
            s.Tick(GuaranteedCritActive.DurationSeconds - 0.01f, false);
            if (!s.IsActive) return "focus active for DurationSeconds";
            s.Tick(0.02f, false);
            if (s.IsActive) return "focus ends after DurationSeconds";
            float cdLeft = GuaranteedCritActive.CooldownSeconds - GuaranteedCritActive.DurationSeconds - 0.01f;
            if (Math.Abs(s.CooldownLeft - cdLeft) > 0.001f) return "focus CD counts from activation";
            if (s.TryActivate()) return "focus blocked during CD";
            if (s.ResolveShot(mid, b.Resolve(mid)) != ChargeShotKind.Full) return "focus expired must not force";
            s.Tick(s.CooldownLeft + 0.01f, false);
            if (!s.IsReady || !s.TryActivate()) return "focus ready after CD";
            // HUD copy.
            if (GuaranteedCritActive.HudText(0f, 0f).IndexOf("就绪", StringComparison.Ordinal) < 0
                || GuaranteedCritActive.HudText(12.34f, 107.2f).IndexOf("12.3s", StringComparison.Ordinal) < 0
                || GuaranteedCritActive.HudText(12.34f, 107.2f).IndexOf("108s", StringComparison.Ordinal) < 0
                || GuaranteedCritActive.HudText(0f, 96.5f).IndexOf("97s", StringComparison.Ordinal) < 0)
                return "focus HUD text (ready / duration / CD)";
            return null;
        }

        static bool ScanCrit(ChargeProfile p, out float lo, out float hi)
        {
            lo = -1f;
            hi = -1f;
            for (float t = 0f; t <= p.Full + 0.5f; t += Step)
            {
                if (p.Resolve(t) != ChargeShotKind.Crit)
                    continue;
                if (lo < 0f) lo = t;
                hi = t;
            }

            return lo >= 0f;
        }

        static bool Near(float v, float target)
        {
            return Math.Abs(v - target) <= target * DpsTolerancePct;
        }

        /// <summary>Evidence line with the numbers the acceptance used.</summary>
        public static string FormatValues()
        {
            var sb = new StringBuilder();
            Append(sb, "0B", ChargeProfile.Base);
            Append(sb, "R2L", ChargeProfile.FromOwned(new List<string> { "R2L" }));
            Append(sb, "R2M", ChargeProfile.FromOwned(new List<string> { "R2M" }));
            Append(sb, "R3", ChargeProfile.FromOwned(new List<string> { "R3" }));
            Append(sb, "R2L+R2M", ChargeProfile.FromOwned(new List<string> { "R2L", "R2M" }));
            Append(sb, "2xR2L", ChargeProfile.FromOwned(new List<string> { "R2L", "R2L" }));
            sb.Append("focus=").Append(GuaranteedCritActive.DurationSeconds.ToString("0")).Append("s/CD")
              .Append(GuaranteedCritActive.CooldownSeconds.ToString("0")).Append("s min")
              .Append(GuaranteedCritActive.FocusMinChargeSeconds.ToString("0.00")).Append("s(<min=no-shot) recover=")
              .Append(ChargeShotRules.RecoverSeconds.ToString("0.00")).Append("s");
            return sb.ToString();
        }

        static void Append(StringBuilder sb, string tag, ChargeProfile p)
        {
            float lo, hi;
            ScanCrit(p, out lo, out hi);
            sb.Append(tag).Append(" full=").Append(p.Full.ToString("0.000"))
              .Append(" weak<").Append(p.WeakMax.ToString("0.000"))
              .Append(" win=[").Append(p.WindowEnter.ToString("0.000")).Append(",").Append(p.WindowExit.ToString("0.000")).Append("]")
              .Append(" scan=[").Append(lo.ToString("0.000")).Append(",").Append(hi.ToString("0.000")).Append("]")
              .Append(" dummyDps=").Append(ChargeShotRules.DummyDps(p).ToString("0.00")).Append(" | ");
        }
    }
}
