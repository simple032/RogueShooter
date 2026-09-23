using UnityEngine;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Follows target with orthographic camera. Default pitch = 45° (GDD 2.5D iso).
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        public const float IsoPitchDegrees = 45f;

        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, -8f, -8f);
        [SerializeField] float pitchDegrees = IsoPitchDegrees;

        public Transform Target => target;
        public float PitchDegrees => pitchDegrees;

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
            ApplyPitch();
            SnapNow();
        }

        public void ConfigureIso(float pitch, Vector3 followOffset)
        {
            pitchDegrees = pitch;
            offset = followOffset;
            ApplyPitch();
            SnapNow();
        }

        public void SnapNow()
        {
            if (target == null)
                return;
            transform.position = target.position + offset;
        }

        void Awake()
        {
            ApplyPitch();
        }

        void LateUpdate()
        {
            SnapNow();
        }

        void ApplyPitch()
        {
            transform.rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
        }
    }
}
