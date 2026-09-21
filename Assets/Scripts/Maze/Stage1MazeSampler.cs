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

        public static string WriteDefault()
        {
            return WriteTo(DefaultPath(), 42);
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
            sb.Append("s (no clock lock, reachability first)");
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
            sb.Append("/").Append(FullChargeKnockback.MidDistance(EnemyKindIds.Shield, true).ToString("0.00"));
            sb.Append(" boss=").Append(FullChargeKnockback.MidDistance(null, false, true).ToString("0.00"));
            sb.Append(" elite=same-species");
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
