using System;
using System.Runtime.CompilerServices;

namespace RogueShooter.Boss
{
    /// <summary>
    /// 终局 Boss 近战距离切换。返回 null 为通过。
    /// 击杀 60–120 秒不在这里用固定 DPS 木桩宣布达标。
    /// </summary>
    public static class FinalBossChecks
    {
        public static string Run()
        {
            string err = CheckLockedHp();
            if (err != null) return err;
            err = CheckDraftConstants();
            if (err != null) return err;
            err = CheckRangedScheduleUntouched();
            if (err != null) return err;
            err = CheckChooseBands();
            if (err != null) return err;
            err = CheckCloseMeleeNoProjectile();
            if (err != null) return err;
            err = CheckFarP1OrderAndTiming();
            if (err != null) return err;
            err = CheckFarP2OrderAndTiming();
            if (err != null) return err;
            err = CheckNoGapBelowThree();
            if (err != null) return err;
            err = CheckRangedStillNeedsView();
            if (err != null) return err;
            err = CheckWindupRecoveryAndSingleHit();
            if (err != null) return err;
            err = CheckWhiffFacingAndRecovery();
            if (err != null) return err;
            err = CheckReplacedMoveNotRepeated();
            if (err != null) return err;
            err = CheckInProgressNotInterrupted();
            if (err != null) return err;
            err = CheckIFrameBlocks();
            if (err != null) return err;
            err = CheckPhaseCutDoesNotInterrupt();
            if (err != null) return err;
            err = CheckP2MeleeAdvances();
            if (err != null) return err;
            err = CheckHpUntouched();
            if (err != null) return err;
            err = CheckLiveEntry();
            if (err != null) return err;
            return null;
        }

        public static string FormatPass()
        {
            return "ACCEPTANCE PASS final-boss melee ≤"
                + FinalBossRules.MeleeTriggerDistance.ToString("0.0")
                + "u dmg=" + FinalBossRules.MeleeDamage.ToString("0")
                + " melee-draft-unlocked locked-hp=" + FinalBossRules.MaxHp.ToString("0")
                + " ranged P1 nail/charge P2 triple/ring"
                + " live=BossFightDriver";
        }

        static string CheckLockedHp()
        {
            string err = Require(FinalBossRules.MaxHp, 1200f, "locked hp changed");
            if (err != null) return err;
            err = Require(BossBrain.DefaultMaxHp, FinalBossRules.MaxHp, "default pool is not the locked hp");
            return err;
        }

        static string CheckDraftConstants()
        {
            string err = Require(FinalBossRules.NailBowDamage, 20f, "nail damage changed");
            if (err != null) return err;
            err = Require(FinalBossRules.ShieldChargeDamage, 25f, "charge damage changed");
            if (err != null) return err;
            err = Require(FinalBossRules.TripleArrowDamage, 18f, "triple damage changed");
            if (err != null) return err;
            err = Require(FinalBossRules.RingBoltDamage, 15f, "ring damage changed");
            if (err != null) return err;
            err = Require(FinalBossRules.RangedMinOpenDistance, 3f, "ranged 3u gate relaxed");
            if (err != null) return err;
            err = Require(BossBrain.Phase2HpFrac, 0.5f, "phase cut changed");
            if (err != null) return err;

            err = Require(FinalBossRules.MeleeTriggerDistance, 4.0f, "melee distance draft");
            if (err != null) return err;
            err = Require(FinalBossRules.MeleeWindupSeconds, 0.70f, "melee windup draft");
            if (err != null) return err;
            err = Require(FinalBossRules.MeleeHitTimeSeconds, 0.70f, "melee hit time draft");
            if (err != null) return err;
            err = Require(FinalBossRules.MeleeHitTimeSeconds, FinalBossRules.MeleeWindupSeconds, "hit time must be the end of windup");
            if (err != null) return err;
            err = Require(FinalBossRules.MeleeActiveSeconds, 0.05f, "melee active draft");
            if (err != null) return err;
            err = Require(FinalBossRules.MeleeRecoverySeconds, 0.80f, "melee recovery draft");
            if (err != null) return err;
            err = Require(FinalBossRules.MeleeDamage, 30f, "melee damage draft");
            if (err != null) return err;
            err = Require(FinalBossRules.MeleeHits, 1, "melee hits draft");
            if (err != null) return err;
            err = Require(FinalBossRules.ProjectileVolley(FinalBossMoveId.Melee), 0, "melee volley");
            if (err != null) return err;

            if (Outside(FinalBossRules.MeleeTriggerDistance, 3f, 5f))
                return "trigger outside [3,5]";
            if (Below(FinalBossRules.MeleeTriggerDistance, FinalBossRules.RangedMinOpenDistance))
                return "gap under 3u";
            if (Outside(FinalBossRules.MeleeWindupSeconds, 0.60f, 0.85f))
                return "windup outside accept";
            if (Outside(FinalBossRules.MeleeActiveSeconds, 0.03f, 0.08f))
                return "active outside accept";
            if (Outside(FinalBossRules.MeleeRecoverySeconds, 0.70f, 1.00f))
                return "recovery outside accept";
            if (Outside(FinalBossRules.MeleeDamage, 26f, 33f))
                return "damage outside accept";
            return null;
        }

