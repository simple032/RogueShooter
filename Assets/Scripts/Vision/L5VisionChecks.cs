using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Iso;
using RogueShooter.Layout;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;

namespace RogueShooter.Vision
{
    /// <summary>
    /// L5 vision / spawn / aggro + iso-prep self-checks. Pure (no scene); returns null on pass.
    /// Runs every scenario with <see cref="IsoConfig.Enabled"/> off and on and restores the switch
    /// and <see cref="L5Rules"/> afterwards. Measured numbers are kept in the static fields below
    /// (<see cref="Report"/>). "Clamped" = probe camera clamp: the view rect is kept inside the room's
    /// projected bounding box (main has no camera clamp; this is the worst case a later clamp could add).
    /// </summary>
    public static class L5VisionChecks
    {
        public const float Aspect = 16f / 9f;

        // ---- measured (filled by Run) ----
        public static float IsoCenteredMinEdgeU;
        public static float IsoCenteredMaxRadialU;
        public static float OrthoCenteredMinEdgeU;
        public static float IsoClampedMinEdgeU_Big;
        public static float IsoClampedNearestOffscreenCellU_Big;
        public static float IsoClampedMinEdgeU_Painted;
        public static float IsoClampedNearestOffscreenCellU_Painted;
        public static float OrthoClampedMinEdgeU_Big;
        public static float OrthoClampedNearestOffscreenCellU_Big;
        public static int SpawnSpotsChecked;
        public static int SpawnFallbacks;
        public static int SpawnNonFallback;
        public static float CornerFallbackDistU;
        public static float CornerFallbackWarnS;
        public static float SmallRoomFallbackDistU;
        public static float SmallRoomFallbackWarnS;
        public static int FacingSamples;

        public static readonly MazeNode BigRoom = Room("L5_big", 0f, 0f, MazeRules.CombatWidth, MazeRules.CombatHeight);
        public static readonly MazeNode PaintedRoom = Room("L5_painted", 100f, 50f, 20f, 16f);
        public static readonly MazeNode MidRoom = Room("L5_mid", -40f, 10f, 30f, 24f);
        public static readonly MazeNode SmallRoom = Room("L5_small", 60f, -40f, 12f, 10f);

        /// <summary>Single ortho source matches the mode: 6 orthographic, ≥ 5.25 aspect-locked iso.</summary>
        public static bool OrthoMatchesMode(float ortho)
        {
            if (Math.Abs(CameraViewService.OrthoPlaySize - 6f) > 0.001f)
                return false;
            if (!IsoConfig.Enabled)
                return Math.Abs(ortho - 6f) < 0.001f;
            return Math.Abs(ortho - CameraViewService.PlayOrthoSize) < 0.001f
                && ortho >= IsoConfig.IsoOrthoSize - 0.001f;
        }

        public static string Run()
        {
            bool iso0 = IsoConfig.Enabled;
            try
            {
                L5Rules.ResetDefaults();
                string err = CheckConfig();
                if (err != null) return err;
                err = CheckOrthoSource();
                if (err != null) return err;
                err = CheckLogicValuesIndependentOfOrtho();
                if (err != null) return err;
                err = CheckIsoOffIdentity();
                if (err != null) return err;
                err = CheckFacingUnitVectors();
                if (err != null) return err;
                err = CheckViewQuadAndFireGate();
                if (err != null) return err;
                err = CheckSpawnRule();
                if (err != null) return err;
                err = CheckFallback();
                if (err != null) return err;
                MeasureClamp();
                return null;
            }
            finally
            {
                IsoConfig.Enabled = iso0;
                L5Rules.ResetDefaults();
            }
        }

