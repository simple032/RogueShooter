using UnityEngine;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Snaps an orthographic camera to a follow target. Z stays at <see cref="offset"/>.
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 0f, -10f);

        public Transform Target => target;

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
            SnapNow();
        }

        public void SnapNow()
        {
            if (target == null)
                return;
            // Iso on: the camera frames the isometric view plane (logic → view). Iso off: identity.
            transform.position = ViewSpace.LogicToCamera(target.position) + offset;
        }

        void LateUpdate()
        {
            SnapNow();
        }
    }
}
