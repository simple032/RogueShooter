using System.IO;
using System.Text;
using RogueShooter.Art;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;

namespace RogueShooter.Combat
{
    /// <summary>Writes Logs/stage1_playable_evidence.csv — collision / arrow / orb / dodge i-frames / spawn land.</summary>
    public static class Stage1PlayableSampler
    {
        public static string DefaultFileName => "stage1_playable_evidence.csv";
        public static string SpawnLandFileName => "stage1_spawn_land.csv";

        public static string DefaultPath()
        {
            return Path.Combine(Stage1MazeSampler.DefaultDirectory(), DefaultFileName);
        }

        public static string StreamingRoot()
        {
            return Path.Combine("Assets", "StreamingAssets", "EnemyPoolDraft");
        }

        public static string StreamingPath()
        {
            return Path.Combine(StreamingRoot(), DefaultFileName);
        }

        public static string SpawnLandDefaultPath()
        {
            return Path.Combine(Stage1MazeSampler.DefaultDirectory(), SpawnLandFileName);
        }

        public static string SpawnLandStreamingPath()
        {
            return Path.Combine(StreamingRoot(), SpawnLandFileName);
        }

        public static string WriteDefault()
        {
            WriteSpawnLandTo(SpawnLandStreamingPath());
            WriteSpawnLandTo(SpawnLandDefaultPath());
            WriteTo(StreamingPath());
            return WriteTo(DefaultPath());
        }

        public static string WriteTo(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = DefaultPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, Csv(), new UTF8Encoding(false));
            return path;
        }

