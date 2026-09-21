using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;

namespace RogueShooter.Combat
{
    /// <summary>Returns null on pass. Collision / projectile / dodge i-frame ACCEPTANCE.</summary>
    public static class Stage1PlayableChecks
    {
        public static string Run()
        {
            string err = CheckDodgeTable();
            if (err != null) return err;
            err = CheckCollisionVolumes();
            if (err != null) return err;
            err = CheckProjectiles();
            if (err != null) return err;
            err = CheckJianHaiFiles();
            if (err != null) return err;
            err = CheckArtHooks();
            if (err != null) return err;
            err = CheckPlaceholderPack();
            if (err != null) return err;
            err = CheckActionSpec();
            if (err != null) return err;
            err = CheckSpawnCadence();
            if (err != null) return err;
            err = CheckSpawnLand();
            if (err != null) return err;
            return null;
        }

        public static string FormatPass()
        {
            var sb = new StringBuilder();
            sb.Append("playable=collision+arrow+orb+dodge-iframes ");
            sb.Append("art=JianHai-PNG-runtime placeholders_p1=");
            sb.Append(JianHaiArtCatalog.PlaceholderP1Count);
            sb.Append(" PPU").Append(JianHaiArtCatalog.Ppu);
            sb.Append(" pivotS1=(").Append(JianHaiArtCatalog.PivotS1.x.ToString("0.0"));
            sb.Append(";").Append(JianHaiArtCatalog.PivotS1.y.ToString("0.00")).Append(") ");
            sb.Append("layers=Player/Mob/Wall/Door/Projectile ");
            sb.Append("door=locked-blocks/open-pass ");
            sb.Append("arrow=jh_proj_arrow_fly speed=").Append(ProjectileRules.ArrowSpeed.ToString("0"));
            sb.Append(" orb=jh_proj_orb_mage_fly ");
            sb.Append("dodge dur=").Append(DodgeRules.DurationSeconds.ToString("0.00"));
            sb.Append("s iframe=").Append(DodgeRules.IFrameStartSeconds.ToString("0.00"));
            sb.Append("-").Append(DodgeRules.IFrameEndSeconds.ToString("0.00"));
            sb.Append("s cd=").Append(DodgeRules.CooldownSeconds.ToString("0.00"));
            sb.Append("s dist=").Append(DodgeRules.Distance.ToString("0.00"));
            sb.Append(" cancel=charge+recover fireBlocked stamina=0");
            sb.Append(" interact=chest+altar");
            sb.Append(" cadence=enter-PortalFx-").Append(MazeRules.PortalHoldSeconds.ToString("0.0"));
            sb.Append("s-w1 interwave-PortalFx-").Append(MazeRules.PortalHoldForWave(2).ToString("0.0"));
            sb.Append("s(≤").Append(MazeRules.InterWavePortalHoldMaxSeconds.ToString("0.0")).Append(")");
            CombatRoomSpawnStats land = CombatRoomSpawn.SampleSeed42();
            sb.Append(" spawn=room-random melee-near/ranged-far");
            sb.Append(" meleeMean=").Append(land.MeleeMean.ToString("0.00"));
            sb.Append(" rangedMean=").Append(land.RangedMean.ToString("0.00"));
            sb.Append(" gap=").Append(land.MeanGap.ToString("0.00"));
            sb.Append(" p50M=").Append(land.MeleeP50.ToString("0.00"));
            sb.Append(" p50R=").Append(land.RangedP50.ToString("0.00"));
            sb.Append(" spread=").Append(land.CenterSpread.ToString("0.00"));
            sb.Append(" minPlayer=").Append(CombatRoomSpawn.MinPlayerDist.ToString("0.00"));
            sb.Append(" avoidHits=").Append(land.AvoidHits);
            sb.Append(" evenRing=").Append(land.EvenRingHits);
            return sb.ToString();
        }

