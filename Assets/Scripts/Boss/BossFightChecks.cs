using System;

namespace RogueShooter.Boss
{
    public static class BossFightChecks
    {
        public static string Run()
        {
            var brain = new BossBrain();
            brain.Configure(FinalBossRules.BaseHp);
            if (brain.UsesMobAi)
                return "BOSS must not use mob AI tree";
            if (Math.Abs(brain.MaxHp - 1200f) > 0.01f || Math.Abs(brain.Hp - 1200f) > 0.01f)
                return "fight brain did not start at 1200";
            if (brain.Phase != BossPhase.IdleOutside || brain.DoorClosed)
                return "start IdleOutside door open";

            brain.NotifyEnter();
            if (brain.Phase != BossPhase.Entering)
                return "enter → Entering";
            brain.Tick(0.02f);
            if (!brain.DoorClosed || brain.Phase != BossPhase.DoorSealed)
                return "enter tick → DoorSealed";
            brain.Tick(0.02f);
            if (brain.Phase != BossPhase.P1 || brain.CurrentMove != BossMoveId.None)
                return "DoorSealed → P1 without an automatic legacy move";
            if (brain.CanUse(BossMoveId.RingBurst) || brain.TryStart(BossMoveId.RingBurst))
                return "RingBurst still starts";
            if (!brain.TryStart(BossMoveId.StraightShot))
                return "P1 single did not start";
            if (brain.TryStart(BossMoveId.BurstShot))
                return "burst replaced the single early";

            brain.ApplyDamage(700f);
            if (brain.Phase != BossPhase.P2 || brain.Phase2Transitions != 1 || Math.Abs(brain.Hp - 500f) > 0.01f)
                return "HP≤50% → P2 once, no heal";
            float held = brain.Hp;
            brain.ApplyDamage(10f);
            if (Math.Abs(brain.Hp - held) > 0.01f)
                return "transition changed hp";
            brain.DrainTransition(2f);
            brain.ApplyDamage(50f);
            if (brain.Phase2Transitions != 1 || Math.Abs(brain.Hp - 450f) > 0.01f)
                return "P2 transition repeated";
            if (brain.CanUse(BossMoveId.RingBurst) || brain.CanUse(BossMoveId.ShieldBash))
                return "wrong-phase skill";

            var kill = new BossBrain();
            kill.Configure(1200f);
            kill.NotifyEnter();
            kill.Tick(0.02f);
            kill.Tick(0.02f);
            kill.ApplyDamage(1200f);
            if (kill.Phase != BossPhase.Defeated || kill.Hp > 0.01f || kill.Phase2Transitions != 0)
                return "lethal skips phase transition";
            if (Math.Abs(FinalBossRules.MoveSpeed(BossPhase.P1) - 2.5f) > 0.001f
                || Math.Abs(FinalBossRules.MoveSpeed(BossPhase.P2) - 4.5f) > 0.001f
                || FinalBossRules.MoveSpeed(BossPhase.P2) >= 6f)
                return "locked movement";
            return null;
        }

        public static string FormatPass(BossBrain brain)
        {
            return "PASS boss clock enter→P1→P2@50% once, ring retired, hp="
                + (brain != null ? brain.MaxHp.ToString("0") : "1200");
        }
    }
}
