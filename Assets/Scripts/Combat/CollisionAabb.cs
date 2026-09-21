using System;

namespace RogueShooter.Combat
{
    /// <summary>Axis-aligned box in world XY. Half-extents are positive.</summary>
    public struct CollisionAabb
    {
        public float X;
        public float Y;
        public float Hx;
        public float Hy;

        public CollisionAabb(float x, float y, float hx, float hy)
        {
            X = x;
            Y = y;
            Hx = hx < 0f ? -hx : hx;
            Hy = hy < 0f ? -hy : hy;
        }

        public float MinX => X - Hx;
        public float MaxX => X + Hx;
        public float MinY => Y - Hy;
        public float MaxY => Y + Hy;

        public static CollisionAabb FromCenterSize(float x, float y, float width, float height)
        {
            return new CollisionAabb(x, y, width * 0.5f, height * 0.5f);
        }

        public CollisionAabb Translated(float dx, float dy)
        {
            return new CollisionAabb(X + dx, Y + dy, Hx, Hy);
        }

        public CollisionAabb Inflated(float pad)
        {
            return new CollisionAabb(X, Y, Hx + pad, Hy + pad);
        }

        public bool Overlaps(CollisionAabb other)
        {
            return MinX <= other.MaxX && MaxX >= other.MinX
                && MinY <= other.MaxY && MaxY >= other.MinY;
        }

        public bool Contains(float px, float py)
        {
            return px >= MinX && px <= MaxX && py >= MinY && py <= MaxY;
        }

        public static bool SegmentHits(float ax, float ay, float bx, float by, CollisionAabb box, float radius)
        {
            CollisionAabb fat = box.Inflated(radius);
            float dx = bx - ax;
            float dy = by - ay;
            float mag = (float)Math.Sqrt(dx * dx + dy * dy);
            if (mag < 0.0001f)
                return fat.Contains(ax, ay);
            dx /= mag;
            dy /= mag;
            float t0 = 0f;
            float t1 = mag;
            if (!ClipAxis(dx, fat.MinX - ax, fat.MaxX - ax, ref t0, ref t1))
                return false;
            if (!ClipAxis(dy, fat.MinY - ay, fat.MaxY - ay, ref t0, ref t1))
                return false;
            return t0 <= t1 && t1 >= 0f && t0 <= mag;
        }

        static bool ClipAxis(float dir, float min, float max, ref float t0, ref float t1)
        {
            const float eps = 1e-8f;
            if (Math.Abs(dir) < eps)
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
