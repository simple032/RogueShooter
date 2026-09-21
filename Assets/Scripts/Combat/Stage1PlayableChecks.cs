using System;
using System.Collections.Generic;
using System.Text;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Maze;
using RogueShooter.Player;

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
            return null;
        }

        public static string FormatPass()
        {
            var sb = new StringBuilder();
            sb.Append("playable=collision+arrow+orb+dodge-iframes ");
            sb.Append("art=JianHai-PNG-runtime ");
            sb.Append("layers=Player/Mob/Wall/Door/Projectile ");
            sb.Append("door=locked-blocks/open-pass ");
            sb.Append("arrow=jh_fx_charge_arrow_tip speed=").Append(ProjectileRules.ArrowSpeed.ToString("0"));
            sb.Append(" orb=jh_fx_mage_orb ");
            sb.Append("dodge dur=").Append(DodgeRules.DurationSeconds.ToString("0.00"));
            sb.Append("s iframe=").Append(DodgeRules.IFrameSeconds.ToString("0.00"));
            sb.Append("s cd=").Append(DodgeRules.CooldownSeconds.ToString("0.00"));
            sb.Append("s dist=").Append(DodgeRules.Distance.ToString("0.00"));
            sb.Append(" interact=chest+altar");
            return sb.ToString();
        }

        static string CheckDodgeTable()
        {
            if (DodgeRules.DurationSeconds < 0.30f || DodgeRules.DurationSeconds > 0.40f)
                return "dodge duration stub must stay 0.3–0.4s";
            if (Math.Abs(DodgeRules.IFrameSeconds - 0.20f) > 0.0001f)
                return "dodge i-frames stub 0.20s";
            if (DodgeRules.CooldownSeconds < 0.80f || DodgeRules.CooldownSeconds > 1.00f)
                return "dodge cooldown stub must stay 0.8–1.0s";
            if (Math.Abs(DodgeRules.MoveSpeedRef - MazeRules.PlayMoveSpeed) > 0.0001f)
                return "dodge distance must derive from play move 6";
            if (Math.Abs(DodgeRules.Distance - DodgeRules.MoveSpeedRef * DodgeRules.DurationSeconds) > 0.0001f)
                return "dodge distance = speed × duration";
            if (DodgeRules.IFrameActive(0.10f, 0f) == false)
                return "i-frame on at t=0.10";
            if (DodgeRules.IFrameActive(0.20f, 0f))
                return "i-frame off at t=0.20";
            if (DodgeRules.IFrameActive(0.21f, 0f))
                return "i-frame off after 0.20s";
            if (!DodgeRules.RollActive(0.34f, 0f) || DodgeRules.RollActive(0.36f, 0f))
                return "roll duration window";
            if (!DodgeRules.OnCooldown(0.89f, 0f) || DodgeRules.OnCooldown(0.91f, 0f))
                return "dodge cooldown window";
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
            if (JianHaiArtCatalog.ArrowFlight != JianHaiArtCatalog.FxTipWarm)
                return "arrow visual uses charge tip art";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.FxMageOrb) != "FX")
                return "orb art folder";
            if (!JianHaiSprites.HasSourceFile(JianHaiArtCatalog.FxMageOrb))
                return "mage orb PNG missing under Art/JianHai/FX";
            if (JianHaiArtCatalog.AssetPath(JianHaiArtCatalog.FxMageOrb)
                != "Assets/Art/JianHai/FX/jh_fx_mage_orb.png")
                return "mage orb asset path";

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
                JianHaiArtCatalog.FxMageOrb,
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

            if (JianHaiSprites.HasSourceFile(EntityAnimCatalog.PlayerWalk))
            { /* drop-in walk frames welcome */ }
            else if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Walk) != JianHaiArtCatalog.PlayerIdle)
                return "missing walk must fall back to idle";
            return null;
        }

        static string CheckArtHooks()
        {
            if (EntityAnimCatalog.PlayerIdle != JianHaiArtCatalog.PlayerIdle)
                return "player idle hook";
            if (EntityAnimCatalog.EnemyIdle != JianHaiArtCatalog.EnemyE1Idle)
                return "enemy idle hook";
            if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Walk) != JianHaiArtCatalog.PlayerIdle)
                return "missing walk must fall back to idle";
            if (EntityAnimCatalog.ResolvePlayer(EntityAnimState.Dodge) != JianHaiArtCatalog.PlayerIdle)
                return "missing dodge clip must fall back to idle";
            if (EntityAnimCatalog.ResolveEnemy(EntityAnimState.Walk) != JianHaiArtCatalog.EnemyE1Idle)
                return "missing enemy walk must fall back to idle";
            string gaps = EntityAnimCatalog.GapNote();
            if (string.IsNullOrEmpty(gaps) || gaps.IndexOf("walk", StringComparison.Ordinal) < 0)
                return "art gap note must list missing walk";
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
