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

        public static bool IsPaused => InteractOpen || RunSettled;

        /// <summary>Connector tally is open. Clock and attacks stop; time scale stays 0 until the run restarts.</summary>
        public static void EnterSettled()
        {
            RunSettled = true;
            Time.timeScale = 0f;
        }
    }
}
