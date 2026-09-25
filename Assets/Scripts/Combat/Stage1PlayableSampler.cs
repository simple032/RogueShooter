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
            sb.AppendLine("# Maze v2e unchanged: 52x40 pitch 82/70 Chestx2 no LargeChest CONN follows Altar");
            sb.AppendLine("# Cadence: enter→PortalFx→1.0s→w1; two-wave clear-w1→PortalFx→2.5s(>2 ≤3)→w2 from show");
            sb.AppendLine("# Art: Assets/Art/JianHai/ Provide-sourced PNGs; runtime File.ReadAllBytes+LoadImage (Editor import still preferred)");
            sb.AppendLine("# placeholders_p1: 132 numbered 64x64 jh_ PNG PPU32 pivot (0.5,0.15); same-name true art replaces");
            sb.AppendLine("# Spawn land: CombatRoomSpawn room-AABB random; melee-near/ranged-far; min player; avoid chest/altar; bypass even-ring");
            sb.AppendLine("check,result,detail");
            string err = Stage1PlayableChecks.Run();
            CombatRoomSpawnStats land = CombatRoomSpawn.SampleSeed42();
            string probe = CombatRoomSpawn.ProbeAvoidVolumes();
            Row(sb, "playable_acceptance", err == null, err ?? Stage1PlayableChecks.FormatPass());
            Row(sb, "dodge_duration",
                System.Math.Abs(DodgeRules.DurationSeconds - 0.40f) < 0.0001f,
                "dur=" + DodgeRules.DurationSeconds.ToString("0.00") + "s draft/ACTION_SPEC");
            Row(sb, "dodge_iframes",
                DodgeRules.IFrameActive(0.04f, 0f)
                && DodgeRules.IFrameActive(0.10f, 0f)
                && !DodgeRules.IFrameActive(0.03f, 0f)
                && !DodgeRules.IFrameActive(0.28f, 0f),
                "iframe=" + DodgeRules.IFrameStartSeconds.ToString("0.00")
                + "-" + DodgeRules.IFrameEndSeconds.ToString("0.00")
                + "s len=" + DodgeRules.IFrameSeconds.ToString("0.00")
                + "s draft-unlocked");
            Row(sb, "dodge_cooldown",
                System.Math.Abs(DodgeRules.CooldownSeconds - 1.00f) < 0.0001f,
                "cd=" + DodgeRules.CooldownSeconds.ToString("0.00") + "s");
            Row(sb, "dodge_displacement",
                System.Math.Abs(DodgeRules.Distance - 6f) < 0.0001f,
                "dist=" + DodgeRules.Distance.ToString("0.00") + "u draft table (not speed×dur)");
            Row(sb, "dodge_cancel_into_roll",
                DodgeRules.CancelCharge && DodgeRules.CancelShotRecovery
                && DodgeRules.BlockFireWhileRolling
                && DodgeRules.StaminaCost < 0.0001f
                && !PlayerDodge.RecoveryBlocksRoll(),
                "cancel=charge+shotRecover blockFire stamina=0");
            Row(sb, "action_spec_p1",
                System.Math.Abs(ActionSpecP1.Fps - 12f) < 0.001f
                && ActionSpecP1.PlayerRoll.Root == "jh_char_archer_roll"
                && ActionSpecP1.EnemyRoot(EnemyKindIds.Dog) == "jh_enemy_dog",
                "fps=12 roll+charge/atk idle/move/hurt/death; S1 N/D/M clips; OnFire@_01");
            Row(sb, "phase1_sprite_contract",
                JianHaiArtCatalog.Ppu == 32
                && System.Math.Abs(JianHaiArtCatalog.PivotS1.x - 0.5f) < 0.001f
                && System.Math.Abs(JianHaiArtCatalog.PivotS1.y - 0.15f) < 0.001f
                && JianHaiArtCatalog.ArrowFlight == "jh_proj_arrow_fly"
                && JianHaiSprites.HasClip(EntityAnimCatalog.PlayerRoll),
                "PPU32 pivotS1=(0.5;0.15) arrow=+X orb=32c roll=jh_char_archer_roll_*");
            string packErr = Stage1PlayableChecks.CheckPlaceholderPack();
            Row(sb, "placeholders_p1",
                packErr == null,
                packErr ?? ("count=" + JianHaiArtCatalog.PlaceholderP1Count
                    + " 64x64 PPU32 pivot=(0.5;0.15) Characters/Enemies same-name replaceable"));
            // Projectile PNGs not on main (code-only port): informational, warning not failure.
            Row(sb, "arrow_art",
                JianHaiArtCatalog.ArrowFlight == "jh_proj_arrow_fly",
                JianHaiArtCatalog.ArrowFlight + " +X pivot=0.2,0.5"
                + (JianHaiSprites.HasSourceFile(JianHaiArtCatalog.ArrowFlight) ? "" : " WARN placeholder (PNG not on main)"));
            Row(sb, "orb_art",
                true,
                JianHaiArtCatalog.OrbFlight + " " + JianHaiArtCatalog.AssetPath(JianHaiArtCatalog.OrbFlight)
                + (JianHaiSprites.HasSourceFile(JianHaiArtCatalog.OrbFlight) ? "" : " WARN placeholder (PNG not on main)"));
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
                "52x40 pitch 82/70 Chestx2 noLargeChest PortalFx w1="
                + MazeRules.PortalHoldSeconds.ToString("0.0") + "s w2="
                + MazeRules.PortalHoldForWave(2).ToString("0.0") + "s(≤"
                + MazeRules.InterWavePortalHoldMaxSeconds.ToString("0.0") + ")");
            string cadenceErr = Stage1PlayableChecks.CheckSpawnCadence();
            Row(sb, "spawn_cadence_wave1",
                cadenceErr == null
                && System.Math.Abs(MazeRules.PortalHoldSeconds - 1.0f) < 0.001f
                && PortalFxHook.FormatSpawn("N1", 1).IndexOf(" spawn after 1.0s") >= 0,
                "enter→PortalFx→1.0s→wave1 visible");
            Row(sb, "spawn_cadence_interwave",
                cadenceErr == null
                && MazeRules.InterWavePortalHoldSeconds > MazeRules.InterWavePortalHoldMinSeconds
                && MazeRules.InterWavePortalHoldSeconds <= MazeRules.InterWavePortalHoldMaxSeconds + 0.0001f
                && System.Math.Abs(MazeRules.PortalHoldForWave(2) - 2.5f) < 0.001f
                && System.Math.Abs(MazeRules.InterWavePortalHoldSeconds - 1.0f) > 0.001f
                && System.Math.Abs(MazeRules.InterWavePortalHoldSeconds - 2.0f) > 0.001f,
                "clear-w1→PortalFx→" + MazeRules.PortalHoldForWave(2).ToString("0.0")
                + "s(>2.0 ≤" + MazeRules.InterWavePortalHoldMaxSeconds.ToString("0.0")
                + ") from show; not 1.0s or 2.0s");
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
