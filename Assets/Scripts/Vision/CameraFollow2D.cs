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

        /// <summary>Orthographic iso: pitch is the downward angle, yaw turns the view. Looks at the target.</summary>
        public void SetIsoView(float pitchDeg, float yawDeg, float distance)
        {
            transform.rotation = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            float back = distance > 0.5f ? distance : 12f;
            offset = -transform.forward * back;
            SnapNow();
        }

        /// <summary>
        /// Look straight at sprites that are already drawn isometric.
        /// A pitched camera would squash that art a second time.
        /// </summary>
        public void SetFaceOn(float distance)
        {
            transform.rotation = Quaternion.identity;
            float back = distance > 0.5f ? distance : 12f;
            offset = new Vector3(0f, 0f, -back);
            SnapNow();
        }

        /// <summary>
        /// Look straight at the XY art. Floor, character, and wall sprites are already
        /// drawn isometric, so a pitched camera must not squash them a second time.
        /// </summary>
        public void SetArtPlane(float distance)
        {
            transform.rotation = Quaternion.identity;
            float back = distance > 0.5f ? distance : 12f;
            offset = new Vector3(0f, 0f, -back);
            SnapNow();
        }

        public void SnapNow()
        {
            if (target == null)
                return;
            transform.position = target.position + offset;
        }

        void LateUpdate()
        {
            SnapNow();
        }
    }
}