        static string Require(float actual, float expect, string reason)
        {
            float delta = actual - expect;
            if (delta > 0.0001f || delta < -0.0001f)
                return reason;
            return null;
        }

        static string Require(int actual, int expect, string reason)
        {
            if (actual != expect)
                return reason;
            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool Outside(float value, float min, float max)
        {
            return value < min || value > max;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool Below(float value, float other)
        {
            return value < other;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool Below(int value, int other)
        {
            return value < other;
        }

        static string CheckRangedScheduleUntouched()
        {
            string err = MatchTiming(BossMoveId.StraightShot, FinalBossMoveId.NailBow, 0.5f, 0.4f, 0.3f);
            if (err != null) return err;
            err = MatchTiming(BossMoveId.WarningCharge, FinalBossMoveId.ShieldCharge, 0.7f, 0.35f, 0.5f);
            if (err != null) return err;
            err = MatchTiming(BossMoveId.TripleShot, FinalBossMoveId.TripleArrow, 0.4f, 0.45f, 0.4f);
            if (err != null) return err;
            err = MatchTiming(BossMoveId.RingBurst, FinalBossMoveId.RingBolt, 0.8f, 0.35f, 0.6f);
            if (err != null) return err;

            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.Melee, out windup, out active, out recovery);
            err = Require(windup, 0.70f, "melee timing");
            if (err != null) return err;
            err = Require(active, 0.05f, "melee timing");
            if (err != null) return err;
            err = Require(recovery, 0.80f, "melee timing");
            if (err != null) return err;
            FinalBossRules.Timing(FinalBossMoveId.NailBow, out windup, out active, out recovery);
            if (recovery > 0.3f + 0.01f)
                return "melee recovery inserted into nail";
            err = Require(FinalBossRules.NailBowVolley, 1, "shot volley changed");
            if (err != null) return err;
            err = Require(FinalBossRules.TripleArrowVolley, 3, "shot volley changed");
            if (err != null) return err;
            err = Require(FinalBossRules.ShieldChargeVolley, 0, "charge volley changed");
            if (err != null) return err;
            if (Below(FinalBossRules.RingBoltVolley, 1))
                return "ring spawns nothing";
            return null;
        }

        static string MatchTiming(BossMoveId brain, FinalBossMoveId finalMove, float windup, float active, float recovery)
        {
            float w;
            float a;
            float r;
            BossBrain.TimingsFor(brain, out w, out a, out r);
            string err = Require(w, windup, brain + " timing changed");
            if (err != null) return err;
            err = Require(a, active, brain + " timing changed");
            if (err != null) return err;
            err = Require(r, recovery, brain + " timing changed");
            if (err != null) return err;
            float w2;
            float a2;
            float r2;
            FinalBossRules.Timing(finalMove, out w2, out a2, out r2);
            err = Require(w2, w, finalMove + " timing forked");
            if (err != null) return err;
            err = Require(a2, a, finalMove + " timing forked");
            if (err != null) return err;
            err = Require(r2, r, finalMove + " timing forked");
            return err;
        }

        static string CheckChooseBands()
        {
            if (FinalBossRules.Choose(4f, true) != FinalBossOpenChoice.Melee)
                return "exactly 4.0 with view must melee";
            if (FinalBossRules.Choose(4f, false) != FinalBossOpenChoice.Melee)
                return "exactly 4.0 without view must melee";
            if (FinalBossRules.Choose(4.01f, true) != FinalBossOpenChoice.Ranged)
                return "just outside 4.0 must stay ranged";
            if (FinalBossRules.Choose(3f, false) != FinalBossOpenChoice.Melee)
                return "3u must not stall";
            if (FinalBossRules.Choose(0f, false) != FinalBossOpenChoice.Melee)
                return "0u must open melee";
            if (FinalBossRules.Choose(6f, false) != FinalBossOpenChoice.Hold)
                return "ranged view gate relaxed";
            if (FinalBossRules.Choose(6f, true) != FinalBossOpenChoice.Ranged)
                return "far in-view must range";
            if (FinalBossRules.Choose(FinalBossRules.RangedMinOpenDistance - 0.5f, false) != FinalBossOpenChoice.Melee)
                return "gap below ranged open";
            return null;
        }

        static string CheckCloseMeleeNoProjectile()
        {
            FinalBossCombat seen = NewAt(4f, true);
            seen.Tick(0f);
            if (seen.CurrentMove != FinalBossMoveId.Melee)
                return "4.0 opened ranged";
            if (seen.ReplacedMove != FinalBossMoveId.NailBow)
                return "4.0 did not take the nail beat";
            seen.Tick(FinalBossRules.MeleeWindupSeconds + 0.01f);
            if (seen.CurrentMove != FinalBossMoveId.Melee)
                return "4.0 left melee";
            if (seen.BeatProjectiles != 0 || seen.ProjectilesAlive != 0)
                return "melee spawned a projectile";
            if (seen.BeatDamage != 0f && seen.BeatDamage != FinalBossRules.MeleeDamage)
                return "melee used ranged damage";
            if (seen.BeatDamage == FinalBossRules.NailBowDamage && seen.BeatDamage != FinalBossRules.MeleeDamage)
                return "melee dealt nail damage";

            FinalBossCombat hidden = NewAt(1.5f, false);
            hidden.Tick(0f);
            if (hidden.CurrentMove != FinalBossMoveId.Melee || hidden.BeatProjectiles != 0)
                return "close out of view did not melee";
            return null;
        }

        static string CheckFarP1OrderAndTiming()
        {
            FinalBossCombat c = NewAt(8f, true);
            c.Tick(0f);
            string err = RunBeat(c, FinalBossMoveId.NailBow, FinalBossMoveId.ShieldCharge, FinalBossRules.NailBowVolley);
            if (err != null) return err;
            err = RunBeat(c, FinalBossMoveId.ShieldCharge, FinalBossMoveId.NailBow, FinalBossRules.ShieldChargeVolley);
            if (err != null) return err;
            err = RunBeat(c, FinalBossMoveId.NailBow, FinalBossMoveId.ShieldCharge, FinalBossRules.NailBowVolley);
            if (err != null) return err;
            return null;
        }

        static string CheckFarP2OrderAndTiming()
        {
            FinalBossCombat c = NewAt(8f, true);
            c.ApplyDamage(c.MaxHp * BossBrain.Phase2HpFrac + 1f);
            if (c.Phase != FinalBossPhase.P2 || c.MaxHp != 1200f)
                return "p2 enter";
            c.Tick(0f);
            string err = RunBeat(c, FinalBossMoveId.TripleArrow, FinalBossMoveId.RingBolt, FinalBossRules.TripleArrowVolley);
            if (err != null) return err;
            err = RunBeat(c, FinalBossMoveId.RingBolt, FinalBossMoveId.TripleArrow, FinalBossRules.RingBoltVolley);
            if (err != null) return err;
            return null;
        }

        static string CheckNoGapBelowThree()
        {
            FinalBossCombat under = NewAt(2.5f, false);
            under.Tick(0f);
            if (under.CurrentMove != FinalBossMoveId.Melee)
                return "under 3u stalled";
            FinalBossCombat zero = NewAt(0f, false);
            zero.Tick(0f);
            if (zero.CurrentMove != FinalBossMoveId.Melee)
                return "0u stalled";
            FinalBossCombat atThree = NewAt(3f, false);
            atThree.Tick(0f);
            if (atThree.CurrentMove != FinalBossMoveId.Melee)
                return "3u stalled";
            return null;
        }

        static string CheckRangedStillNeedsView()
        {
            FinalBossCombat c = NewAt(6f, false);
            int p1 = c.P1Index;
            c.Tick(0.5f);
            c.Tick(0.5f);
            if (c.CurrentMove != FinalBossMoveId.None || c.Step != FinalBossStep.Idle)
                return "opened ranged without body and crown in view";
            if (c.P1Index != p1)
                return "hold advanced the pointer";
            c.BodyAndCrownInView = true;
            c.Tick(0f);
            if (c.CurrentMove != FinalBossMoveId.NailBow || c.P1Index != p1 + 1)
                return "in-view far did not open the pending ranged beat";
            return null;
        }

        static string CheckWindupRecoveryAndSingleHit()
        {
            FinalBossCombat c = NewAt(2f, false);
            c.Tick(0f);
            c.Tick(0.69f);
            if (c.MoveTime >= FinalBossRules.MeleeHitTimeSeconds || c.BeatDamage != 0f)
                return "damage before windup ended";
            c.Tick(0.02f);
            if (c.BeatDamage != FinalBossRules.MeleeDamage || c.BeatHits != 1)
                return "hit was not the draft 30 once at windup end";
            c.Tick(0.01f);
            c.Tick(0.01f);
            if (c.BeatHits != 1 || c.BeatDamage != FinalBossRules.MeleeDamage)
                return "melee hit more than once";
            if (c.BeatProjectiles != 0)
                return "hit spawned a projectile";

            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.Melee, out windup, out active, out recovery);
            float remain = (windup + active + recovery - 0.02f) - c.MoveTime;
            if (remain > 0f)
                c.Tick(remain);
            if (c.CurrentMove != FinalBossMoveId.Melee || c.Step != FinalBossStep.Recovery)
                return "recovery ended early";
            if (c.BeatDamage != FinalBossRules.MeleeDamage)
                return "recovery changed damage";
            return null;
        }

        static string CheckWhiffFacingAndRecovery()
        {
            FinalBossCombat c = NewAt(2f, false);
            c.Tick(0f);
            float fx = c.FacingX;
            float fy = c.FacingY;
            if (fx < 0.9f)
                return "did not face the player at windup";
            c.SetPlayerPosition(c.BossX, c.BossY + 6f);
            c.BodyAndCrownInView = true;
            c.Tick(0.1f);
            if (c.CurrentMove != FinalBossMoveId.Melee)
                return "windup switched back to ranged";
            if (c.FacingX != fx || c.FacingY != fy)
                return "facing changed during windup";
            if (c.BeatDamage != 0f)
                return "windup dealt damage after the player left";

            c.PlacePlayerAtDistance(10f);
            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.Melee, out windup, out active, out recovery);
            float total = windup + active + recovery;
            float keep = (total - 0.02f) - c.MoveTime;
            c.Tick(keep);
            if (c.CurrentMove != FinalBossMoveId.Melee || c.Step != FinalBossStep.Recovery)
                return "recovery cancelled";
            if (c.BeatDamage != 0f || !c.BeatWhiff)
                return "leave during windup should whiff";
            if (c.BeatProjectiles != 0)
                return "whiff spawned a projectile";
            c.Tick(0.04f);
            if (c.LastBeatMove != FinalBossMoveId.Melee || !c.LastBeatWhiff || c.LastBeatProjectiles != 0)
                return "whiff was not kept";
            if (c.LastReplacedMove != FinalBossMoveId.NailBow)
                return "whiff did not consume the nail beat";
            if (c.CurrentMove != FinalBossMoveId.ShieldCharge)
                return "after whiff the next beat was not the charge";
            return null;
        }

