using System;
using RogueShooter.Balance;

namespace RogueShooter.Boss
{
    public enum BossSettleOutcome
    {
        None,
        Win,
        Lose
    }

    /// <summary>W3-03 settle fields: 胜负 / Build / 进门时间段 / 是否跨段.</summary>
    public struct BossSettleReport
    {
        public BossSettleOutcome Outcome;
        public int BuildCount;
        public string EnterPhaseId;
        public string EnterPhaseLabel;
        public int EnterTimeTier;
        public int EndTimeTier;
        public bool CrossedSegment;
        public string EnterAnchorId;
        public float EnterDmgMul;
        public float EndDmgMul;

        public static BossSettleReport From(
            BossSettleOutcome outcome,
            BossBrain brain,
            float endWallMinutes)
        {
            int endTier = BossScaleTable.TimeTierFromMinutes(endWallMinutes);
            bool crossed = brain != null
                && brain.HpLocked
                && (endTier != brain.EnterTimeTier
                    || Math.Abs(brain.LiveDmgMul - brain.EnterDmgMul) > 0.001f);

            string enterPhaseId = PhaseIdFromTier(brain != null ? brain.EnterTimeTier : 0);
            // Prefer wall-clock label at enter via tier map.
            float enterMinsApprox = MinsFromTier(brain != null ? brain.EnterTimeTier : 0);

            return new BossSettleReport
            {
                Outcome = outcome,
                BuildCount = brain != null ? brain.EnterBuild : 0,
                EnterPhaseId = enterPhaseId,
                EnterPhaseLabel = TimePressure.PhaseLabel(enterMinsApprox),
                EnterTimeTier = brain != null ? brain.EnterTimeTier : 0,
                EndTimeTier = endTier,
                CrossedSegment = crossed,
                EnterAnchorId = brain != null ? brain.EnterAnchorId : "",
                EnterDmgMul = brain != null ? brain.EnterDmgMul : 1f,
                EndDmgMul = brain != null ? brain.LiveDmgMul : 1f
            };
        }

        static string PhaseIdFromTier(int tier)
        {
            switch (tier)
            {
                case 1: return "P1";
                case 2: return "P2/P3";
                case 3: return "P4";
                default: return "P?";
            }
        }

        static float MinsFromTier(int tier)
        {
            switch (tier)
            {
                case 1: return 2f;
                case 2: return 8f;
                case 3: return 11f;
                default: return 0f;
            }
        }

        public string OutcomeLabel
        {
            get
            {
                switch (Outcome)
                {
                    case BossSettleOutcome.Win: return "胜";
                    case BossSettleOutcome.Lose: return "负";
                    default: return "—";
                }
            }
        }

        public string CrossLabel
        {
            get { return CrossedSegment ? "是" : "否"; }
        }

        public string FormatLines()
        {
            return string.Format(
                "结果={0} Build={1} 进门段={2}({3}) T{4} 跨段={5} dmg={6:0.00}→{7:0.00}",
                OutcomeLabel, BuildCount, EnterPhaseLabel, EnterPhaseId,
                EnterTimeTier, CrossLabel, EnterDmgMul, EndDmgMul);
        }
    }
}
