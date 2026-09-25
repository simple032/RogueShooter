using System;
using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Vision;

namespace RogueShooter.Spawning
{
    /// <summary>One L5 spawn spot (logic space).</summary>
    public struct L5Spot
    {
        public float X;
        public float Y;
        public float DistPlayer;
        /// <summary>Distance outside the view quad (negative = inside). +∞ when no quad.</summary>
        public float OutsideQuad;
        /// <summary>True when no cell met the rule and the farthest walkable cell was used.</summary>
        public bool Fallback;
        /// <summary>Spawn warning before the unit appears (0 = spawn immediately).</summary>
        public float WarnSeconds;
    }

    /// <summary>
    /// L5 off-screen spawn rule (orthographic and isometric share it; values in <see cref="L5Rules"/>):
    /// a spot is a walkable cell of the current room, <see cref="L5Rules.SpawnMinU"/>–<see cref="L5Rules.SpawnMaxU"/>
    /// from the player, and at least <see cref="L5Rules.SpawnOutsideQuadU"/> outside the screen view quad
    /// (<see cref="ViewSpace.TryGetViewQuad"/>). No such cell → the farthest walkable cell in the room with a
    /// warning of <see cref="L5Rules.FallbackWarnFor"/> (1.0s; 1.5s when that cell is closer than SpawnMinU).
    /// Pure: the caller passes the player position and quad, so checks can drive it headless.
    /// </summary>
    public static class L5Spawn
    {
        /// <summary>Logic cell size (1 cell = 1u). Candidates are cell centres (integer + 0.5).</summary>
        public const float Cell = 1f;

        public static bool DistanceOk(float d)
        {
            return d >= L5Rules.SpawnMinU - 1e-4f && d <= L5Rules.SpawnMaxU + 1e-4f;
        }

        public static float OutsideQuad(Vector2[] quad, float x, float y)
        {
            if (quad == null)
                return float.PositiveInfinity;
            return ViewSpace.SignedOutside(quad, new Vector2(x, y));
        }

        /// <summary>The three L5 conditions minus walkability (checked by the caller's cell set).</summary>
        public static bool MeetsRule(float x, float y, float px, float py, Vector2[] quad)
        {
            float d = Dist(x, y, px, py);
            return DistanceOk(d) && OutsideQuad(quad, x, y) >= L5Rules.SpawnOutsideQuadU - 1e-4f;
        }

        /// <summary>Walkable cell centres inside the room (wall inset, site volumes, optional extra test).</summary>
        public static List<Vector2> WalkableCells(MazeNode room, SpawnAvoid[] avoids, Func<float, float, bool> walkable = null)
        {
            var cells = new List<Vector2>();
            if (room == null)
                return cells;
            float inset = CombatRoomSpawn.RoomInset;
            int x0 = Mathf.FloorToInt(room.Center.X - room.Width * 0.5f);
            int x1 = Mathf.CeilToInt(room.Center.X + room.Width * 0.5f);
            int y0 = Mathf.FloorToInt(room.Center.Y - room.Height * 0.5f);
            int y1 = Mathf.CeilToInt(room.Center.Y + room.Height * 0.5f);
            for (int ix = x0; ix < x1; ix++)
            {
                for (int iy = y0; iy < y1; iy++)
                {
                    float cx = ix + 0.5f * Cell;
                    float cy = iy + 0.5f * Cell;
                    if (!room.Contains(cx, cy, inset))
                        continue;
                    if (CombatRoomSpawn.HitsVolume(cx, cy, avoids))
                        continue;
                    if (walkable != null && !walkable(cx, cy))
                        continue;
                    cells.Add(new Vector2(cx, cy));
                }
            }

            return cells;
        }

        public static bool IsWalkableCell(MazeNode room, SpawnAvoid[] avoids, float x, float y)
        {
            if (room == null)
                return false;
            float fx = x - Mathf.Floor(x);
            float fy = y - Mathf.Floor(y);
            return Mathf.Abs(fx - 0.5f) < 0.01f && Mathf.Abs(fy - 0.5f) < 0.01f
                && room.Contains(x, y, CombatRoomSpawn.RoomInset)
                && !CombatRoomSpawn.HitsVolume(x, y, avoids);
        }

