using UnityEngine;

namespace RogueShooter.Vision
{
    /// <summary>
    /// World-space orthographic view rect. Independent of fog-of-war.
    /// </summary>
    public static class CameraViewMath
    {
        public const float FallbackAspect = 16f / 9f;

        public static float ResolveAspect(Camera camera)
        {
            if (camera == null)
                return FallbackAspect;
            return camera.pixelWidth > 0 ? camera.aspect : FallbackAspect;
        }

        public static Rect GetOrthographicWorldRect(
            Vector3 cameraPosition,
            float orthographicSize,
            float aspect)
        {
            float halfHeight = Mathf.Max(0.01f, orthographicSize);
            float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            return Rect.MinMaxRect(
                cameraPosition.x - halfWidth,
                cameraPosition.y - halfHeight,
                cameraPosition.x + halfWidth,
                cameraPosition.y + halfHeight);
        }

        public static bool ContainsInclusive(Rect viewRect, Vector3 worldPos)
        {
            return worldPos.x >= viewRect.xMin && worldPos.x <= viewRect.xMax
                && worldPos.y >= viewRect.yMin && worldPos.y <= viewRect.yMax;
        }

        /// <summary>
        /// Mouse / screen → world on the z=0 play plane (Camera.ScreenToWorldPoint).
        /// </summary>
        public static Vector3 ScreenToWorldOnPlayPlane(Camera cam, Vector3 screen)
        {
            if (cam == null)
                return Vector3.zero;
            screen.z = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }
    }
}
