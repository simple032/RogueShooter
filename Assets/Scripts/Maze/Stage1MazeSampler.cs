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
            return WriteTo(DefaultPath(), 42);
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
            sb.AppendLine("# Stage-1 walk-only pacing evidence (DRAFT)");
            sb.AppendLine("# seconds = graph path length / moveSpeed");
            sb.Append("moveSpeed=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" pitch=").Append(MazeRules.PitchX.ToString("0")).Append("/")
              .Append(MazeRules.PitchY.ToString("0"));
            sb.Append(" rooms=").Append(MazeRules.CombatWidth.ToString("0")).Append("x")
              .Append(MazeRules.CombatHeight.ToString("0"));
            sb.Append(" hub=").Append(MazeRules.HubWidth.ToString("0")).Append("x")
              .Append(MazeRules.HubHeight.ToString("0"));
            sb.Append(" corridor=").Append(MazeRules.CorridorWidth.ToString("0.#"));
            sb.AppendLine();
            sb.AppendLine("target START→Altar=60-120s visit-all=240±30s (greedy all-nodes)");
            sb.AppendLine("quota Chest×1+Altar×1+Normal×2+START+CONN unchanged");
            sb.AppendLine();
            sb.AppendLine("seed,tpl,altar_u,altar_s,visit_u,visit_s,altar_ok,visit_ok");
            bool allOk = true;
            for (int i = 0; i < seeds.Length; i++)
            {
                Stage1Maze maze = Stage1MazeGen.Generate(seeds[i]);
                MazePacing pace = Stage1MazeGen.MeasurePacing(maze, MazeRules.PlayMoveSpeed);
                bool altarOk = pace.AltarWalkSeconds >= MazeRules.WalkAltarMinSeconds - 0.05f
                    && pace.AltarWalkSeconds <= MazeRules.WalkAltarMaxSeconds + 0.05f;
                float lo = MazeRules.WalkAllTargetSeconds - MazeRules.WalkAllSlackSeconds;
                float hi = MazeRules.WalkAllTargetSeconds + MazeRules.WalkAllSlackSeconds;
                bool visOk = pace.FullWalkSeconds >= lo - 0.05f && pace.FullWalkSeconds <= hi + 0.05f;
                if (!altarOk || !visOk)
                    allOk = false;
                sb.Append(seeds[i]).Append(',').Append(maze.TemplateId).Append(',');
                sb.Append(pace.AltarWalk.ToString("0.0")).Append(',');
                sb.Append(pace.AltarWalkSeconds.ToString("0.0")).Append(',');
                sb.Append(pace.FullWalk.ToString("0.0")).Append(',');
                sb.Append(pace.FullWalkSeconds.ToString("0.0")).Append(',');
                sb.Append(altarOk ? "PASS" : "FAIL").Append(',');
                sb.Append(visOk ? "PASS" : "FAIL");
                sb.AppendLine();
            }

            sb.AppendLine();
            Stage1Maze d = Stage1MazeGen.Generate(42);
            MazePacing dp = Stage1MazeGen.MeasurePacing(d, MazeRules.PlayMoveSpeed);
            sb.AppendLine("# seed 42 detail");
            sb.AppendLine("[S1Maze] " + Stage1MazeGen.FormatGraph(d));
            sb.AppendLine("[S1Maze] " + Stage1MazeGen.FormatQuota(d));
            sb.Append("[S1Maze] walkOnly move=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" altar=").Append(dp.AltarWalk.ToString("0.0")).Append("u/");
            sb.Append(dp.AltarWalkSeconds.ToString("0.0")).Append("s START→ALTAR");
            sb.Append(" (=length/").Append(MazeRules.PlayMoveSpeed.ToString("0")).Append(")");
            sb.Append(" full=").Append(dp.FullWalk.ToString("0.0")).Append("u/");
            sb.Append(dp.FullWalkSeconds.ToString("0.0")).Append("s visit-all");
            sb.Append(" pitch=").Append(MazeRules.PitchX.ToString("0")).Append("/");
            sb.Append(MazeRules.PitchY.ToString("0"));
            sb.AppendLine();
            sb.Append("# ").Append(allOk ? "WALK PACING PASS" : "WALK PACING FAIL");
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
            sb.Append("s (walk-only clocks; combat est not in 60/240)");
            sb.AppendLine();
            sb.Append("[S1Maze] walkOnly move=").Append(MazeRules.PlayMoveSpeed.ToString("0"));
            sb.Append(" altar=").Append(pace.AltarWalk.ToString("0.0")).Append("u/");
            sb.Append(pace.AltarWalkSeconds.ToString("0.0")).Append("s START→ALTAR");
            sb.Append(" (=length/").Append(MazeRules.PlayMoveSpeed.ToString("0")).Append(")");
            sb.Append(" full=").Append(pace.FullWalk.ToString("0.0")).Append("u/");
            sb.Append(pace.FullWalkSeconds.ToString("0.0")).Append("s visit-all");
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
