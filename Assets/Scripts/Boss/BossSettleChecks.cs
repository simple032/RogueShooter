using System;

namespace RogueShooter.Boss
{
    /// <summary>
    /// W3-03 settle labels. Enter goes through FinalBossLive.Begin, so the locked HP is 1200.
    /// Build, phase label, and cross-seg still come from the scale snapshot's damage mul.
    /// </summary>
    public static class BossSettleChecks
    {
        public static string Run()
        {
            var brain = new BossBrain();
            brain.Configure(BossBrain.DefaultMaxHp);
            var snap = BossScaleTable.Resolve(12, 8f, 1.35f, 1.06f);
            FinalBossLive.Begin(brain, ref snap);
            if (Math.Abs(brain.MaxHp - FinalBossRules.MaxHp) > 0.01f || Math.Abs(brain.Hp - FinalBossRules.MaxHp) > 0.01f)
                return "settle enter hp";
            brain.Tick(0.02f);
            brain.Tick(0.02f);
            brain.NotifyTimeCross(11f);

            var win = BossSettleReport.From(BossSettleOutcome.Win, brain, 11f);
            if (win.Outcome != BossSettleOutcome.Win || win.OutcomeLabel != "胜")
                return "win label";
            if (win.BuildCount != 12)
                return "Build=12";
            if (string.IsNullOrEmpty(win.EnterPhaseLabel))
                return "enter phase label";
            if (!win.CrossedSegment || win.CrossLabel != "是")
                return "crossed=是";

            brain.Reset();
            brain.Configure(BossBrain.DefaultMaxHp);
            var loseSnap = BossScaleTable.Resolve(12, 8f, 1.35f, 1.06f);
            FinalBossLive.Begin(brain, ref loseSnap);
            if (Math.Abs(brain.MaxHp - FinalBossRules.MaxHp) > 0.01f || Math.Abs(brain.Hp - FinalBossRules.MaxHp) > 0.01f)
                return "settle enter hp";
            var lose = BossSettleReport.From(BossSettleOutcome.Lose, brain, 8f);
            if (lose.OutcomeLabel != "负")
                return "lose label";
            if (lose.CrossedSegment)
                return "same tier must 跨段=否";
            if (lose.BuildCount != 12)
                return "lose Build";

            return null;
        }

        public static string FormatPass(BossSettleReport r)
        {
            return "ACCEPTANCE PASS W3-03 " + r.FormatLines();
        }
    }
}
