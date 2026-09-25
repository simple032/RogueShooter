using System;
using UnityEngine;
using RogueShooter.Spawning;

namespace RogueShooter.Iso
{
    /// <summary>
    /// Returns null on pass. Pure math; no scene and no gameplay numbers.
    /// Grid expectations are Unity Isometric CellToLocal for cell (1, 0.5, 1),
    /// gap 0, swizzle XYZ. The Tuanjie editor was not required to run these.
    /// </summary>
    public static class IsoProjectionChecks
    {
        public static string Run()
        {
            string err = CheckConstants();
            if (err != null) return err;
            err = CheckGridAgreement();
            if (err != null) return err;
            err = CheckRoundTrip();
            if (err != null) return err;
            err = CheckDirections();
            if (err != null) return err;
            err = CheckViewHeight();
            if (err != null) return err;
            err = CheckSortAndQuad();
            if (err != null) return err;
            err = CheckSectors();
            if (err != null) return err;
            err = CheckMirror();
            if (err != null) return err;
            err = CheckNullCamera();
            if (err != null) return err;
            return null;
        }

        public static string FormatPass()
        {
            return "ACCEPTANCE PASS iso-projection roundtrip+grid+sectors+mirror";
        }

        static string CheckConstants()
        {
            if (Value(IsoProjection.CellW) != 1f)
                return "CellW must be 1";
            if (Value(IsoProjection.CellH) != 0.5f)
                return "CellH must be 0.5";
            if (Value(IsoProjection.PixelsPerUnit) != 128f)
                return "PixelsPerUnit must be 128";
            if (Math.Abs(IsoProjection.ElevationPixelsPerUnit - 78.4f) > 0.001f)
                return "elevation must be 78.4 px per logic unit";
            if (Math.Abs(IsoProjection.ElevationViewPerUnit - (78.4f / 128f)) > 1e-6f)
                return "elevation view scale";
            if (Math.Abs(IsoFacing.Equal45BoundaryDeg - 22.5) > 1e-9)
                return "equal sector half must be 22.5";
            double diamond = IsoFacing.DiamondEdgeBoundaryDeg;
            if (Math.Abs(diamond - (Math.Atan(0.5) * (180.0 / Math.PI))) > 1e-9)
                return "diamond boundary must be atan(0.5) deg";
            if (diamond < 26.0 || diamond > 27.0)
                return "diamond boundary must be about 26.565 deg";
            return null;
        }

        static string CheckGridAgreement()
        {
            // Literal Unity Isometric CellToLocal results, not derived from the helper.
            if (!Near(IsoProjection.LogicToScreen(new Vector2(0f, 0f)), new Vector2(0f, 0f)))
                return "cell (0,0)";
            if (!Near(IsoProjection.LogicToScreen(new Vector2(1f, 0f)), new Vector2(0.5f, 0.25f)))
                return "cell (1,0) must be screen right-up";
            if (!Near(IsoProjection.LogicToScreen(new Vector2(0f, 1f)), new Vector2(-0.5f, 0.25f)))
                return "cell (0,1) must be screen left-up";
            if (!Near(IsoProjection.LogicToScreen(new Vector2(1f, 1f)), new Vector2(0f, 0.5f)))
                return "cell (1,1) must be screen up";
            if (!Near(IsoProjection.LogicToScreen(new Vector2(2f, 1f)), new Vector2(0.5f, 0.75f)))
                return "cell (2,1)";
            if (!Near(IsoProjection.LogicToScreen(new Vector2(-1f, 2f)), new Vector2(-1.5f, 0.25f)))
                return "cell (-1,2)";

            Vector3 withZ = IsoProjection.LogicToView(new Vector3(1f, 0f, 2f), 0f);
            if (!Near(new Vector2(withZ.x, withZ.y), new Vector2(0.5f, 0.25f)) || Math.Abs(withZ.z - 2f) > 1e-5f)
                return "CellToLocal z must pass through and not add to y";
            return null;
        }

