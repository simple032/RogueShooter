using System;
using System.IO;
using System.Text;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Combat;

namespace RogueShooter.Maze
{
    /// <summary>Writes Logs/stage1_maze_evidence.txt — seed, graph, pool draws, lock/clear/open.</summary>
    public static class Stage1MazeSampler
    {
        public static string DefaultFileName => "stage1_maze_evidence.txt";

        public static string DefaultDirectory()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        }

        public static string DefaultPath()
        {
            return Path.Combine(DefaultDirectory(), DefaultFileName);
        }

        public static string PacingFileName => "stage1_pacing_walk_evidence.txt";

        public static string PacingPath()
        {
            return Path.Combine(DefaultDirectory(), PacingFileName);
        }

        public static string TemplatesFileName => "stage1_maze_templates_izc.txt";

        public static string TemplatesPath()
        {
            return Path.Combine(DefaultDirectory(), TemplatesFileName);
        }

        public static string WriteDefault()
        {
            WritePacingTo(PacingPath());
            WriteLayoutTo(Path.Combine(DefaultDirectory(), "stage1_maze_layout_seed42.txt"), 42);
            WriteTemplatesTo(TemplatesPath());
            Stage1PlayableSampler.WriteDefault();
            return WriteTo(DefaultPath(), 42);
        }