        public static L5Spot[] PlaceWave(
            MazeNode room, float px, float py, int n, SpawnAvoid[] avoids, Vector2[] quad, System.Random rng,
            Func<float, float, bool> walkable = null)
        {
            var spots = new L5Spot[Math.Max(0, n)];
            if (n <= 0 || room == null)
                return spots;
            List<Vector2> cells = WalkableCells(room, avoids, walkable);
            var occ = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                spots[i] = PickOne(cells, px, py, quad, occ, rng ?? new System.Random(1));
                occ.Add(new Vector2(spots[i].X, spots[i].Y));
            }

            return spots;
        }

        /// <summary>
        /// Re-validates spots at spawn time against the player/quad now (the player moves during the portal
        /// hold). A spot that still meets the rule keeps no warning; otherwise it is re-picked (random valid
        /// cell, else fallback + warning). Returns the number of spots moved.
        /// </summary>
        public static int EnforceAtSpawn(
            MazeNode room, float px, float py, L5Spot[] spots, SpawnAvoid[] avoids, Vector2[] quad, System.Random rng,
            Func<float, float, bool> walkable = null)
        {
            if (room == null || spots == null)
                return 0;
            List<Vector2> cells = WalkableCells(room, avoids, walkable);
            var occ = new List<Vector2>(spots.Length);
            int moved = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                float x = spots[i].X;
                float y = spots[i].Y;
                bool walk = IsWalkableCell(room, avoids, x, y) && (walkable == null || walkable(x, y));
                if (walk && MeetsRule(x, y, px, py, quad) && !TooClose(x, y, occ))
                {
                    spots[i] = Make(x, y, px, py, quad, false);
                }
                else
                {
                    spots[i] = PickOne(cells, px, py, quad, occ, rng ?? new System.Random(1));
                    moved++;
                }

                occ.Add(new Vector2(spots[i].X, spots[i].Y));
            }

            return moved;
        }

        static L5Spot PickOne(List<Vector2> cells, float px, float py, Vector2[] quad, List<Vector2> occ, System.Random rng)
        {
            int valid = 0;
            int pick = -1;
            int far = -1;
            float farD = -1f;
            int farAny = -1;
            float farAnyD = -1f;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 c = cells[i];
                float d = Dist(c.x, c.y, px, py);
                if (d > farAnyD)
                {
                    farAnyD = d;
                    farAny = i;
                }

                if (TooClose(c.x, c.y, occ))
                    continue;
                if (d > farD)
                {
                    farD = d;
                    far = i;
                }

                if (!MeetsRule(c.x, c.y, px, py, quad))
                    continue;
                valid++;
                // Reservoir sampling: uniform over valid cells without a second list.
                if (rng.Next(valid) == 0)
                    pick = i;
            }

            if (pick >= 0)
                return Make(cells[pick].x, cells[pick].y, px, py, quad, false);
            int fb = far >= 0 ? far : farAny;
            if (fb < 0)
                return new L5Spot { X = px, Y = py, Fallback = true, WarnSeconds = L5Rules.FallbackWarnNearSeconds };
            return Make(cells[fb].x, cells[fb].y, px, py, quad, true);
        }

        static L5Spot Make(float x, float y, float px, float py, Vector2[] quad, bool fallback)
        {
            float d = Dist(x, y, px, py);
            return new L5Spot
            {
                X = x,
                Y = y,
                DistPlayer = d,
                OutsideQuad = OutsideQuad(quad, x, y),
                Fallback = fallback,
                WarnSeconds = fallback ? L5Rules.FallbackWarnFor(d) : 0f
            };
        }

        static bool TooClose(float x, float y, List<Vector2> occ)
        {
            float min = CombatRoomSpawn.PackSep;
            for (int i = 0; i < occ.Count; i++)
            {
                if (Dist(x, y, occ[i].x, occ[i].y) < min)
                    return true;
            }

            return false;
        }

        static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx;
            float dy = ay - by;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
