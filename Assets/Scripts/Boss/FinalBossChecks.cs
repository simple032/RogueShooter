using System;

namespace RogueShooter.Boss
{
    /// <summary>
    /// Headless checks for the seven-skill final boss. Null means pass.
    /// Reference enter is B14 at pressure 1.55, which the 1200 formula resolves to exactly 1200.
    /// </summary>
    public static class FinalBossChecks
    {
        public static string Run()
        {
            string err = CheckLockedHp();
            if (err != null) return err;
            err = CheckScaleShape();
            if (err != null) return err;
            err = CheckDirectionalDamage();
            if (err != null) return err;
            err = CheckP2NoFrontDr();
            if (err != null) return err;
            err = CheckRangedCounter();
            if (err != null) return err;
            err = CheckTripleOneHit();
            if (err != null) return err;
            err = CheckPhaseOnce();
            if (err != null) return err;
            err = CheckLethalSkips();
            if (err != null) return err;
            err = CheckMeleeFanAndCharge();
            if (err != null) return err;
            err = CheckLeapLock();
            if (err != null) return err;
            err = CheckNoRingOrFourMetreMelee();
            if (err != null) return err;
            err = CheckSkillTable();
            if (err != null) return err;
            return null;
        }

        public static string FormatPass()
        {
            return "PASS final-boss redesign hp=" + FinalBossRules.BaseHp.ToString("0")
                + " ref-enter=" + FinalBossRules.EnterMaxHp(14, FinalBossRules.PressureRef).ToString("0")
                + " P1 single/burst/charge/bash P2 triple/leap/bash";
        }

        static string CheckLockedHp()
        {
            if (Math.Abs(FinalBossRules.BaseHp - 1200f) > 0.01f || Math.Abs(FinalBossRules.MaxHp - 1200f) > 0.01f)
                return "base hp is not locked 1200";
            if (Math.Abs(FinalBossRules.BaseHp - 1100f) < 0.01f || Math.Abs(BossScaleTable.BaseHp - 1100f) < 0.01f)
                return "1100 still used as final-boss hp";
            if (Math.Abs(BossScaleTable.BaseHp - 3850f) < 0.01f || Math.Abs(BossBrain.DefaultMaxHp - 3850f) < 0.01f)
                return "3850 still used as final-boss hp";
            if (Math.Abs(BossScaleTable.BaseHp - FinalBossRules.BaseHp) > 0.01f)
                return "scale base diverged from FinalBossRules";
            if (Math.Abs(BossBrain.DefaultMaxHp - FinalBossRules.MaxHp) > 0.01f)
                return "brain default diverged";

            BossBrain brain;
            FinalBossCombat combat;
            BossScaleSnapshot snap;
            string err = EnterReference(out brain, out combat, out snap);
            if (err != null) return err;
            err = Require(snap.MaxHp, 1200f, "enter snapshot hp");
            if (err != null) return err;
            err = Require(brain.MaxHp, 1200f, "brain max after enter");
            if (err != null) return err;
            err = Require(brain.Hp, 1200f, "brain hp after enter");
            if (err != null) return err;
            err = Require(combat.MaxHp, 1200f, "combat max after enter");
            if (err != null) return err;
            err = Require(combat.Hp, 1200f, "combat hp after enter");
            if (err != null) return err;
            if (!brain.HpLocked || brain.Phase != BossPhase.Entering)
                return "enter did not lock";

            var poisoned = new BossScaleSnapshot { BuildCount = 14, Tm = 1.55f, MaxHp = 3850f, DmgMul = 9f };
            var again = new BossBrain();
            again.Configure(3850f);
            FinalBossCombat rewritten = FinalBossLive.Begin(again, ref poisoned);
            err = Require(poisoned.MaxHp, 1200f, "begin left 3850");
            if (err != null) return err;
            err = Require(again.MaxHp, 1200f, "brain kept 3850");
            if (err != null) return err;
            err = Require(rewritten.Hp, 1200f, "combat kept 3850");
            if (err != null) return err;

            var oldPackage = new BossScaleSnapshot { BuildCount = 14, Tm = 1.55f, MaxHp = 1100f };
            var third = new BossBrain();
            third.Configure(1100f);
            FinalBossLive.Begin(third, ref oldPackage);
            err = Require(oldPackage.MaxHp, 1200f, "begin left 1100");
            if (err != null) return err;
            err = Require(third.MaxHp, 1200f, "brain kept 1100");
            if (err != null) return err;
            err = Require(third.Hp, 1200f, "hp kept 1100");
            if (err != null) return err;
            return null;
        }

