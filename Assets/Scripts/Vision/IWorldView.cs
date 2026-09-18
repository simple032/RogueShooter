using UnityEngine;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Shared limited-vision query used by camera follow and later spawners.
    /// Fog / explored mask is out of scope (GDD M1d Should).
    /// </summary>
    public interface IWorldView
    {
        bool IsInCameraView(Vector3 worldPos);
        Rect GetViewRect();
    }
}
