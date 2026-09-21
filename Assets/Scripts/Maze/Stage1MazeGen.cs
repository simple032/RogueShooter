using System;
using System.Collections.Generic;
using System.Text;

namespace RogueShooter.Maze
{
    /// <summary>
    /// Seeded Stage-1 maze only (Spec v0.5 §1/§3). Quota: Chest×2 + Altar×1 +
    /// Normal×2 + START + connector stub. Corridors are edges and never spawn.
    /// Same seed → same graph. No S2/S3 layouts.
    /// v2e: rooms 52×40, pitch 82×70, N口 fixed Normal, 4-slot shuffle,
    /// CONN follows Altar. Ordinary Chest×2 (no LargeChest upgrade).
    /// START door ~18u feel. Folds only if diagonal.
    /// </summary>
    public static class Stage1MazeGen
    {
        struct Slot
        {
            public int Col;
            public int Row;
            public bool Combat;
            public bool Start;
            public bool Connector;
        }

        struct Tpl
        {
            public string Id;
            public Slot Start;
            public Slot Entry;
            public Slot[] Shuffle;
            public int[] ConnDx;
            public int[] ConnDy;
            public int[] EdgeA;
            public int[] EdgeB;
        }

        static readonly Tpl[] Templates = BuildTemplates();

        public static Stage1Maze Generate(int seed)
        {
            var rng = new Random(seed);
            Tpl tpl = Templates[Mod(seed, Templates.Length)];
            MazeNodeKind[] shuffleKinds =
            {
                MazeNodeKind.Altar, MazeNodeKind.Chest, MazeNodeKind.Chest, MazeNodeKind.Normal
            };
            Shuffle(shuffleKinds, rng);

            int startCol = tpl.Start.Col;
            int startRow = tpl.Start.Row;

            var nodes = new MazeNode[7];
            nodes[0] = MakeNode("START", MazeNodeKind.Start,
                CellX(tpl.Start.Col, startCol), CellY(tpl.Start.Row, startRow),
                MazeRules.HubWidth, MazeRules.HubHeight);
            nodes[1] = MakeNode("N1", MazeNodeKind.Normal,
                CellX(tpl.Entry.Col, startCol), CellY(tpl.Entry.Row, startRow),
                MazeRules.CombatWidth, MazeRules.CombatHeight);

            int normalIx = 2;
            int chestIx = 1;
            int altarNode = -1;
            int altarShuffle = -1;
            for (int i = 0; i < 4; i++)
            {
                MazeNodeKind kind = shuffleKinds[i];
                string id;
                if (kind == MazeNodeKind.Normal)
                    id = "N" + (normalIx++);
                else if (kind == MazeNodeKind.Altar)
                    id = "ALTAR";
                else
                {
                    id = chestIx == 1 ? "CHEST" : "CHEST" + chestIx;
                    chestIx++;
                }

                Slot s = tpl.Shuffle[i];
                nodes[2 + i] = MakeNode(id, kind,
                    CellX(s.Col, startCol), CellY(s.Row, startRow),
                    MazeRules.CombatWidth, MazeRules.CombatHeight);
                if (kind == MazeNodeKind.Altar)
                {
                    altarNode = 2 + i;
                    altarShuffle = i;
                }
            }

            if (altarNode < 0)
                altarNode = 2;

            Slot altarSlot = tpl.Shuffle[altarShuffle < 0 ? 0 : altarShuffle];
            int connCol = altarSlot.Col + tpl.ConnDx[altarShuffle < 0 ? 0 : altarShuffle];
            int connRow = altarSlot.Row + tpl.ConnDy[altarShuffle < 0 ? 0 : altarShuffle];
            nodes[6] = MakeNode("CONN", MazeNodeKind.Connector,
                CellX(connCol, startCol), CellY(connRow, startRow),
                MazeRules.CombatWidth, MazeRules.CombatHeight);

            var edges = new MazeEdge[tpl.EdgeA.Length + 1];
            for (int e = 0; e < tpl.EdgeA.Length; e++)
                edges[e] = MakeEdge(nodes[tpl.EdgeA[e]], nodes[tpl.EdgeB[e]]);
            edges[tpl.EdgeA.Length] = MakeEdge(nodes[altarNode], nodes[6]);

            FillNeighbors(nodes, edges);
            var maze = new Stage1Maze
            {
                Seed = seed,
                TemplateId = tpl.Id,
                LargeChestUpgraded = false,
                Nodes = nodes,
                Edges = edges
            };
            maze.Signature = BuildSignature(maze);
            return maze;
        }