        static string CheckScaleShape()
        {
            float hp;
            float dmg;
            FinalBossRules.Weights(14, 1.55f, out hp, out dmg);
            if (Math.Abs(hp - 1f) > 0.001f || Math.Abs(dmg - 1.28f) > 0.001f)
                return "reference weights are not E (1, 1.28)";
            if (Math.Abs(FinalBossRules.OutgoingMultiplier(14, 1.55f) - 1f) > 0.001f)
                return "reference damage was not normalised by 1.28";
            float scaled = FinalBossRules.EnterMaxHp(14, 1.25f);
            float expect = 1200f * 1.25f / 1.55f;
            if (Math.Abs(scaled - expect) > 0.05f)
                return "enter scale is not 1200 * weight * p / 1.55";
            if (Math.Abs(scaled - 1200f) < 1f || Math.Abs(scaled - 1100f) < 1f || Math.Abs(scaled - 3850f) < 1f)
                return "non-reference enter collapsed onto a retired hp";
            float c = FinalBossRules.PressureCoordinate(1.25f);
            if (Math.Abs(c - 2f) > 0.001f)
                return "pressure coordinate at 1.25";
            if (Math.Abs(FinalBossRules.PressureCoordinate(1f) - 1f) > 0.001f)
                return "pressure coordinate at 1";
            if (Math.Abs(FinalBossRules.PressureCoordinate(1.55f) - 3f) > 0.001f)
                return "pressure coordinate at 1.55";
            return null;
        }

        static string CheckDirectionalDamage()
        {
            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP1(out brain, out combat, 6f);
            if (err != null) return err;
            combat.SetFacing(1f, 0f);
            float dealt = FinalBossLive.ApplyDamage(combat, brain, 10f, -1f, 0f, false);
            err = Require(dealt, 5f, "P1 front ordinary");
            if (err != null) return err;
            dealt = FinalBossLive.ApplyDamage(combat, brain, 10f, 1f, 0f, false);
            err = Require(dealt, 10f, "P1 rear ordinary");
            if (err != null) return err;
            dealt = FinalBossLive.ApplyDamage(combat, brain, 10f, 0f, -1f, false);
            err = Require(dealt, 5f, "P1 front boundary");
            if (err != null) return err;
            dealt = FinalBossLive.ApplyDamage(combat, brain, 10f, 1f, 1f, false);
            err = Require(dealt, 10f, "P1 side-rear ordinary");
            if (err != null) return err;
            dealt = FinalBossLive.ApplyDamage(combat, brain, 20f, -1f, 0f, true);
            err = Require(dealt, 20f, "P1 front weak");
            if (err != null) return err;
            dealt = FinalBossLive.ApplyDamage(combat, brain, 20f, 1f, 0f, true);
            err = Require(dealt, 20f, "P1 rear weak");
            if (err != null) return err;
            dealt = FinalBossLive.ApplyDamage(combat, brain, 20f, 0f, -1f, true);
            err = Require(dealt, 20f, "P1 side weak");
            if (err != null) return err;
            if (Math.Abs(combat.Hp - brain.Hp) > 0.01f || Math.Abs(combat.MaxHp - brain.MaxHp) > 0.01f)
                return "combat hp diverged";
            return null;
        }

        static string CheckP2NoFrontDr()
        {
            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP1(out brain, out combat, 6f);
            if (err != null) return err;
            FinalBossLive.ApplyDamage(combat, brain, 700f);
            if (brain.Phase != BossPhase.P2 || combat.Phase != BossPhase.P2)
                return "setup did not reach P2";
            brain.DrainTransition(2f);
            combat.SetFacing(1f, 0f);
            float dealt = FinalBossLive.ApplyDamage(combat, brain, 10f, -1f, 0f, false);
            err = Require(dealt, 10f, "P2 front kept DR");
            if (err != null) return err;
            if (Math.Abs(FinalBossRules.IncomingMultiplier(BossPhase.P2, 1f, 0f, -1f, 0f, false) - 1f) > 0.001f)
                return "P2 multiplier";
            return null;
        }

