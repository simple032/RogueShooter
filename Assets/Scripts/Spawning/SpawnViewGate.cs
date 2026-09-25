using UnityEngine;
using RogueShooter.Vision;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Skip this round when the anchor sits inside the main orthographic camera view
    /// or within the view-edge buffer. Only truly off-view anchors may spawn.
    /// Cluster radius is folded into the pad so stubs cannot pop into the forbidden zone.
    /// Iso on: the view rect (view space) is turned into the logic-space view quad
    /// (<see cref="ViewSpace.QuadFromViewRect"/>) and the pad is a distance outside that quad.
    /// Iso off: unchanged rect path.
    /// </summary>
    public static class SpawnViewGate
    {
        /// <summary>World units beyond the ortho rect that still forbid spawn (producer N4-fix).</summary>
        public const float EdgeBufferWorld = 1.5f;

        public static float EffectivePad => EdgeBufferWorld + Mathf.Max(0f, SpawnCluster.Radius);

        public static bool ShouldSkipSpawn(Vector3 anchorWorldPos)
        {
            return IsInForbiddenZone(anchorWorldPos);
        }

        public static bool IsInForbiddenZone(Vector3 worldPos)
        {
            if (!TryGetViewRect(out Rect view))
                return false;
            if (ViewSpace.IsoOn)
            {
                // Iso: view rect is in view space; forbid inside the logic quad or within pad of it.
                ViewSpace.QuadFromViewRect(view, true, Quad);
                return ViewSpace.SignedOutside(Quad, new Vector2(worldPos.x, worldPos.y)) <= EffectivePad;
            }

            Rect forbidden = Inflate(view, EffectivePad);
            return CameraViewMath.ContainsInclusive(forbidden, worldPos);
        }

        public static bool IsInMainCameraView(Vector3 worldPos)
        {
            if (!TryGetViewRect(out Rect view))
                return false;
            if (ViewSpace.IsoOn)
            {
                ViewSpace.QuadFromViewRect(view, true, Quad);
                return ViewSpace.QuadContains(Quad, new Vector2(worldPos.x, worldPos.y));
            }

            return CameraViewMath.ContainsInclusive(view, worldPos);
        }

        static readonly Vector2[] Quad = new Vector2[4];

        public static bool TryGetViewRect(out Rect view)
        {
            if (CameraViewService.TryGet(out IWorldView svc))
            {
                view = svc.GetViewRect();
                return view.width > 0f && view.height > 0f;
            }

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                view = default;
                return false;
            }

            view = CameraViewMath.GetOrthographicWorldRect(
                cam.transform.position,
                cam.orthographicSize,
                CameraViewMath.ResolveAspect(cam));
            return true;
        }

        public static Rect Inflate(Rect rect, float pad)
        {
            float p = Mathf.Max(0f, pad);
            return Rect.MinMaxRect(
                rect.xMin - p,
                rect.yMin - p,
                rect.xMax + p,
                rect.yMax + p);
        }
    }
}
