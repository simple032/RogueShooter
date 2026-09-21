namespace RogueShooter.Maze
{
    /// <summary>
    /// Ground portal cadence: visible stub at spawn points, then wait
    /// <see cref="MazeRules.PortalHoldForWave"/>, then spawn. Art can replace the stub later.
    /// </summary>
    public static class PortalFxHook
    {
        public static int CallCount;
        public static string LastLine = "";

        public static string FormatShow(string roomId, int wave)
        {
            return "[PortalFx] room=" + (roomId ?? "?") + " wave=" + wave + " show";
        }

        public static string FormatSpawn(string roomId, int wave)
        {
            return FormatSpawn(roomId, wave, MazeRules.PortalHoldForWave(wave));
        }

        public static string FormatSpawn(string roomId, int wave, float holdSeconds)
        {
            return "[PortalFx] room=" + (roomId ?? "?") + " wave=" + wave
                + " spawn after " + holdSeconds.ToString("0.0") + "s";
        }

        /// <summary>Back-compat alias for show.</summary>
        public static string Format(string roomId, int wave)
        {
            return FormatShow(roomId, wave);
        }

        public static string Play(string roomId, int wave)
        {
            return PlayShow(roomId, wave);
        }

        public static string PlayShow(string roomId, int wave)
        {
            LastLine = FormatShow(roomId, wave);
            CallCount++;
            return LastLine;
        }

        public static string PlaySpawn(string roomId, int wave)
        {
            LastLine = FormatSpawn(roomId, wave);
            return LastLine;
        }

        public static void Reset()
        {
            CallCount = 0;
            LastLine = "";
        }
    }
}
