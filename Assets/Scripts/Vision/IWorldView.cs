using UnityEngine;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Shared limited-vision query. World-space only; callers may later key
    /// anchors/paths however they want. Fog / explored mask is out of scope.
    /// </summary>
    public interface IWorldView
    {
        bool IsInCameraView(Vector3 worldPos);
        Rect GetViewRect();
    }
}
