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
                edges[e] = new MazeEdge
                {
                    FromId = a.Id,
                    ToId = b.Id,
                    From = a.Center,
                    To = b.Center,
                    Width = MazeRules.CorridorWidth,
                    Length = a.Center.Dist(b.Center)
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
                    float w = maze.Nodes[u].Center.Dist(maze.Nodes[v].Center);
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
            // Long START approach (4×PitchY) + compact combat cluster so
            // walk-only START→Altar is 60–120s and visit-all is ~240s at move=6.
            // Quota/shuffle unchanged: 4 combat slots + START + CONN stub.
            return new[]
            {
                new Tpl
                {
                    Id = "I",
                    Slots = new[]
                    {
                        S(1, 0, true, false, false),
                        S(1, 4, false, true, false),
                        S(0, 4, false, true, false),
                        S(2, 4, false, true, false),
                        S(1, 5, false, true, false),
                        S(2, 6, false, false, true)
                    },
                    EdgeA = new[] { 0, 1, 1, 1, 4 },
                    EdgeB = new[] { 1, 2, 3, 4, 5 }
                },
                new Tpl
                {
                    Id = "Z",
                    Slots = new[]
                    {
                        S(1, 0, true, false, false),
                        S(1, 4, false, true, false),
                        S(2, 4, false, true, false),
                        S(0, 4, false, true, false),
                        S(1, 5, false, true, false),
                        S(0, 6, false, false, true)
                    },
                    EdgeA = new[] { 0, 1, 1, 1, 4 },
                    EdgeB = new[] { 1, 2, 3, 4, 5 }
                },
                new Tpl
                {
                    Id = "C",
                    Slots = new[]
                    {
                        S(1, 0, true, false, false),
                        S(1, 4, false, true, false),
                        S(0, 4, false, true, false),
                        S(2, 4, false, true, false),
                        S(1, 5, false, true, false),
                        S(3, 5, false, false, true)
                    },
                    EdgeA = new[] { 0, 1, 1, 1, 4 },
                    EdgeB = new[] { 1, 2, 3, 4, 5 }
                }
            };
        }

        static Slot S(int col, int row, bool start, bool combat, bool connector)
        {
            return new Slot { Col = col, Row = row, Start = start, Combat = combat, Connector = connector };
        }
    }
}
