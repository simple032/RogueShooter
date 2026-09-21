namespace RogueShooter.Spawning
{
    /// <summary>Spec v0.5 three-stage dungeon. Base attrs do not scale by this id.</summary>
    public enum StageId
    {
        S1 = 1,
        S2 = 2,
        S3 = 3
    }

    public static class StageIdUtil
    {
        public static string Label(StageId id)
        {
            switch (id)
            {
                case StageId.S2: return "S2";
                case StageId.S3: return "S3";
                default: return "S1";
            }
        }

        public static StageId FromBand(string bandId)
        {
            if (bandId == "Z2")
                return StageId.S2;
            if (bandId == "Z3" || bandId == "Pre")
                return StageId.S3;
            return StageId.S1;
        }

        public static bool GrantsLunge(StageId id)
        {
            return id == StageId.S2 || id == StageId.S3;
        }

        public static bool DogsSpawnInPairs(StageId id)
        {
            return id == StageId.S2 || id == StageId.S3;
        }
    }
}
