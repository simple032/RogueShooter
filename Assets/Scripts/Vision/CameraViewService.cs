using UnityEngine;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Main orthographic camera view (GDD M1d Must). Play ortho is 8
    /// (producer retune; supersedes 4 / 2.75). Occupancy still uses JianHai stub scale.
    /// Full fog-of-war is not implemented here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraViewService : MonoBehaviour, IWorldView
    {
        public const float PlayOrthoSize = 8f;

        /// <summary>PlayerPrefs key for the saved zoom preference (float, 1 = tuned play size).</summary>
        public const string ZoomPrefKey = "StartMenu.Zoom";
        const float MinZoom = .75f, MaxZoom = 1.5f, ZoomStep = .1f;

        static float _zoomScale = 1f;
        static bool _zoomRead;
        float _zoomApplied = 1f;

        /// <summary>Saved zoom, read lazily on first use. A static initializer calling
        /// PlayerPrefs would run inside MonoBehaviour construction during scene load,
        /// which Unity forbids and leaves the type poisoned with TypeInitializationException.</summary>
        public static float ZoomScale
        {
            get
            {
                if (!_zoomRead)
                {
                    _zoomScale = LoadZoom();
                    _zoomRead = true;
                }
                return _zoomScale;
            }
            private set
            {
                _zoomScale = Mathf.Clamp(value, MinZoom, MaxZoom);
                _zoomRead = true;
            }
        }

        static float LoadZoom() { return Mathf.Clamp(PlayerPrefs.GetFloat(ZoomPrefKey, 1f), MinZoom, MaxZoom); }

        /// <summary>Clamps and persists the zoom preference. The lens glides toward it in LateUpdate.</summary>
        public static void SetZoom(float scale)
        {
            ZoomScale = Mathf.Clamp(scale, MinZoom, MaxZoom);
            PlayerPrefs.SetFloat(ZoomPrefKey, ZoomScale);
            PlayerPrefs.Save();
        }

        public static CameraViewService Instance { get; private set; }

        [SerializeField] Camera targetCamera;
        [SerializeField] [Min(0.1f)] float orthographicSize = PlayOrthoSize;

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
            _zoomApplied = ZoomScale;
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
            // Mouse wheel zooms the view while the game is live; paused/reward/settle states ignore it.
            if (!RogueShooter.Player.RunPause.IsPaused)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (wheel != 0f)
                    SetZoom(ZoomScale - Mathf.Sign(wheel) * ZoomStep);
            }
            _zoomApplied = Mathf.MoveTowards(_zoomApplied, ZoomScale, Time.unscaledDeltaTime * 2f);
            ApplyLens();
        }

        public void ApplyLens()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
                return;

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = orthographicSize * _zoomApplied;
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
