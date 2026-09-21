using System;
using RogueShooter.Spawning;

namespace RogueShooter.Maze
{
    /// <summary>Stage-1 maze node class. Corridors are edges and never spawn.</summary>
    public enum MazeNodeKind
    {
        Start = 0,
        Normal = 1,
        Chest = 2,
        LargeChest = 3,
        Altar = 4,
        Connector = 5
    }

    public struct MazeVec2
    {
        public float X;
        public float Y;

        public MazeVec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float SqrDist(MazeVec2 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        public float Dist(MazeVec2 other)
        {
            return (float)Math.Sqrt(SqrDist(other));
        }

        public override string ToString()
        {
            return X.ToString("0.#") + "," + Y.ToString("0.#");
        }
    }

    public sealed class MazeNode
    {
        public string Id;
        public MazeNodeKind Kind;
        public MazeVec2 Center;
        public float Width;
        public float Height;
        public string[] NeighborIds;

        public int WaveCount
        {
            get { return MazeRules.WaveCount(Kind); }
        }

        public bool SpawnsEnemies
        {
            get { return WaveCount > 0; }
        }

        public CombatRoomKind PoolRoom
        {
            get { return MazeRules.ToPoolRoom(Kind); }
        }

        public bool UsesPortalFx
        {
            get { return MazeRules.UsesPortalFx(Kind); }
        }

        public bool Contains(float x, float y, float inset)
        {
            float hw = Width * 0.5f - inset;
            float hh = Height * 0.5f - inset;
            if (hw < 0.2f) hw = 0.2f;
            if (hh < 0.2f) hh = 0.2f;
            return x >= Center.X - hw && x <= Center.X + hw
                && y >= Center.Y - hh && y <= Center.Y + hh;
        }
    }

    public sealed class MazeEdge
    {
        public string FromId;
        public string ToId;
        public MazeVec2 From;
        public MazeVec2 To;
        public float Width;
        public float Length;

        public bool Connects(string a, string b)
        {
            return (FromId == a && ToId == b) || (FromId == b && ToId == a);
        }

        public bool Contains(float x, float y, float pad)
        {
            float ax = From.X;
            float ay = From.Y;
            float bx = To.X;
            float by = To.Y;
            float dx = bx - ax;
            float dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float t = 0f;
            if (len2 > 0.0001f)
                t = ((x - ax) * dx + (y - ay) * dy) / len2;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            float px = ax + dx * t;
            float py = ay + dy * t;
            float half = Width * 0.5f + pad;
            float ox = x - px;
            float oy = y - py;
            return ox * ox + oy * oy <= half * half;
        }
    }

    public sealed class Stage1Maze
    {
        public int Seed;
        public string TemplateId;
        public bool LargeChestUpgraded;
        public MazeNode[] Nodes;
        public MazeEdge[] Edges;
        public string Signature;

        public MazeNode Find(string id)
        {
            if (Nodes == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (Nodes[i].Id == id)
                    return Nodes[i];
            }

            return null;
        }

        public int CountKind(MazeNodeKind kind)
        {
            int n = 0;
            if (Nodes == null)
                return 0;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (Nodes[i].Kind == kind)
                    n++;
            }

            return n;
        }

        public int CombatRoomCount()
        {
            int n = 0;
            if (Nodes == null)
                return 0;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (Nodes[i].SpawnsEnemies)
                    n++;
            }

            return n;
        }

        public int FullClearWaveCount()
        {
            int n = 0;
            if (Nodes == null)
                return 0;
            for (int i = 0; i < Nodes.Length; i++)
                n += Nodes[i].WaveCount;
            return n;
        }
    }

    public struct MazePacing
    {
        public float ShortestWalk;
        public float ShortestWalkSeconds;
        public int ShortestCombatRooms;
        public int ShortestWaves;
        public float ShortestCombatEstimate;
        public float ShortestTotalEstimate;
        public float FullWalk;
        public float FullWalkSeconds;
        public int FullWaves;
        public float FullCombatEstimate;
        public float FullTotalEstimate;
        public bool ConnectorReachable;
        public bool AllReachable;
    }

    /// <summary>Spec v0.5 S1 maze constants. Pacing numbers are intent, not a clock lock.</summary>
    public static class MazeRules
    {
        public const float PlayMoveSpeed = 6f;
        public const float PlayOrtho = 6f;
        public const float PitchX = 44f;
        public const float PitchY = 38f;
        public const float CombatWidth = 16f;
        public const float CombatHeight = 12f;
        public const float HubWidth = 10f;
        public const float HubHeight = 8f;
        public const float CorridorWidth = 4.2f;
        public const float LargeChestChance = 0.25f;
        public const float WavePacingEstimateSeconds = 28f;
        /// <summary>Visible ground portal hold before each wave Instantiate. Not a clock lock.</summary>
        public const float PortalHoldSeconds = 1.0f;
        public const int QuotaNormal = 2;
        public const int QuotaChest = 1;
        public const int QuotaAltar = 1;
        public const int QuotaStart = 1;
        public const int QuotaConnector = 1;

        public static int WaveCount(MazeNodeKind kind)
        {
            switch (kind)
            {
                case MazeNodeKind.Chest:
                case MazeNodeKind.LargeChest:
                case MazeNodeKind.Altar:
                    return 2;
                case MazeNodeKind.Normal:
                    return 1;
                default:
                    return 0;
            }
        }

        public static bool UsesPortalFx(MazeNodeKind kind)
        {
            return WaveCount(kind) > 0;
        }

        public static CombatRoomKind ToPoolRoom(MazeNodeKind kind)
        {
            switch (kind)
            {
                case MazeNodeKind.Altar:
                    return CombatRoomKind.Altar;
                case MazeNodeKind.Chest:
                    return CombatRoomKind.Chest;
                case MazeNodeKind.LargeChest:
                    return CombatRoomKind.LargeChest;
                default:
                    return CombatRoomKind.Normal;
            }
        }

        public static string Label(MazeNodeKind kind)
        {
            switch (kind)
            {
                case MazeNodeKind.Start: return "START";
                case MazeNodeKind.Normal: return "Normal";
                case MazeNodeKind.Chest: return "Chest";
                case MazeNodeKind.LargeChest: return "LargeChest";
                case MazeNodeKind.Altar: return "Altar";
                case MazeNodeKind.Connector: return "Connector";
                default: return kind.ToString();
            }
        }
    }
}
