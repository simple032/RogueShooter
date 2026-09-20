using System;

namespace RogueShooter.Boss
{
    public static class BossFightChecks
    {
        /// <summary>Returns null on pass, else failure reason.</summary>
        public static string Run()
        {
            var brain = new BossBrain();
            brain.Configure(BossBrain.DefaultMaxHp);

            if (brain.UsesMobAi)
                return "BOSS must not use mob AI tree";
            if (brain.Phase != BossPhase.IdleOutside || brain.DoorClosed)
                return "start IdleOutside door open";

            brain.NotifyEnter();
            if (brain.Phase != BossPhase.Entering)
                return "enter → Entering";

            brain.Tick(0.02f);
            if (!brain.DoorClosed || brain.Phase != BossPhase.DoorSealed)
                return "enter tick → DoorSealed";

            brain.Tick(0.02f);
            if (brain.Phase != BossPhase.P1)
                return "DoorSealed → P1";
            if (brain.CurrentMove != BossMoveId.StraightShot || brain.MoveStep != BossMoveStep.Windup)
                return "P1 first move StraightShot Windup";

            // Finish first move timings: 0.5+0.4+0.3
            Advance(brain, 1.3f);
            if (brain.MovesCompleted < 1)
                return "P1 move1 complete";
            if (brain.CurrentMove != BossMoveId.WarningCharge)
                return "P1 alternate → WarningCharge";

            // Drop to ≤50% → P2
            brain.ApplyDamage(brain.MaxHp * 0.55f);
            if (brain.Phase != BossPhase.P2)
                return "HP≤50% → P2";
            if (brain.CurrentMove != BossMoveId.TripleShot)
                return "P2 first move TripleShot";

            Advance(brain, 1.4f);
            if (brain.CurrentMove != BossMoveId.RingBurst && brain.MovesCompleted < 2)
            {
                // may already be on RingBurst after TripleShot completes
            }

            bool sawRing = brain.CurrentMove == BossMoveId.RingBurst;
            float guard = 0f;
            while (!sawRing && guard < 5f)
            {
                brain.Tick(0.05f);
                guard += 0.05f;
                if (brain.CurrentMove == BossMoveId.RingBurst)
                    sawRing = true;
            }

            if (!sawRing)
                return "P2 must use RingBurst";

            brain.ApplyDamage(brain.Hp + 1f);
            if (brain.Phase != BossPhase.Defeated || brain.Hp > 0f)
                return "HP≤0 → Defeated";
            if (brain.UsesMobAi)
                return "still must not use mob AI";

            return null;
        }

        static void Advance(BossBrain brain, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                brain.Tick(0.05f);
                t += 0.05f;
            }
        }

        public static string FormatPass(BossBrain brain)
        {
            return string.Format(
                "ACCEPTANCE PASS W3-01 enter→door={0} P1→P2@50% moves P1={1}/{2} P2={3}/{4} win hp=0 mobAi={5}",
                brain.DoorClosed,
                BossMoveId.StraightShot,
                BossMoveId.WarningCharge,
                BossMoveId.TripleShot,
                BossMoveId.RingBurst,
                brain.UsesMobAi);
        }
    }
}
