namespace RogueShooter.Demo
{
    /// <summary>Opt-in detailed combat diagnostics; normal sessions avoid argument formatting and Console growth.</summary>
    public static class GameTrace
    {
        [System.Diagnostics.Conditional("JIANHAI_TRACE")]
        public static void Log(object message) { UnityEngine.Debug.Log(message); }
    }
}
