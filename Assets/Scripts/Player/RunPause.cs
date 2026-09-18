namespace RogueShooter.Player
{
    /// <summary>
    /// Interact overlay pause (Time.timeScale) plus a flag motors/AI can read.
    /// </summary>
    public static class RunPause
    {
        public static bool InteractOpen;

        public static bool IsPaused => InteractOpen;
    }
}