        static string CheckRangedCounter()
        {
            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP1(out brain, out combat, 6f);
            if (err != null) return err;
            err = FireRanged(brain, combat, BossMoveId.StraightShot, 1);
            if (err != null) return err;
            err = FireRanged(brain, combat, BossMoveId.StraightShot, 2);
            if (err != null) return err;

            Place(combat, 5f);
            if (!combat.TryStart(BossMoveId.WarningCharge))
                return "charge did not start between ranged";
            err = WaitReady(brain, combat);
            if (err != null) return err;
            if (brain.P1RangedUses != 2)
                return "charge consumed the ranged counter";

            Place(combat, 1.5f);
            if (!combat.TryStart(BossMoveId.ShieldBash))
                return "bash did not start between ranged";
            err = WaitReady(brain, combat);
            if (err != null) return err;
            if (brain.P1RangedUses != 2 || brain.NextP1Ranged != BossMoveId.BurstShot)
                return "bash consumed the ranged counter";

            int arrows = combat.ArrowsFired;
            Place(combat, 6f);
            if (!combat.TryStart(BossMoveId.BurstShot))
                return "burst did not start";
            Advance(brain, combat, 1.02f);
            if (brain.P1RangedUses != 3)
                return "burst first arrow did not count once";
            Advance(brain, combat, 1.05f);
            if (brain.P1RangedUses != 3)
                return "later burst arrows counted again";
            if (combat.ArrowsFired - arrows != 3)
                return "burst did not fire three arrows";
            err = WaitReady(brain, combat);
            if (err != null) return err;

            err = FireRanged(brain, combat, BossMoveId.StraightShot, 4);
            if (err != null) return err;
            return null;
        }

        static string CheckTripleOneHit()
        {
            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP2(out brain, out combat);
            if (err != null) return err;
            Place(combat, 0.9f);
            combat.SetFacing(1f, 0f);
            int before = combat.DamageApplications;
            float beforeDmg = combat.DamageToPlayer;
            int arrows = combat.ArrowsFired;
            if (!combat.TryStart(BossMoveId.TripleShot))
                return "triple did not start";
            Advance(brain, combat, 1.05f);
            if (combat.ArrowsFired - arrows != 3)
                return "triple did not spawn three arrows together";
            if (combat.LastVolleyCount != 3)
                return "triple volley count";
            if (Math.Abs(combat.LastVolleyTime - 0.9f) > 0.06f)
                return "triple fire time";
            if (Math.Abs(combat.LastVolleyAngle0 - (-15f)) > 0.01f
                || Math.Abs(combat.LastVolleyAngle1) > 0.01f
                || Math.Abs(combat.LastVolleyAngle2 - 15f) > 0.01f)
                return "triple spread";
            if (combat.DamageApplications - before != 1)
                return "triple applied damage more than once";
            err = Require(combat.DamageToPlayer - beforeDmg, 16f, "triple damage");
            if (err != null) return err;
            return null;
        }

        static string CheckPhaseOnce()
        {
            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP1(out brain, out combat, 6f);
            if (err != null) return err;
            float before = brain.Hp;
            FinalBossLive.ApplyDamage(combat, brain, 700f);
            if (brain.Phase != BossPhase.P2 || combat.Phase != BossPhase.P2 || brain.Phase2Transitions != 1)
                return "half hp did not transition once";
            err = Require(brain.Hp, before - 700f, "threshold hit was healed or clamped");
            if (err != null) return err;
            if (brain.Transitioning && Math.Abs(FinalBossLive.ApplyDamage(combat, brain, 50f)) > 0.01f)
                return "transition still accepted damage";
            brain.DrainTransition(2f);
            FinalBossLive.ApplyDamage(combat, brain, 100f);
            if (brain.Phase2Transitions != 1 || brain.Phase != BossPhase.P2)
                return "second hit transitioned again";
            err = Require(brain.Hp, before - 800f, "second hit");
            if (err != null) return err;
            err = Require(combat.Hp, brain.Hp, "phase hp pools");
            if (err != null) return err;
            return null;
        }

        static string CheckLethalSkips()
        {
            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP1(out brain, out combat, 6f);
            if (err != null) return err;
            FinalBossLive.ApplyDamage(combat, brain, 1200f);
            if (brain.Phase != BossPhase.Defeated || brain.Phase2Transitions != 0 || brain.Hp > 0.01f)
                return "lethal hit still played the transition";
            if (combat.Phase != BossPhase.Defeated)
                return "combat phase lagged the kill";
            return null;
        }

