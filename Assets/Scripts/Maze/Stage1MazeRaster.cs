using System;
using System.Collections.Generic;
using System.Text;

namespace RogueShooter.Maze
{
    /// <summary>Cell content of <see cref="Stage1MazeRaster"/>.</summary>
    public enum MazeCell : byte
    {
        Empty = 0,
        /// <summary>Room floor (inside a node rect).</summary>
        Room = 1,
        /// <summary>Corridor body floor (5u band).</summary>
        Corridor = 2,
        /// <summary>Door line: the 3u floor cells touching a room (lock strip sits on their room edge).</summary>
        Mouth = 3,
        /// <summary>Wall ring: a non-floor cell in the 8-neighbourhood of a floor cell.</summary>
        Wall = 4
    }

    /// <summary>Axis-aligned run of whole wall cells [X, X+W) × [Y, Y+H) (world units, cell = 1u).</summary>
    public struct CellRect
    {
        public int X;
        public int Y;
        public int W;
        public int H;

        public float CenterX => X + W * 0.5f;
        public float CenterY => Y + H * 0.5f;
    }

    /// <summary>
    /// Stage-1 generator → whole-cell grid (cell (x, y) = world square [x, x+1] × [y, y+1]). Single source for
    /// the runtime Tilemap (floor / walls / door frames), the Rigidbody wall footprints and the CollisionWorld
    /// wall volumes (mobs / arrows / orbs). Pure (no UnityEngine).
    /// Rooms: node rects (integer centres, even 52×40 → whole cells). Corridors: 5u band centred on
    /// <see cref="MazeRules.DoorAxis"/>; the gap cell line touching each room is the 3u door (<see cref="MazeCell.Mouth"/>),
    /// its two outer lanes become the 1u wall stubs. Walls: only the ring around the floor (8-neighbourhood),
    /// never the whole bounding box.
    /// </summary>
    public sealed class Stage1MazeRaster
    {
        /// <summary>Empty cells kept around the level bounding box (ring + 1).</summary>
        public const int Margin = 2;

        public int MinX;
        public int MinY;
        public int W;
        public int H;
        public MazeCell[] Cells;
        /// <summary>Node index per room floor cell, −1 elsewhere.</summary>
        public short[] RoomIndex;
        public int FloorCount;
        public int RoomCellCount;
        public int CorridorCount;
        public int MouthCount;
        public int WallCount;
        public List<CellRect> WallRects = new List<CellRect>();
        public Stage1Maze Maze;

        public int MaxX => MinX + W;
        public int MaxY => MinY + H;
        /// <summary>Cells the old "every non-floor cell is a wall" fill would have produced.</summary>
        public int FullFillWallCount => W * H - FloorCount;

        public bool InBounds(int x, int y)
        {
            return x >= MinX && y >= MinY && x < MinX + W && y < MinY + H;
        }

        public MazeCell Get(int x, int y)
        {
            if (!InBounds(x, y))
                return MazeCell.Empty;
            return Cells[(y - MinY) * W + (x - MinX)];
        }

        public int RoomAt(int x, int y)
        {
            if (!InBounds(x, y))
                return -1;
            return RoomIndex[(y - MinY) * W + (x - MinX)];
        }

        public bool IsFloor(int x, int y)
        {
            MazeCell c = Get(x, y);
            return c == MazeCell.Room || c == MazeCell.Corridor || c == MazeCell.Mouth;
        }

        public bool IsWall(int x, int y)
        {
            return Get(x, y) == MazeCell.Wall;
        }

        public bool IsMouth(int x, int y)
        {
            return Get(x, y) == MazeCell.Mouth;
        }

        /// <summary>Cell containing a world point.</summary>
        public static int CellOf(float v)
        {
            return (int)Math.Floor(v);
        }

        void Set(int x, int y, MazeCell c)
        {
            if (!InBounds(x, y))
                return;
            Cells[(y - MinY) * W + (x - MinX)] = c;
        }

