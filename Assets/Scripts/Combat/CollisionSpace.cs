using System.Collections.Generic;

namespace RogueShooter.Combat
{
    public struct CollisionEntry
    {
        public CollisionAabb Box;
        public CollisionLayer Layer;
        public bool Solid;
        public string Name;
        public int Id;
    }

    public struct CollisionHit
    {
        public bool Hit;
        public CollisionLayer Layer;
        public string Name;
        public float X;
        public float Y;
        public float Distance;
    }

    /// <summary>Unity-free AABB world used by Play Mode and ACCEPTANCE.</summary>
    public sealed class CollisionSpace
    {
        readonly List<CollisionEntry> _entries = new List<CollisionEntry>();
        int _nextId = 1;

        public int Count => _entries.Count;

        public IReadOnlyList<CollisionEntry> Entries => _entries;

        public void Clear()
        {
            _entries.Clear();
        }

        public int Add(CollisionAabb box, CollisionLayer layer, bool solid, string name)
        {
            var e = new CollisionEntry
            {
                Box = box,
                Layer = layer,
                Solid = solid,
                Name = name ?? "",
                Id = _nextId++
            };
            _entries.Add(e);
            return e.Id;
        }

        public bool Overlaps(CollisionAabb box, CollisionLayer mask, bool solidsOnly)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                CollisionEntry e = _entries[i];
                if ((e.Layer & mask) == 0)
                    continue;
                if (solidsOnly && !e.Solid)
                    continue;
                if (e.Box.Overlaps(box))
                    return true;
            }

            return false;
        }

        public bool TryFirstOverlap(
            CollisionAabb box, CollisionLayer mask, bool solidsOnly, out CollisionEntry hit)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                CollisionEntry e = _entries[i];
                if ((e.Layer & mask) == 0)
                    continue;
                if (solidsOnly && !e.Solid)
                    continue;
                if (!e.Box.Overlaps(box))
                    continue;
                hit = e;
                return true;
            }

            hit = default(CollisionEntry);
            return false;
        }

        public bool TryMove(ref float x, ref float y, float hx, float hy, float dx, float dy, CollisionLayer solidMask)
        {
            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (dist < 0.00001f)
                return false;

            float limit = hx < hy ? hx : hy;
            if (limit < 0.08f)
                limit = 0.08f;
            int steps = (int)System.Math.Ceiling(dist / limit);
            if (steps < 1)
                steps = 1;
            if (steps > 48)
                steps = 48;

            float x0 = x;
            float y0 = y;
            bool moved = false;
            for (int i = 1; i <= steps; i++)
            {
                float nx = x0 + dx * i / steps;
                float ny = y0 + dy * i / steps;
                var box = new CollisionAabb(nx, ny, hx, hy);
                if (!Overlaps(box, solidMask, true))
                {
                    x = nx;
                    y = ny;
                    moved = true;
                    continue;
                }

                var xOnly = new CollisionAabb(nx, y, hx, hy);
                if (dx != 0f && !Overlaps(xOnly, solidMask, true))
                {
                    x = nx;
                    moved = true;
                }

                var yOnly = new CollisionAabb(x, ny, hx, hy);
                if (dy != 0f && !Overlaps(yOnly, solidMask, true))
                {
                    y = ny;
                    moved = true;
                }

                return moved;
            }

            return moved;
        }

        public CollisionHit Trace(float ox, float oy, float dx, float dy, float maxRange, float radius, CollisionLayer mask)
        {
            float mag = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (mag < 0.0001f || maxRange < 0.0001f)
                return new CollisionHit { Hit = false };

            dx /= mag;
            dy /= mag;
            float best = maxRange + 1f;
            CollisionHit result = new CollisionHit { Hit = false };
            for (int i = 0; i < _entries.Count; i++)
            {
                CollisionEntry e = _entries[i];
                if ((e.Layer & mask) == 0)
                    continue;
                if ((e.Layer & CollisionLayer.Door) != 0 && !e.Solid)
                    continue;
                float dist = DistanceToBox(ox, oy, dx, dy, e.Box.Inflated(radius));
                if (dist < 0f || dist > maxRange || dist >= best)
                    continue;
                best = dist;
                result = new CollisionHit
                {
                    Hit = true,
                    Layer = e.Layer,
                    Name = e.Name,
                    X = ox + dx * dist,
                    Y = oy + dy * dist,
                    Distance = dist
                };
            }

            return result;
        }

        static float DistanceToBox(float ox, float oy, float dx, float dy, CollisionAabb box)
        {
            float t0 = 0f;
            float t1 = 1e6f;
            if (!ClipAxis(dx, box.MinX - ox, box.MaxX - ox, ref t0, ref t1))
                return -1f;
            if (!ClipAxis(dy, box.MinY - oy, box.MaxY - oy, ref t0, ref t1))
                return -1f;
            if (t1 < 0f || t0 > t1)
                return -1f;
            return t0 < 0f ? 0f : t0;
        }

        static bool ClipAxis(float dir, float min, float max, ref float t0, ref float t1)
        {
            const float eps = 1e-8f;
            if (System.Math.Abs(dir) < eps)
                return min <= 0f && max >= 0f;
            float tNear = min / dir;
            float tFar = max / dir;
            if (tNear > tFar)
            {
                float tmp = tNear;
                tNear = tFar;
                tFar = tmp;
            }

            if (tNear > t0)
                t0 = tNear;
            if (tFar < t1)
                t1 = tFar;
            return t0 <= t1;
        }
    }
}