        public static string WriteLayoutTo(string path, int seed)
        {
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(DefaultDirectory(), "stage1_maze_layout_seed42.txt");
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, LayoutText(seed), new UTF8Encoding(false));
            return path;
        }

        public static string LayoutText(int seed)
        {
            Stage1Maze maze = Stage1MazeGen.Generate(seed);
            MazePacing pace = Stage1MazeGen.MeasurePacing(maze, MazeRules.PlayMoveSpeed);
            var sb = new StringBuilder();
            sb.AppendLine("# Stage1MazeSampler layout seed=" + seed + " (v2e 52x40 / pitch 82/70 gap30 START~18u)");
            sb.Append("moveSpeed=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" rooms=").Append(MazeRules.CombatWidth.ToString("0")).Append("x")
              .Append(MazeRules.CombatHeight.ToString("0"));
            sb.Append(" altar=").Append(MazeRules.AltarWidth.ToString("0")).Append("x")
              .Append(MazeRules.AltarHeight.ToString("0"));
            sb.Append(" pitch=").Append(MazeRules.PitchX.ToString("0")).Append("/")
              .Append(MazeRules.PitchY.ToString("0"));
            sb.Append(" hub=").Append(MazeRules.HubWidth.ToString("0")).Append("x")
              .Append(MazeRules.HubHeight.ToString("0"));
            sb.Append(" segMax=").Append(MazeRules.CorridorSegMax.ToString("0")).Append("u/")
              .Append(MazeRules.CorridorSegMaxSeconds.ToString("0")).Append("s");
            sb.AppendLine();
            sb.AppendLine("[S1Maze] " + Stage1MazeGen.FormatGraph(maze));
            sb.AppendLine("[S1Maze] " + Stage1MazeGen.FormatQuota(maze));
            sb.Append("[S1Maze] walkOnly move=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" firstHop=").Append(pace.FirstHop.ToString("0.0")).Append("u/");
            sb.Append(pace.FirstHopSeconds.ToString("0.0")).Append("s (feel ~3s, not a lock)");
            sb.Append(" shortest=").Append(pace.ShortestWalk.ToString("0.0")).Append("u/");
            sb.Append(pace.ShortestWalkSeconds.ToString("0.0")).Append("s START→CONN");
            sb.Append(" maxSeg=").Append(pace.MaxCorridorSeg.ToString("0.0")).Append("u/");
            sb.Append(pace.MaxCorridorSegSeconds.ToString("0.00")).Append("s");
            sb.AppendLine();
            sb.Append("[S1Maze] reachable=").Append(pace.AllReachable ? 1 : 0);
            sb.Append(" conn=").Append(pace.ConnectorReachable ? 1 : 0);
            sb.Append(" tpl=").Append(maze.TemplateId);
            sb.AppendLine();
            sb.AppendLine("# rooms");
            if (maze.Nodes != null)
            {
                for (int i = 0; i < maze.Nodes.Length; i++)
                {
                    MazeNode n = maze.Nodes[i];
                    sb.Append("room ").Append(n.Id);
                    sb.Append(" kind=").Append(MazeRules.Label(n.Kind));
                    sb.Append(" center=").Append(n.Center);
                    sb.Append(" size=").Append(n.Width.ToString("0")).Append("x").Append(n.Height.ToString("0"));
                    sb.Append(" spawn=").Append(n.SpawnsEnemies ? 1 : 0);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("# edges (ortho=straight; diagonal=L-bend only; CONN follows Altar)");
            if (maze.Edges != null)
            {
                for (int i = 0; i < maze.Edges.Length; i++)
                {
                    MazeEdge e = maze.Edges[i];
                    sb.Append("edge ").Append(e.FromId).Append("--").Append(e.ToId);
                    sb.Append(" folds=").Append(e.FoldCount);
                    sb.Append(" len=").Append(e.Length.ToString("0.0")).Append("u/");
                    sb.Append((e.Length / MazeRules.PlayMoveSpeed).ToString("0.0")).Append("s");
                    sb.Append(" maxSeg=").Append(e.MaxSegment.ToString("0.0")).Append("u/");
                    sb.Append((e.MaxSegment / MazeRules.PlayMoveSpeed).ToString("0.00")).Append("s");
                    sb.Append(" pts=");
                    if (e.Points != null)
                    {
                        for (int p = 0; p < e.Points.Length; p++)
                        {
                            if (p > 0) sb.Append(" ");
                            sb.Append(e.Points[p]);
                        }
                    }

                    sb.AppendLine();
                }
            }

            sb.AppendLine("# ASCII topology (col,row) Pitch 82/70  N口 fixed  CONN follows Altar");
            sb.AppendLine(AsciiTopology(maze.TemplateId));
            return sb.ToString();
        }

        public static string AsciiTopology(string templateId)
        {
            if (templateId == "Z")
            {
                return string.Join("\n", new[]
                {
                    "Z                    NE(2,2)",
                    "                        |",
                    "    W(0,1) -- N口(1,1) -- E(2,1) -- E2(3,1)",
                    "                 |",
                    "              START(1,0)",
                    "    N口=fixed Normal  4-slot shuffle Altar+Chest×2+Normal  CONN: W west / E south / E2 east / NE north"
                });
            }

            if (templateId == "C")
            {
                return string.Join("\n", new[]
                {
                    "C              N(1,2) -- NE(2,2)",
                    "                 |         |",
                    "    W(0,1) -- N口(1,1) -- E(2,1)",
                    "                 |",
                    "              START(1,0)",
                    "    N口=fixed Normal  4-slot shuffle  CONN: W west / E east / N north / NE east"
                });
            }

            return string.Join("\n", new[]
            {
                "I     NW(0,2) -- N(1,2)",
                "         |         |",
                "      W(0,1) -- N口(1,1) -- E(2,1)",
                "                   |",
                "                START(1,0)",
                "    N口=fixed Normal  4-slot shuffle Altar+Chest×2+Normal  CONN: W west / E east / NW west / N north"
            });
        }

        public static string WriteTemplatesTo(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = TemplatesPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, TemplatesText(), new UTF8Encoding(false));
            return path;
        }

        public static string TemplatesText()
        {
            return string.Join("\n", new[]
            {
                "# S1 maze I/Z/C templates (v2e 52×40 / pitch 82×70 gap 30)",
                "# Quota Chest×2 + Altar×1 + Normal×2 + START + CONN",
                "# START neighbor = fixed Normal (N口). Not shuffled.",
                "# Remaining 4 combat slots shuffle Altar×1 + Chest×2 + Normal×1.",
                "# CONN follows Altar: orthogonal straight exit, no fake-exit folds.",
                "# Orthogonal adjacency = short straight corridor. Diagonal rooms may L-bend.",
                "# No pure-walk time gate. START→N口 door ~18u / ~3s @ ms6 is feel only.",
                "",
                "I  START(1,0) -- N口(1,1) -- W(0,1) / E(2,1) / N(1,2)",
                "                 W(0,1) -- NW(0,2) -- N(1,2)",
                "   I-1 Altar@W(0,1)   CONN(-1,1) west",
                "   I-2 Altar@E(2,1)   CONN(3,1)  east",
                "   I-3 Altar@NW(0,2)  CONN(-1,2) west",
                "   I-4 Altar@N(1,2)   CONN(1,3)  north",
                "",
                "Z  START(1,0) -- N口(1,1) -- W(0,1) / E(2,1)",
                "                              E(2,1) -- E2(3,1)",
                "                              E(2,1) -- NE(2,2)",
                "   Z-1 Altar@W(0,1)   CONN(-1,1) west",
                "   Z-2 Altar@E(2,1)   CONN(2,0)  south",
                "   Z-3 Altar@E2(3,1)  CONN(4,1)  east",
                "   Z-4 Altar@NE(2,2)  CONN(2,3)  north",
                "",
                "C  START(1,0) -- N口(1,1) -- W(0,1) / E(2,1) / N(1,2)",
                "                              N(1,2) -- NE(2,2)",
                "                              E(2,1) -- NE(2,2)",
                "   C-1 Altar@W(0,1)   CONN(-1,1) west",
                "   C-2 Altar@E(2,1)   CONN(3,1)  east",
                "   C-3 Altar@N(1,2)   CONN(1,3)  north",
                "   C-4 Altar@NE(2,2)  CONN(3,2)  east",
                ""
            });
        }

        public static string WritePacingTo(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = PacingPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, PacingText(), new UTF8Encoding(false));
            return path;
        }

        public static string PacingText()
        {
            int[] seeds = { 1, 2, 17, 42, 99, 2026 };
            var sb = new StringBuilder();
            sb.AppendLine("# Stage-1 walk evidence (v2e)");
            sb.AppendLine("# seconds = center-to-center path / moveSpeed. NO pure-walk time gate.");
            sb.AppendLine("# START→N口 door ~18u / ~3s feel (not a lock). gap≤30u geometry lock.");
            sb.Append("moveSpeed=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" pitch=").Append(MazeRules.PitchX.ToString("0")).Append("/")
              .Append(MazeRules.PitchY.ToString("0"));
            sb.Append(" rooms=").Append(MazeRules.CombatWidth.ToString("0")).Append("x")
              .Append(MazeRules.CombatHeight.ToString("0"));
            sb.Append(" altar=").Append(MazeRules.AltarWidth.ToString("0")).Append("x")
              .Append(MazeRules.AltarHeight.ToString("0"));
            sb.Append(" hub=").Append(MazeRules.HubWidth.ToString("0")).Append("x")
              .Append(MazeRules.HubHeight.ToString("0"));
            sb.Append(" corridor=").Append(MazeRules.CorridorWidth.ToString("0.#"));
            sb.Append(" segMax=").Append(MazeRules.CorridorSegMax.ToString("0"));
            sb.AppendLine();
            sb.AppendLine("ACCEPTANCE rooms=52x40 pitch=82/70 gap=30u startN=fixedNormal connFollowsAltar no-walk-clock");
            sb.AppendLine("quota Chest×2+Altar×1+Normal×2+START+CONN");
            sb.AppendLine();
            sb.AppendLine("seed,tpl,first_u,first_s,short_u,short_s,max_seg_u,max_seg_s,start_n,conn_altar,gap_ok");
            bool allOk = true;
            for (int i = 0; i < seeds.Length; i++)
            {
                Stage1Maze maze = Stage1MazeGen.Generate(seeds[i]);
                MazePacing pace = Stage1MazeGen.MeasurePacing(maze, MazeRules.PlayMoveSpeed);
                MazeNode start = maze.Find("START");
                MazeNode first = start != null && start.NeighborIds != null && start.NeighborIds.Length > 0
                    ? maze.Find(start.NeighborIds[0]) : null;
                bool startN = first != null && first.Kind == MazeNodeKind.Normal;
                MazeNode conn = maze.Find("CONN");
                bool connAltar = false;
                if (conn != null && conn.NeighborIds != null)
                {
                    for (int n = 0; n < conn.NeighborIds.Length; n++)
                    {
                        MazeNode nb = maze.Find(conn.NeighborIds[n]);
                        if (nb != null && nb.Kind == MazeNodeKind.Altar)
                            connAltar = true;
                    }
                }
                bool gapOk = pace.MaxCorridorSeg <= MazeRules.CorridorSegMax + 0.05f;
                if (!startN || !connAltar || !gapOk)
                    allOk = false;
                sb.Append(seeds[i]).Append(',').Append(maze.TemplateId).Append(',');
                sb.Append(pace.FirstHop.ToString("0.0")).Append(',');
                sb.Append(pace.FirstHopSeconds.ToString("0.0")).Append(',');
                sb.Append(pace.ShortestWalk.ToString("0.0")).Append(',');
                sb.Append(pace.ShortestWalkSeconds.ToString("0.0")).Append(',');
                sb.Append(pace.MaxCorridorSeg.ToString("0.0")).Append(',');
                sb.Append(pace.MaxCorridorSegSeconds.ToString("0.00")).Append(',');
                sb.Append(startN ? "PASS" : "FAIL").Append(',');
                sb.Append(connAltar ? "PASS" : "FAIL").Append(',');
                sb.Append(gapOk ? "PASS" : "FAIL");
                sb.AppendLine();
            }

            sb.AppendLine();
            Stage1Maze d = Stage1MazeGen.Generate(42);
            MazePacing dp = Stage1MazeGen.MeasurePacing(d, MazeRules.PlayMoveSpeed);
            sb.AppendLine("# seed 42 detail");
            sb.AppendLine("[S1Maze] " + Stage1MazeGen.FormatGraph(d));
            sb.AppendLine("[S1Maze] " + Stage1MazeGen.FormatQuota(d));
            sb.Append("[S1Maze] walkOnly move=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" firstHop=").Append(dp.FirstHop.ToString("0.0")).Append("u/");
            sb.Append(dp.FirstHopSeconds.ToString("0.0")).Append("s (feel ~3s, not a lock)");
            sb.Append(" shortest=").Append(dp.ShortestWalk.ToString("0.0")).Append("u/");
            sb.Append(dp.ShortestWalkSeconds.ToString("0.0")).Append("s START→CONN");
            sb.Append(" maxSeg=").Append(dp.MaxCorridorSeg.ToString("0.0")).Append("u/");
            sb.Append(dp.MaxCorridorSegSeconds.ToString("0.00")).Append("s");
            sb.Append(" pitch=").Append(MazeRules.PitchX.ToString("0")).Append("/");
            sb.Append(MazeRules.PitchY.ToString("0"));
            sb.Append(" rooms=").Append(MazeRules.CombatWidth.ToString("0")).Append("x")
              .Append(MazeRules.CombatHeight.ToString("0"));
            sb.Append(" altar=").Append(MazeRules.AltarWidth.ToString("0")).Append("x")
              .Append(MazeRules.AltarHeight.ToString("0"));
            sb.AppendLine();
            sb.Append("# ").Append(allOk ? "GEOMETRY PASS (rooms/pitch/gap/startN/connAltar; no walk-clock gate)" : "GEOMETRY FAIL");
            sb.AppendLine();
            return sb.ToString();
        }

        public static string WriteTo(string path, int seed)
        {
            if (string.IsNullOrEmpty(path))
                path = DefaultPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, RunText(seed), new UTF8Encoding(false));
            return path;
        }

        public static string RunText(int seed)
        {
            EnemyPoolDraft.EnsureLoaded();
            var sb = new StringBuilder();
            Stage1Maze maze = Stage1MazeGen.Generate(seed);
            MazePacing pace = Stage1MazeGen.MeasurePacing(maze, MazeRules.PlayMoveSpeed);
            sb.AppendLine("[S1Maze] " + Stage1MazeGen.FormatGraph(maze));
            sb.AppendLine("[S1Maze] " + Stage1MazeGen.FormatQuota(maze));
            sb.Append("[S1Maze] pacing shortestWalk=").Append(pace.ShortestWalk.ToString("0.0"));
            sb.Append("u/").Append(pace.ShortestWalkSeconds.ToString("0.0")).Append("s");
            sb.Append(" rooms=").Append(pace.ShortestCombatRooms);
            sb.Append(" waves=").Append(pace.ShortestWaves);
            sb.Append(" combatEst=").Append(pace.ShortestCombatEstimate.ToString("0"));
            sb.Append("s totalEst=").Append(pace.ShortestTotalEstimate.ToString("0"));
            sb.Append("s | fullWalk=").Append(pace.FullWalk.ToString("0.0"));
            sb.Append("u/").Append(pace.FullWalkSeconds.ToString("0.0")).Append("s");
            sb.Append(" waves=").Append(pace.FullWaves);
            sb.Append(" totalEst=").Append(pace.FullTotalEstimate.ToString("0"));
            sb.Append("s (informational walk; no clock gate)");
            sb.AppendLine();
            sb.Append("[S1Maze] walkOnly move=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" firstHop=").Append(pace.FirstHop.ToString("0.0")).Append("u/");
            sb.Append(pace.FirstHopSeconds.ToString("0.0")).Append("s (feel ~3s, not a lock)");
            sb.Append(" shortest=").Append(pace.ShortestWalk.ToString("0.0")).Append("u/");
            sb.Append(pace.ShortestWalkSeconds.ToString("0.0")).Append("s START→CONN");
            sb.Append(" maxSeg=").Append(pace.MaxCorridorSeg.ToString("0.0")).Append("u/");
            sb.Append(pace.MaxCorridorSegSeconds.ToString("0.00")).Append("s");
            sb.Append(" pitch=").Append(MazeRules.PitchX.ToString("0")).Append("/");
            sb.Append(MazeRules.PitchY.ToString("0"));
            sb.AppendLine();
            sb.Append("[S1Maze] reachable=").Append(pace.AllReachable ? 1 : 0);
            sb.Append(" conn=").Append(pace.ConnectorReachable ? 1 : 0);
            sb.Append(" move=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" ortho=").Append(MazeRules.PlayOrtho.ToString("0"));
            sb.Append(" portalHold=w1-").Append(MazeRules.PortalHoldSeconds.ToString("0.0")).Append("s");
            sb.Append(" w2-").Append(MazeRules.PortalHoldForWave(2).ToString("0.0")).Append("s");
            sb.Append("(≤").Append(MazeRules.InterWavePortalHoldMaxSeconds.ToString("0.0")).Append(")");
            sb.Append(" rooms=").Append(MazeRules.CombatWidth.ToString("0")).Append("x")
              .Append(MazeRules.CombatHeight.ToString("0"));
            sb.Append(" altar=").Append(MazeRules.AltarWidth.ToString("0")).Append("x")
              .Append(MazeRules.AltarHeight.ToString("0"));
            sb.AppendLine();
            FullChargeKnockback.EnsureLoaded();
            sb.Append("[Knockback] ").Append(FullChargeKnockback.LockNote);
            sb.Append(" full≥").Append(ChargeShotRules.RingFillSeconds.ToString("0.00")).Append("s");
            sb.Append(" dog=").Append(FullChargeKnockback.MidDistance(EnemyKindIds.Dog, false).ToString("0.00"));
            sb.Append(" mage=").Append(FullChargeKnockback.MidDistance(EnemyKindIds.CultMage, false).ToString("0.00"));
            sb.Append(" normal=").Append(FullChargeKnockback.MidDistance(EnemyKindIds.Normal, false).ToString("0.00"));
            sb.Append(" grand=").Append(FullChargeKnockback.MidDistance(EnemyKindIds.GrandMage, false).ToString("0.00"));
            sb.Append(" shield=").Append(FullChargeKnockback.MidDistance(EnemyKindIds.Shield, false).ToString("0.00"));
            sb.Append("/root").Append(FullChargeKnockback.ShieldRaisedRootSeconds.ToString("0.0"));
            sb.Append("s ws×").Append(FullChargeKnockback.WeakSpotMul.ToString("0.0"));
            sb.Append(" boss=").Append(FullChargeKnockback.MidDistance(null, false, true).ToString("0.00"));
            sb.Append("/").Append(FullChargeKnockback.HitDistance(null, false, true, true).ToString("0.00"));
            sb.Append(" return=").Append(FullChargeKnockback.ReturnSeconds.ToString("0.0")).Append("s");
            sb.Append(" formula=").Append(FullChargeKnockback.FormulaNote);
            sb.Append(" elite=same-species");
            sb.Append(" 震矢×").Append(KnockbackRewardDraft.DistPctProduct(null).ToString("0.00"));
            sb.Append(" R15_C/R=").Append(KnockbackRewardDraft.IdLow).Append("/").Append(KnockbackRewardDraft.IdMid);
            sb.AppendLine();

            if (maze.Nodes != null)
            {
                for (int i = 0; i < maze.Nodes.Length; i++)
                {
                    MazeNode n = maze.Nodes[i];
                    if (!n.SpawnsEnemies)
                    {
                        sb.Append("[S1Maze] skip-spawn room=").Append(n.Id);
                        sb.Append(" kind=").Append(MazeRules.Label(n.Kind));
                        sb.AppendLine();
                        continue;
                    }

                    var session = new CombatRoomSession(n);
                    CombatStep[] steps = session.DryRun();
                    for (int s = 0; s < steps.Length; s++)
                    {
                        CombatStep step = steps[s];
                        sb.AppendLine(step.Line);
                        if (step.ShouldSpawn)
                        {
                            DrawnComposition d = StageEnemyPool.DrawComposition(
                                StageId.S1, n.PoolRoom, Stage1MazeGen.DrawRng(seed, n.Id, step.Wave));
                            sb.AppendLine(d.LogLine());
                            sb.Append("[StagePool] extra=+").Append(d.ExtraAdded);
                            sb.Append(" elite=").Append(d.EliteCount);
                            sb.Append(" n=").Append(d.UnitCount);
                            sb.Append(" tier=").Append(d.Tier);
                            sb.Append(" DRAFT_NOT_LOCKED");
                            sb.AppendLine();
                        }
                    }
                }
            }

            string err = Stage1MazeChecks.Run();
            sb.Append("# ").Append(err == null ? Stage1MazeChecks.FormatPass() : "ACCEPTANCE FAIL " + err);
            sb.AppendLine();
            sb.AppendLine("# playable");
            sb.Append(Stage1PlayableSampler.Csv());
            return sb.ToString();
        }
    }
}
