using UnityEngine;
using RogueShooter.Vision;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// GDD §7: skip spawn when the anchor is inside the main orthographic camera view rect.
    /// Reusable for later Chest_*, DeadEnd_*, Shop_01, A1..A4, BOSS — do not bake map topology here.
    /// </summary>
    public static class SpawnViewGate
    {
        public static bool ShouldSkipSpawn(Vector3 anchorWorldPos)
        {
            return IsInMainCameraView(anchorWorldPos);
        }

        public static bool IsInMainCameraView(Vector3 worldPos)
        {
            if (CameraViewService.TryGet(out IWorldView view))
                return view.IsInCameraView(worldPos);

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
                return false;

            Rect rect = CameraViewMath.GetOrthographicWorldRect(
                cam.transform.position,
                cam.orthographicSize,
                CameraViewMath.ResolveAspect(cam));
            return CameraViewMath.ContainsInclusive(rect, worldPos);
        }
    }
}
