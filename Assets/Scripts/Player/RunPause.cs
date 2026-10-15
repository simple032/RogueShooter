using UnityEngine;

namespace RogueShooter.Player
{
    /// <summary>
    /// Interact overlay pause (Time.timeScale) plus a flag motors/AI can read.
    /// </summary>
    public static class RunPause
    {
        public static bool InteractOpen;
        public static bool BossTransition;
        public static bool RitualActing;
        public static bool RunSettled;
        /// <summary>Player HP hit zero. Combat stops. Time scale stays as it was so the death view can still move.</summary>
        public static bool CombatEnded;
        /// <summary>ESC pause menu is open. Motors, AI and combat read IsPaused and stop.</summary>
        public static bool MenuOpen;

        public static bool IsPaused => InteractOpen || RunSettled || CombatEnded || BossTransition || RitualActing || MenuOpen;

        /// <summary>Clear every cross-scene static and restore time, so a freshly
        /// loaded scene (start menu or a new run) never inherits stale pause state.</summary>
        public static void ResetAll()
        {
            InteractOpen = false;
            BossTransition = false;
            RitualActing = false;
            RunSettled = false;
            CombatEnded = false;
            MenuOpen = false;
            Time.timeScale = 1f;
        }

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