        public static Stage1MazeRaster Build(Stage1Maze maze)
        {
            if (maze == null || maze.Nodes == null || maze.Nodes.Length == 0)
                throw new ArgumentException("maze");
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                minX = Math.Min(minX, n.Center.X - n.Width * 0.5f);
                maxX = Math.Max(maxX, n.Center.X + n.Width * 0.5f);
                minY = Math.Min(minY, n.Center.Y - n.Height * 0.5f);
                maxY = Math.Max(maxY, n.Center.Y + n.Height * 0.5f);
            }

            float cw = MazeRules.CorridorWidth;
            if (maze.Edges != null)
            {
                for (int i = 0; i < maze.Edges.Length; i++)
                {
                    MazeVec2[] pts = Pts(maze.Edges[i]);
                    for (int p = 0; p < pts.Length; p++)
                    {
                        minX = Math.Min(minX, pts[p].X - cw);
                        maxX = Math.Max(maxX, pts[p].X + cw);
                        minY = Math.Min(minY, pts[p].Y - cw);
                        maxY = Math.Max(maxY, pts[p].Y + cw);
                    }
                }
            }

            var r = new Stage1MazeRaster { Maze = maze };
            r.MinX = (int)Math.Floor(minX) - Margin;
            r.MinY = (int)Math.Floor(minY) - Margin;
            r.W = (int)Math.Ceiling(maxX) + Margin - r.MinX;
            r.H = (int)Math.Ceiling(maxY) + Margin - r.MinY;
            r.Cells = new MazeCell[r.W * r.H];
            r.RoomIndex = new short[r.W * r.H];
            for (int i = 0; i < r.RoomIndex.Length; i++)
                r.RoomIndex[i] = -1;

            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                int x0 = (int)Math.Round(n.Center.X - n.Width * 0.5f);
                int x1 = (int)Math.Round(n.Center.X + n.Width * 0.5f);
                int y0 = (int)Math.Round(n.Center.Y - n.Height * 0.5f);
                int y1 = (int)Math.Round(n.Center.Y + n.Height * 0.5f);
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        r.Set(x, y, MazeCell.Room);
                        r.RoomIndex[(y - r.MinY) * r.W + (x - r.MinX)] = (short)i;
                    }
                }
            }

            if (maze.Edges != null)
            {
                for (int i = 0; i < maze.Edges.Length; i++)
                    r.PaintEdge(maze.Edges[i]);
            }

            r.BuildRing();
            r.MergeWalls();
            return r;
        }

        static MazeVec2[] Pts(MazeEdge e)
        {
            if (e.Points != null && e.Points.Length >= 2)
                return e.Points;
            return new[] { e.From, e.To };
        }

        /// <summary>5u band per segment (grown by half a band at bends), then 3u door lines at both room ends.</summary>
        void PaintEdge(MazeEdge e)
        {
            MazeVec2[] pts = Pts(e);
            int half = (int)Math.Round(MazeRules.CorridorWidth) / 2; // 2 → 5 cells
            int doorHalf = (int)Math.Round(MazeRules.DoorWidth) / 2; // 1 → 3 cells
            for (int s = 0; s < pts.Length - 1; s++)
            {
                MazeVec2 a = pts[s], b = pts[s + 1];
                bool horiz = Math.Abs(b.X - a.X) >= Math.Abs(b.Y - a.Y);
                int growA = s > 0 ? half + 1 : 0;
                int growB = s < pts.Length - 2 ? half + 1 : 0;
                if (horiz)
                {
                    int c = CellOf(a.Y);
                    bool pos = b.X >= a.X;
                    int lo = (int)Math.Round(Math.Min(a.X, b.X)) - (pos ? growA : growB);
                    int hi = (int)Math.Round(Math.Max(a.X, b.X)) - 1 + (pos ? growB : growA);
                    for (int x = lo; x <= hi; x++)
                        for (int y = c - half; y <= c + half; y++)
                            if (Get(x, y) == MazeCell.Empty)
                                Set(x, y, MazeCell.Corridor);
                }
                else
                {
                    int c = CellOf(a.X);
                    bool pos = b.Y >= a.Y;
                    int lo = (int)Math.Round(Math.Min(a.Y, b.Y)) - (pos ? growA : growB);
                    int hi = (int)Math.Round(Math.Max(a.Y, b.Y)) - 1 + (pos ? growB : growA);
                    for (int y = lo; y <= hi; y++)
                        for (int x = c - half; x <= c + half; x++)
                            if (Get(x, y) == MazeCell.Empty)
                                Set(x, y, MazeCell.Corridor);
                }
            }

            DoorLine(pts[0], pts[1], half, doorHalf);
            DoorLine(pts[pts.Length - 1], pts[pts.Length - 2], half, doorHalf);
        }

        /// <summary>Gap cell line touching the room at door point <paramref name="door"/>: 3 mouth cells, stubs cleared.</summary>
        void DoorLine(MazeVec2 door, MazeVec2 toward, int half, int doorHalf)
        {
            bool horiz = Math.Abs(toward.X - door.X) >= Math.Abs(toward.Y - door.Y);
            if (horiz)
            {
                int x = toward.X >= door.X ? (int)Math.Round(door.X) : (int)Math.Round(door.X) - 1;
                int c = CellOf(door.Y);
                for (int y = c - half; y <= c + half; y++)
                {
                    if (Get(x, y) == MazeCell.Room)
                        continue;
                    Set(x, y, Math.Abs(y - c) <= doorHalf ? MazeCell.Mouth : MazeCell.Empty);
                }
            }
            else
            {
                int y = toward.Y >= door.Y ? (int)Math.Round(door.Y) : (int)Math.Round(door.Y) - 1;
                int c = CellOf(door.X);
                for (int x = c - half; x <= c + half; x++)
                {
                    if (Get(x, y) == MazeCell.Room)
                        continue;
                    Set(x, y, Math.Abs(x - c) <= doorHalf ? MazeCell.Mouth : MazeCell.Empty);
                }
            }
        }

        void BuildRing()
        {
            FloorCount = RoomCellCount = CorridorCount = MouthCount = WallCount = 0;
            for (int y = MinY; y < MinY + H; y++)
            {
                for (int x = MinX; x < MinX + W; x++)
                {
                    MazeCell c = Get(x, y);
                    if (c == MazeCell.Room) RoomCellCount++;
                    else if (c == MazeCell.Corridor) CorridorCount++;
                    else if (c == MazeCell.Mouth) MouthCount++;
                }
            }

            FloorCount = RoomCellCount + CorridorCount + MouthCount;
            for (int y = MinY; y < MinY + H; y++)
            {
                for (int x = MinX; x < MinX + W; x++)
                {
                    if (Get(x, y) != MazeCell.Empty)
                        continue;
                    if (FloorNeighbour8(x, y))
                    {
                        Set(x, y, MazeCell.Wall);
                        WallCount++;
                    }
                }
            }
        }

        public bool FloorNeighbour8(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && IsFloor(x + dx, y + dy))
                        return true;
            return false;
        }

        /// <summary>Row runs merged down identical spans: straight ring walls become one rect each.</summary>
        void MergeWalls()
        {
            WallRects.Clear();
            var open = new Dictionary<long, int>();
            var next = new Dictionary<long, int>();
            for (int y = MinY; y < MinY + H; y++)
            {
                next.Clear();
                int x = MinX;
                while (x < MinX + W)
                {
                    if (Get(x, y) != MazeCell.Wall)
                    {
                        x++;
                        continue;
                    }

                    int x0 = x;
                    while (x < MinX + W && Get(x, y) == MazeCell.Wall)
                        x++;
                    long key = ((long)x0 << 32) ^ (uint)(x - x0);
                    int idx;
                    if (open.TryGetValue(key, out idx))
                    {
                        CellRect rc = WallRects[idx];
                        rc.H++;
                        WallRects[idx] = rc;
                    }
                    else
                    {
                        idx = WallRects.Count;
                        WallRects.Add(new CellRect { X = x0, Y = y, W = x - x0, H = 1 });
                    }

                    next[key] = idx;
                }

                var t = open;
                open = next;
                next = t;
            }
        }

        /// <summary>
        /// Null on pass: every non-floor 8-neighbour of a floor cell is a wall, every wall touches floor,
        /// wall rects cover exactly the wall cells, and no floor cell touches the grid border.
        /// </summary>
        public string CheckRing()
        {
            for (int y = MinY; y < MinY + H; y++)
            {
                for (int x = MinX; x < MinX + W; x++)
                {
                    MazeCell c = Get(x, y);
                    bool floor = c == MazeCell.Room || c == MazeCell.Corridor || c == MazeCell.Mouth;
                    if (floor)
                    {
                        if (x <= MinX || y <= MinY || x >= MinX + W - 1 || y >= MinY + H - 1)
                            return "floor on raster border " + x + "," + y;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0)
                                    continue;
                                if (!IsFloor(x + dx, y + dy) && !IsWall(x + dx, y + dy))
                                    return "ring gap at " + (x + dx) + "," + (y + dy) + " next to floor " + x + "," + y;
                            }
                    }
                    else if (c == MazeCell.Wall && !FloorNeighbour8(x, y))
                    {
                        return "wall off the ring at " + x + "," + y;
                    }
                }
            }

            int covered = 0;
            for (int i = 0; i < WallRects.Count; i++)
            {
                CellRect rc = WallRects[i];
                for (int y = rc.Y; y < rc.Y + rc.H; y++)
                    for (int x = rc.X; x < rc.X + rc.W; x++)
                    {
                        if (!IsWall(x, y))
                            return "wall rect covers non-wall " + x + "," + y;
                        covered++;
                    }
            }

            if (covered != WallCount)
                return "wall rects cover " + covered + " of " + WallCount + " wall cells (overlap/miss)";
            return null;
        }

        /// <summary>Floor art family: room / chest / altar (by node kind) or corridor (corridor + door line).</summary>
        public string FloorKind(int x, int y)
        {
            MazeCell c = Get(x, y);
            if (c == MazeCell.Corridor || c == MazeCell.Mouth)
                return "corridor";
            int ri = RoomAt(x, y);
            if (c != MazeCell.Room || ri < 0 || Maze == null)
                return null;
            switch (Maze.Nodes[ri].Kind)
            {
                case MazeNodeKind.Chest:
                case MazeNodeKind.LargeChest:
                    return "chest";
                case MazeNodeKind.Altar:
                    return "altar";
                default:
                    return "room";
            }
        }

        /// <summary>
        /// Wall art for a ring cell (same rules as the old painted builder): door-line neighbours get the
        /// opening frames (the 1u stubs), otherwise face / side / corner by which neighbour is floor.
        /// </summary>
        public string WallTileId(int x, int y)
        {
            if (IsMouth(x, y - 1)) return "jh_wall_s1_stone_opening_s";
            if (IsMouth(x, y + 1)) return "jh_wall_s1_stone_opening_n";
            if (IsMouth(x - 1, y)) return "jh_wall_s1_stone_opening_w";
            if (IsMouth(x + 1, y)) return "jh_wall_s1_stone_opening_e";
            if (IsFloor(x, y - 1)) return "jh_wall_s1_stone_face";
            if (IsFloor(x + 1, y)) return "jh_wall_s1_stone_side_e";
            if (IsFloor(x - 1, y)) return "jh_wall_s1_stone_side_w";
            if (IsFloor(x + 1, y - 1)) return "jh_wall_s1_stone_corner_se";
            if (IsFloor(x - 1, y - 1)) return "jh_wall_s1_stone_corner_sw";
            if (IsFloor(x + 1, y + 1)) return "jh_wall_s1_stone_corner_ne";
            if (IsFloor(x - 1, y + 1)) return "jh_wall_s1_stone_corner_nw";
            return "jh_wall_s1_stone_top";
        }

        /// <summary>
        /// Perf upper bound: 5 × 4 grid of 52×40 rooms on the generator pitches (82 / START 58 + 70 + 70) with every
        /// neighbour joined — bounding box 380×238, the largest footprint Stage-1 templates can reach.
        /// </summary>
        public static Stage1Maze StressMaze()
        {
            const int cols = 5, rows = 4;
            float[] ys = { 0f, MazeRules.StartPitchY, MazeRules.StartPitchY + MazeRules.PitchY, MazeRules.StartPitchY + 2f * MazeRules.PitchY };
            var nodes = new MazeNode[cols * rows];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    nodes[r * cols + c] = new MazeNode
                    {
                        Id = "R" + c + "_" + r,
                        Kind = r == 0 && c == 0 ? MazeNodeKind.Start : MazeNodeKind.Normal,
                        Center = new MazeVec2(c * MazeRules.PitchX, ys[r]),
                        Width = MazeRules.CombatWidth,
                        Height = MazeRules.CombatHeight,
                        NeighborIds = new string[0]
                    };
            var edges = new List<MazeEdge>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    MazeNode a = nodes[r * cols + c];
                    if (c + 1 < cols)
                        edges.Add(StressEdge(a, nodes[r * cols + c + 1], true));
                    if (r + 1 < rows)
                        edges.Add(StressEdge(a, nodes[(r + 1) * cols + c], false));
                }

            return new Stage1Maze { Seed = -1, TemplateId = "STRESS_5x4", Nodes = nodes, Edges = edges.ToArray(), Signature = "stress5x4" };
        }

        static MazeEdge StressEdge(MazeNode a, MazeNode b, bool horiz)
        {
            MazeVec2 pa = horiz
                ? new MazeVec2(a.Center.X + a.Width * 0.5f, MazeRules.DoorAxis(a.Center.Y))
                : new MazeVec2(MazeRules.DoorAxis(a.Center.X), a.Center.Y + a.Height * 0.5f);
            MazeVec2 pb = horiz
                ? new MazeVec2(b.Center.X - b.Width * 0.5f, MazeRules.DoorAxis(b.Center.Y))
                : new MazeVec2(MazeRules.DoorAxis(b.Center.X), b.Center.Y - b.Height * 0.5f);
            float len = pa.Dist(pb);
            return new MazeEdge { FromId = a.Id, ToId = b.Id, From = pa, To = pb, Width = MazeRules.CorridorWidth, Length = len, MaxSegment = len, Points = new[] { pa, pb } };
        }

        /// <summary>Debug ASCII (# wall, . room, : corridor, D mouth).</summary>
        public string Ascii()
        {
            var sb = new StringBuilder();
            for (int y = MinY + H - 1; y >= MinY; y--)
            {
                for (int x = MinX; x < MinX + W; x++)
                {
                    switch (Get(x, y))
                    {
                        case MazeCell.Wall: sb.Append('#'); break;
                        case MazeCell.Room: sb.Append('.'); break;
                        case MazeCell.Corridor: sb.Append(':'); break;
                        case MazeCell.Mouth: sb.Append('D'); break;
                        default: sb.Append(' '); break;
                    }
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        public string Describe()
        {
            return "raster " + W + "x" + H + " floor=" + FloorCount + " (room " + RoomCellCount + " corridor " + CorridorCount
                   + " mouth " + MouthCount + ") ringWalls=" + WallCount + " wallRects=" + WallRects.Count
                   + " fullFillWouldBe=" + FullFillWallCount;
        }
    }
}