        public static int DrawSeed(int mazeSeed, string roomId, int waveIndex)
        {
            unchecked
            {
                int h = mazeSeed * 397;
                if (!string.IsNullOrEmpty(roomId))
                {
                    for (int i = 0; i < roomId.Length; i++)
                        h = (h * 31) + roomId[i];
                }

                h = (h * 17) + waveIndex * 10007;
                if (h == int.MinValue)
                    h = 0;
                return h;
            }
        }

        public static Random DrawRng(int mazeSeed, string roomId, int waveIndex)
        {
            return new Random(DrawSeed(mazeSeed, roomId, waveIndex));
        }

        public static MazePacing MeasurePacing(Stage1Maze maze, float moveSpeed)
        {
            if (moveSpeed < 0.01f)
                moveSpeed = MazeRules.PlayMoveSpeed;
            var pace = new MazePacing();
            if (maze == null || maze.Nodes == null)
                return pace;

            bool[] reach = ReachableFrom(maze, "START");
            pace.AllReachable = true;
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                if (!reach[i])
                    pace.AllReachable = false;
            }

            int conn = IndexOf(maze, "CONN");
            pace.ConnectorReachable = conn >= 0 && reach[conn];

            List<int> path;
            pace.ShortestWalk = ShortestPath(maze, "START", "CONN", out path);
            pace.ShortestWalkSeconds = pace.ShortestWalk / moveSpeed;
            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    MazeNode n = maze.Nodes[path[i]];
                    if (n.SpawnsEnemies)
                    {
                        pace.ShortestCombatRooms++;
                        pace.ShortestWaves += n.WaveCount;
                    }
                }
            }

            pace.ShortestCombatEstimate = pace.ShortestWaves * MazeRules.WavePacingEstimateSeconds;
            pace.ShortestTotalEstimate = pace.ShortestWalkSeconds + pace.ShortestCombatEstimate;

            string altarId = "ALTAR";
            if (IndexOf(maze, altarId) < 0)
                altarId = "CONN";
            List<int> altarPath;
            pace.AltarWalk = ShortestPath(maze, "START", altarId, out altarPath);
            pace.AltarWalkSeconds = pace.AltarWalk / moveSpeed;

            List<int> tour;
            pace.FullWalk = GreedyVisitAll(maze, out tour);
            pace.FullWalkSeconds = pace.FullWalk / moveSpeed;
            pace.FullWaves = maze.FullClearWaveCount();
            pace.FullCombatEstimate = pace.FullWaves * MazeRules.WavePacingEstimateSeconds;
            pace.FullTotalEstimate = pace.FullWalkSeconds + pace.FullCombatEstimate;
            float mx = 0f;
            if (maze.Edges != null)
            {
                for (int i = 0; i < maze.Edges.Length; i++)
                {
                    if (maze.Edges[i].MaxSegment > mx)
                        mx = maze.Edges[i].MaxSegment;
                }
            }

            pace.MaxCorridorSeg = mx;
            pace.MaxCorridorSegSeconds = mx / moveSpeed;

            MazeNode start = maze.Find("START");
            if (start != null && start.NeighborIds != null && start.NeighborIds.Length > 0)
            {
                MazeNode first = maze.Find(start.NeighborIds[0]);
                if (first != null)
                {
                    MazeEdge hop = null;
                    if (maze.Edges != null)
                    {
                        for (int i = 0; i < maze.Edges.Length; i++)
                        {
                            if (maze.Edges[i].Connects(start.Id, first.Id))
                            {
                                hop = maze.Edges[i];
                                break;
                            }
                        }
                    }

                    // Door-to-door net (START edge ~18u / ~3s feel). Not a clock lock.
                    pace.FirstHop = hop != null ? hop.Length : start.Center.Dist(first.Center);
                    pace.FirstHopSeconds = pace.FirstHop / moveSpeed;
                }
            }

            return pace;
        }

        public static string FormatGraph(Stage1Maze maze)
        {
            if (maze == null)
                return "";
            var sb = new StringBuilder();
            sb.Append("seed=").Append(maze.Seed);
            sb.Append(" tpl=").Append(maze.TemplateId);
            sb.Append(" largeChest=").Append(maze.LargeChestUpgraded ? 1 : 0);
            sb.Append(" rooms=");
            if (maze.Nodes != null)
            {
                for (int i = 0; i < maze.Nodes.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    MazeNode n = maze.Nodes[i];
                    sb.Append(n.Id).Append('(').Append(MazeRules.Label(n.Kind)).Append('@').Append(n.Center).Append(')');
                }
            }

            sb.Append(" edges=");
            if (maze.Edges != null)
            {
                for (int i = 0; i < maze.Edges.Length; i++)
                {
                    if (i > 0) sb.Append(';');
                    sb.Append(maze.Edges[i].FromId).Append("--").Append(maze.Edges[i].ToId);
                }
            }

            return sb.ToString();
        }

        public static string FormatQuota(Stage1Maze maze)
        {
            int chest = maze.CountKind(MazeNodeKind.Chest);
            return "quota Chest=" + chest
                + " Altar=" + maze.CountKind(MazeNodeKind.Altar)
                + " Normal=" + maze.CountKind(MazeNodeKind.Normal)
                + " Start=" + maze.CountKind(MazeNodeKind.Start)
                + " Connector=stub";
        }

        static void FillNeighbors(MazeNode[] nodes, MazeEdge[] edges)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var ids = new List<string>();
                for (int e = 0; e < edges.Length; e++)
                {
                    if (edges[e].FromId == nodes[i].Id)
                        ids.Add(edges[e].ToId);
                    else if (edges[e].ToId == nodes[i].Id)
                        ids.Add(edges[e].FromId);
                }

                nodes[i].NeighborIds = ids.ToArray();
            }
        }

        static string BuildSignature(Stage1Maze maze)
        {
            var sb = new StringBuilder();
            sb.Append(maze.Seed).Append('|').Append(maze.TemplateId).Append('|');
            sb.Append(maze.LargeChestUpgraded ? "L" : "C").Append('|');
            var names = new List<string>();
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                names.Add(n.Id + ":" + (int)n.Kind + "@" + n.Center.X.ToString("0") + "," + n.Center.Y.ToString("0"));
            }

            names.Sort(StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(names[i]);
            }

            sb.Append('|');
            var eds = new List<string>();
            for (int i = 0; i < maze.Edges.Length; i++)
            {
                string a = maze.Edges[i].FromId;
                string b = maze.Edges[i].ToId;
                if (string.CompareOrdinal(a, b) > 0)
                {
                    string t = a;
                    a = b;
                    b = t;
                }

                eds.Add(a + "--" + b);
            }

            eds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < eds.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(eds[i]);
            }

            return sb.ToString();
        }

        static bool[] ReachableFrom(Stage1Maze maze, string startId)
        {
            var seen = new bool[maze.Nodes.Length];
            int s = IndexOf(maze, startId);
            if (s < 0)
                return seen;
            var q = new Queue<int>();
            q.Enqueue(s);
            seen[s] = true;
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                string[] nb = maze.Nodes[i].NeighborIds;
                if (nb == null)
                    continue;
                for (int k = 0; k < nb.Length; k++)
                {
                    int j = IndexOf(maze, nb[k]);
                    if (j >= 0 && !seen[j])
                    {
                        seen[j] = true;
                        q.Enqueue(j);
                    }
                }
            }

            return seen;
        }

        static float ShortestPath(Stage1Maze maze, string fromId, string toId, out List<int> path)
        {
            path = null;
            int n = maze.Nodes.Length;
            int src = IndexOf(maze, fromId);
            int dst = IndexOf(maze, toId);
            if (src < 0 || dst < 0)
                return 0f;
            var dist = new float[n];
            var prev = new int[n];
            var used = new bool[n];
            for (int i = 0; i < n; i++)
            {
                dist[i] = float.PositiveInfinity;
                prev[i] = -1;
            }

            dist[src] = 0f;
            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < n; i++)
                {
                    if (!used[i] && dist[i] < best)
                    {
                        best = dist[i];
                        u = i;
                    }
                }

                if (u < 0)
                    break;
                used[u] = true;
                if (u == dst)
                    break;
                string[] nb = maze.Nodes[u].NeighborIds;
                if (nb == null)
                    continue;
                for (int k = 0; k < nb.Length; k++)
                {
                    int v = IndexOf(maze, nb[k]);
                    if (v < 0)
                        continue;
                    float w = EdgeWalk(maze, u, v);
                    if (dist[u] + w < dist[v])
                    {
                        dist[v] = dist[u] + w;
                        prev[v] = u;
                    }
                }
            }

            if (float.IsInfinity(dist[dst]))
                return 0f;
            path = new List<int>();
            for (int cur = dst; cur >= 0; cur = prev[cur])
                path.Add(cur);
            path.Reverse();
            return dist[dst];
        }

        static float GreedyVisitAll(Stage1Maze maze, out List<int> tour)
        {
            tour = new List<int>();
            int start = IndexOf(maze, "START");
            if (start < 0)
                return 0f;
            var need = new List<int>();
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                if (i == start)
                    continue;
                need.Add(i);
            }

            float total = 0f;
            int cur = start;
            tour.Add(cur);
            while (need.Count > 0)
            {
                int best = -1;
                float bestD = float.PositiveInfinity;
                List<int> bestPath = null;
                for (int i = 0; i < need.Count; i++)
                {
                    List<int> p;
                    float d = ShortestPath(maze, maze.Nodes[cur].Id, maze.Nodes[need[i]].Id, out p);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = i;
                        bestPath = p;
                    }
                }

                if (best < 0)
                    break;
                total += bestD;
                if (bestPath != null)
                {
                    for (int i = 1; i < bestPath.Count; i++)
                        tour.Add(bestPath[i]);
                }

                cur = need[best];
                need.RemoveAt(best);
            }

            return total;
        }

        static int IndexOf(Stage1Maze maze, string id)
        {
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                if (maze.Nodes[i].Id == id)
                    return i;
            }

            return -1;
        }

        static void Shuffle(MazeNodeKind[] items, Random rng)
        {
            for (int i = items.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                MazeNodeKind t = items[i];
                items[i] = items[j];
                items[j] = t;
            }
        }

        static int Mod(int value, int m)
        {
            int r = value % m;
            return r < 0 ? r + m : r;
        }

        static Tpl[] BuildTemplates()
        {
            // Approved 12-panel review. START + N口(Normal) fixed. Four shuffle
            // slots Altar/Chest/Chest/Normal. CONN offset is per-Altar-slot.
            // 0=START 1=N口 2..5=shuffle. CONN is appended as node 6.
            return new[]
            {
                new Tpl
                {
                    Id = "I",
                    Start = S(1, 0, true, false, false),
                    Entry = S(1, 1, false, true, false),
                    Shuffle = new[]
                    {
                        S(0, 1, false, true, false),
                        S(2, 1, false, true, false),
                        S(0, 2, false, true, false),
                        S(1, 2, false, true, false)
                    },
                    // I-1 W west, I-2 E east, I-3 NW west, I-4 N north (12-panel).
                    ConnDx = new[] { -1, 1, -1, 0 },
                    ConnDy = new[] { 0, 0, 0, 1 },
                    EdgeA = new[] { 0, 1, 1, 1, 2, 4 },
                    EdgeB = new[] { 1, 2, 3, 5, 4, 5 }
                },
                new Tpl
                {
                    Id = "Z",
                    Start = S(1, 0, true, false, false),
                    Entry = S(1, 1, false, true, false),
                    Shuffle = new[]
                    {
                        S(0, 1, false, true, false),
                        S(2, 1, false, true, false),
                        S(3, 1, false, true, false),
                        S(2, 2, false, true, false)
                    },
                    ConnDx = new[] { -1, 0, 1, 0 },
                    ConnDy = new[] { 0, -1, 0, 1 },
                    EdgeA = new[] { 0, 1, 1, 3, 3 },
                    EdgeB = new[] { 1, 2, 3, 4, 5 }
                },
                new Tpl
                {
                    Id = "C",
                    Start = S(1, 0, true, false, false),
                    Entry = S(1, 1, false, true, false),
                    Shuffle = new[]
                    {
                        S(0, 1, false, true, false),
                        S(2, 1, false, true, false),
                        S(1, 2, false, true, false),
                        S(2, 2, false, true, false)
                    },
                    ConnDx = new[] { -1, 1, 0, 1 },
                    ConnDy = new[] { 0, 0, 1, 0 },
                    EdgeA = new[] { 0, 1, 1, 1, 4, 3 },
                    EdgeB = new[] { 1, 2, 3, 4, 5, 5 }
                }
            };
        }

        static Slot S(int col, int row, bool start, bool combat, bool connector)
        {
            return new Slot { Col = col, Row = row, Start = start, Combat = combat, Connector = connector };
        }

        static float CellX(int col, int startCol)
        {
            return (col - startCol) * MazeRules.PitchX;
        }

        static float CellY(int row, int startRow)
        {
            if (row <= startRow)
                return (row - startRow) * MazeRules.StartPitchY;
            return MazeRules.StartPitchY + (row - startRow - 1) * MazeRules.PitchY;
        }

        static MazeNode MakeNode(string id, MazeNodeKind kind, float x, float y, float w, float h)
        {
            return new MazeNode
            {
                Id = id,
                Kind = kind,
                Center = new MazeVec2(x, y),
                Width = w,
                Height = h
            };
        }

        static MazeEdge MakeEdge(MazeNode a, MazeNode b)
        {
            MazeVec2 doorA = DoorToward(a, b);
            MazeVec2 doorB = DoorToward(b, a);
            MazeVec2[] pts = BuildCorridor(doorA, doorB);
            float len;
            float maxSeg;
            MeasurePoly(pts, out len, out maxSeg);
            return new MazeEdge
            {
                FromId = a.Id,
                ToId = b.Id,
                From = doorA,
                To = doorB,
                Width = MazeRules.CorridorWidth,
                Length = len,
                MaxSegment = maxSeg,
                FoldCount = pts.Length - 1,
                Points = pts
            };
        }

        static float EdgeWalk(Stage1Maze maze, int u, int v)
        {
            // Center-to-center one-pitch hops. Door polylines stay ≤30u.
            return maze.Nodes[u].Center.Dist(maze.Nodes[v].Center);
        }

        static MazeVec2 DoorToward(MazeNode self, MazeNode other)
        {
            float dx = other.Center.X - self.Center.X;
            float dy = other.Center.Y - self.Center.Y;
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                float sx = dx >= 0f ? 1f : -1f;
                return new MazeVec2(self.Center.X + sx * self.Width * 0.5f, self.Center.Y);
            }

            float sy = dy >= 0f ? 1f : -1f;
            return new MazeVec2(self.Center.X, self.Center.Y + sy * self.Height * 0.5f);
        }

        static MazeVec2[] BuildCorridor(MazeVec2 a, MazeVec2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            bool ortho = Math.Abs(dx) < 0.51f || Math.Abs(dy) < 0.51f;
            if (ortho)
                return new[] { a, b };

            // Diagonal only: one L-bend. Never zigzag to pad seconds.
            var corner = Math.Abs(dx) >= Math.Abs(dy)
                ? new MazeVec2(b.X, a.Y)
                : new MazeVec2(a.X, b.Y);
            return new[] { a, corner, b };
        }

        static void MeasurePoly(MazeVec2[] pts, out float len, out float maxSeg)
        {
            len = 0f;
            maxSeg = 0f;
            if (pts == null)
                return;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                float d = pts[i].Dist(pts[i + 1]);
                len += d;
                if (d > maxSeg)
                    maxSeg = d;
            }
        }
    }
}
