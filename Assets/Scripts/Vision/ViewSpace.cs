using UnityEngine;
using RogueShooter.Iso;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Single entry point for camera ↔ logic conversion used by gameplay (input, aim, spawn gate,
    /// view quad). With <see cref="IsoConfig.Enabled"/> off every method is the identity used
    /// before (camera space == logic space). With it on, the camera lives in isometric view space
    /// and everything goes through <see cref="IsoProjection"/>. Gameplay state stays in logic space.
    /// </summary>
    public static class ViewSpace
    {
        public static bool IsoOn => IsoConfig.Enabled;

        /// <summary>Logic position → camera/view position (camera follow target).</summary>
        public static Vector3 LogicToCamera(Vector3 logic)
        {
            return IsoOn ? IsoProjection.LogicToView(logic) : logic;
        }

        /// <summary>Camera/view position → logic ground position (z = 0).</summary>
        public static Vector3 CameraToLogic(Vector3 view)
        {
            if (!IsoOn)
                return new Vector3(view.x, view.y, 0f);
            Vector2 l = IsoProjection.ViewToLogic(view);
            return new Vector3(l.x, l.y, 0f);
        }

        /// <summary>
        /// Raw WASD / stick (screen directions) → logic move vector. Iso off: returned unchanged,
        /// the caller keeps its existing normalization. Iso on: screen direction is inverse-projected
        /// and re-normalized in logic space to the raw magnitude (capped at 1), so logic move speed is
        /// the same in every direction (screen-space speed difference is not compensated).
        /// </summary>
        public static Vector2 InputToLogic(Vector2 raw)
        {
            return InputToLogic(raw, IsoOn);
        }

        public static Vector2 InputToLogic(Vector2 raw, bool iso)
        {
            if (!iso)
                return raw;
            float mag = raw.magnitude;
            if (mag < 1e-5f)
                return Vector2.zero;
            Vector2 logic = IsoProjection.ScreenDirToLogicDir(raw);
            if (logic.sqrMagnitude < 1e-10f)
                return Vector2.zero;
            return logic.normalized * Mathf.Min(1f, mag);
        }

        /// <summary>Mouse pixel → logic ground point (z = 0).</summary>
        public static Vector3 ScreenPixelToLogic(Camera cam, Vector3 pixel)
        {
            if (!IsoOn)
                return CameraViewMath.ScreenToWorldOnPlayPlane(cam, pixel);
            Vector2 l = IsoProjection.ScreenPixelToLogic(cam, pixel);
            return new Vector3(l.x, l.y, 0f);
        }

        public static Camera ViewCamera()
        {
            var svc = CameraViewService.Instance;
            if (svc != null && svc.TargetCamera != null)
                return svc.TargetCamera;
            return Camera.main;
        }

        /// <summary>Current screen view as a logic-space quad (0 BL, 1 BR, 2 TR, 3 TL in view space).</summary>
        public static bool TryGetViewQuad(Vector2[] out4)
        {
            if (out4 == null || out4.Length < 4)
                return false;
            Camera cam = ViewCamera();
            if (cam == null || !cam.orthographic)
                return false;
            Rect rect = CameraViewMath.GetOrthographicWorldRect(
                cam.transform.position, cam.orthographicSize, CameraViewMath.ResolveAspect(cam));
            QuadFromViewRect(rect, IsoOn, out4);
            return rect.width > 0f && rect.height > 0f;
        }

        /// <summary>View-space rect → logic quad. Iso off: the rect corners themselves.</summary>
        public static void QuadFromViewRect(Rect rect, bool iso, Vector2[] out4)
        {
            if (out4 == null || out4.Length < 4)
                return;
            if (iso)
            {
                IsoProjection.ViewQuadInLogic(rect, out4);
                return;
            }

            out4[0] = new Vector2(rect.xMin, rect.yMin);
            out4[1] = new Vector2(rect.xMax, rect.yMin);
            out4[2] = new Vector2(rect.xMax, rect.yMax);
            out4[3] = new Vector2(rect.xMin, rect.yMax);
        }

        /// <summary>View rect for a camera centred on a logic point (probe / checks helper).</summary>
        public static Rect ViewRectAt(Vector3 cameraViewPos, float orthoSize, float aspect)
        {
            return CameraViewMath.GetOrthographicWorldRect(cameraViewPos, orthoSize, aspect);
        }

        readonly static Vector2[] Scratch = new Vector2[4];

        /// <summary>
        /// Logic position inside the current view quad (inclusive). No usable camera → true
        /// (headless callers are not blocked).
        /// </summary>
        public static bool InViewQuad(Vector3 logicPos)
        {
            if (!TryGetViewQuad(Scratch))
                return true;
            return QuadContains(Scratch, new Vector2(logicPos.x, logicPos.y));
        }

        /// <summary>Convex quad containment, inclusive, either winding.</summary>
        public static bool QuadContains(Vector2[] q, Vector2 p)
        {
            bool pos = false, neg = false;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = q[i];
                Vector2 b = q[(i + 1) & 3];
                float c = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (c > 1e-6f) pos = true;
                else if (c < -1e-6f) neg = true;
                if (pos && neg)
                    return false;
            }

            return true;
        }

        /// <summary>Shortest distance from p to the quad boundary.</summary>
        public static float DistanceToQuadEdge(Vector2[] q, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float d = DistToSegment(p, q[i], q[(i + 1) & 3]);
                if (d < best)
                    best = d;
            }

            return best;
        }

        /// <summary>&gt; 0 outside by that distance; ≤ 0 inside (negative edge distance).</summary>
        public static float SignedOutside(Vector2[] q, Vector2 p)
        {
            float d = DistanceToQuadEdge(q, p);
            return QuadContains(q, p) ? -d : d;
        }

        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f)
                return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