        static string CheckMeleeFanAndCharge()
        {
            if (FinalBossRules.InSector(0f, 0f, 1f, 0f, 4f, 0f, 2.6f, 120f, 0.32f))
                return "fan reached 4u";
            if (FinalBossRules.InSector(0f, 0f, 1f, 0f, -2f, 0f, 2.6f, 120f, 0.32f))
                return "fan hit behind";
            if (!FinalBossRules.InSector(0f, 0f, 1f, 0f, 2.85f, 0f, 2.6f, 120f, 0.32f))
                return "fan missed the player circle on the arc";

            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP1(out brain, out combat, 1.5f);
            if (err != null) return err;
            if (!combat.TryStart(BossMoveId.ShieldBash))
                return "bash did not start";
            int hits = combat.DamageApplications;
            Advance(brain, combat, 1.05f);
            if (combat.DamageApplications - hits != 1)
                return "bash hit count";
            err = Require(combat.DamageToPlayer, 20f, "bash damage");
            if (err != null) return err;
            if (combat.ArrowsFired != 0)
                return "bash fired an arrow";

            err = WaitReady(brain, combat);
            if (err != null) return err;
            Place(combat, 5f);
            hits = combat.DamageApplications;
            if (!combat.TryStart(BossMoveId.WarningCharge))
                return "charge did not start";
            Advance(brain, combat, 2.0f);
            if (combat.DamageApplications - hits != 1)
                return "charge hit more than once";
            err = Require(combat.DamageToPlayer - 20f, 24f, "charge damage");
            if (err != null) return err;
            return null;
        }

        static string CheckLeapLock()
        {
            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP2(out brain, out combat);
            if (err != null) return err;
            Place(combat, 6f);
            if (!combat.TryStart(BossMoveId.LeapSlam))
                return "leap did not start";
            Advance(brain, combat, 0.2f);
            float hp = brain.Hp;
            float wind = FinalBossLive.ApplyDamage(combat, brain, 10f, 1f, 0f, false);
            if (Math.Abs(wind - 10f) > 0.01f)
                return "leap windup was immune";
            Advance(brain, combat, 0.85f);
            float blocked = FinalBossLive.ApplyDamage(combat, brain, 10f, 1f, 0f, false);
            if (Math.Abs(blocked) > 0.01f || Math.Abs(brain.Hp - (hp - 10f)) > 0.01f)
                return "leap air still took a ground hit";
            float lockedX = combat.LeapTargetX;
            float lockedY = combat.LeapTargetY;
            if (!combat.LeapTargetLocked)
                return "leap did not lock";
            combat.PlacePlayer(0f, 30f, 0f);
            Advance(brain, combat, 0.8f);
            if (Math.Abs(combat.LeapTargetX - lockedX) > 0.05f || Math.Abs(combat.LeapTargetY - lockedY) > 0.05f)
                return "leap followed the player after takeoff";
            if (Math.Abs(combat.DamageToPlayer) > 0.01f)
                return "leap hit the abandoned lock point's empty ground";

            err = WaitReady(brain, combat);
            if (err != null) return err;
            Place(combat, 6f);
            if (brain.CanUse(BossMoveId.LeapSlam) || combat.TryStart(BossMoveId.LeapSlam))
                return "leap was allowed twice in a row";
            if (!combat.TryStart(BossMoveId.TripleShot))
                return "gap skill after leap";
            err = WaitReady(brain, combat);
            if (err != null) return err;
            float cooldown = 0f;
            while (!brain.CanUse(BossMoveId.LeapSlam) && cooldown < 8f)
            {
                Advance(brain, combat, 0.2f);
                cooldown += 0.2f;
            }
            Place(combat, 6f);
            float dmg = combat.DamageToPlayer;
            int hits = combat.DamageApplications;
            if (!combat.TryStart(BossMoveId.LeapSlam))
                return "leap did not start after a gap";
            Advance(brain, combat, 1.75f);
            if (combat.DamageApplications - hits != 1)
                return "leap land hit count";
            err = Require(combat.DamageToPlayer - dmg, 26f, "leap damage");
            if (err != null) return err;
            Advance(brain, combat, 0.3f);
            if (combat.DamageApplications - hits != 1)
                return "leap hit a second time";
            return null;
        }

