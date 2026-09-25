namespace RogueShooter.Iso
{
    /// <summary>
    /// Archer keeps eight rendered facings. Skeleton west facings mirror the east sources.
    /// </summary>
    public static class IsoFacing
    {
        public static readonly string[] Eight =
        {
            "n", "ne", "e", "se", "s", "sw", "w", "nw"
        };

        public static bool ArcherFlips(string facing)
        {
            return false;
        }

        public static bool TrySkeleton(string facing, out string source, out bool flipX)
        {
            switch (facing)
            {
                case "nw":
                    source = "ne";
                    flipX = true;
                    return true;
                case "w":
                    source = "e";
                    flipX = true;
                    return true;
                case "sw":
                    source = "se";
                    flipX = true;
                    return true;
                case "n":
                case "ne":
                case "e":
                case "se":
                case "s":
                    source = facing;
                    flipX = false;
                    return true;
                default:
                    source = facing;
                    flipX = false;
                    return false;
            }
        }
    }
}
