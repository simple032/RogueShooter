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
            err = CheckHysteresis();
            if (err != null) return err;
            err = CheckMirror();
            if (err != null) return err;
            err = CheckNullCamera();
            if (err != null) return err;
            return null;
        }

        public static string FormatPass()
        {
            return "ACCEPTANCE PASS iso-projection roundtrip+grid+sectors+hysteresis+mirror";
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
            if (Math.Abs(IsoFacing.SectorHalfDeg - 22.5) > 1e-9)
                return "sector half must be 22.5";
            if (IsoConfig.FacingMode != SectorMode.DiamondAligned)
                return "default facing mode must be diamond-aligned";
            if (Math.Abs(IsoConfig.DefaultFacingHysteresisDeg - 4f) > 0.001f)
                return "hysteresis placeholder must be 4 degrees TBD";
            double ne = IsoFacing.NeScreenDeg;
            if (Math.Abs(ne - (Math.Atan(0.5) * (180.0 / Math.PI))) > 1e-6)
                return "NE screen ray must be atan(0.5)";
            if (ne < 26.0 || ne > 27.0)
                return "NE screen ray must be about 26.565 deg";
            double eNe = ScreenDeg(IsoProjection.LogicDirToScreenDir(LogicOnDeg(-22.5)));
            double neN = ScreenDeg(IsoProjection.LogicDirToScreenDir(LogicOnDeg(22.5)));
            if (Math.Abs(eNe - 11.70) > 0.15)
                return "E|NE screen boundary must be about 11.7, not the 13.3 bisector";
            if (Math.Abs(neN - 50.36) > 0.2)
                return "NE|N screen boundary must be about 50.3, not the 58.3 bisector";
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
            SectorMode prev = IsoConfig.FacingMode;
            try
            {
                if (IsoFacing.FromLogic(Vector2.zero, Dir8.N) != Dir8.N)
                    return "zero logic dir must return fallback";
                if (IsoFacing.FromLogic(new Vector2(1e-5f, 0f), Dir8.S) != Dir8.S)
                    return "near-zero logic dir must return fallback";
                if (IsoFacing.FromScreen(Vector2.zero, Dir8.W) != Dir8.W)
                    return "zero screen dir must return fallback";

                IsoConfig.FacingMode = SectorMode.DiamondAligned;
                string err = ExpectDefaultRays();
                if (err != null)
                    return err;
                err = CheckAimBands();
                if (err != null)
                    return err;
                if (IsoFacing.FromLogic(new Vector2(1e-3f, 0f), Dir8.S) != Dir8.NE)
                    return "small non-zero +logic X is NE";

                // Logic boundary 22.5° (NE|N) belongs to N. Screen boundary 22.5° belongs to NE.
                if (IsoFacing.FromLogic(LogicOnDeg(22.5), Dir8.S) != Dir8.N)
                    return "logic boundary 22.5 must be N";
                if (IsoFacing.FromLogic(LogicOnDeg(22.3), Dir8.S) != Dir8.NE)
                    return "just inside logic NE";

                IsoConfig.FacingMode = SectorMode.EqualScreen45;
                if (IsoFacing.FromScreen(ScreenDir(0.0), Dir8.S) != Dir8.E)
                    return "equal-screen east";
                if (IsoFacing.FromScreen(ScreenDir(22.5), Dir8.S) != Dir8.NE)
                    return "equal-screen boundary 22.5 must be NE";
                if (IsoFacing.FromScreen(ScreenDir(22.3), Dir8.S) != Dir8.E)
                    return "just inside equal-screen E";
                if (IsoFacing.FromScreen(ScreenDir(90.0), Dir8.S) != Dir8.N)
                    return "equal-screen north";
                if (IsoFacing.FromScreen(ScreenDir(-90.0), Dir8.S) != Dir8.S)
                    return "equal-screen south";

                // Same arrow, two modes, via the one config setting.
                Vector2 screen20 = LogicFromScreenDeg(20.0);
                Vector2 screen60 = LogicFromScreenDeg(60.0);
                IsoConfig.FacingMode = SectorMode.EqualScreen45;
                if (IsoFacing.FromLogic(screen20, Dir8.S) != Dir8.E)
                    return "screen 20 equal-mode must be E";
                if (IsoFacing.FromLogic(screen60, Dir8.S) != Dir8.NE)
                    return "screen 60 equal-mode must be NE";
                IsoConfig.FacingMode = SectorMode.DiamondAligned;
                if (IsoFacing.FromLogic(screen20, Dir8.S) != Dir8.NE)
                    return "screen 20 diamond-mode must be NE";
                if (IsoFacing.FromLogic(screen60, Dir8.S) != Dir8.N)
                    return "screen 60 diamond-mode must be N";
                return null;
            }
            finally
            {
                IsoConfig.FacingMode = prev;
            }
        }

        static string ExpectDefaultRays()
        {
            if (IsoConfig.FacingMode != SectorMode.DiamondAligned)
                return "default ray test requires diamond mode";

            // Four diamond edges (logic axes) and four screen axes.
            if (Ray(new Vector2(1f, 0f), Dir8.NE)) return "diamond +X must be NE";
            if (Ray(new Vector2(0f, 1f), Dir8.NW)) return "diamond +Y must be NW";
            if (Ray(new Vector2(-1f, 0f), Dir8.SW)) return "diamond -X must be SW";
            if (Ray(new Vector2(0f, -1f), Dir8.SE)) return "diamond -Y must be SE";
            if (Ray(new Vector2(1f, -1f), Dir8.E)) return "screen +X must be E";
            if (Ray(new Vector2(1f, 1f), Dir8.N)) return "screen +Y must be N";
            if (Ray(new Vector2(-1f, 1f), Dir8.W)) return "screen -X must be W";
            if (Ray(new Vector2(-1f, -1f), Dir8.S)) return "screen -Y must be S";
            return null;
        }

        /// <summary>
        /// Hysteresis-free screen aim. NE is about 11.7°..50.3°, so 12°..50° is NE
        /// and 11° is still E. The screen bisectors (13.3° and 58.3°) are not used.
        /// The other rows are that same logic-space rule around the circle.
        /// </summary>
        static string CheckAimBands()
        {
            if (Aim(11.0, Dir8.E) || Aim(0.0, Dir8.E) || Aim(-10.0, Dir8.E))
                return "screen below 11 must be E";
            if (Aim(12.0, Dir8.NE) || Aim(30.0, Dir8.NE) || Aim(50.0, Dir8.NE))
                return "screen 12 to 50 must be NE";
            if (Aim(51.0, Dir8.N) || Aim(90.0, Dir8.N) || Aim(129.0, Dir8.N))
                return "screen just past 50.3 through 129 must be N";
            if (Aim(130.0, Dir8.NW) || Aim(168.0, Dir8.NW))
                return "screen 130 to 168 must be NW";
            if (Aim(169.0, Dir8.W) || Aim(180.0, Dir8.W) || Aim(191.0, Dir8.W))
                return "screen 169 to 191 must be W";
            if (Aim(192.0, Dir8.SW) || Aim(210.0, Dir8.SW) || Aim(230.0, Dir8.SW))
                return "screen 192 to 230 must be SW";
            if (Aim(231.0, Dir8.S) || Aim(270.0, Dir8.S) || Aim(309.0, Dir8.S) || Aim(-51.0, Dir8.S))
                return "screen 231 to 309 must be S";
            if (Aim(-50.0, Dir8.SE) || Aim(-12.0, Dir8.SE) || Aim(310.0, Dir8.SE) || Aim(348.0, Dir8.SE))
                return "screen -50 to -12 must be SE";
            return null;
        }

        static bool Aim(double screenDeg, Dir8 expect)
        {
            return IsoFacing.FromScreen(ScreenDir(screenDeg), Dir8.S) != expect;
        }

        static bool Ray(Vector2 logicDir, Dir8 expect)
        {
            if (IsoFacing.FromLogic(logicDir, Dir8.S) != expect)
                return true;
            Vector2 screen = IsoProjection.LogicDirToScreenDir(logicDir);
            return IsoFacing.FromScreen(screen, Dir8.S) != expect;
        }

        static string CheckHysteresis()
        {
            SectorMode prevMode = IsoConfig.FacingMode;
            float prevH = IsoConfig.FacingHysteresisDeg;
            try
            {
                IsoConfig.FacingMode = SectorMode.DiamondAligned;
                IsoConfig.FacingHysteresisDeg = IsoConfig.DefaultFacingHysteresisDeg;
                var sticky = new IsoFacingTracker();
                if (Math.Abs(sticky.HysteresisDeg - 4f) > 0.001f)
                    return "tracker must take the TBD 4 degree placeholder";

                // Sweep across the NE|N logic boundary (22.5°) and jitter inside the band.
                double[] stayNe = { 20.0, 22.4, 23.0, 24.0, 25.0, 26.0, 22.0, 24.5, 21.0 };
                for (int i = 0; i < stayNe.Length; i++)
                {
                    Dir8 got = sticky.Update(LogicOnDeg(stayNe[i]), Dir8.S);
                    if (got != Dir8.NE)
                        return "hysteresis must hold NE through " + stayNe[i];
                }

                if (IsoFacing.FromLogic(LogicOnDeg(23.0), Dir8.S) != Dir8.N)
                    return "pure FromLogic at 23 must already be N";
                if (IsoFacing.FromLogic(LogicOnDeg(21.0), Dir8.S) != Dir8.NE)
                    return "pure FromLogic at 21 must be NE";

                if (sticky.Update(LogicOnDeg(27.0), Dir8.S) != Dir8.N)
                    return "past 4 degrees of hysteresis must switch to N";
                if (IsoFacing.FromLogic(LogicOnDeg(20.0), Dir8.S) != Dir8.NE)
                    return "pure FromLogic at 20 must be NE";
                double[] stayN = { 26.0, 24.0, 23.0, 20.0, 19.5 };
                for (int i = 0; i < stayN.Length; i++)
                {
                    if (sticky.Update(LogicOnDeg(stayN[i]), Dir8.S) != Dir8.N)
                        return "hysteresis must hold N through " + stayN[i];
                }

                if (sticky.Update(LogicOnDeg(18.0), Dir8.S) != Dir8.NE)
                    return "crossing back past hysteresis must return to NE";
                if (sticky.Update(Vector2.zero, Dir8.W) != Dir8.NE)
                    return "near-zero must keep the held facing";

                var hair = new IsoFacingTracker(0f);
                if (hair.Update(LogicOnDeg(21.0), Dir8.S) != Dir8.NE)
                    return "zero hysteresis starts on NE";
                if (hair.Update(LogicOnDeg(23.0), Dir8.S) != Dir8.N)
                    return "zero hysteresis must flip at the boundary";
                if (hair.Update(LogicOnDeg(21.0), Dir8.S) != Dir8.NE)
                    return "zero hysteresis must flip back";
                return null;
            }
            finally
            {
                IsoConfig.FacingMode = prevMode;
                IsoConfig.FacingHysteresisDeg = prevH;
            }
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

        static Vector2 LogicOnDeg(double logicDeg)
        {
            double r = logicDeg * Math.PI / 180.0;
            return new Vector2((float)Math.Cos(r), (float)Math.Sin(r));
        }

        static Vector2 LogicFromScreenDeg(double screenDeg)
        {
            return IsoProjection.ScreenDirToLogicDir(ScreenDir(screenDeg));
        }

        static double ScreenDeg(Vector2 screen)
        {
            double a = Math.Atan2(screen.y, screen.x) * (180.0 / Math.PI);
            if (a < 0.0)
                a += 360.0;
            return a;
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