        static string CheckRoundTrip()
        {
            Vector2[] points =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-1f, 2f),
                new Vector2(2f, -3f),
                new Vector2(0.5f, 0.5f),
                new Vector2(10.25f, -4.75f),
                new Vector2(100f, 80f),
                new Vector2(-30.5f, 12.125f)
            };
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 screen = IsoProjection.LogicToScreen(points[i]);
                Vector2 back = IsoProjection.ScreenToLogic(screen);
                if (!Near(back, points[i]))
                    return "round-trip logic " + points[i].x + "," + points[i].y;
                Vector2 again = IsoProjection.LogicToScreen(back);
                if (!Near(again, screen))
                    return "round-trip screen " + points[i].x + "," + points[i].y;
            }
            return null;
        }

        static string CheckDirections()
        {
            Vector2 scaled = IsoProjection.LogicDirToScreenDir(new Vector2(2f, 0f));
            if (!Near(scaled, new Vector2(1f, 0.5f)))
                return "logic dir (2,0) must stay scaled, not unit length";
            if (!Near(IsoProjection.ScreenDirToLogicDir(scaled), new Vector2(2f, 0f)))
                return "screen dir inverse";
            Vector2 zero = IsoProjection.LogicDirToScreenDir(Vector2.zero);
            if (zero.sqrMagnitude > 0f)
                return "zero dir must stay zero";
            if (IsoProjection.ScreenDirToLogicDir(Vector2.zero).sqrMagnitude > 0f)
                return "zero screen dir";

            Vector2 skew = IsoProjection.LogicDirToScreenDir(new Vector2(3f, -1f));
            if (!Near(IsoProjection.ScreenDirToLogicDir(skew), new Vector2(3f, -1f)))
                return "skew dir round-trip";
            return null;
        }

        static string CheckViewHeight()
        {
            Vector3 logic = new Vector3(2f, -1f, 4f);
            Vector3 view = IsoProjection.LogicToView(logic, 3f);
            if (Math.Abs(view.x - 1.5f) > 1e-4f)
                return "view x";
            float expectY = 0.25f + 3f * (78.4f / 128f);
            if (Math.Abs(view.y - expectY) > 1e-4f)
                return "view y must add 78.4px elevation";
            if (Math.Abs(view.z - 4f) > 1e-5f)
                return "view z passthrough";
            if (!Near(IsoProjection.ViewToLogic(view, 3f), new Vector2(2f, -1f)))
                return "view inverse with height";
            Vector2 forgotHeight = IsoProjection.ViewToLogic(view, 0f);
            if (Near(forgotHeight, new Vector2(2f, -1f)))
                return "dropping height must not invert to the ground cell";
            return null;
        }

        static string CheckSortAndQuad()
        {
            if (Math.Abs(IsoProjection.SortKey(new Vector2(2f, 3f)) - 5f) > 1e-5f)
                return "sort key x+y";
            if (!(IsoProjection.SortKey(new Vector2(1f, 1f)) > IsoProjection.SortKey(new Vector2(0f, 0f))))
                return "higher screen cell must have a larger sort key";
            Vector2 low = IsoProjection.LogicToScreen(new Vector2(0f, 0f));
            Vector2 high = IsoProjection.LogicToScreen(new Vector2(1f, 1f));
            if (!(high.y > low.y))
                return "sort key must follow screen y";

            Rect view = Rect.MinMaxRect(-1f, -0.5f, 1f, 0.5f);
            var quad = new Vector2[4];
            IsoProjection.ViewQuadInLogic(view, quad);
            if (!Near(quad[0], new Vector2(-2f, 0f)))
                return "quad BL";
            if (!Near(quad[1], new Vector2(0f, -2f)))
                return "quad BR";
            if (!Near(quad[2], new Vector2(2f, 0f)))
                return "quad TR";
            if (!Near(quad[3], new Vector2(0f, 2f)))
                return "quad TL";

            IsoProjection.ViewQuadInLogic(view, null);
            IsoProjection.ViewQuadInLogic(view, new Vector2[2]);
            return null;
        }

        static string CheckSectors()
        {
            if (IsoFacing.FromLogic(Vector2.zero, Dir8.N) != Dir8.N)
                return "zero logic dir must return fallback";
            if (IsoFacing.FromLogic(new Vector2(1e-5f, 0f), Dir8.S) != Dir8.S)
                return "near-zero logic dir must return fallback";
            if (IsoFacing.FromScreen(Vector2.zero, Dir8.W) != Dir8.W)
                return "zero screen dir must return fallback";

            // Screen cardinals and diagonals sit in the same sector in both modes.
            if (!ExpectScreen(0.0, Dir8.E)) return "screen E";
            if (!ExpectScreen(45.0, Dir8.NE)) return "screen NE";
            if (!ExpectScreen(90.0, Dir8.N)) return "screen N";
            if (!ExpectScreen(135.0, Dir8.NW)) return "screen NW";
            if (!ExpectScreen(180.0, Dir8.W)) return "screen W";
            if (!ExpectScreen(-135.0, Dir8.SW)) return "screen SW";
            if (!ExpectScreen(-90.0, Dir8.S)) return "screen S";
            if (!ExpectScreen(-45.0, Dir8.SE)) return "screen SE";

            // Equal 45°: boundary at 22.5 belongs to the CCW sector (NE).
            if (Facing(22.5, SectorMode.Equal45) != Dir8.NE)
                return "equal boundary +22.5 must be NE";
            if (Facing(22.3, SectorMode.Equal45) != Dir8.E)
                return "just inside equal E";
            if (Facing(-22.5, SectorMode.Equal45) != Dir8.E)
                return "equal boundary -22.5 must stay E";
            if (Facing(67.5, SectorMode.Equal45) != Dir8.N)
                return "equal boundary 67.5 must be N";
            if (Facing(67.3, SectorMode.Equal45) != Dir8.NE)
                return "just inside equal NE";

            double edge = IsoFacing.DiamondEdgeBoundaryDeg;
            if (Facing(edge, SectorMode.DiamondEdge) != Dir8.NE)
                return "diamond edge angle must be NE";
            if (Facing(edge - 0.2, SectorMode.DiamondEdge) != Dir8.E)
                return "just inside diamond E";
            if (Facing(edge + 0.2, SectorMode.DiamondEdge) != Dir8.NE)
                return "just outside diamond E";
            if (Facing(90.0 - edge, SectorMode.DiamondEdge) != Dir8.N)
                return "diamond NE|N boundary must be N";
            if (Facing(90.0 - edge - 0.2, SectorMode.DiamondEdge) != Dir8.NE)
                return "just inside diamond NE";

            // +logic X is the diamond edge. Equal45 puts it in NE (26.6>22.5).
            // Diamond mode puts the same ray on the boundary, owned by NE.
            if (IsoFacing.FromLogic(new Vector2(1f, 0f), Dir8.S, SectorMode.Equal45) != Dir8.NE)
                return "+logic X equal45 must be NE";
            if (IsoFacing.FromLogic(new Vector2(1f, 0f), Dir8.S, SectorMode.DiamondEdge) != Dir8.NE)
                return "+logic X diamond must be NE";
            if (IsoFacing.FromLogic(new Vector2(0f, 1f), Dir8.S, SectorMode.Equal45) != Dir8.NW)
                return "+logic Y equal45 must be NW";
            if (IsoFacing.FromLogic(new Vector2(0f, 1f), Dir8.S, SectorMode.DiamondEdge) != Dir8.W)
                return "+logic Y diamond boundary must be W";

            // Pure screen east is logic (1,-1).
            if (IsoFacing.FromLogic(new Vector2(1f, -1f), Dir8.S) != Dir8.E)
                return "logic (1,-1) is screen east";
            if (IsoFacing.FromLogic(new Vector2(1f, 1f), Dir8.S) != Dir8.N)
                return "logic (1,1) is screen north";

            // Custom half-angle: 30° keeps the diamond edge inside E; 10° does not.
            if (IsoFacing.FromLogic(new Vector2(1f, 0f), Dir8.S, 30f) != Dir8.E)
                return "custom 30 deg half must include +logic X in E";
            if (IsoFacing.FromLogic(new Vector2(1f, 0f), Dir8.S, 10f) != Dir8.NE)
                return "custom 10 deg half must put +logic X in NE";
            if (Facing(0.0, 0f) != Dir8.E)
                return "clamped half-angle still classifies east";

            // A vector above the epsilon still has a direction.
            if (IsoFacing.FromLogic(new Vector2(1e-3f, 0f), Dir8.S, SectorMode.Equal45) != Dir8.NE)
                return "small non-zero +logic X";
            return null;
        }

        static string CheckMirror()
        {
            string err = ExpectEight(IsoFacing.KindArcher);
            if (err != null) return err;
            err = ExpectEight("player");
            if (err != null) return err;
            err = ExpectEight("jh_char_archer");
            if (err != null) return err;
            err = ExpectEight("CHAR");
            if (err != null) return err;
            err = ExpectEight(null);
            if (err != null) return err;
            err = ExpectEight("boss");
            if (err != null) return err;

            string[] monsters =
            {
                EnemyKindIds.Normal, "e1", "skel", "jh_enemy_e1_skel", "骸兵",
                EnemyKindIds.Dog, "dog", "jh_enemy_dog", "猎犬",
                EnemyKindIds.CultMage, "mage", "jh_enemy_mage", "法师",
                EnemyKindIds.Shield, "盾兵",
                EnemyKindIds.GrandMage, "grand", "大邪术师"
            };
            for (int i = 0; i < monsters.Length; i++)
            {
                err = ExpectFive(monsters[i]);
                if (err != null) return err;
            }

            if (IsoFacing.ArtFrameId("jh_char_archer_walk", "s", 3) != "jh_char_archer_walk_s_03")
                return "archer frame id must match landed walk_s_03";
            if (IsoFacing.ArtFrameId("jh_enemy_e1_skel_walk", "ne", 0) != "jh_enemy_e1_skel_walk_ne_00")
                return "enemy frame id";
            string suffix;
            bool flip;
            IsoFacing.Resolve(EnemyKindIds.Dog, Dir8.W, out suffix, out flip);
            if (IsoFacing.ArtFrameId("jh_enemy_dog_walk", suffix, 2) != "jh_enemy_dog_walk_e_02" || !flip)
                return "dog west frame uses e + flipX";

            err = ExpectResolve(IsoFacing.EightDirNoMirror, Dir8.NW, "nw", false);
            if (err != null) return err;
            err = ExpectResolve(IsoFacing.FiveDirMirrorWest, Dir8.NW, "ne", true);
            if (err != null) return err;
            err = ExpectResolve(IsoFacing.FiveDirMirrorWest, Dir8.W, "e", true);
            if (err != null) return err;
            err = ExpectResolve(IsoFacing.FiveDirMirrorWest, Dir8.SW, "se", true);
            if (err != null) return err;
            err = ExpectResolve(IsoFacing.FiveDirMirrorWest, Dir8.N, "n", false);
            if (err != null) return err;
            return null;
        }

        static string CheckNullCamera()
        {
            if (IsoProjection.ScreenPixelToLogic(null, new Vector3(10f, 20f, 0f)) != Vector2.zero)
                return "null camera pixel must be logic zero";
            var quad = new Vector2[4];
            quad[0] = Vector2.one;
            IsoProjection.ViewQuadInLogic((Camera)null, quad);
            if (quad[0] != Vector2.zero || quad[3] != Vector2.zero)
                return "null camera quad must clear";
            IsoProjection.ViewQuadInLogic((Camera)null, null);
            return null;
        }

        static bool ExpectScreen(double deg, Dir8 dir)
        {
            return Facing(deg, SectorMode.Equal45) == dir
                && Facing(deg, SectorMode.DiamondEdge) == dir;
        }

        static Dir8 Facing(double deg, SectorMode mode)
        {
            return IsoFacing.FromScreen(ScreenDir(deg), Dir8.S, mode);
        }

        static Dir8 Facing(double deg, float half)
        {
            return IsoFacing.FromScreen(ScreenDir(deg), Dir8.S, half);
        }

        static Vector2 ScreenDir(double deg)
        {
            double r = deg * Math.PI / 180.0;
            return new Vector2((float)Math.Cos(r), (float)Math.Sin(r));
        }

        static string ExpectEight(string kindId)
        {
            if (IsoFacing.UsesWestMirror(kindId))
                return "eight-dir kind mirrored " + (kindId ?? "null");
            Dir8[] all = { Dir8.S, Dir8.SE, Dir8.E, Dir8.NE, Dir8.N, Dir8.NW, Dir8.W, Dir8.SW };
            for (int i = 0; i < all.Length; i++)
            {
                string err = ExpectResolve(kindId, all[i], IsoFacing.Suffix(all[i]), false);
                if (err != null)
                    return err;
            }
            return null;
        }

        static string ExpectFive(string kindId)
        {
            if (!IsoFacing.UsesWestMirror(kindId))
                return "five-dir kind not mirrored " + kindId;
            string err = ExpectResolve(kindId, Dir8.N, "n", false);
            if (err != null) return err;
            err = ExpectResolve(kindId, Dir8.NE, "ne", false);
            if (err != null) return err;
            err = ExpectResolve(kindId, Dir8.E, "e", false);
            if (err != null) return err;
            err = ExpectResolve(kindId, Dir8.SE, "se", false);
            if (err != null) return err;
            err = ExpectResolve(kindId, Dir8.S, "s", false);
            if (err != null) return err;
            err = ExpectResolve(kindId, Dir8.NW, "ne", true);
            if (err != null) return err;
            err = ExpectResolve(kindId, Dir8.W, "e", true);
            if (err != null) return err;
            err = ExpectResolve(kindId, Dir8.SW, "se", true);
            if (err != null) return err;
            return null;
        }

        static string ExpectResolve(string kindId, Dir8 dir, string suffix, bool flip)
        {
            string got;
            bool gotFlip;
            IsoFacing.Resolve(kindId, dir, out got, out gotFlip);
            if (got != suffix || gotFlip != flip)
                return "resolve " + (kindId ?? "null") + " " + dir;
            return null;
        }

        static string ExpectResolve(FacingSheet sheet, Dir8 dir, string suffix, bool flip)
        {
            string got;
            bool gotFlip;
            IsoFacing.Resolve(sheet, dir, out got, out gotFlip);
            if (got != suffix || gotFlip != flip)
                return "resolve sheet " + dir;
            return null;
        }

        // Keep const locks out of a constant expression so a passing build is not CS0162.
        static float Value(float v)
        {
            return v;
        }

        static bool Near(Vector2 a, Vector2 b)
        {
            return Math.Abs(a.x - b.x) <= 1e-4f && Math.Abs(a.y - b.y) <= 1e-4f;
        }
    }
}
