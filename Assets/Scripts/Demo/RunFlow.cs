using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Trial ends the run at the stage-1 connector. Full flow is not wired.
    /// </summary>
    public enum RunFlowMode
    {
        Stage1Trial = 0,
        Full = 1
    }

    public static class RunFlow
    {
        public const RunFlowMode DefaultMode = RunFlowMode.Stage1Trial;
        public const string TrialLabel = "第一阶段试玩";
        public const string FullLabel = "完整流程";

        public static RunFlowMode Mode = DefaultMode;
        public static string LastHalt;

        public static bool IsStage1Trial
        {
            get { return Mode == RunFlowMode.Stage1Trial; }
        }

        public static string Label
        {
            get { return IsStage1Trial ? TrialLabel : FullLabel; }
        }

        /// <summary>
        /// Full flow stops here. Does not settle the trial and does not build stage-2 rooms.
        /// </summary>
        public static string HaltFull()
        {
            LastHalt = "完整流程已停下，没有接上第二阶段";
            Debug.Log("[RunFlow] " + LastHalt);
            return LastHalt;
        }
    }
}