        static string CheckNoRingOrFourMetreMelee()
        {
            BossBrain brain;
            FinalBossCombat combat;
            string err = OpenP1(out brain, out combat, 3.1f);
            if (err != null) return err;
            combat.AutoSelect = true;
            Advance(brain, combat, 0.4f);
            if (brain.CurrentMove != BossMoveId.None)
                return "3.1u gap still opened an attack";
            if (combat.TryStart(BossMoveId.RingBurst) || brain.CanUse(BossMoveId.RingBurst))
                return "ring burst is still selectable";

            Place(combat, 4f);
            Advance(brain, combat, 0.1f);
            if (brain.CurrentMove == BossMoveId.ShieldBash || brain.CurrentMove == BossMoveId.CrossbowBash)
                return "4u still used the old melee replace";

            err = OpenP1(out brain, out combat, 2.2f);
            if (err != null) return err;
            combat.AutoSelect = true;
            Advance(brain, combat, 0.1f);
            if (brain.CurrentMove != BossMoveId.ShieldBash)
                return "close P1 did not shield bash";
            if (combat.ArrowsFired != 0)
                return "close P1 fired a projectile";
            return null;
        }

        static string CheckSkillTable()
        {
            string err = Skill(BossMoveId.StraightShot, 1f, 0.5f, 0.05f, 1.65f, 4f, 18f, 3.5f, 10f);
            if (err != null) return err;
            err = Skill(BossMoveId.BurstShot, 1f, 0.65f, 1.05f, 1.85f, 4f, 12f, 3.5f, 10f);
            if (err != null) return err;
            err = Skill(BossMoveId.WarningCharge, 1.15f, 0.6f, 0.7f, 1.8f, 6f, 24f, 3.5f, 7f);
            if (err != null) return err;
            err = Skill(BossMoveId.ShieldBash, 0.85f, 0.3f, 0.15f, 1.4f, 4.8f, 20f, 0f, 2.8f);
            if (err != null) return err;
            err = Skill(BossMoveId.TripleShot, 0.9f, 0.4f, 0.05f, 1.3f, 3f, 16f, 3.5f, 10f);
            if (err != null) return err;
            err = Skill(BossMoveId.LeapSlam, 0.7f, 0.7f, 1f, 1.65f, 6f, 26f, 4f, 9f);
            if (err != null) return err;
            err = Skill(BossMoveId.CrossbowBash, 0.7f, 0.15f, 0.1f, 1f, 3f, 22f, 0f, 3f);
            if (err != null) return err;
            if (Math.Abs(FinalBossRules.MoveSpeed(BossPhase.P1) - 2.5f) > 0.001f
                || Math.Abs(FinalBossRules.MoveSpeed(BossPhase.P2) - 4.5f) > 0.001f)
                return "move speed";
            if (Math.Abs(FinalBossRules.TurnSpeed(BossPhase.P1) - 90f) > 0.001f
                || Math.Abs(FinalBossRules.TurnSpeed(BossPhase.P2) - 180f) > 0.001f)
                return "turn speed";
            if (Math.Abs(FinalBossRules.Quiet(BossPhase.P1) - 1.1f) > 0.001f
                || Math.Abs(FinalBossRules.Quiet(BossPhase.P2) - 0.35f) > 0.001f)
                return "quiet";
            if (Math.Abs(FinalBossRules.Skill(BossMoveId.ShieldBash).Angle - 120f) > 0.01f
                || Math.Abs(FinalBossRules.Skill(BossMoveId.ShieldBash).Radius - 2.6f) > 0.01f)
                return "p1 fan";
            if (Math.Abs(FinalBossRules.Skill(BossMoveId.CrossbowBash).Angle - 100f) > 0.01f
                || Math.Abs(FinalBossRules.Skill(BossMoveId.CrossbowBash).Radius - 2.8f) > 0.01f)
                return "p2 fan";
            return null;
        }

        static string Skill(BossMoveId move, float windup, float lockAt, float active, float recovery, float cooldown, float damage, float min, float max)
        {
            BossSkill skill = FinalBossRules.Skill(move);
            if (Math.Abs(skill.Windup - windup) > 0.001f || Math.Abs(skill.Lock - lockAt) > 0.001f
                || Math.Abs(skill.Active - active) > 0.001f || Math.Abs(skill.Recovery - recovery) > 0.001f
                || Math.Abs(skill.Cooldown - cooldown) > 0.001f || Math.Abs(skill.Damage - damage) > 0.001f
                || Math.Abs(skill.Min - min) > 0.001f || Math.Abs(skill.Max - max) > 0.001f)
                return move + " drifted from the design table";
            return null;
        }

