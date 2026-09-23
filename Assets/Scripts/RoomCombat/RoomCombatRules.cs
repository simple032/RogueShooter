using System;
using UnityEngine;

namespace RogueShooter.RoomCombat
{
    /// <summary>
    /// GDD §4.5 pure rules: second-wave by room kind; foot-circle spacing = r_i + r_j.
    /// Does not invent a free-form min-gap number — only collision-circle non-overlap.
    /// </summary>
    public static class RoomCombatRules
    {
        /// <summary>Shared stub foot radius (ground projection). Spacing check uses sum of radii.</summary>
        public const float DefaultFootRadius = 0.32f;

        public static bool UsesRoomCombat(RoomCombatKind kind)
        {
            return kind == RoomCombatKind.Normal
                   || kind == RoomCombatKind.SmallChest
                   || kind == RoomCombatKind.Altar;
        }

        /// <summary>GDD table: 小宝箱房 / 祭坛房 only. Ordinary = one wave.</summary>
        public static bool WantsSecondWave(RoomCombatKind kind)
        {
            return kind == RoomCombatKind.SmallChest || kind == RoomCombatKind.Altar;
        }

        public static int ExpectedWaveCount(RoomCombatKind kind)
        {
            if (!UsesRoomCombat(kind))
                return 0;
            return WantsSecondWave(kind) ? 2 : 1;
        }

        /// <summary>
        /// True when every pair of foot circles on the ground projection does not intersect
        /// (center distance &gt;= r_i + r_j). Coincident points fail.
        /// </summary>
        public static bool FootCirclesClear(Vector2[] centers, float[] radii)
        {
            if (centers == null || centers.Length == 0)
                return true;
            if (radii == null || radii.Length != centers.Length)
                return false;

            for (int i = 0; i < centers.Length; i++)
            {
                float ri = radii[i];
                if (ri < 0.0001f)
                    return false;
                for (int j = i + 1; j < centers.Length; j++)
                {
                    float need = ri + radii[j];
                    float dx = centers[i].x - centers[j].x;
                    float dy = centers[i].y - centers[j].y;
                    float distSq = dx * dx + dy * dy;
                    if (distSq < need * need - 1e-6f)
                        return false;
                }
            }

            return true;
        }

        public static bool FootCirclesClear(Vector2[] centers, float radius)
        {
            if (centers == null)
                return true;
            var radii = new float[centers.Length];
            for (int i = 0; i < radii.Length; i++)
                radii[i] = radius;
            return FootCirclesClear(centers, radii);
        }

        /// <summary>Pack N points inside AABB with step &gt;= 2r (grid). Returns false if cannot fit.</summary>
        public static bool TryPackPoints(Rect bounds, int count, float radius, out Vector2[] points)
        {
            points = Array.Empty<Vector2>();
            if (count <= 0)
                return true;
            if (radius < 0.0001f)
                return false;

            float step = radius * 2f;
            float pad = radius;
            float x0 = bounds.xMin + pad;
            float y0 = bounds.yMin + pad;
            float x1 = bounds.xMax - pad;
            float y1 = bounds.yMax - pad;
            if (x1 < x0 || y1 < y0)
                return false;

            var list = new System.Collections.Generic.List<Vector2>(count);
            for (float y = y0; y <= y1 + 1e-4f && list.Count < count; y += step)
            {
                for (float x = x0; x <= x1 + 1e-4f && list.Count < count; x += step)
                    list.Add(new Vector2(x, y));
            }

            if (list.Count < count)
                return false;
            points = list.GetRange(0, count).ToArray();
            return FootCirclesClear(points, radius);
        }
    }
}
