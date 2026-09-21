using System.IO;
using System.Text;
using RogueShooter.Art;
using RogueShooter.Maze;
using RogueShooter.Player;

namespace RogueShooter.Combat
{
    /// <summary>Writes Logs/stage1_playable_evidence.csv — collision / arrow / orb / dodge i-frames.</summary>
    public static class Stage1PlayableSampler
    {
        public static string DefaultFileName => "stage1_playable_evidence.csv";

        public static string DefaultPath()
        {
            return Path.Combine(Stage1MazeSampler.DefaultDirectory(), DefaultFileName);
        }

        public static string StreamingPath()
        {
            string root = Path.Combine("Assets", "StreamingAssets", "EnemyPoolDraft");
            return Path.Combine(root, DefaultFileName);
        }

        public static string WriteDefault()
        {
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

        public static string Csv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Stage-1 playable evidence (collision / projectiles / dodge i-frames)");
            sb.AppendLine("# Maze v2e unchanged: 52x40 pitch 82/70 Chestx2 no LargeChest CONN follows Altar PortalFx 1.0s");
            sb.AppendLine("check,result,detail");
            string err = Stage1PlayableChecks.Run();
            Row(sb, "playable_acceptance", err == null, err ?? Stage1PlayableChecks.FormatPass());
            Row(sb, "dodge_duration",
                DodgeRules.DurationSeconds >= 0.30f && DodgeRules.DurationSeconds <= 0.40f,
                "dur=" + DodgeRules.DurationSeconds.ToString("0.00") + "s");
            Row(sb, "dodge_iframes",
                DodgeRules.IFrameActive(0.10f, 0f) && !DodgeRules.IFrameActive(0.21f, 0f),
                "iframe=" + DodgeRules.IFrameSeconds.ToString("0.00") + "s t=0.10 block t=0.21 apply");
            Row(sb, "dodge_cooldown",
                DodgeRules.CooldownSeconds >= 0.80f && DodgeRules.CooldownSeconds <= 1.00f,
                "cd=" + DodgeRules.CooldownSeconds.ToString("0.00") + "s");
            Row(sb, "dodge_distance_derived",
                System.Math.Abs(DodgeRules.Distance - DodgeRules.MoveSpeedRef * DodgeRules.DurationSeconds) < 0.0001f,
                "dist=" + DodgeRules.Distance.ToString("0.00") + "=move6×dur (not a new table row)");
            Row(sb, "arrow_art",
                JianHaiArtCatalog.ArrowFlight == JianHaiArtCatalog.FxTipWarm,
                JianHaiArtCatalog.ArrowFlight);
            Row(sb, "orb_art",
                JianHaiArtCatalog.FxMageOrb == "jh_fx_mage_orb",
                JianHaiArtCatalog.FxMageOrb + " placeholder (no PNG in Art/JianHai/FX)");
            Row(sb, "arrow_speed_reuses_orb",
                System.Math.Abs(ProjectileRules.ArrowSpeed - ProjectileRules.OrbSpeed) < 0.0001f,
                "speed=" + ProjectileRules.ArrowSpeed.ToString("0"));
            Row(sb, "maze_v2e_geometry",
                System.Math.Abs(MazeRules.CombatWidth - 52f) < 0.001f
                && System.Math.Abs(MazeRules.PitchX - 82f) < 0.001f
                && MazeRules.QuotaChest == 2
                && !Stage1MazeGen.Generate(42).LargeChestUpgraded,
                "52x40 pitch 82/70 Chestx2 noLargeChest PortalFx=" + MazeRules.PortalHoldSeconds.ToString("0.0") + "s");
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