        static string CheckDodgeTable()
        {
            if (Math.Abs(DodgeRules.DurationSeconds - 0.40f) > 0.0001f)
                return "dodge duration draft 0.40s";
            if (Math.Abs(DodgeRules.IFrameStartSeconds - 0.04f) > 0.0001f)
                return "iframe start draft 0.04s (unlocked table)";
            if (Math.Abs(DodgeRules.IFrameEndSeconds - 0.28f) > 0.0001f)
                return "iframe end draft 0.28s (unlocked table)";
            if (Math.Abs(DodgeRules.IFrameSeconds - 0.24f) > 0.0001f)
                return "iframe length 0.24s (0.04–0.28)";
            if (Math.Abs(DodgeRules.CooldownSeconds - 1.00f) > 0.0001f)
                return "dodge cooldown draft 1.00s";
            if (Math.Abs(DodgeRules.Distance - 6f) > 0.0001f)
                return "dodge displacement draft 6u";
            if (Math.Abs(DodgeRules.StaminaCost) > 0.0001f)
                return "dodge stamina cost must stay 0";
            if (!DodgeRules.CancelCharge || !DodgeRules.CancelShotRecovery)
                return "roll must cancel charge and shot recovery";
            if (!DodgeRules.BlockFireWhileRolling)
                return "no fire while rolling";
            if (PlayerDodge.RecoveryBlocksRoll())
                return "recovery must not block roll";
            if (DodgeRules.IFrameActive(0.03f, 0f))
                return "startup 0–0.04s is hittable";
            if (DodgeRules.IFrameActive(0.04f, 0f) == false)
                return "i-frame on at t=0.04";
            if (DodgeRules.IFrameActive(0.10f, 0f) == false)
                return "i-frame on at t=0.10";
            if (DodgeRules.IFrameActive(DodgeRules.IFrameEndSeconds, 0f))
                return "i-frame off at end";
            if (DodgeRules.IFrameActive(0.29f, 0f))
                return "i-frame off after window";
            if (!DodgeRules.RollActive(0.39f, 0f) || DodgeRules.RollActive(0.41f, 0f))
                return "roll duration window 0.40s";
            if (!DodgeRules.OnCooldown(0.99f, 0f) || DodgeRules.OnCooldown(1.01f, 0f))
                return "dodge cooldown window 1.00s";
            if (PlayerVitals.HitBlockedByIFrame(true))
            { /* expected */ }
            else
                return "iframe must block hits";
            if (PlayerVitals.HitBlockedByIFrame(false))
                return "no iframe must apply hit";
            return null;
        }

        static string CheckCollisionVolumes()
        {
            Stage1Maze maze = Stage1MazeGen.Generate(42);
            List<MazeSolid> solids = MazeCollisionBuilder.Build(maze);
            if (solids == null || solids.Count < 8)
                return "maze solids missing";
            int walls = 0;
            int doors = 0;
            for (int i = 0; i < solids.Count; i++)
            {
                if (solids[i].Door)
                    doors++;
                else
                    walls++;
            }

            if (walls < 8)
                return "need wall volumes";
            if (doors < 2)
                return "need door volumes";

            MazeNode start = maze.Find("START");
            if (start == null)
                return "missing START";
            CollisionSpace space = Fill(solids, true);
            float hx = CollisionRules.PlayerHalfX;
            float hy = CollisionRules.PlayerHalfY;
            float x = start.Center.X;
            float y = start.Center.Y;
            float n = start.Height * 0.5f - hy - CollisionRules.WallThickness - 1f;
            space.TryMove(ref x, ref y, hx, hy, 0f, n, CollisionRules.SolidMask);
            if (Math.Abs(y - start.Center.Y) < 0.5f)
                return "player should walk toward north door inside room";

            MazeSolid startDoor = FindDoor(solids, "START");
            if (startDoor.Width < 0.01f)
                return "START door volume missing";

            CollisionSpace locked = Fill(solids, true);
            x = startDoor.X;
            y = startDoor.Y - startDoor.Height * 0.5f - hy - 0.02f;
            float y0 = y;
            locked.TryMove(ref x, ref y, hx, hy, 0f, 3f, CollisionRules.SolidMask);
            if (y - y0 > 1.2f)
                return "locked door must block player";

            CollisionSpace open = Fill(solids, false);
            x = startDoor.X;
            y = startDoor.Y - startDoor.Height * 0.5f - hy - 0.02f;
            y0 = y;
            open.TryMove(ref x, ref y, hx, hy, 0f, 3f, CollisionRules.SolidMask);
            if (y - y0 < 1.0f)
                return "open door must allow player through opening";

            CollisionSpace wallOnly = Fill(solids, true);
            x = start.Center.X + start.Width * 0.25f;
            y = start.Center.Y + start.Height * 0.5f - hy - 0.05f;
            y0 = y;
            wallOnly.TryMove(ref x, ref y, hx, hy, 0f, 4f, CollisionRules.SolidMask);
            if (y - y0 > 0.8f)
                return "wall must block player (no ghosting)";
            return null;
        }