        public static string FormatPass()
        {
            return "ACCEPTANCE PASS L5 vision " + L5Rules.Describe()
                   + " spawnSpots=" + SpawnSpotsChecked + " fallback=" + SpawnFallbacks
                   + " facing=" + FacingSamples;
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[L5] config " + L5Rules.Describe());
            sb.AppendLine("[L5] iso 5.25 16:9 centred: player→screen edge min=" + F(IsoCenteredMinEdgeU)
                          + "u (quad boundary max radial=" + F(IsoCenteredMaxRadialU) + "u, +M=" + F(IsoCenteredMaxRadialU + L5Rules.SpawnOutsideQuadU) + " ≤ Dmax 22)");
            sb.AppendLine("[L5] ortho 6 16:9 centred: player→screen edge min=" + F(OrthoCenteredMinEdgeU)
                          + "u vs ranged aggro " + F(L5Rules.AggroRangedU) + "u (report only)");
            sb.AppendLine("[L5] iso 5.25 clamped 52x40: player→edge min=" + F(IsoClampedMinEdgeU_Big)
                          + "u nearestOffscreenWalkable=" + F(IsoClampedNearestOffscreenCellU_Big) + "u");
            sb.AppendLine("[L5] iso 5.25 clamped 20x16: player→edge min=" + F(IsoClampedMinEdgeU_Painted)
                          + "u nearestOffscreenWalkable=" + F(IsoClampedNearestOffscreenCellU_Painted) + "u");
            sb.AppendLine("[L5] ortho 6 clamped 52x40: player→edge min=" + F(OrthoClampedMinEdgeU_Big)
                          + "u nearestOffscreenWalkable=" + F(OrthoClampedNearestOffscreenCellU_Big) + "u");
            bool stage4 = IsoClampedMinEdgeU_Big >= L5Rules.Stage4MinScreenEdgeU;
            sb.AppendLine("[L5] stage4 need ≥" + F(L5Rules.Stage4MinScreenEdgeU) + "u → " + (stage4 ? "OK" : "SHORT (would drop ranged aggro to " + F(L5Rules.Stage4RangedAggroIfShortU) + "u; report only, value unchanged)"));
            sb.AppendLine("[L5] spawn spots=" + SpawnSpotsChecked + " nonFallback=" + SpawnNonFallback + " fallback=" + SpawnFallbacks);
            sb.AppendLine("[L5] corner(20x16 iso clamped) fallback dist=" + F(CornerFallbackDistU) + "u warn=" + F(CornerFallbackWarnS)
                          + "s; small(12x10) dist=" + F(SmallRoomFallbackDistU) + "u warn=" + F(SmallRoomFallbackWarnS) + "s");
            return sb.ToString();
        }

        // ------------------------------------------------------------------

        static string CheckConfig()
        {
            if (L5Rules.AggroMeleeU != 8f || L5Rules.AggroRangedU != 10f)
                return "aggro 8/10";
            if (L5Rules.OrbRangeU != 12f || L5Rules.OrbFlightSeconds != 1f)
                return "orb 12u / 1.0s";
            if (!L5Rules.OrbRangeOrWallOnly || !L5Rules.RangedFireRequiresInView || !L5Rules.SpawnRuleEnabled)
                return "switches default on";
            if (L5Rules.SpawnMinU != 14f || L5Rules.SpawnMaxU != 22f || L5Rules.SpawnOutsideQuadU != 2f)
                return "spawn D 14–22 M 2";
            if (L5Rules.DefaultSpawnMaxU < 22f)
                return "spawn max must not go below 22";
            if (L5Rules.FallbackWarnSeconds != 1f || L5Rules.FallbackWarnNearSeconds != 1.5f)
                return "warn 1.0 / 1.5";
            if (L5Rules.FallbackWarnFor(13.9f) != 1.5f || L5Rules.FallbackWarnFor(14f) != 1f)
                return "warn near threshold = SpawnMinU";
            return null;
        }

        static string CheckOrthoSource()
        {
            IsoConfig.Enabled = false;
            if (Math.Abs(CameraViewService.PlayOrthoSize - 6f) > 0.001f) return "ortho off 6";
            if (Math.Abs(CameraViewService.PlayOrthoSizeFor(4f / 3f) - 6f) > 0.001f) return "ortho off ignores aspect";
            if (Math.Abs(MazeRules.PlayOrtho - CameraViewService.PlayOrthoSize) > 0.001f) return "MazeRules forwards";
            if (Math.Abs(EnemyCombatRules.PlayOrthoSize - CameraViewService.PlayOrthoSize) > 0.001f) return "EnemyCombatRules forwards";
            if (Math.Abs(CameraViewService.EffectiveOrtho(6f, Aspect) - 6f) > 0.001f) return "effective off = serialized";
            IsoConfig.Enabled = true;
            if (Math.Abs(CameraViewService.PlayOrthoSizeFor(Aspect) - 5.25f) > 0.001f) return "iso 16:9 5.25";
            if (Math.Abs(CameraViewService.PlayOrthoSizeFor(21f / 9f) - 5.25f) > 0.001f) return "iso 21:9 5.25";
            if (Math.Abs(CameraViewService.PlayOrthoSizeFor(4f / 3f) - IsoConfig.OrthoSizeForAspect(4f / 3f)) > 0.001f) return "iso 4:3 aspect lock";
            if (Math.Abs(CameraViewService.EffectiveOrtho(6f, Aspect) - 5.25f) > 0.001f) return "effective iso ignores serialized 6";
            if (!OrthoMatchesMode(CameraViewService.PlayOrthoSize)) return "iso property " + CameraViewService.PlayOrthoSize;
            if (Math.Abs(MazeRules.PlayOrtho - CameraViewService.PlayOrthoSize) > 0.001f) return "MazeRules forwards iso";
            IsoConfig.Enabled = false;
            return null;
        }

