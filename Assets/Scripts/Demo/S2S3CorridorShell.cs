using System.Collections.Generic;
using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Junction-aware corridor walls for the hand-authored S2/S3 layouts.
    /// Stage1IsoArt.BuildCorridorShell paints per-leg without corner clipping,
    /// which the all-straight S1 templates never exercise; the S2/S3 layouts
    /// turn and fork everywhere, so walls are derived from the merged segments:
    /// L-turns wrap the outer wall around the corner, open the inner mouth and
    /// backfill the corner block; T-branches gap the trunk wall for the branch
    /// mouth (a horizontal branch also carves its masonry band through the gap)
    /// and trim the branch walls to the trunk's flank; ends that touch a room
    /// stay flush with the room's own walls.
    /// </summary>
    public static class S2S3CorridorShell
    {
        const float HW = 4f;          // corridor half width
        const float WallBand = 0.55f; // cutaway thickness
        const float Key = HW + WallBand; // 4.55, outer wall line
        const float MasonryH = 1.45f;    // north masonry face height

        static readonly Color CutawayColor = new Color(.20f, .24f, .28f);
        static readonly Color HighlightColor = new Color(.30f, .34f, .37f);
        static readonly Color JointColor = new Color(.10f, .13f, .16f);
        static readonly Color CorniceColor = new Color(.27f, .31f, .35f);
        static readonly Color FootColor = new Color(.11f, .14f, .17f);
        static readonly Color PlinthColor = new Color(.16f, .20f, .24f);
        static readonly Color PlinthTopColor = new Color(.29f, .33f, .36f);

        const int CutawayOrder = -30, HighlightOrder = -29, JointOrder = -28, BayOrder = -16,
            CorniceOrder = -14, CorniceShadowOrder = -13, FootOrder = -12,
            PlinthOrder = -11, PlinthTopOrder = -10;

        public static void Build(S2S3Layout layout, Transform parent, List<GameObject> into)
        {
            List<S2S3Seg> segs = S2S3Layouts.Segments(layout);
            var fills = new List<Vector3>();
            for (int i = 0; i < segs.Count; i++)
            {
                S2S3Seg seg = segs[i];
                if (seg.Vertical)
                    BuildVertical(seg, parent, into);
                else
                    BuildHorizontal(seg, fills, parent, into);
            }
            for (int i = 0; i < fills.Count; i++)
                Stage1IsoArt.Band("Corridor corner block", fills[i], new Vector2(Key, HW),
                    CutawayColor, CutawayOrder, parent, into);
        }

        static void BuildHorizontal(S2S3Seg seg, List<Vector3> fills, Transform parent, List<GameObject> into)
        {
            float y = seg.Line;
            float nLo = seg.Lo, nHi = seg.Hi;
            float sLo = seg.Lo, sHi = seg.Hi;
            var northGaps = new List<Vector2>();
            var southGaps = new List<Vector2>();

            // West end: a corner wrapping south opens the plinth and stretches the
            // masonry over the corner; a corner continuing north gaps the masonry
            // and wraps the plinth instead. A T-branch end stops at the trunk flank.
            S2S3End end = seg.LoEnd;
            if (end.Kind == S2S3EndKind.Corner)
            {
                if (end.Dir == 'S')
                {
                    nLo = seg.Lo - Key;
                    sLo = seg.Lo + Key;
                    fills.Add(new Vector3(seg.Lo - Key, y, 0f));
                }
                else
                {
                    nLo = seg.Lo + Key;
                    sLo = seg.Lo - Key;
                    fills.Add(new Vector3(seg.Lo - Key, y - HW, 0f));
                }
            }
            else if (end.Kind == S2S3EndKind.JoinInto)
            {
                if (end.Dir == 'E') { nLo = seg.Lo + HW; sLo = seg.Lo + HW; }
                else { nLo = seg.Lo - HW; sLo = seg.Lo - HW; }
            }

            end = seg.HiEnd;
            if (end.Kind == S2S3EndKind.Corner)
            {
                if (end.Dir == 'S')
                {
                    nHi = seg.Hi + Key;
                    sHi = seg.Hi - Key;
                    fills.Add(new Vector3(seg.Hi, y, 0f));
                }
                else
                {
                    nHi = seg.Hi - Key;
                    sHi = seg.Hi + Key;
                    fills.Add(new Vector3(seg.Hi, y - HW, 0f));
                }
            }
            else if (end.Kind == S2S3EndKind.JoinInto)
            {
                if (end.Dir == 'E') { nHi = seg.Hi + HW; sHi = seg.Hi + HW; }
                else { nHi = seg.Hi - HW; sHi = seg.Hi - HW; }
            }

            for (int i = 0; i < seg.Joins.Count; i++)
            {
                S2S3Join join = seg.Joins[i];
                var gaps = join.From == 'S' ? southGaps : northGaps;
                gaps.Add(new Vector2(join.At - Key, join.At + Key));
            }

            List<Vector2> north = Intervals(nLo, nHi, northGaps);
            for (int i = 0; i < north.Count; i++)
            {
                float a = north[i].x, b = north[i].y;
                Stage1IsoArt.MasonryBays("Corridor north masonry", a, y + HW, b - a, parent, into);
                Stage1IsoArt.Band("Corridor cornice", new Vector3(a - .15f, y + HW + MasonryH, 0f),
                    new Vector2(b - a + .3f, .19f), CorniceColor, CorniceOrder, parent, into);
                Stage1IsoArt.Band("Corridor cornice shadow", new Vector3(a, y + HW + MasonryH - .10f, 0f),
                    new Vector2(b - a, .09f), JointColor, CorniceShadowOrder, parent, into);
                Stage1IsoArt.Band("Corridor foot", new Vector3(a, y + HW - .17f, 0f),
                    new Vector2(b - a, .18f), FootColor, FootOrder, parent, into);
            }
            List<Vector2> south = Intervals(sLo, sHi, southGaps);
            for (int i = 0; i < south.Count; i++)
            {
                float a = south[i].x, b = south[i].y;
                Stage1IsoArt.Band("Corridor plinth", new Vector3(a, y - Key + 0.10f, 0f),
                    new Vector2(b - a, .45f), PlinthColor, PlinthOrder, parent, into);
                Stage1IsoArt.Band("Corridor plinth top", new Vector3(a, y - HW - .08f, 0f),
                    new Vector2(b - a, .10f), PlinthTopColor, PlinthTopOrder, parent, into);
            }
        }

        static void BuildVertical(S2S3Seg seg, Transform parent, List<GameObject> into)
        {
            float x = seg.Line;
            float wLo = seg.Lo, wHi = seg.Hi;
            float eLo = seg.Lo, eHi = seg.Hi;
            var westGaps = new List<Vector2>();
            var eastGaps = new List<Vector2>();

            // South end: the cutaway on the far side of the turn extends to meet
            // the partner's masonry; the near side stops at its plinth line.
            S2S3End end = seg.LoEnd;
            if (end.Kind == S2S3EndKind.Corner)
            {
                if (end.Dir == 'E') { wLo = seg.Lo - HW; eLo = seg.Lo + HW; }
                else { eLo = seg.Lo - HW; wLo = seg.Lo + HW; }
            }
            else if (end.Kind == S2S3EndKind.JoinInto)
            {
                wLo = seg.Lo + HW;
                eLo = seg.Lo + HW;
            }

            end = seg.HiEnd;
            if (end.Kind == S2S3EndKind.Corner)
            {
                if (end.Dir == 'E') { wHi = seg.Hi + HW; eHi = seg.Hi - HW; }
                else { eHi = seg.Hi + HW; wHi = seg.Hi - HW; }
            }
            else if (end.Kind == S2S3EndKind.JoinInto)
            {
                wHi = seg.Hi - HW;
                eHi = seg.Hi - HW;
            }

            for (int i = 0; i < seg.Joins.Count; i++)
            {
                S2S3Join join = seg.Joins[i];
                // A horizontal branch pushes its masonry band through the trunk
                // wall, so the gap grows by the masonry height on the north side.
                var gaps = join.From == 'W' ? westGaps : eastGaps;
                gaps.Add(new Vector2(join.At - Key, join.At + HW + MasonryH));
            }

            List<Vector2> west = Intervals(wLo, wHi, westGaps);
            for (int i = 0; i < west.Count; i++)
            {
                float a = west[i].x, b = west[i].y;
                Stage1IsoArt.Band("Corridor west cutaway", new Vector3(x - Key, a, 0f),
                    new Vector2(WallBand, b - a), CutawayColor, CutawayOrder, parent, into);
                Stage1IsoArt.Band("Corridor west highlight", new Vector3(x - Key, a, 0f),
                    new Vector2(.10f, b - a), HighlightColor, HighlightOrder, parent, into);
                Stage1IsoArt.Joints("Corridor west joint", x - Key, a, Mathf.FloorToInt(b - a), parent, into);
            }
            List<Vector2> east = Intervals(eLo, eHi, eastGaps);
            for (int i = 0; i < east.Count; i++)
            {
                float a = east[i].x, b = east[i].y;
                Stage1IsoArt.Band("Corridor east cutaway", new Vector3(x + HW, a, 0f),
                    new Vector2(WallBand, b - a), CutawayColor, CutawayOrder, parent, into);
                Stage1IsoArt.Band("Corridor east highlight", new Vector3(x + HW, a, 0f),
                    new Vector2(.10f, b - a), HighlightColor, HighlightOrder, parent, into);
                Stage1IsoArt.Joints("Corridor east joint", x + HW, a, Mathf.FloorToInt(b - a), parent, into);
            }
        }

        /// <summary>Base span minus the listed gaps, as draw intervals.</summary>
        static List<Vector2> Intervals(float lo, float hi, List<Vector2> gaps)
        {
            var result = new List<Vector2>();
            if (hi - lo < 0.05f)
                return result;
            if (gaps == null || gaps.Count == 0)
            {
                result.Add(new Vector2(lo, hi));
                return result;
            }
            gaps.Sort((a, b) => a.x.CompareTo(b.x));
            float cur = lo;
            for (int i = 0; i < gaps.Count; i++)
            {
                float a = Mathf.Max(lo, gaps[i].x);
                float b = Mathf.Min(hi, gaps[i].y);
                if (b <= cur)
                    continue;
                if (a > cur + 0.05f)
                    result.Add(new Vector2(cur, a));
                if (b > cur)
                    cur = b;
                if (cur >= hi)
                    break;
            }
            if (cur < hi - 0.05f)
                result.Add(new Vector2(cur, hi));
            return result;
        }
    }
}
