using UnityEngine;

namespace RogueShooter.Player
{
    /// <summary>
    /// Interact overlay pause (Time.timeScale) plus a flag motors/AI can read.
    /// </summary>
    public static class RunPause
    {
        public static bool InteractOpen;
        public static bool RunSettled;
        /// <summary>Player HP hit zero. Combat stops. Time scale stays as it was so the death view can still move.</summary>
        public static bool CombatEnded;

        public static bool IsPaused => InteractOpen || RunSettled || CombatEnded;

        public static void EnterCombatEnd()
        {
            CombatEnded = true;
        }

        public static void ClearCombatEnd()
        {
            CombatEnded = false;
        }

        /// <summary>Connector tally is open. Clock and attacks stop; time scale stays 0 until the run restarts.</summary>
        public static void EnterSettled()
        {
            RunSettled = true;
            Time.timeScale = 0f;
        }
    }
}