        static string CheckReplacedMoveNotRepeated()
        {
            FinalBossCombat c = NewAt(2f, true);
            c.Tick(0f);
            if (c.CurrentMove != FinalBossMoveId.Melee || c.ReplacedMove != FinalBossMoveId.NailBow)
                return "first close beat";
            c.PlacePlayerAtDistance(8f);
            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.Melee, out windup, out active, out recovery);
            c.Tick(windup + active + recovery);
            if (c.LastReplacedMove != FinalBossMoveId.NailBow)
                return "replaced nail was lost";
            if (c.CurrentMove != FinalBossMoveId.ShieldCharge)
                return "nail repeated after melee";
            return null;
        }

        static string CheckInProgressNotInterrupted()
        {
            FinalBossCombat c = NewAt(8f, true);
            c.Tick(0f);
            c.Tick(0.2f);
            if (c.CurrentMove != FinalBossMoveId.NailBow || c.Step != FinalBossStep.Windup)
                return "nail did not start";
            c.PlacePlayerAtDistance(1f);
            c.BodyAndCrownInView = false;
            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.NailBow, out windup, out active, out recovery);
            float total = windup + active + recovery;
            c.Tick((total - 0.02f) - c.MoveTime);
            if (c.CurrentMove != FinalBossMoveId.NailBow)
                return "in-progress nail became melee";
            if (c.BeatProjectiles != FinalBossRules.NailBowVolley)
                return "committed nail did not fire";
            c.Tick(0.04f);
            if (c.LastBeatMove != FinalBossMoveId.NailBow || c.LastBeatProjectiles != FinalBossRules.NailBowVolley)
                return "nail beat was replaced mid-cast";
            if (c.CurrentMove != FinalBossMoveId.Melee || c.ReplacedMove != FinalBossMoveId.ShieldCharge)
                return "next beat did not read the new distance";
            return null;
        }

        static string CheckIFrameBlocks()
        {
            FinalBossCombat c = NewAt(2f, false);
            c.PlayerInvulnerable = true;
            c.Tick(0f);
            c.Tick(FinalBossRules.MeleeWindupSeconds + FinalBossRules.MeleeActiveSeconds);
            if (c.BeatDamage != 0f || c.BeatHits != 0 || !c.BeatWhiff)
                return "existing iframe still hit";
            if (c.CurrentMove != FinalBossMoveId.Melee)
                return "iframe changed the move";

            c.PlayerInvulnerable = false;
            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.Melee, out windup, out active, out recovery);
            float remain = (windup + active + recovery) - c.MoveTime;
            if (remain > 0f)
                c.Tick(remain);
            if (c.CurrentMove != FinalBossMoveId.Melee)
                return "second beat was not melee";
            c.Tick(FinalBossRules.MeleeHitTimeSeconds);
            if (c.BeatDamage != FinalBossRules.MeleeDamage || c.BeatHits != 1)
                return "iframe stayed on after the existing window";
            return null;
        }

        static string CheckPhaseCutDoesNotInterrupt()
        {
            FinalBossCombat boundary = new FinalBossCombat();
            boundary.ApplyDamage(boundary.MaxHp * BossBrain.Phase2HpFrac - 1f);
            if (boundary.Phase != FinalBossPhase.P1 || boundary.MaxHp != 1200f)
                return "phase cut moved earlier";
            boundary.ApplyDamage(1f);
            if (boundary.Phase != FinalBossPhase.P2 || boundary.Hp <= 0f)
                return "phase cut moved later";

            FinalBossCombat c = NewAt(8f, true);
            c.Tick(0f);
            c.Tick(0.2f);
            c.ApplyDamage(c.MaxHp * BossBrain.Phase2HpFrac + 1f);
            if (c.Phase != FinalBossPhase.P2)
                return "did not enter p2";
            if (c.CurrentMove != FinalBossMoveId.NailBow)
                return "phase change cancelled the beat";
            if (c.MaxHp != 1200f)
                return "phase change retuned hp";

            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.NailBow, out windup, out active, out recovery);
            float total = windup + active + recovery;
            c.Tick((total - 0.02f) - c.MoveTime);
            if (c.CurrentMove != FinalBossMoveId.NailBow)
                return "phase change interrupted nail";
            c.Tick(0.04f);
            if (c.LastBeatMove != FinalBossMoveId.NailBow)
                return "nail did not finish";
            if (c.CurrentMove != FinalBossMoveId.TripleArrow)
                return "p2 did not take the next beat";
            return null;
        }

        static string CheckP2MeleeAdvances()
        {
            FinalBossCombat c = NewAt(2f, false);
            c.ApplyDamage(c.MaxHp * BossBrain.Phase2HpFrac);
            if (c.Phase != FinalBossPhase.P2)
                return "p2";
            c.Tick(0f);
            if (c.CurrentMove != FinalBossMoveId.Melee || c.ReplacedMove != FinalBossMoveId.TripleArrow)
                return "p2 close did not replace triple";
            c.PlacePlayerAtDistance(8f);
            c.BodyAndCrownInView = true;
            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.Melee, out windup, out active, out recovery);
            c.Tick(windup + active + recovery);
            if (c.LastBeatProjectiles != 0 || c.LastReplacedMove != FinalBossMoveId.TripleArrow)
                return "p2 melee fired the triple";
            if (c.CurrentMove != FinalBossMoveId.RingBolt)
                return "p2 repeated triple";
            return null;
        }

        /// <summary>
        /// 场景入口 BossFightDriver.BeginEnter / Update 调用 FinalBossLive.Begin / Step。
        /// 这里走同一对方法。单独 new FinalBossCombat 不算接上。
        /// </summary>
        static string CheckLiveEntry()
        {
            string err = Require(FinalBossRules.MaxHp, 1200f, "locked hp changed");
            if (err != null) return err;
            err = Require(BossBrain.DefaultMaxHp, FinalBossRules.MaxHp, "default pool is not the locked hp");
            if (err != null) return err;
            // Scale stub stays 3850. The live fight must not keep that MaxHp.
            err = Require(BossScaleTable.BaseHp, 3850f, "scale stub base hp changed");
            if (err != null) return err;

            BossBrain brain;
            FinalBossCombat combat;
            err = EnterLive(out brain, out combat);
            if (err != null) return err;
            if (brain.MaxHp == BossScaleTable.BaseHp || combat.MaxHp == BossScaleTable.BaseHp)
                return "live fight kept scaled hp";
            err = Require(brain.MaxHp, 1200f, "live max hp");
            if (err != null) return err;
            err = Require(brain.Hp, 1200f, "live hp");
            if (err != null) return err;
            err = Require(combat.MaxHp, 1200f, "combat max hp");
            if (err != null) return err;
            err = Require(combat.Hp, 1200f, "combat hp");
            if (err != null) return err;
            if (!brain.HpLocked || brain.Phase != BossPhase.Entering || brain.CurrentMove != BossMoveId.None)
                return "enter started a brain move";

            float lockedHp = brain.MaxHp;
            brain.NotifyTimeCross(11f);
            err = Require(brain.MaxHp, lockedHp, "cross changed live max hp");
            if (err != null) return err;
            err = Require(combat.MaxHp, lockedHp, "cross changed combat max hp");
            if (err != null) return err;
            err = Require(brain.Hp, lockedHp, "cross changed live hp");
            if (err != null) return err;

            err = LiveCloseThenFar(brain, combat);
            if (err != null) return err;

            err = EnterLive(out brain, out combat);
            if (err != null) return err;
            err = LiveHoldThenNail(brain, combat);
            if (err != null) return err;

            err = EnterLive(out brain, out combat);
            if (err != null) return err;
            err = LiveP2Melee(brain, combat);
            if (err != null) return err;
            return null;
        }

        static string EnterLive(out BossBrain brain, out FinalBossCombat combat)
        {
            brain = new BossBrain();
            brain.Configure(BossBrain.DefaultMaxHp);
            combat = null;
            if (brain.MaxHp != FinalBossRules.MaxHp)
                return "default pool is not the locked hp";
            var snap = new BossScaleSnapshot
            {
                AnchorId = "A",
                BuildCount = 14,
                TimeTier = 2,
                MaxHp = BossScaleTable.BaseHp,
                DmgMul = 1.28f,
                HpMul = 1f
            };
            if (snap.MaxHp != BossScaleTable.BaseHp || snap.MaxHp == FinalBossRules.MaxHp)
                return "fixture was not the scale stub hp";
            combat = FinalBossLive.Begin(brain, ref snap);
            if (combat == null)
                return "live begin missing combat";
            if (snap.MaxHp == BossScaleTable.BaseHp)
                return "begin left scaled max hp";
            string err = Require(snap.MaxHp, 1200f, "begin snapshot hp");
            if (err != null) return err;
            err = Require(brain.LiveDmgMul, 1.28f, "begin dropped dmg mul");
            if (err != null) return err;
            return null;
        }

        static string LiveCloseThenFar(BossBrain brain, FinalBossCombat combat)
        {
            var seal = FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, 4f, 0f, false, false, false);
            if (seal != FinalBossFightClock.DoorSealed)
                return "first live tick should only seal";
            string err = Require(brain.MaxHp, 1200f, "door seal max hp");
            if (err != null) return err;
            err = Require(brain.Hp, 1200f, "door seal hp");
            if (err != null) return err;
            err = Require(combat.MaxHp, 1200f, "door seal combat max hp");
            if (err != null) return err;
            err = Require(combat.Hp, 1200f, "door seal combat hp");
            if (err != null) return err;
            if (brain.Phase != BossPhase.DoorSealed || brain.CurrentMove != BossMoveId.None || brain.MovesCompleted != 0)
                return "door tick started brain rotation";
            if (combat.CurrentMove != FinalBossMoveId.None)
                return "door tick opened a beat";

            var missing = FinalBossLive.Step(brain, combat, 0.02f, false, 0f, 0f, 0f, 0f, false, false, false);
            if (missing != FinalBossFightClock.NoPlayer || combat.CurrentMove != FinalBossMoveId.None)
                return "missing player opened a beat";

            var opened = FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, 4f, 0f, false, false, false);
            if (opened != FinalBossFightClock.Ticked)
                return "close beat did not tick";
            if (combat.CurrentMove != FinalBossMoveId.Melee || combat.BeatProjectiles != 0)
                return "live close did not melee";
            if (combat.ReplacedMove != FinalBossMoveId.NailBow || combat.P1Index != 1)
                return "live melee did not take the nail slot";
            if (brain.Phase != BossPhase.DoorSealed || brain.CurrentMove != BossMoveId.None)
                return "live fight still on brain rotation";

            FinalBossLive.Step(brain, combat, 0.2f, true, 0f, 0f, 8f, 0f, true, true, false);
            if (combat.CurrentMove != FinalBossMoveId.Melee)
                return "windup switched back to ranged";

            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.Melee, out windup, out active, out recovery);
            float left = windup + active + recovery - combat.MoveTime;
            FinalBossLive.Step(brain, combat, left, true, 0f, 0f, 8f, 0f, true, true, false);
            if (combat.LastBeatMove != FinalBossMoveId.Melee || !combat.LastBeatWhiff || combat.LastBeatProjectiles != 0)
                return "live whiff fired a projectile";
            if (combat.LastReplacedMove != FinalBossMoveId.NailBow)
                return "live replaced nail was dropped";
            if (combat.CurrentMove != FinalBossMoveId.ShieldCharge)
                return "live pointer did not advance";
            if (combat.BeatProjectiles != FinalBossRules.ShieldChargeVolley)
                return "live charge spawned a volley";
            if (brain.CurrentMove != BossMoveId.None || brain.Phase == BossPhase.P1)
                return "brain rotation ran beside live combat";
            return null;
        }

        static string LiveHoldThenNail(BossBrain brain, FinalBossCombat combat)
        {
            FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, 8f, 0f, true, false, false);
            var held = FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, 8f, 0f, true, false, false);
            if (held != FinalBossFightClock.Ticked)
                return "hold did not reach combat";
            if (combat.CurrentMove != FinalBossMoveId.None || combat.P1Index != 0)
                return "far without crown opened ranged";

            FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, 8f, 0f, true, true, false);
            if (combat.CurrentMove != FinalBossMoveId.NailBow || combat.Step != FinalBossStep.Windup)
                return "far in view did not open nail";
            FinalBossLive.Step(brain, combat, 0.1f, true, 0f, 0f, 1f, 0f, true, true, false);
            if (combat.CurrentMove != FinalBossMoveId.NailBow || combat.BeatProjectiles != 0)
                return "in-progress nail was interrupted";

            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(FinalBossMoveId.NailBow, out windup, out active, out recovery);
            float intoVolley = windup - combat.MoveTime;
            FinalBossLive.Step(brain, combat, intoVolley, true, 0f, 0f, 1f, 0f, true, true, false);
            if (combat.CurrentMove != FinalBossMoveId.NailBow)
                return "nail changed when the player walked in";
            if (combat.BeatProjectiles != FinalBossRules.NailBowVolley)
                return "in-progress nail skipped its volley";
            if (brain.CurrentMove != BossMoveId.None)
                return "brain played the nail";
            return null;
        }

        static string LiveP2Melee(BossBrain brain, FinalBossCombat combat)
        {
            FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, 2f, 0f, false, false, false);
            FinalBossLive.ApplyDamage(combat, brain, combat.MaxHp * BossBrain.Phase2HpFrac + 1f);
            if (combat.Phase != FinalBossPhase.P2)
                return "live phase cut missed";
            if (brain.Phase != BossPhase.DoorSealed || brain.CurrentMove != BossMoveId.None)
                return "live phase cut started a brain phase";
            string err = Require(brain.Hp, combat.Hp, "live hp pools diverged");
            if (err != null) return err;
            err = Require(brain.MaxHp, 1200f, "p2 changed max hp");
            if (err != null) return err;

            var opened = FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, 2f, 0f, false, false, false);
            if (opened != FinalBossFightClock.Ticked)
                return "p2 close did not tick";
            if (combat.CurrentMove != FinalBossMoveId.Melee || combat.ReplacedMove != FinalBossMoveId.TripleArrow)
                return "p2 close did not melee";
            if (combat.BeatProjectiles != 0)
                return "p2 melee spawned a projectile";
            if (brain.Phase == BossPhase.P2 || brain.CurrentMove != BossMoveId.None)
                return "p2 still driven by brain";
            return null;
        }

        static string CheckHpUntouched()
        {
            FinalBossCombat c = NewAt(2f, false);
            float hp = c.Hp;
            c.Tick(0f);
            c.Tick(FinalBossRules.MeleeHitTimeSeconds);
            if (c.MaxHp != 1200f || c.Hp != hp)
                return "melee changed boss hp";
            if (c.BeatDamage != FinalBossRules.MeleeDamage)
                return "player-facing hit missing";
            return null;
        }

        static string RunBeat(FinalBossCombat c, FinalBossMoveId expect, FinalBossMoveId next, int projectiles)
        {
            if (c.CurrentMove != expect)
                return "expected " + expect + " got " + c.CurrentMove;
            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(expect, out windup, out active, out recovery);
            float total = windup + active + recovery;
            c.Tick(total - 0.02f);
            if (c.CurrentMove != expect)
                return expect + " ended early";
            if (c.BeatProjectiles != projectiles)
                return expect + " projectiles " + c.BeatProjectiles;
            c.Tick(0.04f);
            if (c.LastBeatMove != expect || c.LastBeatProjectiles != projectiles)
                return expect + " beat record";
            if (c.CurrentMove != next)
                return "after " + expect + " got " + c.CurrentMove;
            return null;
        }

        static FinalBossCombat NewAt(float distance, bool inView)
        {
            var c = new FinalBossCombat();
            c.PlacePlayerAtDistance(distance);
            c.BodyAndCrownInView = inView;
            c.PlayerInvulnerable = false;
            return c;
        }
    }
}
