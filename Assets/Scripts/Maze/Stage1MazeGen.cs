using System;
using System.Collections.Generic;
using System.Text;

namespace RogueShooter.Maze
{
    /// <summary>
    /// Seeded Stage-1 maze only (Spec v0.5 §1/§3). Quota: Chest×1 + Altar×1 +
    /// Normal×2 + START + connector stub. Corridors are edges and never spawn.
    /// Same seed → same graph. No S2/S3 layouts.
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
            public Slot[] Slots;
            public int[] EdgeA;
            public int[] EdgeB;
            public int BranchFolds;
            public float BranchMinLen;
        }

        static readonly Tpl[] Templates = BuildTemplates();

        public static Stage1Maze Generate(int seed)
        {
            var rng = new Random(seed);
            Tpl tpl = Templates[Mod(seed, Templates.Length)];
            MazeNodeKind[] combatKinds = { MazeNodeKind.Normal, MazeNodeKind.Normal, MazeNodeKind.Chest, MazeNodeKind.Altar };
            Shuffle(combatKinds, rng);
            bool large = rng.NextDouble() < MazeRules.LargeChestChance;

            int startCol = 0;
            int startRow = 0;
            for (int i = 0; i < tpl.Slots.Length; i++)
            {
                if (tpl.Slots[i].Start)
                {
                    startCol = tpl.Slots[i].Col;
                    startRow = tpl.Slots[i].Row;
                    break;
                }
            }

            var nodes = new MazeNode[tpl.Slots.Length];
            int combatIx = 0;
            int normalIx = 1;
            for (int i = 0; i < tpl.Slots.Length; i++)
            {
                Slot s = tpl.Slots[i];
                MazeNodeKind kind;
                string id;
                float w;
                float h;
                if (s.Start)
                {
                    kind = MazeNodeKind.Start;
                    id = "START";
                    w = MazeRules.HubWidth;
                    h = MazeRules.HubHeight;
                }
                else if (s.Connector)
                {
                    kind = MazeNodeKind.Connector;
                    id = "CONN";
                    w = MazeRules.HubWidth;
                    h = MazeRules.HubHeight;
                }
                else
                {
                    kind = combatKinds[combatIx++];
                    if (kind == MazeNodeKind.Chest && large)
                        kind = MazeNodeKind.LargeChest;
                    if (kind == MazeNodeKind.Normal)
                        id = "N" + (normalIx++);
                    else if (kind == MazeNodeKind.Altar)
                        id = "ALTAR";
                    else if (kind == MazeNodeKind.LargeChest)
                        id = "LARGE";
                    else
                        id = "CHEST";
                    w = MazeRules.CombatWidth;
                    h = MazeRules.CombatHeight;
                }

                nodes[i] = new MazeNode
                {
                    Id = id,
                    Kind = kind,
                    Center = new MazeVec2(
                        (s.Col - startCol) * MazeRules.PitchX,
                        (s.Row - startRow) * MazeRules.PitchY),
                    Width = w,
                    Height = h
                };
            }

            var edges = new MazeEdge[tpl.EdgeA.Length];
            for (int e = 0; e < tpl.EdgeA.Length; e++)
            {
                MazeNode a = nodes[tpl.EdgeA[e]];
                MazeNode b = nodes[tpl.EdgeB[e]];
                MazeVec2 doorA = DoorToward(a, b);
                MazeVec2 doorB = DoorToward(b, a);
                bool stem = a.Kind == MazeNodeKind.Start || b.Kind == MazeNodeKind.Start;
                int folds = stem ? MazeRules.FoldStem : tpl.BranchFolds;
                float minLen = stem ? MazeRules.FoldStemMinLen : tpl.BranchMinLen;
                MazeVec2[] pts = BuildFold(doorA, doorB, folds, minLen, MazeRules.CorridorSegMax);
                float len;
                float maxSeg;
                MeasurePoly(pts, out len, out maxSeg);
                edges[e] = new MazeEdge
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

            FillNeighbors(nodes, edges);
            var maze = new Stage1Maze
            {
                Seed = seed,
                TemplateId = tpl.Id,
                LargeChestUpgraded = large,
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
            int chest = maze.CountKind(MazeNodeKind.Chest) + maze.CountKind(MazeNodeKind.LargeChest);
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
            // Compact adjacent rooms (Pitch 66/58). Time comes from folded
            // short corridors (≤30u / 5s per segment), not long straight pitch.
            // I/Z: stem ≥12 folds, branches ≥5; C: branches ≥3 (use 4).
            return new[]
            {
                new Tpl
                {
                    Id = "I",
                    Slots = new[]
                    {
                        S(1, 0, true, false, false),
                        S(1, 1, false, true, false),
                        S(0, 1, false, true, false),
                        S(2, 1, false, true, false),
                        S(1, 2, false, true, false),
                        S(2, 2, false, false, true)
                    },
                    EdgeA = new[] { 0, 1, 1, 1, 4 },
                    EdgeB = new[] { 1, 2, 3, 4, 5 },
                    BranchFolds = MazeRules.FoldBranchIZ,
                    BranchMinLen = MazeRules.FoldBranchIZMinLen
                },
                new Tpl
                {
                    Id = "Z",
                    Slots = new[]
                    {
                        S(1, 0, true, false, false),
                        S(1, 1, false, true, false),
                        S(2, 1, false, true, false),
                        S(0, 1, false, true, false),
                        S(1, 2, false, true, false),
                        S(0, 2, false, false, true)
                    },
                    EdgeA = new[] { 0, 1, 1, 1, 4 },
                    EdgeB = new[] { 1, 2, 3, 4, 5 },
                    BranchFolds = MazeRules.FoldBranchIZ,
                    BranchMinLen = MazeRules.FoldBranchIZMinLen
                },
                new Tpl
                {
                    Id = "C",
                    Slots = new[]
                    {
                        S(1, 0, true, false, false),
                        S(1, 1, false, true, false),
                        S(0, 1, false, true, false),
                        S(2, 1, false, true, false),
                        S(1, 2, false, true, false),
                        S(3, 1, false, false, true)
                    },
                    EdgeA = new[] { 0, 1, 1, 1, 3 },
                    EdgeB = new[] { 1, 2, 3, 4, 5 },
                    BranchFolds = MazeRules.FoldBranchC,
                    BranchMinLen = MazeRules.FoldBranchCMinLen
                }
            };
        }

        static Slot S(int col, int row, bool start, bool combat, bool connector)
        {
            return new Slot { Col = col, Row = row, Start = start, Combat = combat, Connector = connector };
        }

        static float EdgeWalk(Stage1Maze maze, int u, int v)
        {
            string a = maze.Nodes[u].Id;
            string b = maze.Nodes[v].Id;
            for (int i = 0; i < maze.Edges.Length; i++)
            {
                if (maze.Edges[i].Connects(a, b))
                    return maze.Edges[i].Length;
            }

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

        static MazeVec2[] BuildFold(MazeVec2 a, MazeVec2 b, int minFolds, float minLen, float segMax)
        {
            var pts = new List<MazeVec2>();
            pts.Add(a);
            pts.Add(b);
            SplitLong(pts, segMax);
            float sign = 1f;
            int guard = 0;
            while (pts.Count - 1 < minFolds && guard < 80)
            {
                guard++;
                int best = 0;
                float bestD = -1f;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    float d = pts[i].Dist(pts[i + 1]);
                    if (d > bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }

                InsertSquare(pts, best, segMax, sign);
                sign = -sign;
                SplitLong(pts, segMax);
            }

            guard = 0;
            while (PolyLen(pts) + 0.05f < minLen && guard < 40)
            {
                guard++;
                float need = minLen - PolyLen(pts);
                float spur = Math.Min(segMax, Math.Max(8f, need * 0.5f));
                int i = Math.Min(1, pts.Count - 2);
                MazeVec2 p = pts[i];
                MazeVec2 q = pts[i + 1];
                float dx = q.X - p.X;
                float dy = q.Y - p.Y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < 0.001f)
                    d = 1f;
                var mid = new MazeVec2(p.X + (-dy / d) * spur, p.Y + (dx / d) * spur);
                pts.Insert(i + 1, p);
                pts.Insert(i + 1, mid);
                SplitLong(pts, segMax);
            }

            SplitLong(pts, segMax);
            return pts.ToArray();
        }

        static void SplitLong(List<MazeVec2> pts, float segMax)
        {
            int i = 0;
            float sign = 1f;
            while (i < pts.Count - 1)
            {
                if (pts[i].Dist(pts[i + 1]) > segMax + 0.05f)
                {
                    InsertSquare(pts, i, segMax, sign);
                    sign = -sign;
                    continue;
                }

                i++;
            }
        }

        static void InsertSquare(List<MazeVec2> pts, int i, float L, float sign)
        {
            MazeVec2 p0 = pts[i];
            MazeVec2 p1 = pts[i + 1];
            float dx = p1.X - p0.X;
            float dy = p1.Y - p0.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            float ux = 0f;
            float uy = 1f;
            if (d >= 0.000001f)
            {
                ux = dx / d;
                uy = dy / d;
            }

            float px = -uy * sign;
            float py = ux * sign;
            float adv = Math.Min(L, d);
            var a = new MazeVec2(p0.X + px * L, p0.Y + py * L);
            var b = new MazeVec2(a.X + ux * adv, a.Y + uy * adv);
            var c = new MazeVec2(p0.X + ux * adv, p0.Y + uy * adv);
            var ins = new List<MazeVec2>();
            if (p0.Dist(a) > 0.05f)
                ins.Add(a);
            MazeVec2 prev = ins.Count > 0 ? ins[ins.Count - 1] : p0;
            if (prev.Dist(b) > 0.05f && b.Dist(p1) > 0.05f)
                ins.Add(b);
            if (c.Dist(p1) > 0.05f && c.Dist(b) > 0.05f)
            {
                MazeVec2 last = ins.Count > 0 ? ins[ins.Count - 1] : p0;
                if (last.Dist(c) > 0.05f)
                    ins.Add(c);
            }

            for (int k = 0; k < ins.Count; k++)
                pts.Insert(i + 1 + k, ins[k]);
        }

        static float PolyLen(List<MazeVec2> pts)
        {
            float t = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
                t += pts[i].Dist(pts[i + 1]);
            return t;
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
