using System;
using System.IO;
using System.Text;
using RogueShooter.Player;
using RogueShooter.Spawning;

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

        public static string WriteDefault()
        {
            WritePacingTo(PacingPath());
            WriteLayoutTo(Path.Combine(DefaultDirectory(), "stage1_maze_layout_seed42.txt"), 42);
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
            sb.AppendLine("# Stage1MazeSampler layout seed=" + seed + " (v2c B_推荐 100x80 / pitch 130/110 gap30)");
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
            sb.Append(pace.FirstHopSeconds.ToString("0.0")).Append("s (1 pitch)");
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

            sb.AppendLine("# straight ortho edges (Manhattan=1; each door gap ≤30u)");
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

            sb.AppendLine("# ASCII topology (col,row) Pitch 130/110  all edges Manhattan=1");
            sb.AppendLine(AsciiTopology(maze.TemplateId));
            return sb.ToString();
        }

        public static string AsciiTopology(string templateId)
        {
            if (templateId == "Z")
            {
                return string.Join("\n", new[]
                {
                    "Z  CONN(0,2) -- N(1,2)",
                    "              |",
                    " W2(-1,1) -- W(0,1) -- entry(1,1) -- E(2,1)",
                    "                          |",
                    "                       START(1,0)",
                    "    star 2·Py+Px ≈59s  Manhattan=1  CONN adj N  extra=W2"
                });
            }

            if (templateId == "C")
            {
                return string.Join("\n", new[]
                {
                    "C                    c2(3,1) -- c3(3,2) -- CONN(4,2)",
                    "                         |",
                    " extra(0,1) -- c0(1,1) -- c1(2,1)",
                    "                  |",
                    "               START(1,0)",
                    "    snake 2·Py+3·Px ≈102s  CONN adj c3  extra west stub  Manhattan=1"
                });
            }

            return string.Join("\n", new[]
            {
                "I              N(1,2) -- CONN(2,2)",
                "                 |",
                "    W(0,1) -- entry(1,1) -- E(2,1) -- E2(3,1)",
                "                 |",
                "              START(1,0)",
                "    star 2·Py+Px ≈59s  Manhattan=1  CONN adj N  extra=E2"
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
            sb.AppendLine("# Stage-1 walk-only evidence (v2c B_推荐)");
            sb.AppendLine("# seconds = center-to-center path / moveSpeed. 60/240 is NOT a lock.");
            sb.AppendLine("# I no-fold ≈ 2·Py+Px ≈59s; C snake ≈ 2·Py+3·Px ≈102s (CSV B_推荐).");
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
            sb.AppendLine("ACCEPTANCE rooms=100x80 pitch=130/110 gap=30u START=1pitch manhattan=1 CONN-adj-combat v2c-B_推荐");
            sb.AppendLine("quota Chest×2+Altar×1+Normal×2+START+CONN");
            sb.AppendLine();
            sb.AppendLine("seed,tpl,first_u,first_s,short_u,short_s,max_seg_u,max_seg_s,hop_ok,corr_ok");
            bool allOk = true;
            for (int i = 0; i < seeds.Length; i++)
            {
                Stage1Maze maze = Stage1MazeGen.Generate(seeds[i]);
                MazePacing pace = Stage1MazeGen.MeasurePacing(maze, MazeRules.PlayMoveSpeed);
                bool hopOk = Math.Abs(pace.FirstHop - MazeRules.PitchY) <= 0.51f
                    || Math.Abs(pace.FirstHop - MazeRules.PitchX) <= 0.51f;
                bool corrOk = pace.MaxCorridorSegSeconds <= MazeRules.CorridorSegMaxSeconds + 0.05f;
                if (!hopOk || !corrOk)
                    allOk = false;
                sb.Append(seeds[i]).Append(',').Append(maze.TemplateId).Append(',');
                sb.Append(pace.FirstHop.ToString("0.0")).Append(',');
                sb.Append(pace.FirstHopSeconds.ToString("0.0")).Append(',');
                sb.Append(pace.ShortestWalk.ToString("0.0")).Append(',');
                sb.Append(pace.ShortestWalkSeconds.ToString("0.0")).Append(',');
                sb.Append(pace.MaxCorridorSeg.ToString("0.0")).Append(',');
                sb.Append(pace.MaxCorridorSegSeconds.ToString("0.00")).Append(',');
                sb.Append(hopOk ? "PASS" : "FAIL").Append(',');
                sb.Append(corrOk ? "PASS" : "FAIL");
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
            sb.Append(dp.FirstHopSeconds.ToString("0.0")).Append("s (1 pitch)");
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
            sb.Append("# ").Append(allOk ? "WALK GATES PASS (hop=1pitch corr≤5s; 60/240 not locked)" : "WALK GATES FAIL");
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
            sb.Append("s (walk-only clocks; 60/240 not a path lock)");
            sb.AppendLine();
            sb.Append("[S1Maze] walkOnly move=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" firstHop=").Append(pace.FirstHop.ToString("0.0")).Append("u/");
            sb.Append(pace.FirstHopSeconds.ToString("0.0")).Append("s (1 pitch)");
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
            sb.Append(" portalHold=").Append(MazeRules.PortalHoldSeconds.ToString("0.0")).Append("s");
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
            return sb.ToString();
        }
    }
}
