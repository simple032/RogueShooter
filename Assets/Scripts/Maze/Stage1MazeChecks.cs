using System;
using System.Text;
using RogueShooter.Ai;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Maze
{
    /// <summary>Returns null on pass. Stage-1 maze skeleton only.</summary>
    public static class Stage1MazeChecks
    {
        public static string Run()
        {
            string err = CheckFeelLocks();
            if (err != null) return err;
            err = CheckQuotaAndReach();
            if (err != null) return err;
            err = CheckSeedStable();
            if (err != null) return err;
            err = CheckCombatLoop();
            if (err != null) return err;
            err = CheckPoolRouting();
            if (err != null) return err;
            return null;
        }

        public static string FormatPass()
        {
            var sb = new StringBuilder();
            sb.Append("ACCEPTANCE PASS stage1-maze ");
            sb.Append("quota=Chest×1+Altar×1+Normal×2+CONN-stub ");
            sb.Append("seeded=1 corridors=noSpawn ");
            sb.Append("combat=enter→lock→PortalFx→1.0s→spawn→clear→open ");
            sb.Append("everyWave=PortalFx-visible ");
            sb.Append("chestAltar=2waves+[PortalFx] ");
            sb.Append("normal=1wave+[PortalFx] ");
            sb.Append("pool=S1 Normal/Chest→N Altar/LargeChest→E ");
            sb.Append("ortho=6 move=6 ");
            sb.Append("rooms=20x16 hub=13x11 pitch=52/46 ");
            sb.Append("knockback=draftMid dog4.32/mage2.16/normal2.7/grand1.98/shield2.7|1.35/boss0.30 ");
            sb.Append("return=0.6s formula=move×0.6(non-boss) ");
            sb.Append("fullCharge≥0.70s weak=0 elite=same ");
            sb.Append("pacing=reachability-first no-clock-lock ");
            sb.Append("S2S3=not-built");
            return sb.ToString();
        }

        static string CheckFeelLocks()
        {
            if (Math.Abs(MazeRules.PlayOrtho - 6f) > 0.001f
                || Math.Abs(CameraViewService.PlayOrthoSize - 6f) > 0.001f
                || Math.Abs(EnemyCombatRules.PlayOrthoSize - 6f) > 0.001f)
                return "play ortho 6";
            if (Math.Abs(MazeRules.PlayMoveSpeed - 6f) > 0.001f
                || Math.Abs(EnemyCombatRules.WalkPlayerStub - 6f) > 0.001f
                || Math.Abs(EnemyPoolDraft.DraftPlayerMove - 6f) > 0.001f)
                return "play moveSpeed 6";
            if (Math.Abs(ChargeShotRules.GreenEnterSeconds - 0.68f) > 0.001f
                || Math.Abs(ChargeShotRules.GreenExitSeconds - 0.72f) > 0.001f)
                return "weak window must stay 0.68–0.72";
            if (Math.Abs(MazeRules.PortalHoldSeconds - 1.0f) > 0.001f)
                return "portal hold 1.0s";
            if (!MazeRules.UsesPortalFx(MazeNodeKind.Normal)
                || !MazeRules.UsesPortalFx(MazeNodeKind.Chest)
                || !MazeRules.UsesPortalFx(MazeNodeKind.Altar))
                return "every combat wave uses PortalFx";
            if (MazeRules.UsesPortalFx(MazeNodeKind.Start)
                || MazeRules.UsesPortalFx(MazeNodeKind.Connector))
                return "START/CONN must not portal";
            if (MazeRules.CombatWidth < 19.5f || MazeRules.CombatHeight < 15.5f)
                return "S1 combat rooms must be one step larger than 16x12";
            if (MazeRules.PitchX <= MazeRules.CombatWidth || MazeRules.PitchY <= MazeRules.CombatHeight)
                return "pitch must exceed room size";
            string kb = CheckKnockbackDraft();
            if (kb != null)
                return kb;
            return null;
        }

        static string CheckKnockbackDraft()
        {
            FullChargeKnockback.EnsureLoaded();
            if (FullChargeKnockback.Applies(ChargeShotKind.Weak, 0.30f)
                || FullChargeKnockback.Applies(ChargeShotKind.Full, 0.50f)
                || FullChargeKnockback.Applies(ChargeShotKind.None, 0.80f))
                return "weak/partial charge must not knockback";
            if (!FullChargeKnockback.Applies(ChargeShotKind.Full, 0.70f)
                || !FullChargeKnockback.Applies(ChargeShotKind.Full, 0.80f)
                || !FullChargeKnockback.Applies(ChargeShotKind.Crit, 0.70f))
                return "full charge held≥0.70 must knockback (not crit-only)";
            if (FullChargeKnockback.Applies(ChargeShotKind.Crit, 0.68f))
                return "crit below ring-full 0.70 must not knockback";
            if (!NearKb(FullChargeKnockback.MidDistance(EnemyKindIds.Dog, false), 4.32f))
                return "kb dog mid 4.32";
            if (!NearKb(FullChargeKnockback.MidDistance(EnemyKindIds.CultMage, false), 2.16f))
                return "kb mage mid 2.16";
            if (!NearKb(FullChargeKnockback.MidDistance(EnemyKindIds.Normal, false), 2.7f))
                return "kb normal mid 2.7";
            if (!NearKb(FullChargeKnockback.MidDistance(EnemyKindIds.GrandMage, false), 1.98f))
                return "kb grand mid 1.98";
            if (!NearKb(FullChargeKnockback.MidDistance(EnemyKindIds.Shield, false), 2.7f))
                return "kb shield open 2.7";
            if (!NearKb(FullChargeKnockback.MidDistance(EnemyKindIds.Shield, true), 1.35f))
                return "kb shield raised 1.35";
            if (!NearKb(FullChargeKnockback.MidDistance(null, false, true), 0.30f))
                return "kb boss 0.30";
            if (!NearKb(FullChargeKnockback.ReturnSeconds, 0.6f))
                return "kb t_return 0.6s";
            if (!NearKb(FullChargeKnockback.SlideSeconds(4.32f, false), 0.6f))
                return "non-boss slide uses t_return 0.6s";
            float bossT = FullChargeKnockback.SlideSeconds(0.30f, true);
            if (NearKb(bossT, 0.6f))
                return "boss must not use 0.6s return formula";
            float e = FullChargeKnockback.MidDistanceEliteSame(EnemyKindIds.Dog, false, true);
            if (!NearKb(e, FullChargeKnockback.MidDistance(EnemyKindIds.Dog, false)))
                return "elite same species knockback";
            if (FullChargeKnockback.Source == null
                || FullChargeKnockback.Source.IndexOf("DRAFT_NOT_LOCKED", StringComparison.Ordinal) < 0)
                return "knockback table must stay DRAFT_NOT_LOCKED";
            return null;
        }

        static bool NearKb(float a, float b)
        {
            return Math.Abs(a - b) < 0.001f;
        }

        static string CheckQuotaAndReach()
        {
            int[] seeds = { 1, 2, 17, 42, 99, 2026 };
            for (int s = 0; s < seeds.Length; s++)
            {
                Stage1Maze maze = Stage1MazeGen.Generate(seeds[s]);
                string q = CheckOneQuota(maze);
                if (q != null)
                    return "seed " + seeds[s] + " " + q;
                MazePacing pace = Stage1MazeGen.MeasurePacing(maze, MazeRules.PlayMoveSpeed);
                if (!pace.AllReachable)
                    return "seed " + seeds[s] + " not all reachable";
                if (!pace.ConnectorReachable)
                    return "seed " + seeds[s] + " CONN not reachable";
                if (pace.ShortestCombatRooms < 2)
                    return "seed " + seeds[s] + " shortest must visit ≥2 combat rooms";
                if (maze.Edges == null || maze.Edges.Length < 4)
                    return "seed " + seeds[s] + " need corridors";
                for (int i = 0; i < maze.Nodes.Length; i++)
                {
                    MazeNode n = maze.Nodes[i];
                    if (n.Kind == MazeNodeKind.Start || n.Kind == MazeNodeKind.Connector)
                    {
                        if (n.SpawnsEnemies)
                            return n.Id + " must not spawn";
                    }
                }
            }

            return null;
        }

        static string CheckOneQuota(Stage1Maze maze)
        {
            if (maze.CountKind(MazeNodeKind.Start) != 1)
                return "need START×1";
            if (maze.CountKind(MazeNodeKind.Connector) != 1)
                return "need CONN stub×1";
            if (maze.CountKind(MazeNodeKind.Normal) != 2)
                return "need Normal×2 got " + maze.CountKind(MazeNodeKind.Normal);
            if (maze.CountKind(MazeNodeKind.Altar) != 1)
                return "need Altar×1";
            int chests = maze.CountKind(MazeNodeKind.Chest) + maze.CountKind(MazeNodeKind.LargeChest);
            if (chests != 1)
                return "need Chest×1 (or LargeChest upgrade) got " + chests;
            if (maze.CombatRoomCount() != 4)
                return "S1 combat rooms must be 4";
            int waves = maze.FullClearWaveCount();
            if (waves != 6)
                return "S1 full-clear waves must be 2+2+1+1=6 got " + waves;
            return null;
        }

        static string CheckSeedStable()
        {
            Stage1Maze a = Stage1MazeGen.Generate(42);
            Stage1Maze b = Stage1MazeGen.Generate(42);
            if (a.Signature != b.Signature)
                return "seed 42 not reproducible";
            if (Stage1MazeGen.FormatGraph(a) != Stage1MazeGen.FormatGraph(b))
                return "seed 42 graph line not stable";
            Stage1Maze c = Stage1MazeGen.Generate(7);
            if (c.Signature == a.Signature)
                return "seed 7 must differ from 42";
            int drawA = Stage1MazeGen.DrawSeed(42, "ALTAR", 1);
            int drawB = Stage1MazeGen.DrawSeed(42, "ALTAR", 1);
            if (drawA != drawB)
                return "draw seed not stable";
            if (Stage1MazeGen.DrawSeed(42, "ALTAR", 2) == drawA)
                return "wave 2 draw seed must differ";
            return null;
        }

        static string CheckCombatLoop()
        {
            Stage1Maze maze = Stage1MazeGen.Generate(42);
            MazeNode corridorHost = maze.Find("START");
            var startSession = new CombatRoomSession(corridorHost);
            if (startSession.Enter().Length != 0)
                return "START/non-combat must not lock or spawn";

            MazeNode altar = maze.Find("ALTAR");
            if (altar == null)
                return "missing ALTAR";
            var altarS = new CombatRoomSession(altar);
            CombatStep[] dry = altarS.DryRun();
            if (!HasKind(dry, "lock") || !HasKind(dry, "open") || !HasKind(dry, "clear"))
                return "altar loop missing lock/clear/open";
            int portals = CountKind(dry, "portal");
            int spawns = CountKind(dry, "spawn");
            if (portals != 2 || spawns != 2)
                return "altar must portal+spawn twice got portal=" + portals + " spawn=" + spawns;
            if (!PortalThenSpawn(dry))
                return "altar cadence PortalFx then spawn";
            if (!StartsWith(dry[0].Line, "[S1Maze] lock "))
                return "first step must lock " + dry[0].Line;
            if (!StartsWith(LastOfKind(dry, "open").Line, "[S1Maze] open "))
                return "last open line";
            if (altarS.Phase != CombatRoomPhase.Cleared || altarS.DoorsLocked)
                return "altar should be open after dry-run";

            MazeNode chest = maze.Find("CHEST");
            if (chest == null)
                chest = maze.Find("LARGE");
            if (chest == null)
                return "missing chest room";
            var chestS = new CombatRoomSession(chest);
            CombatStep[] cd = chestS.DryRun();
            if (CountKind(cd, "portal") != 2 || CountKind(cd, "spawn") != 2)
                return "chest/large must two waves + portal";
            if (!PortalThenSpawn(cd))
                return "chest cadence PortalFx then spawn";

            MazeNode n1 = maze.Find("N1");
            if (n1 == null)
                return "missing N1";
            var nS = new CombatRoomSession(n1);
            CombatStep[] nd = nS.DryRun();
            if (CountKind(nd, "portal") != 1)
                return "normal room one PortalFx got " + CountKind(nd, "portal");
            if (CountKind(nd, "spawn") != 1 || CountKind(nd, "clear") != 1)
                return "normal room one wave";
            if (!HasKind(nd, "lock") || !HasKind(nd, "open"))
                return "normal room lock/open";
            if (!PortalThenSpawn(nd))
                return "normal cadence PortalFx then spawn";

            string show = PortalFxHook.FormatShow("ALTAR", 1);
            if (show != "[PortalFx] room=ALTAR wave=1 show")
                return "portal show contract " + show;
            string spawn = PortalFxHook.FormatSpawn("N1", 1);
            if (spawn != "[PortalFx] room=N1 wave=1 spawn after 1.0s")
                return "portal spawn contract " + spawn;
            return null;
        }

        static bool PortalThenSpawn(CombatStep[] steps)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].Kind != "portal")
                    continue;
                if (i + 1 >= steps.Length || steps[i + 1].Kind != "spawn")
                    return false;
                if (!StartsWith(steps[i].Line, "[PortalFx] ") || steps[i].Line.IndexOf(" show", StringComparison.Ordinal) < 0)
                    return false;
                if (!StartsWith(steps[i + 1].Line, "[PortalFx] ")
                    || steps[i + 1].Line.IndexOf(" spawn after 1.0s", StringComparison.Ordinal) < 0)
                    return false;
            }

            return HasKind(steps, "portal");
        }

        static string CheckPoolRouting()
        {
            if (!EnemyPoolDraft.EnsureLoaded())
                return "draft CSV load: " + EnemyPoolDraft.LoadError;

            Stage1Maze maze = Stage1MazeGen.Generate(42);
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                if (!n.SpawnsEnemies)
                    continue;
                for (int w = 1; w <= n.WaveCount; w++)
                {
                    DrawnComposition d = StageEnemyPool.DrawComposition(
                        StageId.S1, n.PoolRoom, Stage1MazeGen.DrawRng(maze.Seed, n.Id, w));
                    if (string.IsNullOrEmpty(d.CompId))
                        return n.Id + " empty draw";
                    string log = d.LogLine();
                    if (log.IndexOf("[StagePool] S1 room=", StringComparison.Ordinal) != 0)
                        return "log contract " + log;
                    bool wantE = CombatRoomKindUtil.PrefersEnhanced(n.PoolRoom);
                    if (wantE && d.Tier != "enhanced")
                        return n.Id + " must enhanced got " + d.Tier + " " + d.CompId;
                    if (!wantE && d.Tier != "normal")
                        return n.Id + " must normal got " + d.Tier + " " + d.CompId;
                    if (n.Kind == MazeNodeKind.Chest && d.Tier != "normal")
                        return "ordinary Chest must draw normal";
                    if (d.ExtraAdded != 0)
                        return n.Id + " extra must stay 0";
                    if (d.UnitCount < EnemyPoolDraft.WaveCountMin || d.UnitCount > EnemyPoolDraft.WaveCountMax + 1)
                        return n.Id + " wave size " + d.UnitCount;
                }
            }

            DrawnComposition chest = StageEnemyPool.DrawComposition(
                StageId.S1, CombatRoomKind.Chest, new Random(3));
            if (chest.Tier != "normal")
                return "S1 ordinary Chest → normal";
            DrawnComposition altar = StageEnemyPool.DrawComposition(
                StageId.S1, CombatRoomKind.Altar, new Random(3));
            if (altar.Tier != "enhanced")
                return "S1 Altar → enhanced";
            return null;
        }

        static bool HasKind(CombatStep[] steps, string kind)
        {
            return CountKind(steps, kind) > 0;
        }

        static int CountKind(CombatStep[] steps, string kind)
        {
            int n = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].Kind == kind)
                    n++;
            }

            return n;
        }

        static CombatStep LastOfKind(CombatStep[] steps, string kind)
        {
            for (int i = steps.Length - 1; i >= 0; i--)
            {
                if (steps[i].Kind == kind)
                    return steps[i];
            }

            return default(CombatStep);
        }

        static bool StartsWith(string s, string prefix)
        {
            return !string.IsNullOrEmpty(s)
                && s.IndexOf(prefix, StringComparison.Ordinal) == 0;
        }
    }
}