        static string CheckProjectiles()
        {
            if (Math.Abs(ProjectileRules.ArrowSpeed - EnemyCombatRules.OrbSpeedAbsStub) > 0.0001f)
                return "arrow speed must reuse orb speed stub";
            if (Math.Abs(ProjectileRules.ArrowMaxRange - 8f) > 0.0001f)
                return "arrow range must stay charge 8";
            if (Math.Abs(ProjectileRules.ArrowHitRadius - EnemyCombatRules.OrbHitRadiusStub) > 0.0001f)
                return "arrow hit radius must reuse orb 0.40";
            if (JianHaiArtCatalog.ArrowFlight != "jh_proj_arrow_fly")
                return "arrow visual uses PHASE1 jh_proj_arrow_fly";
            if (Math.Abs(JianHaiArtCatalog.PivotForArtId(JianHaiArtCatalog.ArrowFlight).x - 0.2f) > 0.001f
                || Math.Abs(JianHaiArtCatalog.PivotForArtId(JianHaiArtCatalog.ArrowFlight).y - 0.5f) > 0.001f)
                return "arrow pivot mid-rear (0.2,0.5) facing +X";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.ArrowFlight) != "Projectiles")
                return "arrow folder Projectiles";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.OrbFlight) != "Projectiles")
                return "orb art folder Projectiles";
            if (!JianHaiSprites.HasSourceFile(JianHaiArtCatalog.ArrowFlight))
                return "arrow PNG missing jh_proj_arrow_fly";
            if (!JianHaiSprites.HasSourceFile(JianHaiArtCatalog.OrbFlight))
                return "mage orb PNG missing jh_proj_orb_mage_fly";
            if (JianHaiArtCatalog.AssetPath(JianHaiArtCatalog.OrbFlight)
                != "Assets/Art/JianHai/Projectiles/jh_proj_orb_mage_fly.png")
                return "mage orb asset path";
            int ow, oh;
            JianHaiArtCatalog.CanvasForArtId(JianHaiArtCatalog.OrbFlight, out ow, out oh);
            if (ow != 32 || oh != 32)
                return "orb canvas 32x32";
            if (Math.Abs(JianHaiArtCatalog.PivotForArtId(JianHaiArtCatalog.OrbFlight).x - 0.5f) > 0.001f
                || Math.Abs(JianHaiArtCatalog.PivotForArtId(JianHaiArtCatalog.OrbFlight).y - 0.5f) > 0.001f)
                return "orb pivot center";
            if (JianHaiArtCatalog.SortingOrderForArtId(JianHaiArtCatalog.ArrowFlight)
                != JianHaiArtCatalog.SortingOrderProjectile)
                return "projectile sorting order 30";
            if (JianHaiArtCatalog.Ppu != 32)
                return "PHASE1 PPU 32";
            if (Math.Abs(JianHaiArtCatalog.PivotS1.x - 0.5f) > 0.001f
                || Math.Abs(JianHaiArtCatalog.PivotS1.y - 0.15f) > 0.001f)
                return "S1 pivot (0.5,0.15)";

            Stage1Maze maze = Stage1MazeGen.Generate(42);
            List<MazeSolid> solids = MazeCollisionBuilder.Build(maze);
            MazeNode start = maze.Find("START");
            CollisionSpace locked = Fill(solids, true);
            CollisionHit wallHit = locked.Trace(
                start.Center.X + start.Width * 0.25f,
                start.Center.Y,
                0f, 1f,
                40f,
                ProjectileRules.ArrowHitRadius,
                CollisionLayer.Wall | CollisionLayer.Door);
            if (!wallHit.Hit || wallHit.Layer != CollisionLayer.Wall)
                return "arrow must hit north wall off-opening";

            MazeSolid door = FindDoor(solids, "START");
            CollisionHit doorHit = locked.Trace(
                door.X, door.Y - 2f,
                0f, 1f,
                8f,
                ProjectileRules.ArrowHitRadius,
                CollisionLayer.Door);
            if (!doorHit.Hit || doorHit.Layer != CollisionLayer.Door)
                return "arrow/orb must hit locked door";

            CollisionSpace open = Fill(solids, false);
            CollisionHit openHit = open.Trace(
                door.X, door.Y - 2f,
                0f, 1f,
                8f,
                ProjectileRules.OrbHitRadius,
                CollisionLayer.Door);
            if (openHit.Hit)
                return "open door must not stop projectile";

            var mobSpace = new CollisionSpace();
            mobSpace.Add(
                new CollisionAabb(start.Center.X + 3f, start.Center.Y, CollisionRules.MobHalfX, CollisionRules.MobHalfY),
                CollisionLayer.Mob, false, "mob");
            CollisionHit mobHit = mobSpace.Trace(
                start.Center.X, start.Center.Y, 1f, 0f, ProjectileRules.ArrowMaxRange,
                ProjectileRules.ArrowHitRadius, CollisionLayer.Mob);
            if (!mobHit.Hit || mobHit.Layer != CollisionLayer.Mob)
                return "arrow trajectory must hit mob volume";
            return null;
        }

        static string CheckJianHaiFiles()
        {
            string[] must =
            {
                JianHaiArtCatalog.PlayerIdle,
                JianHaiArtCatalog.EnemyE1Idle,
                JianHaiArtCatalog.BossIdle,
                JianHaiArtCatalog.ArrowFlight,
                JianHaiArtCatalog.FxTipIdle,
                JianHaiArtCatalog.OrbFlight,
                JianHaiArtCatalog.PlayerRollRoot + "_00",
                JianHaiArtCatalog.TileFloorSpawn,
                JianHaiArtCatalog.TileFloorCorridor,
                JianHaiArtCatalog.TileFloorAltar,
                JianHaiArtCatalog.TileFloorHub,
                JianHaiArtCatalog.WallStone,
                JianHaiArtCatalog.PropGateHub,
                JianHaiArtCatalog.SpriteNameForHook("Chest_01", "closed"),
                JianHaiArtCatalog.SpriteNameForHook("A_Shared", "idle")
            };
            for (int i = 0; i < must.Length; i++)
            {
                if (!JianHaiSprites.HasSourceFile(must[i]))
                    return "missing JianHai PNG " + must[i];
            }

            if (JianHaiSprites.HasSourceFile(EntityAnimCatalog.PlayerWalk)
                || JianHaiSprites.HasClip(EntityAnimCatalog.PlayerWalk))
            { /* drop-in walk frames welcome */ }
            else if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Walk) != JianHaiArtCatalog.PlayerIdle)
                return "missing walk must fall back to idle";
            if (!JianHaiSprites.HasClip(EntityAnimCatalog.PlayerRoll))
                return "roll clip jh_char_archer_roll_* missing";
            return null;
        }

        static string CheckArtHooks()
        {
            if (EntityAnimCatalog.PlayerIdle != JianHaiArtCatalog.PlayerIdle)
                return "player idle hook";
            if (EntityAnimCatalog.EnemyIdle != JianHaiArtCatalog.EnemyE1Idle)
                return "enemy idle hook";
            if (EntityAnimCatalog.Present(EntityAnimCatalog.PlayerWalk))
            {
                if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Walk) != EntityAnimCatalog.PlayerWalk)
                    return "player walk present must resolve";
            }
            else if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Walk) != JianHaiArtCatalog.PlayerIdle)
                return "missing walk must fall back to idle";
            if (EntityAnimCatalog.Present(EntityAnimCatalog.PlayerRoll))
            {
                if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Dodge) != EntityAnimCatalog.PlayerRoll)
                    return "roll clip present must resolve";
            }
            else if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Dodge) != JianHaiArtCatalog.PlayerIdle)
                return "missing roll clip must fall back to idle";
            string e1Walk = ActionSpecP1.EnemyWalk(EnemyKindIds.Normal).Root;
            if (EntityAnimCatalog.Present(e1Walk))
            {
                if (EntityAnimCatalog.ResolveEnemy(EntityAnimState.Walk) != e1Walk)
                    return "present enemy walk must resolve";
            }
            else if (EntityAnimCatalog.ResolveEnemy(EntityAnimState.Walk) != JianHaiArtCatalog.EnemyE1Idle)
                return "missing enemy walk must fall back to idle";
            string gaps = EntityAnimCatalog.GapNote();
            if (string.IsNullOrEmpty(gaps) || gaps.IndexOf("placeholders_p1", StringComparison.Ordinal) < 0)
                return "art gap note must name placeholders_p1";
            if (gaps.IndexOf("n/e/w", StringComparison.Ordinal) < 0)
                return "art gap note must list missing walk n/e/w";
            return null;
        }

        public static string CheckPlaceholderPack()
        {
            int n = CountNumberedJhPngs("Characters") + CountNumberedJhPngs("Enemies");
            if (n != JianHaiArtCatalog.PlaceholderP1Count)
                return "placeholders_p1 must be " + JianHaiArtCatalog.PlaceholderP1Count
                    + " numbered 64x64 PNG, have " + n;
            string dimErr = CheckNumberedPngSize("Characters", 64, 64);
            if (dimErr != null) return dimErr;
            dimErr = CheckNumberedPngSize("Enemies", 64, 64);
            if (dimErr != null) return dimErr;
            if (Math.Abs(JianHaiArtCatalog.PivotS1.x - 0.5f) > 0.001f
                || Math.Abs(JianHaiArtCatalog.PivotS1.y - 0.15f) > 0.001f
                || JianHaiArtCatalog.Ppu != 32)
                return "placeholders_p1 PPU32 pivot (0.5,0.15)";
            if (!File.Exists(Path.Combine("Assets", "Art", "JianHai", "_Spec", "PLACEHOLDERS_P1_INDEX.md")))
                return "missing PLACEHOLDERS_P1_INDEX.md";
            if (!File.Exists(ActionSpecP1.SpecFile))
                return "missing ACTION_SPEC_P1_v01.md";

            string[] roots = EntityAnimCatalog.PlaceholderClipRoots;
            for (int i = 0; i < roots.Length; i++)
            {
                if (!EntityAnimCatalog.Present(roots[i]))
                    return "placeholders_p1 missing clip " + roots[i];
            }

            if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Walk) != EntityAnimCatalog.PlayerWalk)
                return "player walk pack must resolve walk (not idle)";
            if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Charge) != EntityAnimCatalog.PlayerCharge)
                return "player charge pack must resolve";
            if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Attack) != EntityAnimCatalog.PlayerAtk)
                return "player atk pack must resolve";
            if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Dodge) != EntityAnimCatalog.PlayerRoll)
                return "player roll pack must resolve";
            if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Hurt) != EntityAnimCatalog.PlayerHurt)
                return "player hurt pack must resolve";
            if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Death) != EntityAnimCatalog.PlayerDie)
                return "player die pack must resolve";

            if (EntityAnimCatalog.ResolveEnemy(EntityAnimState.Walk, EnemyKindIds.Normal)
                != ActionSpecP1.EnemyWalk(EnemyKindIds.Normal).Root)
                return "e1 walk pack must resolve";
            if (EntityAnimCatalog.ResolveEnemy(EntityAnimState.Walk, EnemyKindIds.Dog)
                != ActionSpecP1.EnemyWalk(EnemyKindIds.Dog).Root)
                return "dog walk pack must resolve";
            if (EntityAnimCatalog.ResolveEnemy(EntityAnimState.Attack, EnemyKindIds.CultMage)
                != ActionSpecP1.EnemyAttack(EnemyKindIds.CultMage).Root)
                return "mage cast pack must resolve";
            if (ActionSpecP1.EnemyChase(EnemyKindIds.CultMage).Root
                != ActionSpecP1.EnemyWalk(EnemyKindIds.CultMage).Root)
                return "mage chase must reuse walk per ACTION_SPEC";
            return null;
        }

        static int CountNumberedJhPngs(string folder)
        {
            string dir = Path.Combine("Assets", "Art", "JianHai", folder);
            if (!Directory.Exists(dir))
                return 0;
            int n = 0;
            string[] files = Directory.GetFiles(dir, "jh_*.png");
            for (int i = 0; i < files.Length; i++)
            {
                if (IsNumberedJhName(Path.GetFileNameWithoutExtension(files[i])))
                    n++;
            }

            return n;
        }

        static string CheckNumberedPngSize(string folder, int width, int height)
        {
            string dir = Path.Combine("Assets", "Art", "JianHai", folder);
            if (!Directory.Exists(dir))
                return "missing JianHai folder " + folder;
            string[] files = Directory.GetFiles(dir, "jh_*.png");
            for (int i = 0; i < files.Length; i++)
            {
                if (!IsNumberedJhName(Path.GetFileNameWithoutExtension(files[i])))
                    continue;
                int w, h;
                if (!TryReadPngSize(files[i], out w, out h))
                    return "bad PNG header " + Path.GetFileName(files[i]);
                if (w != width || h != height)
                    return Path.GetFileName(files[i]) + " must be " + width + "x" + height
                        + " have " + w + "x" + h;
            }

            return null;
        }

        static bool IsNumberedJhName(string stem)
        {
            if (string.IsNullOrEmpty(stem) || stem.Length < 3)
                return false;
            return stem[stem.Length - 3] == '_'
                && char.IsDigit(stem[stem.Length - 2])
                && char.IsDigit(stem[stem.Length - 1]);
        }

        static bool TryReadPngSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            FileStream fs = null;
            try
            {
                fs = File.OpenRead(path);
                byte[] hdr = new byte[24];
                int got = fs.Read(hdr, 0, 24);
                if (got < 24)
                    return false;
                if (hdr[0] != 0x89 || hdr[1] != 0x50 || hdr[2] != 0x4e || hdr[3] != 0x47)
                    return false;
                width = (hdr[16] << 24) | (hdr[17] << 16) | (hdr[18] << 8) | hdr[19];
                height = (hdr[20] << 24) | (hdr[21] << 16) | (hdr[22] << 8) | hdr[23];
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (fs != null)
                    fs.Dispose();
            }
        }

        static string CheckActionSpec()
        {
            if (Math.Abs(ActionSpecP1.Fps - 12f) > 0.001f)
                return "ACTION_SPEC fps 12";
            if (ActionSpecP1.PlayerRoll.Root != "jh_char_archer_roll")
                return "player roll root";
            if (ActionSpecP1.PlayerFire.EventName != "OnFire" || ActionSpecP1.PlayerFire.EventFrame != 1)
                return "OnFire @ atk _01";
            if (ActionSpecP1.PlayerOnFireSeconds < 0.05f || ActionSpecP1.PlayerOnFireSeconds > 0.12f)
                return "OnFire time ~1/12s";
            if (Math.Abs(ActionSpecP1.PlayerRoll.Duration - DodgeRules.DurationSeconds) > 0.0001f)
                return "roll clip duration must follow DodgeRules";
            if (ActionSpecP1.ChargePoseFrame(0.05f) > 1)
                return "charge pose start _00/_01";
            if (ActionSpecP1.ChargePoseFrame(0.70f) != 4)
                return "charge pose green _04";
            if (ActionSpecP1.ChargePoseFrame(0.80f) != 5)
                return "charge pose full _05";
            if (ActionSpecP1.EnemyRoot(EnemyKindIds.Normal) != "jh_enemy_e1_skel")
                return "E1 skel root";
            if (ActionSpecP1.EnemyRoot(EnemyKindIds.Dog) != "jh_enemy_dog")
                return "dog root";
            if (ActionSpecP1.EnemyRoot(EnemyKindIds.CultMage) != "jh_enemy_mage")
                return "mage root";
            if (ActionSpecP1.EnemyAttack(EnemyKindIds.CultMage).EventName != "OnOrbSpawn")
                return "mage OnOrbSpawn";
            if (ActionSpecP1.EnemyAttack(EnemyKindIds.Normal).EventName != "OnHitOpen")
                return "skel OnHitOpen";
            if (ActionSpecP1.Cardinal(0f, -1f) != "s" || ActionSpecP1.Cardinal(1f, 0f) != "e")
                return "cardinal n/e/s/w";
            if (ActionSpecP1.PlayerRollIFrameStartFrame > 1)
                return "i-frame start still on early roll frames";
            if (ActionSpecP1.PlayerRollIFrameEndFrame < 4)
                return "i-frame end should reach mid-roll frames before recover";
            if (EntityAnimCatalog.PlayerSprite(EntityAnimState.Dodge) != EntityAnimCatalog.PlayerRoll)
                return "dodge maps to roll clip";
            if (EntityAnimCatalog.ResolveEnemy(EntityAnimState.Walk, EnemyKindIds.Dog)
                != EntityAnimCatalog.ResolveEnemyIdle(EnemyKindIds.Dog)
                && !EntityAnimCatalog.Present("jh_enemy_dog_walk"))
                return "missing dog walk falls back to dog/E1 idle";
            return null;
        }

        public static string CheckSpawnCadence()
        {
            if (Math.Abs(MazeRules.PortalHoldSeconds - 1.0f) > 0.001f)
                return "enter→PortalFx→wave1 must be 1.0s";
            if (Math.Abs(MazeRules.InterWavePortalHoldMaxSeconds - 3.0f) > 0.001f)
                return "inter-wave hard cap must be 3.0s";
            if (MazeRules.InterWavePortalHoldSeconds <= MazeRules.InterWavePortalHoldMinSeconds + 0.0001f)
                return "inter-wave hold must be >2.0s (not 1.0s or 2.0s)";
            if (MazeRules.InterWavePortalHoldSeconds > MazeRules.InterWavePortalHoldMaxSeconds + 0.0001f)
                return "inter-wave hold must be ≤3.0s";
            if (Math.Abs(MazeRules.InterWavePortalHoldSeconds - 2.5f) > 0.001f)
                return "inter-wave hold must be 2.5s from PortalFx show";
            if (Math.Abs(MazeRules.InterWavePortalHoldSeconds - MazeRules.PortalHoldSeconds) < 0.001f)
                return "inter-wave must not reuse wave-1 1.0s";
            if (Math.Abs(MazeRules.PortalHoldForWave(1) - 1.0f) > 0.001f)
                return "wave 1 hold 1.0s";
            if (Math.Abs(MazeRules.PortalHoldForWave(2) - 2.5f) > 0.001f)
                return "wave 2 hold 2.5s";
            string w1 = PortalFxHook.FormatSpawn("N1", 1);
            if (w1 != "[PortalFx] room=N1 wave=1 spawn after 1.0s")
                return "wave1 spawn log " + w1;
            string w2 = PortalFxHook.FormatSpawn("ALTAR", 2);
            if (w2 != "[PortalFx] room=ALTAR wave=2 spawn after 2.5s")
                return "wave2 spawn log " + w2;
            return null;
        }

        static string CheckSpawnLand()
        {
            if (Math.Abs(CombatRoomSpawn.ChestClearance - 2f) > 0.001f)
                return "chest clearance must reuse HOOKS r≈2";
            if (Math.Abs(CombatRoomSpawn.AltarClearance - 2.5f) > 0.001f)
                return "altar clearance must reuse LOCK 2.5";
            if (Math.Abs(CombatRoomSpawn.MinPlayerDist - CombatRoomSpawn.ChestClearance) > 0.001f)
                return "min player dist reuses chest r=2";
            if (Math.Abs(CombatRoomSpawn.RoomInset - (CollisionRules.WallThickness + CollisionRules.MobHalfX)) > 0.001f)
                return "room inset = wall+body";
            if (CombatRoomSpawn.IsRanged(EnemyKindIds.CultMage) == false)
                return "cult mage is ranged";
            if (CombatRoomSpawn.IsRanged(EnemyKindIds.Normal) || CombatRoomSpawn.IsRanged(EnemyKindIds.Dog))
                return "E1/dog are melee";

            string probe = CombatRoomSpawn.ProbeAvoidVolumes();
            if (probe != null)
                return "avoid probe " + probe;

            CombatRoomSpawnStats s = CombatRoomSpawn.SampleSeed42();
            if (s.MeleeN < 8 || s.RangedN < 8)
                return "spawn sample too small";
            if (s.MinPlayerHits != 0)
                return "min-player hits " + s.MinPlayerHits;
            if (s.AvoidHits != 0)
                return "chest/altar avoid hits " + s.AvoidHits;
            if (s.EvenRingHits > s.MeleeN / 5)
                return "still even-ring r=0.85 hits=" + s.EvenRingHits;
            if (s.CenterSpread < 3f)
                return "center spread " + s.CenterSpread.ToString("0.00") + " still looks like tiny ring";
            if (s.MeleeMean + 1.5f > s.RangedMean)
                return "ranged must land farther than melee meanM="
                    + s.MeleeMean.ToString("0.00") + " meanR=" + s.RangedMean.ToString("0.00");
            if (s.MeleeP50 + 1.0f > s.RangedP50)
                return "ranged p50 must exceed melee p50+1 p50M="
                    + s.MeleeP50.ToString("0.00") + " p50R=" + s.RangedP50.ToString("0.00");
            if (s.MeleeMin + 0.001f < CombatRoomSpawn.MinPlayerDist
                || s.RangedMin + 0.001f < CombatRoomSpawn.MinPlayerDist)
                return "sample inside min player dist";
            return null;
        }

        static CollisionSpace Fill(List<MazeSolid> solids, bool doorsSolid)
        {
            var space = new CollisionSpace();
            for (int i = 0; i < solids.Count; i++)
            {
                MazeSolid s = solids[i];
                bool solid = s.Door ? doorsSolid : true;
                space.Add(
                    CollisionAabb.FromCenterSize(s.X, s.Y, s.Width, s.Height),
                    s.Layer, solid, s.Name);
            }

            return space;
        }

        static MazeSolid FindDoor(List<MazeSolid> solids, string roomId)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                if (solids[i].Door && solids[i].RoomId == roomId)
                    return solids[i];
            }

            return default(MazeSolid);
        }
    }
}
