using System;
using UnityEngine;
using RogueShooter.Spawning;

namespace RogueShooter.Iso
{
    /// <summary>
    /// Eight screen-space facings. 0 degrees is screen +X (viewer's right, East).
    /// Angles increase counter-clockwise. Facing is chosen from what the
    /// viewer sees, after <see cref="IsoProjection.LogicDirToScreenDir"/>.
    /// </summary>
    public enum Dir8
    {
        S = 0,
        SE = 1,
        E = 2,
        NE = 3,
        N = 4,
        NW = 5,
        W = 6,
        SW = 7
    }

    /// <summary>
    /// How wide the screen-cardinal sectors (E/N/W/S) are.
    /// <see cref="Equal45"/> is the default. <see cref="DiamondEdge"/> puts a
    /// boundary on the isometric diamond edge at atan(CellH/CellW)=atan(0.5).
    /// A custom half-angle can be passed instead; design has not locked this.
    /// </summary>
    public enum SectorMode
    {
        /// <summary>Equal 45° sectors. Boundary at 22.5° off each screen cardinal.</summary>
        Equal45 = 0,

        /// <summary>
        /// Cardinal sectors are 2*atan(0.5) wide (~53.13°). The +logic-X ray
        /// (screen angle atan(0.5) ≈ 26.565°) lies on the E|NE boundary.
        /// </summary>
        DiamondEdge = 1
    }

    /// <summary>
    /// Which authored frames a character sheet has.
    /// <see cref="MirrorWest"/> false: all 8 suffixes, never flip.
    /// true: N/NE/E/SE/S are authored; NW/W/SW reuse ne/e/se with flipX.
    /// </summary>
    public struct FacingSheet
    {
        public readonly bool MirrorWest;

        public FacingSheet(bool mirrorWest)
        {
            MirrorWest = mirrorWest;
        }
    }

    /// <summary>
    /// Screen-space facing and the art-direction table.
    /// Suffixes are lowercase and match STYLE_SPEC §3.3 / landed frames:
    /// <c>jh_char_archer_walk_s_03.png</c> → suffix <c>s</c>.
    /// <see cref="ArtFrameId"/> is the same string
    /// <c>JianHaiSprites.ResolveClipArt</c> tries first (<c>root_dir_ff</c>).
    /// Current enemy PNGs omit the direction token; this table is the iso
    /// contract, and it does not change how those clips resolve today.
    /// flipX is SpriteRenderer.flipX (horizontal mirror). Authored east-side
    /// art (e/ne/se) flipped becomes w/nw/sw.
    ///
    /// Sector tie-break: each boundary ray belongs to the counter-clockwise
    /// sector. Cardinal sector E is [−half, +half). Snap within 1e-4° so a
    /// float reconstruction of the boundary stays on that ray.
    /// Diamond-edge mode: +logic X (the right-hand diamond edge) → NE;
    /// +logic Y (the left-hand diamond edge) → W.
    /// </summary>
    public static class IsoFacing
    {
        /// <summary>sqrMagnitude below this (length under 1e-4) is no direction.</summary>
        public const float DirEpsilonSqr = 1e-8f;

        /// <summary>Half-width of a screen-cardinal sector in <see cref="SectorMode.Equal45"/>.</summary>
        public const double Equal45BoundaryDeg = 22.5;

        /// <summary>Archer / player sheet id. All 8 directions, no mirror.</summary>
        public const string KindArcher = "archer";

        public static readonly FacingSheet EightDirNoMirror = new FacingSheet(false);
        public static readonly FacingSheet FiveDirMirrorWest = new FacingSheet(true);

        /// <summary>
        /// atan(CellH/CellW) in degrees ≈ 26.565051177°. Half-width of the
        /// screen-cardinal sectors in <see cref="SectorMode.DiamondEdge"/>.
        /// </summary>
        public static double DiamondEdgeBoundaryDeg
        {
            get { return Math.Atan(IsoProjection.CellH / IsoProjection.CellW) * (180.0 / Math.PI); }
        }

