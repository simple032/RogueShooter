using System;
using UnityEngine;
using RogueShooter.Spawning;

namespace RogueShooter.Iso
{
    /// <summary>
    /// Eight facing labels. The name is what the viewer sees (screen compass),
    /// not the logic-axis name. 0 degrees on screen is +X (viewer's right, East).
    /// See <see cref="IsoFacing"/> for which logic ray maps to each label.
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
    /// Selected only by <see cref="IsoConfig.FacingMode"/>. Callers do not pass a mode.
    /// </summary>
    public enum SectorMode
    {
        /// <summary>
        /// Default. Art directions are 45° steps in logic space. On screen those
        /// eight rays are the horizontal and vertical axes plus the four diamond
        /// edges, so the gaps alternate about 26.6° and 63.4°. Classification
        /// inverse-projects a screen direction and splits at equal 45° in logic space.
        /// </summary>
        DiamondAligned = 0,

        /// <summary>Equal 45° sectors in screen space. Boundaries at 22.5° + k*45°.</summary>
        EqualScreen45 = 1
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
    /// Facing labels and the art-direction table. The active split is
    /// <see cref="IsoConfig.FacingMode"/> (default <see cref="SectorMode.DiamondAligned"/>).
    /// <see cref="FromLogic"/> and <see cref="FromScreen"/> are hysteresis-free.
    /// Stickiness lives on <see cref="IsoFacingTracker"/>.
    ///
    /// Diamond-aligned map (logic angle is atan2(y, x), 0 = +logic X, CCW).
    /// The label is the screen compass of that ray:
    /// logic (1, -1) / -45° → screen +X → E;
    /// logic (1, 0) / 0° → diamond up-right (~26.6°) → NE;
    /// logic (1, 1) / 45° → screen +Y → N;
    /// logic (0, 1) / 90° → diamond up-left (~153.4°) → NW;
    /// logic (-1, 1) / 135° → screen -X → W;
    /// logic (-1, 0) / 180° → diamond down-left (~-153.4°) → SW;
    /// logic (-1, -1) / -135° → screen -Y → S;
    /// logic (0, -1) / -90° → diamond down-right (~-26.6°) → SE.
    /// +logic X is NE, not East. Screen East is the horizontal axis.
    /// Gaps on screen: ~26.6° from E to NE (and W to SW, and the matching
    /// pairs), ~63.4° from NE to N (and N to NW, and the matching pairs).
    /// The boundary ray belongs to the counter-clockwise sector.
    ///
    /// Suffixes are lowercase and match STYLE_SPEC §3.3 / landed frames:
    /// <c>jh_char_archer_walk_s_03.png</c> → suffix <c>s</c>.
    /// <see cref="ArtFrameId"/> is the same string
    /// <c>JianHaiSprites.ResolveClipArt</c> tries first (<c>root_dir_ff</c>).
    /// Current enemy PNGs omit the direction token; this table is the iso
    /// contract, and it does not change how those clips resolve today.
    /// flipX is SpriteRenderer.flipX (horizontal mirror). Authored east-side
    /// art (e/ne/se) flipped becomes w/nw/sw.
    /// </summary>
    public static class IsoFacing
    {
        /// <summary>sqrMagnitude below this (length under 1e-4) is no direction.</summary>
        public const float DirEpsilonSqr = 1e-8f;

        /// <summary>Half-width of one sector in the active angle space (logic or screen).</summary>
        public const double SectorHalfDeg = 22.5;

        /// <summary>Archer / player sheet id. All 8 directions, no mirror.</summary>
        public const string KindArcher = "archer";

        public static readonly FacingSheet EightDirNoMirror = new FacingSheet(false);
        public static readonly FacingSheet FiveDirMirrorWest = new FacingSheet(true);

        /// <summary>
        /// Screen angle of +logic X (the NE ray), atan(CellH/CellW) degrees, about 26.565.
        /// Screen North is 90°, so the NE-to-N gap is about 63.435°.
        /// </summary>
        public static double NeScreenDeg
        {
            get { return Math.Atan(IsoProjection.CellH / IsoProjection.CellW) * (180.0 / Math.PI); }
        }

        /// <summary>
        /// Hysteresis-free. Near-zero <paramref name="logicDir"/> returns <paramref name="fallback"/>.
        /// Uses <see cref="IsoConfig.FacingMode"/>.
        /// </summary>
        public static Dir8 FromLogic(Vector2 logicDir, Dir8 fallback)
        {
            if (logicDir.sqrMagnitude < DirEpsilonSqr)
                return fallback;
            if (IsoConfig.FacingMode == SectorMode.EqualScreen45)
                return ClassifyCompass(ScreenAngleDeg(IsoProjection.LogicDirToScreenDir(logicDir)));
            return ClassifyCompass(LogicAngleDeg(logicDir) + 45.0);
        }

