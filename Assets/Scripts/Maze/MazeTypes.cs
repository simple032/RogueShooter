using System;
using RogueShooter.Spawning;
using RogueShooter.Vision;

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
        public float MaxSegment;
        public int FoldCount;
        public MazeVec2[] Points;

        public bool Connects(string a, string b)
        {
            return (FromId == a && ToId == b) || (FromId == b && ToId == a);
        }

        public bool Contains(float x, float y, float pad)
        {
            if (Points != null && Points.Length >= 2)
            {
                for (int i = 0; i < Points.Length - 1; i++)
                {
                    if (SegContains(Points[i].X, Points[i].Y, Points[i + 1].X, Points[i + 1].Y, x, y, pad))
                        return true;
                }

                return false;
            }

            return SegContains(From.X, From.Y, To.X, To.Y, x, y, pad);
        }

        bool SegContains(float ax, float ay, float bx, float by, float x, float y, float pad)
        {
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
        public float AltarWalk;
        public float AltarWalkSeconds;
        public float FullWalk;
        public float FullWalkSeconds;
        public int FullWaves;
        public float FullCombatEstimate;
        public float FullTotalEstimate;
        public float MaxCorridorSeg;
        public float MaxCorridorSegSeconds;
        public float FirstHop;
        public float FirstHopSeconds;
        public bool ConnectorReachable;
        public bool AllReachable;
    }

    /// <summary>
    /// Spec v0.5 S1 maze. v2e lock: rooms 52×40 (Normal/Chest/Altar same),
    /// pitch 82×70, ortho door gap 30u. START neighbor is a fixed Normal.
    /// Four combat slots shuffle Altar×1+Chest×2+Normal×1; CONN follows Altar.
    /// No pure-walk time gate. Folds only on diagonal links.
    /// </summary>
    public static class MazeRules
    {
        public const float PlayMoveSpeed = 6f;
        /// <summary>Forwards to <see cref="CameraViewService.PlayOrthoSize"/> (6 ortho / 5.25 iso).</summary>
        public static float PlayOrtho => CameraViewService.PlayOrthoSize;
        /// <summary>v2e. Pitch = room + 30u gap. START door is a shorter feel hop (~18u), not a clock lock.</summary>
        public const float CombatWidth = 52f;
        public const float CombatHeight = 40f;
        public const float PitchX = 82f;
        public const float PitchY = 70f;
        public const float HubWidth = 52f;
        public const float HubHeight = 40f;
        public const float AltarWidth = 52f;
        public const float AltarHeight = 40f;
        public const float CorridorWidth = 8f;
        public const float CorridorSegMax = 30f;
        public const float CorridorSegMaxSeconds = 5f;
        /// <summary>START→N口 door gap feel (~3s @ 6). Not an ACCEPTANCE clock.</summary>
        public const float StartDoorGap = 18f;
        public const float StartPitchY = 58f;
        public const float WavePacingEstimateSeconds = 28f;
        /// <summary>
        /// Producer cadence table. PortalFx stays visible for the hold (clock starts at show).
        /// Wave 1 (enter): 1.0s. Inter-wave (chest/altar after clear w1): 2.5s.
        /// Inter-wave must be &gt;2.0s and ≤3.0s — not 1.0s and not 2.0s.
        /// </summary>
        public const float PortalHoldSeconds = 1.0f;
        public const float InterWavePortalHoldSeconds = 2.5f;
        /// <summary>Exclusive lower bound: inter-wave hold must be greater than this.</summary>
        public const float InterWavePortalHoldMinSeconds = 2.0f;
        public const float InterWavePortalHoldMaxSeconds = 3.0f;

        public static float PortalHoldForWave(int wave)
        {
            if (wave <= 1)
                return PortalHoldSeconds;
            float hold = InterWavePortalHoldSeconds;
            if (hold > InterWavePortalHoldMaxSeconds)
                hold = InterWavePortalHoldMaxSeconds;
            if (hold < 0f)
                hold = 0f;
            return hold;
        }

        public const int QuotaNormal = 2;
        public const int QuotaChest = 2;
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