        /// <summary>Acceptance 1: ortho 6 ↔ 5.25 does not change aggro / orb range (logic values).</summary>
        static string CheckLogicValuesIndependentOfOrtho()
        {
            float[] detectM = new float[2], detectR = new float[2], detectG = new float[2], orb = new float[2], flight = new float[2], ortho = new float[2];
            for (int k = 0; k < 2; k++)
            {
                IsoConfig.Enabled = k == 1;
                ortho[k] = CameraViewService.PlayOrthoSize;
                detectM[k] = MobFourStateAi.DetectRadiusFor(false, EnemyKindCatalog.Normal.AttackRange);
                detectR[k] = MobFourStateAi.DetectRadiusFor(true, EnemyKindCatalog.CultMage.AttackRange);
                detectG[k] = MobFourStateAi.DetectRadiusFor(true, EnemyKindCatalog.GrandMage.AttackRange);
                float speed = EnemyCombatRules.OrbSpeedForKind(EnemyKindIds.CultMage, EnemyKindCatalog.CultMage.WalkSpeedPlayStub);
                orb[k] = EnemyCombatRules.OrbMaxRange(speed);
                flight[k] = orb[k] / speed;
            }

            IsoConfig.Enabled = false;
            // Iso: 5.25 at 16:9 and wider (aspect-locked above 5.25 for narrower live cameras).
            if (Math.Abs(ortho[0] - 6f) > 0.001f || ortho[1] < 5.25f - 0.001f || ortho[1] > 5.99f)
                return "ortho sweep must be 6 → 5.25 (got " + ortho[0] + "/" + ortho[1] + ")";
            if (detectM[0] != 8f || detectM[1] != 8f) return "melee aggro 8 in both";
            if (detectR[0] != 10f || detectR[1] != 10f || detectG[0] != 10f || detectG[1] != 10f) return "ranged aggro 10 in both";
            if (Math.Abs(orb[0] - 12f) > 0.001f || Math.Abs(orb[1] - 12f) > 0.001f) return "orb range 12 in both";
            if (Math.Abs(flight[0] - 1f) > 0.001f || Math.Abs(flight[1] - 1f) > 0.001f) return "orb flight 1.0s";
            // Legacy formula at ortho 6 was 14.93u; must not be what we return any more.
            if (Math.Abs(EnemyCombatRules.OrbMaxRange(12f) - EnemyCombatRules.CameraWidth(6f, Aspect) * 0.7f) < 0.01f)
                return "orb range still camera-width based";
            // Range capped by flight time when a slower orb is configured.
            if (Math.Abs(L5Rules.OrbMaxRange(6f) - 6f) > 0.001f) return "orb range = min(range, speed × flight)";
            return null;
        }

        /// <summary>Acceptance 5 (conversion part): iso off → every conversion is the pre-PR identity.</summary>
        static string CheckIsoOffIdentity()
        {
            IsoConfig.Enabled = false;
            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iy = -1; iy <= 1; iy++)
                {
                    var raw = new Vector2(ix, iy);
                    if (ViewSpace.InputToLogic(raw) != raw) return "input identity off";
                    Vector2 legacy = raw;
                    if (legacy.sqrMagnitude > 1f) legacy.Normalize();
                    if ((PlayerMotor2D.MoveInput(raw, false) - legacy).sqrMagnitude > 1e-10f) return "move input legacy off";
                    // Legacy dodge: input normalized, else last facing, else right.
                    Vector3 lf = new Vector3(0.6f, -0.8f, 0f);
                    Vector3 legacyRoll = raw.sqrMagnitude > 0.01f ? (Vector3)raw.normalized : lf;
                    if ((PlayerDodge.RollDirFrom(raw, lf, Vector3.right, false) - legacyRoll.normalized).sqrMagnitude > 1e-10f) return "roll legacy off";
                }
            }

