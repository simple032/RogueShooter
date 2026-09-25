using UnityEngine;

namespace RogueShooter.Iso
{
    /// <summary>
    /// Keeps this camera on the isometric sort axis. The project camera does not serialize it.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class IsoSortAxis : MonoBehaviour
    {
        public Vector3 Axis = new Vector3(0f, 1f, 0f);

        void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            var cam = GetComponent<Camera>();
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = Axis;
        }
    }
}
