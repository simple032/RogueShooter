using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RogueShooter.Balance
{
    /// <summary>
    /// Loads v0.4.2-LOCK CSVs from StreamingAssets/BalanceLock_v042.
    /// That folder is the source of truth — not gameplay literals.
    /// </summary>
    public static class BalanceLockLoader
    {
        public const string FolderName = "BalanceLock_v042";

        public static readonly string[] RequiredCsvs =
        {
            "balance_build_expected_v042b.csv",
            "balance_rarity_v042b.csv",
            "balance_power_boss_v042b.csv",
            "balance_boss_anchors_v042b.csv",
            "balance_segment_enemy_v042b.csv",
            "balance_t_wave_v042d.csv",
            "balance_rhythm_spawn_v042d.csv",
            "balance_time_scale_v042d.csv",
            "balance_path_clock_v042d.csv",
            "balance_shop_gold_locked.csv",
        };

        public static string CatalogDirectory()
        {
            if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
                return Path.Combine(Application.streamingAssetsPath, FolderName);
            return Path.Combine(Application.dataPath, "StreamingAssets", FolderName);
        }

        public static bool TryLoad(out BalanceLockData data, out string error)
        {
            data = null;
            string dir = CatalogDirectory();
            if (!Directory.Exists(dir))
            {
                error = "Missing lock folder " + dir;
                return false;
            }

            var files = new Dictionary<string, CsvTable>(StringComparer.OrdinalIgnoreCase);
            var loaded = new List<string>();
            for (int i = 0; i < RequiredCsvs.Length; i++)
            {
                string name = RequiredCsvs[i];
                string path = Path.Combine(dir, name);
                if (!File.Exists(path))
                {
                    error = "Missing required CSV " + name + " under " + dir;
                    return false;
                }

                files[name] = CsvTable.Parse(File.ReadAllText(path));
                loaded.Add(name);
            }

            string defaultsPath = Path.Combine(dir, "demo_spawnband_defaults.csv");
            CsvTable defaults = File.Exists(defaultsPath)
                ? CsvTable.Parse(File.ReadAllText(defaultsPath))
                : CsvTable.Parse("key,value,status,note\n");
            if (File.Exists(defaultsPath))
                loaded.Add("demo_spawnband_defaults.csv");

            var gaps = new List<string>();
            data = new BalanceLockData
            {
                lockVersion = "v0.4.2-LOCK",
                sourceOfTruth = "Assets/StreamingAssets/" + FolderName + "/",
                intervalComposeSource = "balance_rhythm_spawn_v042d.csv",
            };

            ApplyBuild(files["balance_build_expected_v042b.csv"], data);
            ApplyRarity(files["balance_rarity_v042b.csv"], data);
            ApplyTimeScale(files["balance_time_scale_v042d.csv"], data);
            ApplyWaves(files["balance_t_wave_v042d.csv"], data);
            ApplyRhythmAndSegments(
                files["balance_rhythm_spawn_v042d.csv"],
                files["balance_segment_enemy_v042b.csv"],
                data,
                gaps);
            ApplyClockBands(data, gaps);
            ApplyAnchors(files["balance_segment_enemy_v042b.csv"], data);
            ApplyShopGold(files["balance_shop_gold_locked.csv"], data, gaps);
            ApplyDemoDefaults(defaults, data, gaps);

            data.loadedFiles = loaded.ToArray();
            data.gaps = gaps.ToArray();

            error = BalanceLock.Validate(data);
            if (error != null)
            {
                data = null;
                return false;
            }

            BalanceLock.Current = data;
            return true;
        }

        static void ApplyBuild(CsvTable table, BalanceLockData data)
        {
            data.pSpawn = CsvTable.ToFloat(table.Kv("P_spawn"));
            data.nAltar = CsvTable.ToInt(table.Kv("n_altar"));
            data.nChestSlots = CsvTable.ToInt(table.Kv("n_chest_slots"));
            data.eBuildAlpha = CsvTable.ToFloat(table.Kv("E_build_alpha"));
            data.eBuildBeta = CsvTable.ToFloat(table.Kv("E_build_beta"));
            data.eBuildGamma = CsvTable.ToFloat(table.Kv("E_build_gamma"));
        }

        static void ApplyRarity(CsvTable table, BalanceLockData data)
        {
            var chest = new List<RarityWeight>();
            var altar = new List<RarityWeight>();
            foreach (string[] row in table.DataRows())
            {
                var w = new RarityWeight
                {
                    rarity = table.Get(row, "rarity"),
                    weight = CsvTable.ToFloat(table.Get(row, "weight")),
                    score = CsvTable.ToInt(table.Get(row, "rarityScore")),
                };
                string source = table.Get(row, "source");
                if (source == "chest")
                    chest.Add(w);
                else if (source == "altar")
                    altar.Add(w);
            }

            data.chestRarity = chest.ToArray();
            data.altarRarity = altar.ToArray();
        }

        static void ApplyTimeScale(CsvTable table, BalanceLockData data)
        {
            var list = new List<TimeScaleMul>();
            foreach (string[] row in table.DataRows())
            {
                list.Add(new TimeScaleMul
                {
                    id = table.Get(row, "seg"),
                    tStartMin = CsvTable.ToFloat(table.Get(row, "t_start_min")),
                    tEndMin = CsvTable.ToFloat(table.Get(row, "t_end_min")),
                    enemyAttrMul = CsvTable.ToFloat(table.Get(row, "enemy_attr_mul"), 1f),
                    spawnIntervalMul = CsvTable.ToFloat(table.Get(row, "spawn_interval_mul"), 1f),
                });
            }

            data.timeScale = list.ToArray();
            string pre = table.MetaValue("Pre_ready_mid");
            if (!string.IsNullOrEmpty(pre))
                data.preReadyMidMin = CsvTable.ToFloat(pre);
            string clear = table.MetaValue("clear_median");
            if (!string.IsNullOrEmpty(clear))
                data.clearMedianMin = CsvTable.ToFloat(clear);
        }

        static void ApplyWaves(CsvTable table, BalanceLockData data)
        {
            var list = new List<WaveSpec>();
            foreach (string[] row in table.DataRows())
            {
                string seg = table.Get(row, "segment");
                list.Add(new WaveSpec
                {
                    id = MapSegment(seg),
                    count = CsvTable.ToInt(table.Get(row, "waves")),
                    tWaveMinSeconds = CsvTable.ToFloat(table.Get(row, "t_wave_min_s")),
                    tWaveMidSeconds = CsvTable.ToFloat(table.Get(row, "t_wave_mid_s")),
                    tWaveMaxSeconds = CsvTable.ToFloat(table.Get(row, "t_wave_max_s")),
                });
            }

            data.waves = list.ToArray();
        }

        static void ApplyRhythmAndSegments(CsvTable rhythm, CsvTable segment, BalanceLockData data, List<string> gaps)
        {
            var segs = new List<SegmentMul>();
            foreach (string[] row in rhythm.DataRows())
            {
                string id = rhythm.Get(row, "zone");
                if (id != "Z1" && id != "Z2" && id != "Z3")
                    continue;

                var mul = new SegmentMul
                {
                    id = id,
                    baseIntervalSeconds = CsvTable.ToFloat(rhythm.Get(row, "base_interval_s")),
                    intervalMul = CsvTable.ToFloat(rhythm.Get(row, "seg_int_mul"), 1f),
                    timeRef = rhythm.Get(row, "time_ref"),
                    expectedEffInterval = CsvTable.ToFloat(rhythm.Get(row, "eff_interval_s")),
                    hpMul = CsvTable.ToFloat(rhythm.Get(row, "enemy_hp_mul"), 1f),
                    dmgMul = CsvTable.ToFloat(rhythm.Get(row, "enemy_dmg_mul"), 1f),
                    eBuildMid = CsvTable.ToFloat(rhythm.Get(row, "eBuildMid")),
                };

                string[] segRow = FindZone(segment, id);
                if (segRow != null)
                {
                    mul.eBuildMid = CsvTable.ToFloat(segment.Get(segRow, "E_build_mid"), mul.eBuildMid);
                    mul.hpMul = CsvTable.ToFloat(segment.Get(segRow, "enemy_hp_mul"), mul.hpMul);
                    mul.dmgMul = CsvTable.ToFloat(segment.Get(segRow, "enemy_dmg_mul"), mul.dmgMul);
                    mul.csvCombinedIntervalMul = CsvTable.ToFloat(segment.Get(segRow, "spawn_interval_mul"));
                    string clock = segment.Get(segRow, "clock");
                    float a, b;
                    if (CsvTable.TryClockRange(clock, out a, out b))
                    {
                        mul.segmentClockStartMin = a;
                        mul.segmentClockEndMin = b;
                    }
                }

                segs.Add(mul);
            }

            data.segments = segs.ToArray();
                string pre = rhythm.MetaValue("Pre_ready");
                if (data.preReadyMidMin <= 0f && !string.IsNullOrEmpty(pre))
                    data.preReadyMidMin = CsvTable.ToFloat(pre);

            gaps.Add("segment_enemy spawn_interval_mul is combined-looking (Z1 0.91 / Z2 0.82 / Z3 0.745); spawn compose uses rhythm seg_int_mul × time_scale");
        }

        static void ApplyClockBands(BalanceLockData data, List<string> gaps)
        {
            TimeScaleMul t0 = data.GetTimeScale("T0");
            TimeScaleMul t1 = data.GetTimeScale("T1");
            TimeScaleMul t2 = data.GetTimeScale("T2");
            float z3End = data.preReadyMidMin;
            if (z3End <= 0f)
            {
                SegmentMul z3seg = data.GetSegment("Z3");
                if (z3seg != null && z3seg.segmentClockEndMin > 0f)
                {
                    z3End = z3seg.segmentClockEndMin;
                    gaps.Add("GAP Pre_ready_mid missing; Z3 band end used segment_enemy clock");
                }
            }

            data.clockBands = new[]
            {
                new ClockBand { id = "Z1", startMin = t0 != null ? t0.tStartMin : 0f, endMin = t0 != null ? t0.tEndMin : 3f },
                new ClockBand { id = "Z2", startMin = t1 != null ? t1.tStartMin : 3f, endMin = t1 != null ? t1.tEndMin : 6f },
                new ClockBand { id = "Z3", startMin = t2 != null ? t2.tStartMin : 6f, endMin = z3End },
            };

            SegmentMul z3 = data.GetSegment("Z3");
            if (z3 != null && z3.segmentClockEndMin > 0f && Math.Abs(z3.segmentClockEndMin - z3End) > 0.01f)
            {
                gaps.Add("Z3 spawn-band end=" + z3End.ToString("0.##") +
                         "′ COMPOSED from time_scale/rhythm Pre_ready_mid (segment_enemy Z3 clock ends " +
                         z3.segmentClockEndMin.ToString("0.##") + "′)");
            }
        }

        static void ApplyAnchors(CsvTable segment, BalanceLockData data)
        {
            string raw = segment.MetaValue("anchors");
            data.switchAnchors = new[]
            {
                new SwitchAnchor { id = "Z1_End", at = "HubA" },
                new SwitchAnchor { id = "Z2_End", at = "HubB" },
                new SwitchAnchor { id = "Z2_End_beta", at = "A5前" },
                new SwitchAnchor { id = "Z2_End_gamma", at = "A6前" },
                new SwitchAnchor { id = "Z3_End", at = "Pre entrance" },
            };
            data.switchAnchorSource = string.IsNullOrEmpty(raw) ? "demo ids" : raw;
        }

        static void ApplyShopGold(CsvTable table, BalanceLockData data, List<string> gaps)
        {
            data.shopGoldStatus = table.Kv("file_status");
            if (string.IsNullOrEmpty(data.shopGoldStatus))
                data.shopGoldStatus = "LOADED";
            if (data.shopGoldStatus == "MISSING_FROM_LOCK_PACK" || table.Kv("status") == "GAP")
                gaps.Add("balance_shop_gold_locked.csv: pack file missing — stub only; gold prices not invented");

            foreach (string[] row in table.Rows)
            {
                string status = table.Get(row, "status");
                string key = table.Get(row, "key");
                if (status == "GAP" && key != "file_status")
                    gaps.Add("shop gold " + key + " empty (not invented)");
            }
        }

        static void ApplyDemoDefaults(CsvTable table, BalanceLockData data, List<string> gaps)
        {
            data.noSpawnRadius = CsvTable.ToFloat(table.Kv("no_spawn_radius"));
            if (data.noSpawnRadius <= 0f)
            {
                gaps.Add("GAP no_spawn_radius missing from demo_spawnband_defaults.csv");
                data.noSpawnRadius = 2.5f;
            }
            string kinds = table.Kv("no_spawn_kinds");
            data.noSpawnCoreKinds = string.IsNullOrEmpty(kinds)
                ? new[] { "Hub", "Altar", "Chest", "Shop", "Pre" }
                : kinds.Split('|');
            data.demoClockScale = CsvTable.ToFloat(table.Kv("demo_clock_scale"));
            if (data.demoClockScale <= 0f)
            {
                gaps.Add("GAP demo_clock_scale missing from demo_spawnband_defaults.csv");
                data.demoClockScale = 20f;
            }

            for (int i = 0; i < table.Rows.Count; i++)
            {
                if (table.Get(table.Rows[i], "status") == "DEMO_STUB")
                    gaps.Add("DEMO_STUB " + table.Get(table.Rows[i], "key") + "=" + table.Get(table.Rows[i], "value"));
            }
        }

        static string[] FindZone(CsvTable table, string id)
        {
            foreach (string[] row in table.DataRows())
            {
                if (table.Get(row, "zone") == id)
                    return row;
            }

            return null;
        }

        static string MapSegment(string segment)
        {
            switch (segment)
            {
                case "S1": return "Z1";
                case "S2": return "Z2";
                case "S3": return "Z3";
                default: return segment;
            }
        }
    }
}
