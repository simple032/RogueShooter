using System.Collections.Generic;
using UnityEngine;

namespace RogueShooter.Combat
{
    /// <summary>Live AABB registry. Source of truth for walk / projectile / dodge.</summary>
    public static class CollisionWorld
    {
        static readonly List<CollisionVolume> Volumes = new List<CollisionVolume>();

        public static IReadOnlyList<CollisionVolume> All => Volumes;

        public static void Register(CollisionVolume volume)
        {
            if (volume == null)
                return;
            if (!Volumes.Contains(volume))
                Volumes.Add(volume);
        }

        public static void Unregister(CollisionVolume volume)
        {
            Volumes.Remove(volume);
        }

        public static void Clear()
        {
            Volumes.Clear();
        }

        public static CollisionSpace Snapshot()
        {
            var space = new CollisionSpace();
            for (int i = Volumes.Count - 1; i >= 0; i--)
            {
                CollisionVolume v = Volumes[i];
                if (v == null)
                {
                    Volumes.RemoveAt(i);
                    continue;
                }

                if (!v.isActiveAndEnabled)
                    continue;
                space.Add(v.WorldAabb, v.Layer, v.Solid, v.name);
            }

            return space;
        }

        public static bool Overlaps(CollisionAabb box, CollisionLayer mask, bool solidsOnly)
        {
            for (int i = Volumes.Count - 1; i >= 0; i--)
            {
                CollisionVolume v = Volumes[i];
                if (v == null)
                {
                    Volumes.RemoveAt(i);
                    continue;
                }

                if (!v.isActiveAndEnabled)
                    continue;
                if ((v.Layer & mask) == 0)
                    continue;
                if (solidsOnly && !v.Solid)
                    continue;
                if (v.WorldAabb.Overlaps(box))
                    return true;
            }

            return false;
        }

        public static bool TryMove(Transform body, float hx, float hy, float dx, float dy)
        {
            if (body == null)
                return false;
            Vector3 p = body.position;
            float x = p.x;
            float y = p.y;
            CollisionSpace space = SnapshotWithout(body);
            bool moved = space.TryMove(ref x, ref y, hx, hy, dx, dy, CollisionRules.SolidMask);
            p.x = x;
            p.y = y;
            body.position = p;
            return moved;
        }

        public static CollisionHit Trace(
            float ox, float oy, float dx, float dy, float maxRange, float radius, CollisionLayer mask,
            Transform ignore)
        {
            return SnapshotWithout(ignore).Trace(ox, oy, dx, dy, maxRange, radius, mask);
        }

        static CollisionSpace SnapshotWithout(Transform ignore)
        {
            var space = new CollisionSpace();
            for (int i = Volumes.Count - 1; i >= 0; i--)
            {
                CollisionVolume v = Volumes[i];
                if (v == null)
                {
                    Volumes.RemoveAt(i);
                    continue;
                }

                if (!v.isActiveAndEnabled)
                    continue;
                if (ignore != null && (v.transform == ignore || v.transform.IsChildOf(ignore)))
                    continue;
                space.Add(v.WorldAabb, v.Layer, v.Solid, v.name);
            }

            return space;
        }
    }
}