        public static string WriteSpawnLandTo(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = SpawnLandDefaultPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, CombatRoomSpawn.DumpSeed42Csv(), new UTF8Encoding(false));
            return path;
        }

        public static string Csv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Stage-1 playable evidence (collision / projectiles / dodge i-frames / JianHai PNG / spawn land)");
            sb.AppendLine("# Maze v2e unchanged: 52x40 pitch 82/70 Chestx2 no LargeChest CONN follows Altar PortalFx 1.0s");
            sb.AppendLine("# Art: Assets/Art/JianHai/ Provide-sourced PNGs; runtime File.ReadAllBytes+LoadImage (Editor import still preferred)");
            sb.AppendLine("# Spawn: CombatRoomSpawn room-AABB random; bypass SpawnCluster.Offset r=0.85 even-ring");
            sb.AppendLine("check,result,detail");
            string err = Stage1PlayableChecks.Run();
            CombatRoomSpawnStats land = CombatRoomSpawn.SampleSeed42();
            string probe = CombatRoomSpawn.ProbeAvoidVolumes();
            Row(sb, "playable_acceptance", err == null, err ?? Stage1PlayableChecks.FormatPass());
            Row(sb, "dodge_duration",
                DodgeRules.DurationSeconds >= 0.30f && DodgeRules.DurationSeconds <= 0.40f,
                "dur=" + DodgeRules.DurationSeconds.ToString("0.00") + "s ACTION_SPEC");
            Row(sb, "dodge_iframes",
                DodgeRules.IFrameActive(0.10f, 0f)
                && !DodgeRules.IFrameActive(0.07f, 0f)
                && !DodgeRules.IFrameActive(0.29f, 0f),
                "iframe=" + DodgeRules.IFrameStartSeconds.ToString("0.00")
                + "-" + DodgeRules.IFrameEndSeconds.ToString("0.00")
                + "s len=" + DodgeRules.IFrameSeconds.ToString("0.00")
                + "s suggested-unlocked");
            Row(sb, "dodge_cooldown",
                DodgeRules.CooldownSeconds >= 0.80f && DodgeRules.CooldownSeconds <= 1.00f,
                "cd=" + DodgeRules.CooldownSeconds.ToString("0.00") + "s");
            Row(sb, "dodge_distance_derived",
                System.Math.Abs(DodgeRules.Distance - DodgeRules.MoveSpeedRef * DodgeRules.DurationSeconds) < 0.0001f,
                "dist=" + DodgeRules.Distance.ToString("0.00") + "=move6×dur (not a new table row)");
            Row(sb, "action_spec_p1",
                System.Math.Abs(ActionSpecP1.Fps - 12f) < 0.001f
                && ActionSpecP1.PlayerRoll.Root == "jh_char_archer_roll"
                && ActionSpecP1.EnemyRoot(EnemyKindIds.Dog) == "jh_enemy_dog",
                "fps=12 roll+charge/atk idle/move/hurt/death; S1 N/D/M clips; OnFire@_01");
            Row(sb, "arrow_art",
                JianHaiArtCatalog.ArrowFlight == JianHaiArtCatalog.FxTipWarm
                && JianHaiSprites.HasSourceFile(JianHaiArtCatalog.ArrowFlight),
                JianHaiArtCatalog.ArrowFlight);
            Row(sb, "orb_art",
                JianHaiSprites.HasSourceFile(JianHaiArtCatalog.FxMageOrb),
                JianHaiArtCatalog.FxMageOrb + " " + JianHaiArtCatalog.AssetPath(JianHaiArtCatalog.FxMageOrb));
            Row(sb, "jianhai_png_runtime",
                JianHaiSprites.HasSourceFile(JianHaiArtCatalog.PlayerIdle)
                && JianHaiSprites.HasSourceFile(JianHaiArtCatalog.TileFloorCorridor)
                && JianHaiSprites.HasSourceFile(JianHaiArtCatalog.WallStone),
                "disk LoadImage + Editor import; tiled floors/walls localScale=1 explicit hx/hy");
            Row(sb, "arrow_speed_reuses_orb",
                System.Math.Abs(ProjectileRules.ArrowSpeed - ProjectileRules.OrbSpeed) < 0.0001f,
                "speed=" + ProjectileRules.ArrowSpeed.ToString("0"));
            Row(sb, "maze_v2e_geometry",
                System.Math.Abs(MazeRules.CombatWidth - 52f) < 0.001f
                && System.Math.Abs(MazeRules.PitchX - 82f) < 0.001f
                && MazeRules.QuotaChest == 2
                && !Stage1MazeGen.Generate(42).LargeChestUpgraded,
                "52x40 pitch 82/70 Chestx2 noLargeChest PortalFx=" + MazeRules.PortalHoldSeconds.ToString("0.0") + "s");
            Row(sb, "spawn_land_room_random",
                land.CenterSpread >= 3f && land.EvenRingHits <= land.MeleeN / 5,
                "spread=" + land.CenterSpread.ToString("0.00")
                + " evenRingHits=" + land.EvenRingHits
                + " bypass=SpawnCluster.Offset r=0.85");
            Row(sb, "spawn_land_melee_near_ranged_far",
                land.MeleeMean + 1.5f <= land.RangedMean && land.MeleeP50 + 1.0f <= land.RangedP50,
                "meleeMean=" + land.MeleeMean.ToString("0.00")
                + " rangedMean=" + land.RangedMean.ToString("0.00")
                + " gap=" + land.MeanGap.ToString("0.00")
                + " p10/p50/p90 melee=" + land.MeleeP10.ToString("0.00")
                + "/" + land.MeleeP50.ToString("0.00")
                + "/" + land.MeleeP90.ToString("0.00")
                + " ranged=" + land.RangedP10.ToString("0.00")
                + "/" + land.RangedP50.ToString("0.00")
                + "/" + land.RangedP90.ToString("0.00"));
            Row(sb, "spawn_land_min_player",
                land.MinPlayerHits == 0
                && land.MeleeMin + 0.001f >= CombatRoomSpawn.MinPlayerDist
                && land.RangedMin + 0.001f >= CombatRoomSpawn.MinPlayerDist,
                "min=" + CombatRoomSpawn.MinPlayerDist.ToString("0.00")
                + " meleeMin=" + land.MeleeMin.ToString("0.00")
                + " rangedMin=" + land.RangedMin.ToString("0.00")
                + " hits=" + land.MinPlayerHits);
            Row(sb, "spawn_land_avoid_chest_altar",
                land.AvoidHits == 0 && probe == null,
                "placedAvoidHits=" + land.AvoidHits
                + " probe=" + (probe ?? "PASS chest r=2 altar r=2.5 AABB+body")
                + " samples=" + SpawnLandFileName);
            sb.Append("art_gaps,NOTE,").Append(EntityAnimCatalog.GapNote());
            sb.AppendLine();
            sb.Append("# ").Append(err == null ? "PLAYABLE PASS" : "PLAYABLE FAIL");
            sb.AppendLine();
            return sb.ToString();
        }

        static void Row(StringBuilder sb, string check, bool pass, string detail)
        {
            sb.Append(check).Append(',').Append(pass ? "PASS" : "FAIL").Append(',');
            if (!string.IsNullOrEmpty(detail))
                sb.Append(detail.Replace(',', ';'));
            sb.AppendLine();
        }
    }
}
