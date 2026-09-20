using System;
using System.Collections.Generic;
using System.Text;
using RogueShooter.Build;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// TTK sample: HP ScaledHp*bagMul; DPS from crit2 rewards (not stub10, not x1.15^B).
    /// Bm from encounter Build. is_elite=1 for elite pool only.
    /// </summary>
    public static class EnemyTtkSampler
    {
        static readonly int[] Builds = { 6, 10, 14 };
        static readonly float[] ArriveMins = { 2f, 5.5f, 8.5f };

        public struct Row
        {
            public string SpawnClass;
            public string BagId;
            public string EnemyId;
            public int IsElite;
            public int Build;
            public float T;
            public float Hp;
            public float Dps;
            public float TtkS;
            public string Outcome;
        }

        public static string CsvHeader()
        {
            return "spawn_class,bag_id,enemy_id,is_elite,build,t,hp,dps,ttk_s,outcome";
        }

        public static string FormatCsv(Row row)
        {
            return string.Format(
                "{0},{1},{2},{3},{4},{5:0.###},{6:0.#},{7:0.#},{8:0.###},{9}",
                Escape(row.SpawnClass),
                Escape(row.BagId),
                row.EnemyId,
                row.IsElite,
                row.Build,
                row.T,
                row.Hp,
                row.Dps,
                row.TtkS,
                row.Outcome);
        }

        public static List<string> DemoLoadoutForBuild(int build)
        {
            var ids = new List<string>();
            string[] cycle = { "R1M", "R4M", "R1L", "R5L", "R2M", "R6L", "R7M", "R4L" };
            int left = build < 0 ? 0 : build;
            int i = 0;
            while (left > 0 && i < 64)
            {
                string id = cycle[i % cycle.Length];
                if (!RewardCatalog.TryGet(id, out RewardRow row))
                {
                    i++;
                    continue;
                }
                int cost = row.BuildEquiv > 0 ? row.BuildEquiv : 1;
                if (cost > left)
                {
                    if (left >= 1 && RewardCatalog.TryGet("R1L", out _))
                    {
                        ids.Add("R1L");
                        left -= 1;
                    }
                    else break;
                }
                else
                {
                    ids.Add(id);
                    left -= cost;
                }
                i++;
            }
            return ids;
        }

        public static Row RunOnce(
            string spawnClass,
            string bagId,
            string enemyId,
            bool isEliteBag,
            int build,
            float wallMinutes,
            IList<string> ownedRewardIds)
        {
            if (string.IsNullOrEmpty(enemyId)) enemyId = "E1";
            if (build < 0) build = 0;

            float tm = SpawnWaveCatalog.TimeMul(wallMinutes);
            float bm = SpawnWaveCatalog.BuildMul(build);
            float mul = isEliteBag ? SpawnWaveCatalog.EliteHpMul : SpawnWaveCatalog.SixBagHpMul;
            float hp = SpawnWaveCatalog.ScaledHp(enemyId, tm, bm, mul);
            float dps = Crit2Dps.FromIds(ownedRewardIds, true).Dps;
            if (dps < 1f) dps = Crit2Dps.Dps0();

            float left = hp;
            float ttk = 0f;
            const float dt = 0.05f;
            const float guard = 600f;
            while (left > 0f && ttk < guard)
            {
                left -= dps * dt;
                ttk += dt;
            }

            return new Row
            {
                SpawnClass = string.IsNullOrEmpty(spawnClass) ? "" : spawnClass,
                BagId = string.IsNullOrEmpty(bagId) ? "" : bagId,
                EnemyId = enemyId.ToUpperInvariant(),
                IsElite = isEliteBag ? 1 : 0,
                Build = build,
                T = wallMinutes,
                Hp = hp,
                Dps = dps,
                TtkS = ttk,
                Outcome = left <= 0f ? "win" : "timeout"
            };
        }

        public static string RunBatchCsv(int seed0)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CsvHeader());
            var rng = new Random(seed0);
            int n = 0;
            var classes = (SpawnClassId[])Enum.GetValues(typeof(SpawnClassId));
            for (int c = 0; c < classes.Length; c++)
            {
                for (int bi = 0; bi < Builds.Length; bi++)
                {
                    for (int ti = 0; ti < ArriveMins.Length; ti++)
                    {
                        var g = SpawnWaveCatalog.RollGroup(classes[c], rng);
                        string clsLabel = SpawnWaveCatalog.ClassLabel(classes[c]);
                        string bagId = SpawnWaveCatalog.BagId(classes[c], g);
                        var loadout = DemoLoadoutForBuild(Builds[bi]);
                        if (g.Members == null) continue;
                        for (int m = 0; m < g.Members.Length; m++)
                        {
                            int count = g.Members[m].Count < 1 ? 1 : g.Members[m].Count;
                            for (int k = 0; k < count; k++)
                            {
                                sb.AppendLine(FormatCsv(RunOnce(clsLabel, bagId, g.Members[m].KindId, false, Builds[bi], ArriveMins[ti], loadout)));
                                n++;
                            }
                        }
                    }
                }
            }
            for (int bi = 0; bi < Builds.Length; bi++)
            {
                for (int ti = 0; ti < ArriveMins.Length; ti++)
                {
                    var bag = EliteChestPool.RollBag(rng);
                    if (bag.Members == null) continue;
                    var loadout = DemoLoadoutForBuild(Builds[bi]);
                    for (int m = 0; m < bag.Members.Length; m++)
                    {
                        int count = bag.Members[m].Count < 1 ? 1 : bag.Members[m].Count;
                        for (int k = 0; k < count; k++)
                        {
                            sb.AppendLine(FormatCsv(RunOnce("elite_chest", bag.BagId, bag.Members[m].KindId, true, Builds[bi], ArriveMins[ti], loadout)));
                            n++;
                        }
                    }
                }
            }
            sb.Append("# samples=").Append(n)
                .Append(" note=crit2_dps;Bm=encounter_build;six_mul=")
                .Append(SpawnWaveCatalog.SixBagHpMul.ToString("0.#"))
                .Append(";elite_mul=")
                .Append(SpawnWaveCatalog.EliteHpMul.ToString("0.#"));
            return sb.ToString();
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