        static string FireRanged(BossBrain brain, FinalBossCombat combat, BossMoveId move, int uses)
        {
            Place(combat, 6f);
            string wait = WaitCan(brain, combat, move);
            if (wait != null)
                return wait;
            if (brain.NextP1Ranged != move || !combat.TryStart(move))
                return "expected ranged " + move + " next=" + brain.NextP1Ranged;
            Advance(brain, combat, 1.02f);
            if (brain.P1RangedUses != uses)
                return move + " counter " + brain.P1RangedUses;
            return WaitReady(brain, combat);
        }

        static string EnterReference(out BossBrain brain, out FinalBossCombat combat, out BossScaleSnapshot snap)
        {
            brain = new BossBrain();
            brain.Configure(BossBrain.DefaultMaxHp);
            snap = BossScaleTable.Resolve(14, 10f, FinalBossRules.PressureRef, 1f);
            combat = FinalBossLive.Begin(brain, ref snap);
            combat.AutoSelect = false;
            combat.AutoMove = false;
            combat.AllowActions = true;
            combat.SetFacing(1f, 0f);
            return null;
        }

        static string OpenP1(out BossBrain brain, out FinalBossCombat combat, float playerX)
        {
            BossScaleSnapshot snap;
            string err = EnterReference(out brain, out combat, out snap);
            if (err != null) return err;
            FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, playerX, 0f, true, true, false);
            if (brain.Phase != BossPhase.DoorSealed || brain.CurrentMove != BossMoveId.None)
                return "first step should only seal";
            FinalBossLive.Step(brain, combat, 0.02f, true, 0f, 0f, playerX, 0f, true, true, false);
            if (brain.Phase != BossPhase.P1 || !brain.ReadyToSelect)
                return "second step should be a ready P1";
            Place(combat, playerX);
            combat.SetFacing(1f, 0f);
            return null;
        }

        static string OpenP2(out BossBrain brain, out FinalBossCombat combat)
        {
            string err = OpenP1(out brain, out combat, 6f);
            if (err != null) return err;
            FinalBossLive.ApplyDamage(combat, brain, 700f);
            if (brain.Phase != BossPhase.P2 || brain.Phase2Transitions != 1)
                return "could not open P2";
            brain.DrainTransition(2f);
            Place(combat, 6f);
            combat.SetFacing(1f, 0f);
            if (!brain.ReadyToSelect)
                return "P2 was not ready after the cut";
            return null;
        }

        static void Place(FinalBossCombat combat, float playerX)
        {
            combat.SetBossPosition(0f, 0f);
            combat.PlacePlayer(playerX, 0f, 0f);
            combat.SetFacing(1f, 0f);
        }

        static void Advance(BossBrain brain, FinalBossCombat combat, float seconds)
        {
            float t = 0f;
            while (t < seconds - 0.00001f)
            {
                float dt = 0.05f;
                if (t + dt > seconds)
                    dt = seconds - t;
                FinalBossLive.Step(
                    brain, combat, dt, true,
                    combat.BossX, combat.BossY, combat.PlayerX, combat.PlayerY,
                    true, true, combat.PlayerInvulnerable);
                t += dt;
            }
        }

        static string WaitCan(BossBrain brain, FinalBossCombat combat, BossMoveId move)
        {
            float guard = 0f;
            while ((!brain.ReadyToSelect || !brain.CanUse(move)) && guard < 20f)
            {
                Advance(brain, combat, 0.05f);
                guard += 0.05f;
            }

            if (!brain.ReadyToSelect || !brain.CanUse(move))
                return "not ready to use " + move;
            return null;
        }

        static string WaitReady(BossBrain brain, FinalBossCombat combat)
        {
            float guard = 0f;
            while (!brain.ReadyToSelect && guard < 20f)
            {
                Advance(brain, combat, 0.05f);
                guard += 0.05f;
            }

            if (!brain.ReadyToSelect)
                return "skill never became ready";
            return null;
        }

        static string Require(float actual, float expect, string reason)
        {
            if (Math.Abs(actual - expect) > 0.05f)
                return reason + " got " + actual.ToString("0.###");
            return null;
        }
    }
}
