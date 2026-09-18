using UnityEngine;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Main orthographic camera view (GDD M1d Must). Tune <see cref="orthographicSize"/>
    /// to change the visible world rect. Full fog-of-war is not implemented here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraViewService : MonoBehaviour, IWorldView
    {
        public static CameraViewService Instance { get; private set; }

        [SerializeField] Camera targetCamera;
        [SerializeField] [Min(0.1f)] float orthographicSize = 2.5f;

        public float OrthographicSize => orthographicSize;

        public static bool TryGet(out IWorldView view)
        {
            view = Instance;
            return view != null;
        }

        public void Configure(float size)
        {
            orthographicSize = Mathf.Max(0.1f, size);
            ApplyLens();
        }

        void Awake()
        {
            Instance = this;
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();
            ApplyLens();
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void LateUpdate()
        {
            ApplyLens();
        }

        public void ApplyLens()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
                return;

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = orthographicSize;
        }

        public Rect GetViewRect()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
                return new Rect(0f, 0f, 0f, 0f);

            return CameraViewMath.GetOrthographicWorldRect(
                cam.transform.position,
                cam.orthographicSize,
                CameraViewMath.ResolveAspect(cam));
        }

        public bool IsInCameraView(Vector3 worldPos)
        {
            return CameraViewMath.ContainsInclusive(GetViewRect(), worldPos);
        }

        void OnDrawGizmos()
        {
            var cam = targetCamera != null ? targetCamera : GetComponent<Camera>();
            if (cam == null || !cam.orthographic)
                return;

            Rect rect = CameraViewMath.GetOrthographicWorldRect(
                cam.transform.position,
                Application.isPlaying ? cam.orthographicSize : orthographicSize,
                CameraViewMath.ResolveAspect(cam));
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(rect.center.x, rect.center.y, 0f), new Vector3(rect.width, rect.height, 0f));
        }
    }
}
