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
            // placeholders_p1 (132 PNG) was not ported: main's PR#15/#16 frames replace it.
            // CheckPlaceholderPack stays as an informational row in Stage1PlayableSampler.
            err = CheckActionSpec();
            if (err != null) return err;
            err = CheckSpawnCadence();
            if (err != null) return err;
            err = CheckSpawnLand();
            if (err != null) return err;
            err = CheckRetestFixes();
            if (err != null) return err;
            err = CheckDoorGeometry();
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
            // Projectile PNGs are not on main yet (code-only port): a generated placeholder is used.
            // Report as a warning once, not an acceptance failure / LogError.
            WarnMissingProjectileArt();
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
                ProjectileRules.ArrowBlockRadius,
                CollisionLayer.Wall | CollisionLayer.Door);
            if (!wallHit.Hit || wallHit.Layer != CollisionLayer.Wall)
                return "arrow must hit north wall off-opening";

            MazeSolid door = FindDoor(solids, "START");
            CollisionHit doorHit = locked.Trace(
                door.X, door.Y - 2f,
                0f, 1f,
                8f,
                ProjectileRules.ArrowBlockRadius,
                CollisionLayer.Door);
            if (!doorHit.Hit || doorHit.Layer != CollisionLayer.Door)
                return "arrow/orb must hit locked door";

            // PR#19 retest (N1): a sideways shot from just inside a closed door must not graze the
            // door strip. Shaft radius clears it; the old 0.40 disc did not.
            float sideY = door.Y - door.Height * 0.5f - 0.2f;
            CollisionHit side = locked.Trace(door.X - door.Width * 0.5f - 0.5f, sideY, 1f, 0f,
                door.Width + 1f, ProjectileRules.ArrowBlockRadius, CollisionLayer.Wall | CollisionLayer.Door);
            if (side.Hit && side.Layer == CollisionLayer.Door)
                return "sideways arrow beside a closed door must not hit the door";
            if (!(ProjectileRules.ArrowBlockRadius < ProjectileRules.ArrowHitRadius))
                return "arrow block (shaft) radius must be below the mob hit radius";

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
            // Stop point == settlement point: the arrow stops where it enters the mob volume inflated by
            // ArrowHitRadius and damages that mob; there is no second radius test (was 0.80 stop vs 0.75 hit).
            float expect = 3f - CollisionRules.MobHalfX - ProjectileRules.ArrowHitRadius;
            float sweep = CollisionSpace.SweepDistance(start.Center.X, start.Center.Y, 1f, 0f,
                new CollisionAabb(start.Center.X + 3f, start.Center.Y, CollisionRules.MobHalfX, CollisionRules.MobHalfY)
                    .Inflated(ProjectileRules.ArrowHitRadius));
            if (Math.Abs(mobHit.Distance - expect) > 0.001f || Math.Abs(sweep - mobHit.Distance) > 0.001f)
                return "arrow mob contact distance must equal volume+hit radius (" + sweep.ToString("0.00") + ")";
            return null;
        }

        static string CheckJianHaiFiles()
        {
            string[] must =
            {
                JianHaiArtCatalog.PlayerIdle,
                JianHaiArtCatalog.EnemyE1Idle,
                JianHaiArtCatalog.BossIdle,
                // ArrowFlight / OrbFlight PNGs are not on main yet → generated placeholder.
                JianHaiArtCatalog.FxTipIdle,
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

        /// <summary>
        /// PR#19 retest (e43d220): atk/cast replays each attack cycle, player never under a room mask
        /// in the doorway, corpses leave collision and are removed after the death clip, patrol walk/idle
        /// uses movement intent with a hold. Runtime behaviour is covered by the play-mode probe; this
        /// pins the constants the fixes rely on.
        /// </summary>
        static string CheckRetestFixes()
        {
            int entity = JianHaiArtCatalog.SortingOrderForArtId(JianHaiArtCatalog.PlayerIdle);
            if (!(RogueShooter.Demo.Stage1MazeDemo.CoverSortingOrder > entity))
                return "room cover must sort above entities";
            if (!(RogueShooter.Demo.Stage1MazeDemo.PlayerDoorwayOrder > RogueShooter.Demo.Stage1MazeDemo.CoverSortingOrder))
                return "player doorway order must sort above room covers";
            if (MobFourStateAi.MoveHoldSeconds <= 0f || MobFourStateAi.MoveHoldSeconds > 0.5f)
                return "patrol move hold must be (0, 0.5]s";
            if (MobFourStateAi.CorpseLingerSeconds < 0f)
                return "corpse linger must be ≥ 0";
            foreach (string k in new[] { EnemyKindIds.Normal, EnemyKindIds.Dog, EnemyKindIds.CultMage, EnemyKindIds.Shield, EnemyKindIds.GrandMage })
            {
                if (ActionSpecP1.EnemyDeath(k).Duration <= 0.01f)
                    return "death clip duration missing for " + k;
                ActionClipDef atk = ActionSpecP1.EnemyAttack(k);
                if (atk.Duration <= 0.01f)
                    return "attack clip duration missing for " + k;
            }

            return null;
        }

        /// <summary>
        /// 走廊宽度_建议_v02 + PR#19: every door strip is exactly MazeRules.DoorWidth (corridor − 2 × stub),
        /// centred on MazeRules.DoorAxis, on whole cells, sealed by the wall segments on both sides; the room
        /// cover edge lies on the door strip centre plane and spans the opening; reveal (0.3) is past the
        /// strip and lock (2.0) is past strip + player box. Generator seeds 1–12 (the hand-painted layout is gone:
        /// Stage1 always runs Stage1MazeGen). Then <see cref="CheckGenRaster"/> on the same seeds.
        /// </summary>
        static string CheckDoorGeometry()
        {
            float dw = MazeRules.DoorWidth;
            if (Math.Abs(CollisionRules.DoorOpening - dw) > 0.001f
                || Math.Abs(MazeRules.DoorOpeningFor(MazeRules.CorridorWidth) - dw) > 0.001f
                || Math.Abs(MazeRules.CorridorWidth - dw - 2f * MazeRules.DoorStub) > 0.001f)
                return "door width must be corridor − 2 × stub (" + MazeRules.CorridorWidth + "/" + dw + ")";
            float half = CollisionRules.WallThickness * 0.5f;
            float reveal = RogueShooter.Demo.Stage1MazeDemo.RevealInset;
            float lockIn = RogueShooter.Demo.Stage1MazeDemo.LockTriggerInset;
            if (!(reveal > half))
                return "reveal inset must clear the door strip";
            if (!(lockIn > reveal) || !(lockIn >= half + CollisionRules.PlayerHalfY))
                return "lock inset must be past reveal and past strip + player box";

            var mazes = new List<Stage1Maze>();
            for (int s = 1; s <= 12; s++)
                mazes.Add(Stage1MazeGen.Generate(s));
            for (int m = 0; m < mazes.Count; m++)
            {
                Stage1Maze maze = mazes[m];
                List<MazeSolid> solids = MazeCollisionBuilder.Build(maze, MazeRules.CorridorWidth);
                for (int i = 0; i < solids.Count; i++)
                {
                    MazeSolid d = solids[i];
                    if (!d.Door)
                        continue;
                    string tag = maze.Signature + " " + d.Name;
                    MazeNode room = maze.Find(d.RoomId);
                    MazeNode other = maze.Find(d.OtherId);
                    if (room == null || other == null)
                        return "door rooms missing " + tag;
                    int side;
                    float along;
                    MazeCollisionBuilder.DoorOnWall(room, other, out side, out along);
                    bool horiz = side == MazeCollisionBuilder.SideN || side == MazeCollisionBuilder.SideS;
                    float span = horiz ? d.Width : d.Height;
                    float thick = horiz ? d.Height : d.Width;
                    float mid = horiz ? d.X : d.Y;
                    float plane = horiz ? d.Y : d.X;
                    if (Math.Abs(span - dw) > 0.001f || Math.Abs(thick - CollisionRules.WallThickness) > 0.001f)
                        return "lock strip must be " + dw + "u wide " + tag;
                    float roomMid = horiz ? room.Center.X : room.Center.Y;
                    if (Math.Abs(mid - MazeRules.DoorAxis(roomMid)) > 0.001f)
                        return "door centre must be DoorAxis " + tag;
                    float a0 = mid - span * 0.5f, a1 = mid + span * 0.5f;
                    if (Math.Abs(a0 - Math.Round(a0)) > 0.001f || Math.Abs(a1 - Math.Round(a1)) > 0.001f)
                        return "door edges must be whole cells " + tag;

                    float cx, cy, cw, ch;
                    RogueShooter.Demo.Stage1MazeDemo.CoverRect(room, out cx, out cy, out cw, out ch);
                    float edge = side == MazeCollisionBuilder.SideN ? cy + ch * 0.5f
                        : side == MazeCollisionBuilder.SideS ? cy - ch * 0.5f
                        : side == MazeCollisionBuilder.SideE ? cx + cw * 0.5f
                        : cx - cw * 0.5f;
                    float c0 = horiz ? cx - cw * 0.5f : cy - ch * 0.5f;
                    float c1 = horiz ? cx + cw * 0.5f : cy + ch * 0.5f;
                    if (Math.Abs(edge - plane) > 0.001f)
                        return "cover edge must sit on the door centre plane " + tag;
                    if (a0 < c0 - 0.001f || a1 > c1 + 0.001f)
                        return "cover edge must span the door opening " + tag;

                    bool sealLo = false, sealHi = false;
                    for (int j = 0; j < solids.Count; j++)
                    {
                        MazeSolid w = solids[j];
                        if (w.Door || w.RoomId != d.RoomId)
                            continue;
                        float wp = horiz ? w.Y : w.X;
                        float wt = horiz ? w.Height : w.Width;
                        if (Math.Abs(wp - plane) > 0.001f || wt > 1f)
                            continue;
                        float w0 = (horiz ? w.X - w.Width * 0.5f : w.Y - w.Height * 0.5f);
                        float w1 = (horiz ? w.X + w.Width * 0.5f : w.Y + w.Height * 0.5f);
                        if (Math.Abs(w1 - a0) < 0.001f) sealLo = true;
                        if (Math.Abs(w0 - a1) < 0.001f) sealHi = true;
                    }

                    if (!sealLo || !sealHi)
                        return "lock strip must meet the wall on both sides " + tag;
                }
            }

            return CheckGenRaster(mazes);
        }

        // ---- measured by CheckGenRaster (report) ----
        public static int GenSeedsChecked;
        public static int GenMaxWallCells;
        public static int GenMaxFullFill;
        public static int GenMaxRasterW;
        public static int GenMaxRasterH;
        public static int GenMaxWallRects;
        public static int GenCorridorProbes;

        /// <summary>
        /// Runtime geometry = Stage1MazeRaster of the generator: rooms 52×40 on whole cells; every door is a 3-cell
        /// door line on DoorAxis touching the room with wall stubs on both sides and the lock strip on its room
        /// edge; walls are exactly the 8-neighbour ring (no gap, nothing else) and the merged wall rects cover
        /// them; corridor walls are CollisionWorld Wall solids — a mob box cannot step into them and an arrow /
        /// orb trace across the corridor stops on them (fix: corridor walls were missing from the mob/arrow layer).
        /// </summary>
        public static string CheckGenRaster(List<Stage1Maze> mazes)
        {
            GenSeedsChecked = 0;
            GenMaxWallCells = GenMaxFullFill = GenMaxRasterW = GenMaxRasterH = GenMaxWallRects = GenCorridorProbes = 0;
            for (int m = 0; m < mazes.Count; m++)
            {
                Stage1Maze maze = mazes[m];
                Stage1MazeRaster r = Stage1MazeRaster.Build(maze);
                string tag = "seed " + maze.Seed + " ";
                string err = r.CheckRing();
                if (err != null)
                    return tag + err;
                for (int i = 0; i < maze.Nodes.Length; i++)
                {
                    MazeNode n = maze.Nodes[i];
                    if (Math.Abs(n.Width - MazeRules.CombatWidth) > 0.001f || Math.Abs(n.Height - MazeRules.CombatHeight) > 0.001f)
                        return tag + "room must be " + MazeRules.CombatWidth + "x" + MazeRules.CombatHeight + " " + n.Id;
                    int x0 = (int)Math.Round(n.Center.X - n.Width * 0.5f), y0 = (int)Math.Round(n.Center.Y - n.Height * 0.5f);
                    if (Math.Abs(n.Center.X - n.Width * 0.5f - x0) > 0.001f || Math.Abs(n.Center.Y - n.Height * 0.5f - y0) > 0.001f)
                        return tag + "room edges must be whole cells " + n.Id;
                    if (r.Get(x0, y0) != MazeCell.Room || r.Get(x0 + (int)n.Width - 1, y0 + (int)n.Height - 1) != MazeCell.Room)
                        return tag + "room floor missing " + n.Id;
                }

                List<MazeSolid> solids = MazeCollisionBuilder.Build(maze, MazeRules.CorridorWidth);
                int doors = 0;
                for (int i = 0; i < solids.Count; i++)
                {
                    MazeSolid d = solids[i];
                    if (!d.Door)
                        continue;
                    doors++;
                    MazeNode room = maze.Find(d.RoomId), other = maze.Find(d.OtherId);
                    int side;
                    float along;
                    MazeCollisionBuilder.DoorOnWall(room, other, out side, out along);
                    bool horiz = side == MazeCollisionBuilder.SideN || side == MazeCollisionBuilder.SideS;
                    int c = Stage1MazeRaster.CellOf(along);
                    // Door line = the gap cells just outside the room edge.
                    int line = side == MazeCollisionBuilder.SideN ? (int)Math.Round(room.Center.Y + room.Height * 0.5f)
                        : side == MazeCollisionBuilder.SideS ? (int)Math.Round(room.Center.Y - room.Height * 0.5f) - 1
                        : side == MazeCollisionBuilder.SideE ? (int)Math.Round(room.Center.X + room.Width * 0.5f)
                        : (int)Math.Round(room.Center.X - room.Width * 0.5f) - 1;
                    int half = (int)Math.Round(MazeRules.DoorWidth) / 2;
                    for (int k = -half - 1; k <= half + 1; k++)
                    {
                        int x = horiz ? c + k : line, y = horiz ? line : c + k;
                        bool inDoor = Math.Abs(k) <= half;
                        if (inDoor && !r.IsMouth(x, y))
                            return tag + "door line must be " + MazeRules.DoorWidth + " floor cells on DoorAxis " + d.Name;
                        if (!inDoor && !r.IsWall(x, y))
                            return tag + "door stub must be wall " + d.Name + " at " + x + "," + y;
                    }
                }

                if (r.MouthCount != doors * (int)Math.Round(MazeRules.DoorWidth))
                    return tag + "mouth cells " + r.MouthCount + " != doors " + doors + " × " + MazeRules.DoorWidth;

                err = CheckCorridorWallsBlock(maze, r);
                if (err != null)
                    return tag + err;
                GenSeedsChecked++;
                GenMaxWallCells = Math.Max(GenMaxWallCells, r.WallCount);
                GenMaxFullFill = Math.Max(GenMaxFullFill, r.FullFillWallCount);
                GenMaxRasterW = Math.Max(GenMaxRasterW, r.W);
                GenMaxRasterH = Math.Max(GenMaxRasterH, r.H);
                GenMaxWallRects = Math.Max(GenMaxWallRects, r.WallRects.Count);
            }

            return null;
        }

        /// <summary>
        /// Corridor wall rects in a CollisionSpace (what Stage1GenWorld registers in CollisionWorld): from each
        /// corridor segment midpoint a mob box pushed sideways stops at the corridor edge, and arrow / orb traces
        /// sideways hit a Wall within the half corridor.
        /// </summary>
        static string CheckCorridorWallsBlock(Stage1Maze maze, Stage1MazeRaster r)
        {
            var space = new CollisionSpace();
            for (int i = 0; i < r.WallRects.Count; i++)
            {
                CellRect rc = r.WallRects[i];
                space.Add(new CollisionAabb(rc.CenterX, rc.CenterY, rc.W * 0.5f, rc.H * 0.5f), CollisionLayer.Wall, true, "GenWall_" + i);
            }

            float halfC = MazeRules.CorridorWidth * 0.5f;
            for (int e = 0; e < maze.Edges.Length; e++)
            {
                MazeEdge edge = maze.Edges[e];
                MazeVec2 a = edge.Points[0], b = edge.Points[edge.Points.Length - 1];
                bool horiz = Math.Abs(b.X - a.X) >= Math.Abs(b.Y - a.Y);
                float mx = (a.X + b.X) * 0.5f, my = (a.Y + b.Y) * 0.5f;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float dx = horiz ? 0f : sgn, dy = horiz ? sgn : 0f;
                    float x = mx, y = my;
                    space.TryMove(ref x, ref y, CollisionRules.MobHalfX, CollisionRules.MobHalfY, dx * 6f, dy * 6f, CollisionRules.SolidMask);
                    float off = horiz ? Math.Abs(y - my) : Math.Abs(x - mx);
                    if (off > halfC - CollisionRules.MobHalfX + 0.01f)
                        return "mob walked into corridor wall " + edge.FromId + "-" + edge.ToId + " off=" + off.ToString("0.00");
                    CollisionHit arrow = space.Trace(mx, my, dx, dy, 10f, ProjectileRules.ArrowBlockRadius, CollisionLayer.Wall | CollisionLayer.Door);
                    if (!arrow.Hit || arrow.Layer != CollisionLayer.Wall || arrow.Distance > halfC + 0.01f)
                        return "arrow must stop on corridor wall " + edge.FromId + "-" + edge.ToId;
                    CollisionHit orb = space.Trace(mx, my, dx, dy, 10f, ProjectileRules.OrbHitRadius, CollisionLayer.Wall | CollisionLayer.Door);
                    if (!orb.Hit || orb.Layer != CollisionLayer.Wall || orb.Distance > halfC + 0.01f)
                        return "orb must stop on corridor wall " + edge.FromId + "-" + edge.ToId;
                    GenCorridorProbes++;
                }
            }

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

        static bool _warnedProjectileArt;

        /// <summary>Missing jh_proj_arrow_fly / jh_proj_orb_mage_fly → one Debug.LogWarning per session.</summary>
        public static string MissingProjectileArt()
        {
            string miss = "";
            if (!JianHaiSprites.HasSourceFile(JianHaiArtCatalog.ArrowFlight))
                miss += JianHaiArtCatalog.ArrowFlight + " ";
            if (!JianHaiSprites.HasSourceFile(JianHaiArtCatalog.OrbFlight))
                miss += JianHaiArtCatalog.OrbFlight + " ";
            return miss.Trim();
        }

        static void WarnMissingProjectileArt()
        {
            if (_warnedProjectileArt)
                return;
            string miss = MissingProjectileArt();
            if (miss.Length == 0)
                return;
            _warnedProjectileArt = true;
            UnityEngine.Debug.LogWarning("[Stage1Playable] placeholder projectile art (PNG not on main): " + miss);
        }
    }
}
