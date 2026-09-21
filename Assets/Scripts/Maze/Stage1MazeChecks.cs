using System;
using System.Text;
using RogueShooter.Ai;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;
using RogueShooter.Build;

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
            sb.Append("rooms=36x28 hub=26x22 pitch=66/58 ");
            sb.Append("corridorSeg≤5s folds=stem12/IZ5/C4 ");
            sb.Append("walkAltar=60-120s walkAll=240±30s ");
            sb.Append("knockback=draftMid dog4.32/mage2.16/normal2.7/grand1.98/shield2.7|root0.5/boss0.30 ");
            sb.Append("ws=×1.5+stagger0.5 shieldShatter=1.0s ");
            sb.Append("return=0.6s formula=move×0.6(non-boss) ");
            sb.Append("fullCharge≥0.70s weak=0 elite=same ");
            sb.Append("zhenshi=R15_C+20%/R15_R+40% mul-then-ws ");
            sb.Append("pacing=walkOnly-clocks combatEst-not-locked ");
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
            if (MazeRules.CombatWidth < 35.5f || MazeRules.CombatHeight < 27.5f
                || MazeRules.CombatWidth > 36.51f || MazeRules.CombatHeight > 28.51f)
                return "S1 combat rooms must be 36x28";
            if (MazeRules.PitchX <= MazeRules.CombatWidth || MazeRules.PitchY <= MazeRules.CombatHeight)
                return "pitch must exceed room size";
            if (MazeRules.PitchX > 70.01f || MazeRules.PitchY > 62.01f)
                return "pitch 130/115 void; max ~70/62 for ≤5s corridors";
            if (Math.Abs(MazeRules.PitchX - 66f) > 1.01f || Math.Abs(MazeRules.PitchY - 58f) > 1.01f)
                return "pitch ~66/58 v2b";
            if (MazeRules.PitchX - MazeRules.CombatWidth > 30.51f
                || MazeRules.PitchY - MazeRules.CombatHeight > 30.51f)
                return "net gap Pitch-room must be ≤30u";
            if (Math.Abs(MazeRules.HubWidth - 26f) > 0.51f || Math.Abs(MazeRules.HubHeight - 22f) > 0.51f)
                return "hub ~26x22";
            if (MazeRules.CorridorSegMax > 30.01f
                || MazeRules.CorridorSegMaxSeconds > 5.01f)
                return "corridor segment max 30u / 5s";
            if (MazeRules.FoldStem < 12 || MazeRules.FoldBranchIZ < 4 || MazeRules.FoldBranchC < 3)
                return "I/Z stem≥12 branch≥4; C branch≥3 folds";
            if (Math.Abs(MazeRules.PlayMoveSpeed - 6f) > 0.001f)
                return "walk-only pacing keeps moveSpeed 6";
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
                || !FullChargeKnockback.Applies(ChargeShotKind.Crit, 0.70f)
                || !FullChargeKnockback.Applies(ChargeShotKind.Crit, 0.68f))
                return "full charge held≥0.70 or weak-spot window must CC";
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
            if (!NearKb(FullChargeKnockback.MidDistance(EnemyKindIds.Shield, true), 0f))
                return "kb shield raised body 0";
            if (!NearKb(FullChargeKnockback.MidDistance(null, false, true), 0.30f))
                return "kb boss 0.30";
            if (!NearKb(FullChargeKnockback.WeakSpotMul, 1.5f))
                return "kb weak-spot mul 1.5";
            if (!NearKb(FullChargeKnockback.HitDistance(EnemyKindIds.Dog, false, false, true), 6.48f))
                return "kb dog weak-spot 6.48";
            if (!NearKb(FullChargeKnockback.HitDistance(EnemyKindIds.Normal, false, false, true), 4.05f))
                return "kb normal weak-spot 4.05";
            if (!NearKb(FullChargeKnockback.HitDistance(EnemyKindIds.Shield, true, false, true), 4.05f))
                return "kb shield raised weak-spot uses unshielded×1.5";
            if (!NearKb(FullChargeKnockback.HitDistance(null, false, true, true), 0.45f))
                return "kb boss weak-spot 0.45";
            if (!FullChargeKnockback.RootsOnBodyHit(EnemyKindIds.Shield, true, false))
                return "shield raised body must root";
            if (FullChargeKnockback.RootsOnBodyHit(EnemyKindIds.Shield, true, true)
                || FullChargeKnockback.RootsOnBodyHit(EnemyKindIds.Shield, false, false)
                || FullChargeKnockback.RootsOnBodyHit(EnemyKindIds.Dog, true, false))
                return "root only shield-raised non-weak-spot";
            if (!NearKb(FullChargeKnockback.ShieldRaisedRootSeconds, 0.5f))
                return "shield raised root 0.5s";
            if (Math.Abs(EnemyCombatRules.ShieldWeakSpotStaggerSeconds - 1.00f) > 0.001f)
                return "shield shatter stagger 1.0s";
            if (Math.Abs(ChargeShotRules.WeakSpotStaggerSeconds - 0.50f) > 0.001f)
                return "normal weak-spot stagger 0.5s";
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
            float eWs = FullChargeKnockback.HitDistance(EnemyKindIds.Dog, false, false, true);
            if (!NearKb(eWs, 6.48f))
                return "elite same species weak-spot knockback";
            string zh = CheckZhenShiDraft();
            if (zh != null)
                return zh;
            if (FullChargeKnockback.Source == null
                || FullChargeKnockback.Source.IndexOf("DRAFT_NOT_LOCKED", StringComparison.Ordinal) < 0)
                return "knockback table must stay DRAFT_NOT_LOCKED";
            return null;
        }

        static string CheckZhenShiDraft()
        {
            KnockbackRewardDraft.EnsureLoaded();
            if (KnockbackRewardDraft.CatalogHasHighTier())
                return "震矢 must have no high tier";
            if (!RewardCatalog.TryGet(KnockbackRewardDraft.IdLow, out RewardRow c)
                || !NearKb(c.Value, 0.20f) || c.BuildEquiv != 1 || c.Tier != RewardTier.Low)
                return "R15_C catalog";
            if (!RewardCatalog.TryGet(KnockbackRewardDraft.IdMid, out RewardRow r)
                || !NearKb(r.Value, 0.40f) || r.BuildEquiv != 2 || r.Tier != RewardTier.Mid)
                return "R15_R catalog";
            var none = new string[0];
            if (!NearKb(KnockbackRewardDraft.DistPctProduct(none), 1f))
                return "震矢 empty product 1";
            var low = new[] { KnockbackRewardDraft.IdLow };
            var mid = new[] { KnockbackRewardDraft.IdMid };
            var both = new[] { KnockbackRewardDraft.IdLow, KnockbackRewardDraft.IdMid };
            if (!NearKb(KnockbackRewardDraft.DistPctProduct(low), 1.20f))
                return "R15_C product 1.20";
            if (!NearKb(KnockbackRewardDraft.DistPctProduct(mid), 1.40f))
                return "R15_R product 1.40";
            if (!NearKb(KnockbackRewardDraft.DistPctProduct(both), 1.20f * 1.40f))
                return "震矢 stack mul 1.20*1.40";
            float dogC = FullChargeKnockback.HitDistance(
                EnemyKindIds.Dog, false, false, false, KnockbackRewardDraft.DistPctProduct(low));
            if (!NearKb(dogC, 4.32f * 1.20f))
                return "dog full +R15_C";
            float dogR = FullChargeKnockback.HitDistance(
                EnemyKindIds.Dog, false, false, false, KnockbackRewardDraft.DistPctProduct(mid));
            if (!NearKb(dogR, 4.32f * 1.40f))
                return "dog full +R15_R";
            float dogRws = FullChargeKnockback.HitDistance(
                EnemyKindIds.Dog, false, false, true, KnockbackRewardDraft.DistPctProduct(mid));
            if (!NearKb(dogRws, 4.32f * 1.40f * 1.50f))
                return "dog weak-spot after 震矢 then ×1.5";
            float raisedC = FullChargeKnockback.HitDistance(
                EnemyKindIds.Shield, true, false, false, KnockbackRewardDraft.DistPctProduct(mid));
            if (!NearKb(raisedC, 0f) || !FullChargeKnockback.RootsOnBodyHit(EnemyKindIds.Shield, true, false))
                return "震矢 must not convert shield-raised body to knockback";
            float shieldWs = FullChargeKnockback.HitDistance(
                EnemyKindIds.Shield, true, false, true, KnockbackRewardDraft.DistPctProduct(low));
            if (!NearKb(shieldWs, 2.7f * 1.20f * 1.50f))
                return "shield WS shatter then 震矢 then ×1.5";
            if (FullChargeKnockback.Applies(ChargeShotKind.Weak, 0.30f))
                return "weak charge still no KB with 震矢 table loaded";
            if (KnockbackRewardDraft.Source == null
                || KnockbackRewardDraft.Source.IndexOf("DRAFT_NOT_LOCKED", StringComparison.Ordinal) < 0)
                return "震矢 table must stay DRAFT_NOT_LOCKED";
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
                if (pace.AltarWalkSeconds < MazeRules.WalkAltarMinSeconds - 0.05f
                    || pace.AltarWalkSeconds > MazeRules.WalkAltarMaxSeconds + 0.05f)
                    return "seed " + seeds[s] + " START→Altar walk " + pace.AltarWalkSeconds.ToString("0.0")
                        + "s not in 60-120";
                float allLo = MazeRules.WalkAllTargetSeconds - MazeRules.WalkAllSlackSeconds;
                float allHi = MazeRules.WalkAllTargetSeconds + MazeRules.WalkAllSlackSeconds;
                if (pace.FullWalkSeconds < allLo - 0.05f || pace.FullWalkSeconds > allHi + 0.05f)
                    return "seed " + seeds[s] + " visit-all walk " + pace.FullWalkSeconds.ToString("0.0")
                        + "s not in 240±30";
                if (pace.MaxCorridorSegSeconds > MazeRules.CorridorSegMaxSeconds + 0.05f)
                    return "seed " + seeds[s] + " corridor seg " + pace.MaxCorridorSegSeconds.ToString("0.00")
                        + "s > 5s";
                if (maze.Edges == null || maze.Edges.Length < 4)
                    return "seed " + seeds[s] + " need corridors";
                for (int e = 0; e < maze.Edges.Length; e++)
                {
                    MazeEdge edge = maze.Edges[e];
                    if (edge.MaxSegment > MazeRules.CorridorSegMax + 0.05f)
                        return "seed " + seeds[s] + " " + edge.FromId + "--" + edge.ToId
                            + " seg " + edge.MaxSegment.ToString("0.0") + "u > 30";
                    bool stem = edge.FromId == "START" || edge.ToId == "START";
                    int minFolds = stem ? MazeRules.FoldStem
                        : (maze.TemplateId == "C" ? MazeRules.FoldBranchC : MazeRules.FoldBranchIZ);
                    if (edge.FoldCount < minFolds)
                        return "seed " + seeds[s] + " " + edge.FromId + "--" + edge.ToId
                            + " folds=" + edge.FoldCount + " < " + minFolds;
                }
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
