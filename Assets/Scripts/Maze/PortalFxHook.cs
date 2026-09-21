namespace RogueShooter.Maze
{
    /// <summary>
    /// Ground portal VFX is outsourced. Skeleton only logs the hook.
    /// </summary>
    public static class PortalFxHook
    {
        public static int CallCount;
        public static string LastLine = "";

        public static string Format(string roomId, int wave)
        {
            return "[PortalFx] room=" + (roomId ?? "?") + " wave=" + wave + " stub";
        }

        public static string Play(string roomId, int wave)
        {
            LastLine = Format(roomId, wave);
            CallCount++;
            return LastLine;
        }

        public static void Reset()
        {
            CallCount = 0;
            LastLine = "";
        }
    }
}