        /// <summary>
        /// Hysteresis-free. In diamond-aligned mode the screen direction is
        /// inverse-projected, then split at equal 45° in logic space.
        /// Near-zero <paramref name="screenDir"/> returns <paramref name="fallback"/>.
        /// </summary>
        public static Dir8 FromScreen(Vector2 screenDir, Dir8 fallback)
        {
            if (screenDir.sqrMagnitude < DirEpsilonSqr)
                return fallback;
            if (IsoConfig.FacingMode == SectorMode.EqualScreen45)
                return ClassifyCompass(ScreenAngleDeg(screenDir));
            Vector2 logic = IsoProjection.ScreenDirToLogicDir(screenDir);
            if (logic.sqrMagnitude < DirEpsilonSqr)
                return fallback;
            return ClassifyCompass(LogicAngleDeg(logic) + 45.0);
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

        static double LogicAngleDeg(Vector2 logicDir)
        {
            return Math.Atan2(logicDir.y, logicDir.x) * (180.0 / Math.PI);
        }

        static double ScreenAngleDeg(Vector2 screenDir)
        {
            return Math.Atan2(screenDir.y, screenDir.x) * (180.0 / Math.PI);
        }

        /// <summary>
        /// Compass angle used by both modes: screen atan2, or logic atan2 + 45
        /// so logic 0° (+X, the NE ray) lands on the NE compass center (45°).
        /// </summary>
        internal static double CompassDeg(Vector2 logicDir, SectorMode mode)
        {
            if (mode == SectorMode.EqualScreen45)
                return ScreenAngleDeg(IsoProjection.LogicDirToScreenDir(logicDir));
            return LogicAngleDeg(logicDir) + 45.0;
        }

        internal static bool ExceedsHeldBoundary(Vector2 logicDir, Dir8 held, float hysteresisDeg, SectorMode mode)
        {
            double past = AngularDistance(CompassDeg(logicDir, mode), CompassCenter(held)) - SectorHalfDeg;
            if (past < 0.0)
                past = 0.0;
            double h = hysteresisDeg < 0f ? 0.0 : hysteresisDeg;
            return past >= h;
        }

        static double CompassCenter(Dir8 dir)
        {
            switch (dir)
            {
                case Dir8.E: return 0.0;
                case Dir8.NE: return 45.0;
                case Dir8.N: return 90.0;
                case Dir8.NW: return 135.0;
                case Dir8.W: return 180.0;
                case Dir8.SW: return 225.0;
                case Dir8.S: return 270.0;
                default: return 315.0;
            }
        }

        static double AngularDistance(double angleDeg, double centerDeg)
        {
            double d = angleDeg - centerDeg;
            d = d % 360.0;
            if (d < 0.0)
                d += 360.0;
            if (d > 180.0)
                d = 360.0 - d;
            return d;
        }

        static Dir8 ClassifyCompass(double degrees)
        {
            const double half = SectorHalfDeg;
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

    /// <summary>
    /// Per-caller facing with boundary hysteresis. One instance per character
    /// (a class, so a copy does not drop the held direction).
    /// <see cref="IsoFacing.FromLogic"/> stays hysteresis-free.
    /// The held direction changes only after the angle passes the sector
    /// boundary by <see cref="HysteresisDeg"/>. That angle is logic degrees
    /// in diamond-aligned mode and screen degrees in equal-screen mode.
    /// A near-zero vector keeps the held facing.
    /// </summary>
    public sealed class IsoFacingTracker
    {
        public Dir8 Current { get; private set; }
        public bool HasFacing { get; private set; }

        /// <summary>Degrees past the boundary required to switch. Negative is treated as 0.</summary>
        public float HysteresisDeg { get; set; }

        SectorMode _mode;

        /// <summary>Uses <see cref="IsoConfig.FacingHysteresisDeg"/> (TBD placeholder, default 4).</summary>
        public IsoFacingTracker()
            : this(IsoConfig.FacingHysteresisDeg)
        {
        }

        public IsoFacingTracker(float hysteresisDeg)
        {
            HysteresisDeg = hysteresisDeg;
            _mode = IsoConfig.FacingMode;
        }

        public Dir8 Update(Vector2 logicDir, Dir8 fallback)
        {
            SectorMode mode = IsoConfig.FacingMode;
            if (logicDir.sqrMagnitude < IsoFacing.DirEpsilonSqr)
            {
                if (!HasFacing)
                {
                    Current = fallback;
                    HasFacing = true;
                    _mode = mode;
                }
                return Current;
            }

            Dir8 raw = IsoFacing.FromLogic(logicDir, fallback);
            if (!HasFacing || mode != _mode)
            {
                Current = raw;
                HasFacing = true;
                _mode = mode;
                return Current;
            }

            if (raw != Current && IsoFacing.ExceedsHeldBoundary(logicDir, Current, HysteresisDeg, mode))
                Current = raw;
            return Current;
        }
    }
}
