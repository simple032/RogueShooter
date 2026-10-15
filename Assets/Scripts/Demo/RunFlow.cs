using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Trial ends the run at the stage-1 connector. Full flow advances through all three stage maps.
    /// </summary>
    public enum RunFlowMode
    {
        Stage1Trial = 0,
        Full = 1
    }

    public static class RunFlow
    {
        public const RunFlowMode DefaultMode = RunFlowMode.Full;
        public const string TrialLabel = "第一阶段试玩";
        public const string FullLabel = "三阶段连续关卡";

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
        /// Compatibility diagnostic for callers outside the stage-map controller.
        /// </summary>
        public static string HaltFull()
        {
            LastHalt = "当前为三阶段完整关卡，大祭坛后进入最终BOSS";
            Debug.Log("[RunFlow] " + LastHalt);
            return LastHalt;
        }
    }
}
