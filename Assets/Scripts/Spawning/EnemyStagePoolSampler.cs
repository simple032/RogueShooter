using System;
using System.IO;
using System.Text;
using RogueShooter.Ai;
using RogueShooter.Player;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Scripted S1/S2/S3 × room → composition sampler. Writes Logs/stage_pool_sampler.csv.
    /// </summary>
    public static class EnemyStagePoolSampler
    {
        public static string CsvHeader()
        {
            return "stage,room,tier,comp_id,comp_label,kinds,n_total,extra,elite_count,hp_list,atk_list,lunge,dog_pairs,note";
        }

        public static string DefaultFileName => "stage_pool_sampler.csv";

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
            return WriteTo(DefaultPath());
        }

        public static string WriteTo(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = DefaultPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, RunCsv(), new UTF8Encoding(false));
            return path;
        }

        public static string RunCsv()
        {
            EnemyPoolDraft.EnsureLoaded();
            var sb = new StringBuilder();
            sb.AppendLine(CsvHeader());
            var rng = new Random(42);
            StageId[] stages = { StageId.S1, StageId.S2, StageId.S3 };
            CombatRoomKind[] rooms =
            {
                CombatRoomKind.Normal, CombatRoomKind.Altar, CombatRoomKind.Chest, CombatRoomKind.LargeChest
            };
            int rows = 0;
            for (int s = 0; s < stages.Length; s++)
            {
                for (int r = 0; r < rooms.Length; r++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        DrawnComposition d = StageEnemyPool.DrawComposition(stages[s], rooms[r], rng);
                        sb.Append(StageIdUtil.Label(d.Stage)).Append(',')
                            .Append(CombatRoomKindUtil.Label(d.RoomKind)).Append(',')
                            .Append(d.Tier).Append(',')
                            .Append(d.CompId).Append(',')
                            .Append(Escape(d.CompLabel)).Append(',')
                            .Append(Escape(d.KindsToken)).Append(',')
                            .Append(d.UnitCount).Append(',')
                            .Append(d.ExtraAdded).Append(',')
                            .Append(d.EliteCount).Append(',')
                            .Append(Escape(HpList(d))).Append(',')
                            .Append(Escape(AtkList(d))).Append(',')
                            .Append(EnemyCombatRules.CanLunge(EnemyKindIds.Normal, d.Stage) ? 1 : 0).Append(',')
                            .Append(StageIdUtil.DogsSpawnInPairs(d.Stage) ? 1 : 0).Append(',')
                            .Append("DRAFT_NOT_LOCKED");
                        sb.AppendLine();
                        rows++;
                    }
                }
            }

            sb.Append("# samples=").Append(rows)
                .Append(" source=").Append(EnemyPoolDraft.Source)
                .Append(" dps0b=").Append(EnemyPoolDraft.DraftDps0B.ToString("0"))
                .Append(" weak=").Append(ChargeShotRules.GreenEnterSeconds.ToString("0.00"))
                .Append('-').Append(ChargeShotRules.GreenExitSeconds.ToString("0.00"))
                .Append(" ring=").Append(ChargeShotRules.RingFillSeconds.ToString("0.00"))
                .Append(" stagger=").Append(ChargeShotRules.WeakSpotStaggerSeconds.ToString("0.00"))
                .Append(" recover=").Append(ChargeShotRules.RecoverSeconds.ToString("0.00"))
                .Append(" ortho=").Append(EnemyCombatRules.PlayOrthoSize.ToString("0"))
                .Append(" thrust=[").Append(EnemyPoolDraft.LungeDamageMinEasy)
                .Append(',').Append(EnemyPoolDraft.LungeDamageMaxEasy).Append(']');
            sb.AppendLine();
            string err = StageEnemyPoolChecks.Run();
            sb.Append("# ").Append(err == null ? StageEnemyPoolChecks.FormatPass() : "ACCEPTANCE FAIL " + err);
            sb.AppendLine();
            var lines = StageEnemyPoolChecks.SampleKindLines();
            for (int i = 0; i < lines.Count; i++)
                sb.Append("# ").Append(lines[i]).AppendLine();
            return sb.ToString();
        }

        static string HpList(DrawnComposition d)
        {
            if (d.Units == null)
                return "";
            var sb = new StringBuilder();
            for (int i = 0; i < d.Units.Length; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(d.Units[i].KindId);
                if (d.Units[i].Elite) sb.Append('*');
                sb.Append(':').Append(d.Units[i].Hp);
            }

            return sb.ToString();
        }

        static string AtkList(DrawnComposition d)
        {
            if (d.Units == null)
                return "";
            var sb = new StringBuilder();
            for (int i = 0; i < d.Units.Length; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(d.Units[i].Atk.ToString("0.##"));
            }

            return sb.ToString();
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