        /// <summary>Default sectors: equal 45°. Near-zero <paramref name="logicDir"/> returns <paramref name="fallback"/>.</summary>
        public static Dir8 FromLogic(Vector2 logicDir, Dir8 fallback)
        {
            return FromLogic(logicDir, fallback, SectorMode.Equal45);
        }

        public static Dir8 FromLogic(Vector2 logicDir, Dir8 fallback, SectorMode mode)
        {
            if (logicDir.sqrMagnitude < DirEpsilonSqr)
                return fallback;
            return FromScreenVector(IsoProjection.LogicDirToScreenDir(logicDir), HalfAngle(mode));
        }

        /// <summary>
        /// <paramref name="cardinalHalfAngleDeg"/> is the half-width of E/N/W/S
        /// in screen degrees. Values outside (0, 45) are clamped to [0.001, 44.999].
        /// Diagonal sectors receive the remaining angle (90 − 2*half each).
        /// </summary>
        public static Dir8 FromLogic(Vector2 logicDir, Dir8 fallback, float cardinalHalfAngleDeg)
        {
            if (logicDir.sqrMagnitude < DirEpsilonSqr)
                return fallback;
            return FromScreenVector(IsoProjection.LogicDirToScreenDir(logicDir), cardinalHalfAngleDeg);
        }

        public static Dir8 FromScreen(Vector2 screenDir, Dir8 fallback)
        {
            return FromScreen(screenDir, fallback, SectorMode.Equal45);
        }

        public static Dir8 FromScreen(Vector2 screenDir, Dir8 fallback, SectorMode mode)
        {
            if (screenDir.sqrMagnitude < DirEpsilonSqr)
                return fallback;
            return FromScreenVector(screenDir, HalfAngle(mode));
        }

        public static Dir8 FromScreen(Vector2 screenDir, Dir8 fallback, float cardinalHalfAngleDeg)
        {
            if (screenDir.sqrMagnitude < DirEpsilonSqr)
                return fallback;
            return FromScreenVector(screenDir, cardinalHalfAngleDeg);
        }

        /// <summary>Lowercase art token: s, se, e, ne, n, nw, w, sw. Not the mirrored token.</summary>
        public static string Suffix(Dir8 dir)
        {
            switch (dir)
            {
                case Dir8.S: return "s";
                case Dir8.SE: return "se";
                case Dir8.E: return "e";
                case Dir8.NE: return "ne";
                case Dir8.N: return "n";
                case Dir8.NW: return "nw";
                case Dir8.W: return "w";
                case Dir8.SW: return "sw";
                default: return "s";
            }
        }

        /// <summary>
        /// <c>root_dir_ff</c>, the first id <c>JianHaiSprites.ResolveClipArt</c> looks up.
        /// Frame 3 → <c>03</c>. Example: <c>jh_char_archer_walk_s_03</c>.
        /// </summary>
        public static string ArtFrameId(string clipRoot, string dirSuffix, int frame)
        {
            int f = frame < 0 ? 0 : frame;
            string ff = f < 10 ? "0" + f : f.ToString();
            if (string.IsNullOrEmpty(clipRoot))
                return (dirSuffix ?? "") + "_" + ff;
            if (string.IsNullOrEmpty(dirSuffix))
                return clipRoot + "_" + ff;
            return clipRoot + "_" + dirSuffix + "_" + ff;
        }

        public static void Resolve(string kindId, Dir8 dir, out string dirSuffix, out bool flipX)
        {
            Resolve(SheetFor(kindId), dir, out dirSuffix, out flipX);
        }

        public static void Resolve(FacingSheet sheet, Dir8 dir, out string dirSuffix, out bool flipX)
        {
            if (!sheet.MirrorWest)
            {
                dirSuffix = Suffix(dir);
                flipX = false;
                return;
            }

            switch (dir)
            {
                case Dir8.NW:
                    dirSuffix = "ne";
                    flipX = true;
                    return;
                case Dir8.W:
                    dirSuffix = "e";
                    flipX = true;
                    return;
                case Dir8.SW:
                    dirSuffix = "se";
                    flipX = true;
                    return;
                default:
                    dirSuffix = Suffix(dir);
                    flipX = false;
                    return;
            }
        }