            // analog magnitude kept below 1 (screen speed not compensated; off = same as before)
            if ((ViewSpace.InputToLogic(new Vector2(0.5f, 0f)) - new Vector2(0.5f, 0f)).sqrMagnitude > 1e-10f) return "analog identity off";
            var p = new Vector3(3.25f, -7.5f, 0f);
            if (ViewSpace.LogicToCamera(p) != p) return "follow identity off";
            if (ViewSpace.CameraToLogic(p) != p) return "camera→logic identity off";
            var q = new Vector2[4];
            Rect r = Rect.MinMaxRect(-3f, -2f, 5f, 4f);
            ViewSpace.QuadFromViewRect(r, false, q);
            if (q[0] != new Vector2(-3f, -2f) || q[2] != new Vector2(5f, 4f)) return "quad = rect off";
            if (!ViewSpace.QuadContains(q, new Vector2(5f, 4f)) || ViewSpace.QuadContains(q, new Vector2(5.01f, 0f))) return "quad inclusive = ContainsInclusive";
            if (Math.Abs(ViewSpace.DistanceToQuadEdge(q, new Vector2(1f, 1f)) - 3f) > 1e-4f) return "rect edge distance";
            return null;
        }

        /// <summary>Acceptance 6: LastFacing / RollDir / Aim / Mob facing are logic-space unit vectors.</summary>
        static string CheckFacingUnitVectors()
        {
            FacingSamples = 0;
            var rng = new System.Random(7);
            for (int m = 0; m < 2; m++)
            {
                bool iso = m == 1;
                IsoConfig.Enabled = iso;
                for (int ix = -1; ix <= 1; ix++)
                {
                    for (int iy = -1; iy <= 1; iy++)
                    {
                        var raw = new Vector2(ix, iy);
                        Vector2 move = PlayerMotor2D.MoveInput(raw, iso);
                        Vector3 face = PlayerMotor2D.FacingFromInput(move, new Vector3(0.3f, 0.4f, 0f));
                        if (!Unit(face)) return "LastFacing unit iso=" + iso + " raw=" + raw;
                        if (raw != Vector2.zero)
                        {
                            if (Math.Abs(move.magnitude - 1f) > 1e-4f) return "WASD normalized in logic iso=" + iso;
                            // Screen direction preserved: logic → screen is parallel to the key's screen direction.
                            Vector2 back = iso ? IsoProjection.LogicDirToScreenDir(move) : move;
                            if (Vector2.Dot(back.normalized, raw.normalized) < 0.9999f) return "WASD screen dir iso=" + iso + " raw=" + raw;
                        }

                        Vector3 roll = PlayerDodge.RollDirFrom(raw, face, Vector3.zero, iso);
                        if (!Unit(roll)) return "RollDir unit iso=" + iso;
                        if (raw != Vector2.zero && (roll - face).sqrMagnitude > 1e-6f) return "roll = move facing iso=" + iso;
                        FacingSamples += 2;
                    }
                }

                for (int i = 0; i < 64; i++)
                {
                    var origin = new Vector3(R(rng, 30f), R(rng, 30f), 0f);
                    var target = new Vector3(R(rng, 30f), R(rng, 30f), 0f);
                    if (!Unit(PlayerCharge.AimFrom(target, origin))) return "aim unit";
                    if (!Unit(MobFourStateAi.FacingToward(target - origin, Vector3.right))) return "mob facing unit";
                    FacingSamples += 2;
                }

                if (!Unit(PlayerCharge.AimFrom(Vector3.one, Vector3.one))) return "aim degenerate";
                if (!Unit(MobFourStateAi.FacingToward(Vector3.zero, new Vector3(2f, 0f, 0f)))) return "mob facing keep prev";
                if (!Unit(PlayerMotor2D.FacingFromInput(new Vector2(0.4f, 0f), Vector3.right))) return "analog facing unit";
                if (!Unit(PlayerDodge.RollDirFrom(Vector2.zero, Vector3.zero, Vector3.zero, iso))) return "roll default";
            }

            IsoConfig.Enabled = false;
            return null;
        }

        /// <summary>Acceptance 4: off-screen mobs neither detect first nor fire (iso 5.25); ortho numbers reported.</summary>
        static string CheckViewQuadAndFireGate()
        {
            var q = new Vector2[4];
            IsoConfig.Enabled = true;
            float isoOrtho = CameraViewService.PlayOrthoSizeFor(Aspect);
            Vector3 player = new Vector3(4f, -3f, 0f);
            ViewSpace.QuadFromViewRect(ViewSpace.ViewRectAt(ViewSpace.LogicToCamera(player), isoOrtho, Aspect), true, q);
            IsoCenteredMinEdgeU = ViewSpace.DistanceToQuadEdge(q, player);
            IsoCenteredMaxRadialU = 0f;
            for (int i = 0; i < 4; i++)
                IsoCenteredMaxRadialU = Mathf.Max(IsoCenteredMaxRadialU, Vector2.Distance(q[i], player));
            if (Math.Abs(IsoCenteredMinEdgeU - 13.2f) > 0.05f) return "iso centred edge ≈13.2 got " + F(IsoCenteredMinEdgeU);
            if (IsoCenteredMaxRadialU + L5Rules.SpawnOutsideQuadU > L5Rules.SpawnMaxU + 1e-3f)
                return "Dmax 22 must cover the quad corner + M (" + F(IsoCenteredMaxRadialU) + ")";
            if (L5Rules.AggroRangedU >= IsoCenteredMinEdgeU || L5Rules.AggroMeleeU >= IsoCenteredMinEdgeU)
                return "iso: aggro must stay inside the view";
            // Every point within ranged aggro of the player is on screen → no off-screen detection first.
            for (int a = 0; a < 360; a += 3)
            {
                float rad = a * Mathf.Deg2Rad;
                var pt = new Vector2(player.x + Mathf.Cos(rad) * L5Rules.AggroRangedU, player.y + Mathf.Sin(rad) * L5Rules.AggroRangedU);
                if (!ViewSpace.QuadContains(q, pt)) return "iso: ranged aggro ring leaves the view at " + a;
            }

            // Fire gate: outside the quad → no fire; inside → fire; melee unaffected; switch off → legacy.
            Vector2 outside = q[1] + (q[1] - new Vector2(player.x, player.y)).normalized * 0.5f;
            if (MobFourStateAi.RangedMayFire(true, ViewSpace.QuadContains(q, outside))) return "ranged fired outside quad";
            if (!MobFourStateAi.RangedMayFire(true, ViewSpace.QuadContains(q, new Vector2(player.x + 9f, player.y)))) return "ranged blocked inside quad";
            if (!MobFourStateAi.RangedMayFire(false, false)) return "melee must not be gated";
            L5Rules.RangedFireRequiresInView = false;
            bool legacy = MobFourStateAi.RangedMayFire(true, false);
            L5Rules.RangedFireRequiresInView = true;
            if (!legacy) return "fire gate switch";

            IsoConfig.Enabled = false;
            ViewSpace.QuadFromViewRect(ViewSpace.ViewRectAt(player, CameraViewService.PlayOrthoSize, Aspect), false, q);
            OrthoCenteredMinEdgeU = ViewSpace.DistanceToQuadEdge(q, player);
            if (Math.Abs(OrthoCenteredMinEdgeU - 6f) > 0.001f) return "ortho centred edge 6";
            // Ortho: the ranged fire gate still holds even though ranged aggro (10) > screen edge (6).
            var offTop = new Vector2(player.x, player.y + 8f);
            if (ViewSpace.QuadContains(q, offTop) || MobFourStateAi.RangedMayFire(true, ViewSpace.QuadContains(q, offTop)))
                return "ortho: off-screen caster must not fire";
            return null;
        }

        /// <summary>Acceptance 2: every non-fallback spot is 14–22u, ≥2u outside the quad, on a walkable room cell.</summary>
        static string CheckSpawnRule()
        {
            SpawnSpotsChecked = 0;
            SpawnFallbacks = 0;
            SpawnNonFallback = 0;
            var rooms = new[] { BigRoom, MidRoom, PaintedRoom, SmallRoom };
            var avoidsBig = new[] { CombatRoomSpawn.FromSite(SiteKind.Chest, 10.5f, 6.5f), CombatRoomSpawn.FromSite(SiteKind.Altar, -15.5f, -8.5f) };
            var q = new Vector2[4];
            for (int m = 0; m < 2; m++)
            {
                IsoConfig.Enabled = m == 1;
                float ortho = CameraViewService.PlayOrthoSizeFor(Aspect);
                for (int ri = 0; ri < rooms.Length; ri++)
                {
                    MazeNode room = rooms[ri];
                    SpawnAvoid[] avoids = room == BigRoom ? avoidsBig : new SpawnAvoid[0];
                    List<Vector2> cells = L5Spawn.WalkableCells(room, avoids);
                    for (int clamp = 0; clamp < 2; clamp++)
                    {
                        for (int ci = 0; ci < cells.Count; ci += 5)
                        {
                            Vector2 pl = cells[ci];
                            Rect view = clamp == 1
                                ? ClampedViewRect(room, pl, ortho, Aspect, IsoConfig.Enabled)
                                : ViewSpace.ViewRectAt(ViewSpace.LogicToCamera(pl), ortho, Aspect);
                            ViewSpace.QuadFromViewRect(view, IsoConfig.Enabled, q);
                            L5Spot[] spots = L5Spawn.PlaceWave(room, pl.x, pl.y, 6, avoids, q, new System.Random(ci * 13 + ri * 7 + m));
                            string err = ValidateSpots(room, avoids, cells, pl, q, spots);
                            if (err != null)
                                return err + " iso=" + IsoConfig.Enabled + " room=" + room.Id + " clamp=" + clamp + " player=" + pl;
                            // Re-validation at spawn time after the player moved 3u keeps the rule.
                            Vector2 moved = pl + new Vector2(3f, 1f);
                            if (room.Contains(moved.x, moved.y, CombatRoomSpawn.RoomInset))
                            {
                                view = clamp == 1
                                    ? ClampedViewRect(room, moved, ortho, Aspect, IsoConfig.Enabled)
                                    : ViewSpace.ViewRectAt(ViewSpace.LogicToCamera(moved), ortho, Aspect);
                                ViewSpace.QuadFromViewRect(view, IsoConfig.Enabled, q);
                                L5Spawn.EnforceAtSpawn(room, moved.x, moved.y, spots, avoids, q, new System.Random(ci));
                                err = ValidateSpots(room, avoids, cells, moved, q, spots);
                                if (err != null)
                                    return "enforce " + err + " iso=" + IsoConfig.Enabled + " room=" + room.Id;
                            }
                        }
                    }
                }
            }

            IsoConfig.Enabled = false;
            if (SpawnNonFallback < 100) return "spawn sweep produced too few rule spots (" + SpawnNonFallback + ")";
            return null;
        }

        static string ValidateSpots(MazeNode room, SpawnAvoid[] avoids, List<Vector2> cells, Vector2 pl, Vector2[] q, L5Spot[] spots)
        {
            var occ = new List<Vector2>();
            for (int i = 0; i < spots.Length; i++)
            {
                L5Spot s = spots[i];
                SpawnSpotsChecked++;
                if (!L5Spawn.IsWalkableCell(room, avoids, s.X, s.Y)) return "spot not a walkable room cell " + s.X + "," + s.Y;
                for (int j = 0; j < occ.Count; j++)
                    if (Vector2.Distance(occ[j], new Vector2(s.X, s.Y)) < CombatRoomSpawn.PackSep - 1e-4f) return "spots overlap";
                float d = Vector2.Distance(pl, new Vector2(s.X, s.Y));
                if (!s.Fallback)
                {
                    SpawnNonFallback++;
                    if (d < L5Rules.SpawnMinU - 1e-3f || d > L5Rules.SpawnMaxU + 1e-3f) return "dist " + F(d) + " outside 14–22";
                    if (ViewSpace.SignedOutside(q, new Vector2(s.X, s.Y)) < L5Rules.SpawnOutsideQuadU - 1e-3f) return "spot < 2u outside view quad";
                    if (s.WarnSeconds != 0f) return "rule spot must not warn";
                }
                else
                {
                    SpawnFallbacks++;
                    // Fallback only when no free cell met the rule, and then the farthest free cell.
                    float farD = -1f;
                    for (int c = 0; c < cells.Count; c++)
                    {
                        bool free = true;
                        for (int j = 0; j < occ.Count; j++)
                            if (Vector2.Distance(occ[j], cells[c]) < CombatRoomSpawn.PackSep) free = false;
                        if (!free) continue;
                        if (L5Spawn.MeetsRule(cells[c].x, cells[c].y, pl.x, pl.y, q)) return "fallback while a rule cell existed";
                        farD = Mathf.Max(farD, Vector2.Distance(cells[c], pl));
                    }

                    if (Math.Abs(d - farD) > 1e-3f) return "fallback not the farthest cell";
                    if (Math.Abs(s.WarnSeconds - L5Rules.FallbackWarnFor(d)) > 1e-4f) return "fallback warn";
                    if (s.WarnSeconds < 0.99f) return "fallback must warn ≥1.0s";
                }

                occ.Add(new Vector2(s.X, s.Y));
            }

            return null;
        }

        /// <summary>Acceptance 3: player in a room corner with the camera clamped → fallback + warning.</summary>
        static string CheckFallback()
        {
            var q = new Vector2[4];
            IsoConfig.Enabled = true;
            float ortho = CameraViewService.PlayOrthoSizeFor(Aspect);
            var none = new SpawnAvoid[0];

            List<Vector2> cells = L5Spawn.WalkableCells(PaintedRoom, none);
            Vector2 corner = Corner(cells, -1f, -1f);
            ViewSpace.QuadFromViewRect(ClampedViewRect(PaintedRoom, corner, ortho, Aspect, true), true, q);
            L5Spot[] s = L5Spawn.PlaceWave(PaintedRoom, corner.x, corner.y, 3, none, q, new System.Random(3));
            if (s.Length != 3 || !s[0].Fallback) return "corner 20x16 iso clamped must fall back";
            CornerFallbackDistU = s[0].DistPlayer;
            CornerFallbackWarnS = s[0].WarnSeconds;
            if (Math.Abs(CornerFallbackWarnS - L5Rules.FallbackWarnFor(CornerFallbackDistU)) > 1e-4f) return "corner warn";

            cells = L5Spawn.WalkableCells(SmallRoom, none);
            corner = Corner(cells, 1f, 1f);
            ViewSpace.QuadFromViewRect(ClampedViewRect(SmallRoom, corner, ortho, Aspect, true), true, q);
            s = L5Spawn.PlaceWave(SmallRoom, corner.x, corner.y, 2, none, q, new System.Random(5));
            if (!s[0].Fallback) return "small room must fall back";
            SmallRoomFallbackDistU = s[0].DistPlayer;
            SmallRoomFallbackWarnS = s[0].WarnSeconds;
            if (SmallRoomFallbackDistU >= L5Rules.SpawnMinU || Math.Abs(SmallRoomFallbackWarnS - 1.5f) > 1e-4f) return "near fallback warns 1.5s";

            // Ortho corner of the big room: a rule spot exists → no fallback (sanity).
            IsoConfig.Enabled = false;
            cells = L5Spawn.WalkableCells(BigRoom, none);
            corner = Corner(cells, -1f, -1f);
            ViewSpace.QuadFromViewRect(ClampedViewRect(BigRoom, corner, 6f, Aspect, false), false, q);
            s = L5Spawn.PlaceWave(BigRoom, corner.x, corner.y, 3, none, q, new System.Random(9));
            if (s[0].Fallback) return "ortho big-room corner should find rule spots";
            return null;
        }

        static void MeasureClamp()
        {
            IsoConfig.Enabled = true;
            float iso = CameraViewService.PlayOrthoSizeFor(Aspect);
            ClampProbe(BigRoom, iso, true, out IsoClampedMinEdgeU_Big, out IsoClampedNearestOffscreenCellU_Big);
            ClampProbe(PaintedRoom, iso, true, out IsoClampedMinEdgeU_Painted, out IsoClampedNearestOffscreenCellU_Painted);
            IsoConfig.Enabled = false;
            ClampProbe(BigRoom, 6f, false, out OrthoClampedMinEdgeU_Big, out OrthoClampedNearestOffscreenCellU_Big);
        }

        static void ClampProbe(MazeNode room, float ortho, bool iso, out float minEdge, out float nearestOff)
        {
            var q = new Vector2[4];
            List<Vector2> cells = L5Spawn.WalkableCells(room, new SpawnAvoid[0]);
            minEdge = float.PositiveInfinity;
            nearestOff = float.PositiveInfinity;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 pl = cells[i];
                ViewSpace.QuadFromViewRect(ClampedViewRect(room, pl, ortho, Aspect, iso), iso, q);
                minEdge = Mathf.Min(minEdge, ViewSpace.SignedOutside(q, pl) <= 0f ? ViewSpace.DistanceToQuadEdge(q, pl) : 0f);
                for (int j = 0; j < cells.Count; j++)
                {
                    if (ViewSpace.QuadContains(q, cells[j]))
                        continue;
                    nearestOff = Mathf.Min(nearestOff, Vector2.Distance(pl, cells[j]));
                }
            }
        }

        /// <summary>
        /// Probe camera clamp: view rect centred on the player (view space), then shifted so it stays inside
        /// the room's projected bounding box; centred on the room on an axis where the view is larger.
        /// </summary>
        public static Rect ClampedViewRect(MazeNode room, Vector2 playerLogic, float ortho, float aspect, bool iso)
        {
            float hx = room.Width * 0.5f, hy = room.Height * 0.5f;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(room.Center.X + ((i & 1) == 0 ? -hx : hx), room.Center.Y + ((i & 2) == 0 ? -hy : hy), 0f);
                Vector3 v = iso ? IsoProjection.LogicToView(c) : c;
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }

            Vector3 pv = iso ? IsoProjection.LogicToView(new Vector3(playerLogic.x, playerLogic.y, 0f)) : new Vector3(playerLogic.x, playerLogic.y, 0f);
            float halfH = ortho, halfW = ortho * aspect;
            float cx = maxX - minX <= 2f * halfW ? (minX + maxX) * 0.5f : Mathf.Clamp(pv.x, minX + halfW, maxX - halfW);
            float cy = maxY - minY <= 2f * halfH ? (minY + maxY) * 0.5f : Mathf.Clamp(pv.y, minY + halfH, maxY - halfH);
            return ViewSpace.ViewRectAt(new Vector3(cx, cy, 0f), ortho, aspect);
        }

        /// <summary>Debug dump for the view-quad / spawn-point picture (scenario,type,x,y,a,b).</summary>
        public static string DumpDebugCsv()
        {
            bool iso0 = IsoConfig.Enabled;
            var sb = new StringBuilder();
            sb.AppendLine("scenario,type,x,y,a,b");
            try
            {
                L5Rules.ResetDefaults();
                DumpScenario(sb, "iso_on_52x40_clamped", BigRoom, new Vector2(-17.5f, -12.5f), true, true);
                DumpScenario(sb, "iso_off_52x40_clamped", BigRoom, new Vector2(-17.5f, -12.5f), false, true);
                DumpScenario(sb, "iso_on_52x40_centred", BigRoom, new Vector2(2.5f, 1.5f), true, false);
                DumpScenario(sb, "iso_on_20x16_corner_fallback", PaintedRoom, Corner(L5Spawn.WalkableCells(PaintedRoom, new SpawnAvoid[0]), -1f, -1f), true, true);
            }
            finally
            {
                IsoConfig.Enabled = iso0;
            }

            return sb.ToString();
        }

        static void DumpScenario(StringBuilder sb, string name, MazeNode room, Vector2 pl, bool iso, bool clamp)
        {
            IsoConfig.Enabled = iso;
            float ortho = CameraViewService.PlayOrthoSizeFor(Aspect);
            var q = new Vector2[4];
            Rect view = clamp ? ClampedViewRect(room, pl, ortho, Aspect, iso) : ViewSpace.ViewRectAt(ViewSpace.LogicToCamera(pl), ortho, Aspect);
            ViewSpace.QuadFromViewRect(view, iso, q);
            float hx = room.Width * 0.5f, hy = room.Height * 0.5f;
            Row(sb, name, "room", room.Center.X - hx, room.Center.Y - hy, room.Width, room.Height);
            for (int i = 0; i < 4; i++) Row(sb, name, "quad", q[i].x, q[i].y, i, 0);
            Row(sb, name, "player", pl.x, pl.y, ViewSpace.DistanceToQuadEdge(q, pl), ortho);
            var none = new SpawnAvoid[0];
            foreach (Vector2 c in L5Spawn.WalkableCells(room, none))
            {
                float d = Vector2.Distance(c, pl);
                float o = ViewSpace.SignedOutside(q, c);
                int cls = L5Spawn.MeetsRule(c.x, c.y, pl.x, pl.y, q) ? 1 : (o < 0f ? 0 : 2);
                Row(sb, name, "cell", c.x, c.y, cls, d);
            }

            L5Spot[] s = L5Spawn.PlaceWave(room, pl.x, pl.y, 6, none, q, new System.Random(11));
            for (int i = 0; i < s.Length; i++) Row(sb, name, "spot", s[i].X, s[i].Y, s[i].Fallback ? 1 : 0, s[i].WarnSeconds);
        }

        static void Row(StringBuilder sb, string n, string t, float x, float y, float a, float b)
        {
            sb.Append(n).Append(',').Append(t).Append(',').Append(F(x)).Append(',').Append(F(y)).Append(',')
              .Append(F(a)).Append(',').Append(F(b)).Append('\n');
        }

        static Vector2 Corner(List<Vector2> cells, float sx, float sy)
        {
            Vector2 best = cells[0];
            float bestV = float.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                float v = cells[i].x * sx + cells[i].y * sy;
                if (v > bestV) { bestV = v; best = cells[i]; }
            }

            return best;
        }

        static MazeNode Room(string id, float cx, float cy, float w, float h)
        {
            return new MazeNode { Id = id, Kind = MazeNodeKind.Normal, Center = new MazeVec2(cx, cy), Width = w, Height = h, NeighborIds = new string[0] };
        }

        static bool Unit(Vector3 v)
        {
            return Math.Abs(v.magnitude - 1f) < 1e-4f && Math.Abs(v.z) < 1e-6f;
        }

        static float R(System.Random rng, float s)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0) * s;
        }

        static string F(float v)
        {
            if (float.IsInfinity(v)) return "inf";
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