        /// <summary>
        /// True for the five-direction enemy sheets (骸兵 E1, 猎犬 E2, 法师 E3,
        /// plus SHIELD / GRAND, which share that sheet). Unknown ids, including
        /// the archer, return false (8 dirs, no flip).
        /// </summary>
        public static bool UsesWestMirror(string kindId)
        {
            if (string.IsNullOrEmpty(kindId))
                return false;
            if (Eq(kindId, EnemyKindIds.Normal) || Eq(kindId, "skel") || Eq(kindId, "e1_skel")
                || Eq(kindId, "jh_enemy_e1_skel") || kindId == "骸兵" || kindId == "普通小怪")
                return true;
            if (Eq(kindId, EnemyKindIds.Dog) || Eq(kindId, "dog") || Eq(kindId, "jh_enemy_dog")
                || kindId == "猎犬" || kindId == "狗")
                return true;
            if (Eq(kindId, EnemyKindIds.CultMage) || Eq(kindId, "mage") || Eq(kindId, "jh_enemy_mage")
                || kindId == "法师" || kindId == "邪法师")
                return true;
            if (Eq(kindId, EnemyKindIds.Shield) || kindId == "盾兵")
                return true;
            if (Eq(kindId, EnemyKindIds.GrandMage) || Eq(kindId, "grand") || kindId == "大邪术师")
                return true;
            return false;
        }

        public static FacingSheet SheetFor(string kindId)
        {
            return UsesWestMirror(kindId) ? FiveDirMirrorWest : EightDirNoMirror;
        }

        static Dir8 FromScreenVector(Vector2 screenDir, double halfAngleDeg)
        {
            double rad = Math.Atan2(screenDir.y, screenDir.x);
            double deg = rad * (180.0 / Math.PI);
            return FromAngleDeg(deg, halfAngleDeg);
        }

        static double HalfAngle(SectorMode mode)
        {
            if (mode == SectorMode.DiamondEdge)
                return DiamondEdgeBoundaryDeg;
            return Equal45BoundaryDeg;
        }

        static Dir8 FromAngleDeg(double degrees, double halfAngleDeg)
        {
            double half = halfAngleDeg;
            if (half < 0.001)
                half = 0.001;
            else if (half > 44.999)
                half = 44.999;

            double a = degrees;
            if (a < 0.0)
                a += 360.0;
            if (a >= 360.0)
                a -= 360.0;
            a = SnapToBoundary(a, half);

            // CCW half-open: the boundary ray belongs to the next sector.
            if (a >= 360.0 - half || a < half)
                return Dir8.E;
            if (a < 90.0 - half)
                return Dir8.NE;
            if (a < 90.0 + half)
                return Dir8.N;
            if (a < 180.0 - half)
                return Dir8.NW;
            if (a < 180.0 + half)
                return Dir8.W;
            if (a < 270.0 - half)
                return Dir8.SW;
            if (a < 270.0 + half)
                return Dir8.S;
            return Dir8.SE;
        }

        const double BoundarySnapDeg = 1e-4;

        static double SnapToBoundary(double a, double half)
        {
            if (Near(a, half)) return half;
            if (Near(a, 90.0 - half)) return 90.0 - half;
            if (Near(a, 90.0 + half)) return 90.0 + half;
            if (Near(a, 180.0 - half)) return 180.0 - half;
            if (Near(a, 180.0 + half)) return 180.0 + half;
            if (Near(a, 270.0 - half)) return 270.0 - half;
            if (Near(a, 270.0 + half)) return 270.0 + half;
            if (Near(a, 360.0 - half)) return 360.0 - half;
            return a;
        }

        static bool Near(double a, double b)
        {
            double d = a - b;
            if (d < 0.0)
                d = -d;
            return d <= BoundarySnapDeg;
        }

        static bool Eq(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
