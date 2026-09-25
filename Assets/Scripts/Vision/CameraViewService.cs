using UnityEngine;
using RogueShooter.Iso;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Main orthographic camera view (GDD M1d Must). Play ortho comes from
    /// <see cref="PlayOrthoSize"/>: 6 (orthographic) or 5.25 / aspect-locked (IsoConfig.Enabled).
    /// <see cref="GetViewRect"/> is in camera (view) space; with iso on use
    /// <see cref="ViewSpace.TryGetViewQuad"/> for the logic-space quad. Occupancy still uses JianHai stub scale.
    /// Full fog-of-war is not implemented here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraViewService : MonoBehaviour, IWorldView
    {
        /// <summary>Orthographic presentation size (producer retune).</summary>
        public const float OrthoPlaySize = 6f;

        /// <summary>
        /// Single source for the play orthographic size. Iso off: 6. Iso on:
        /// <see cref="IsoConfig.OrthoSizeForAspect"/> for the view camera's aspect
        /// (no camera → <see cref="IsoConfig.IsoOrthoSize"/> 5.25).
        /// </summary>
        public static float PlayOrthoSize
        {
            get
            {
                if (!IsoConfig.Enabled)
                    return OrthoPlaySize;
                Camera cam = Instance != null && Instance.targetCamera != null ? Instance.targetCamera : Camera.main;
                return cam != null ? PlayOrthoSizeFor(CameraViewMath.ResolveAspect(cam)) : IsoConfig.IsoOrthoSize;
            }
        }

        /// <summary>Play ortho size for an explicit aspect (iso off ignores aspect).</summary>
        public static float PlayOrthoSizeFor(float aspect)
        {
            return IsoConfig.Enabled ? IsoConfig.OrthoSizeForAspect(aspect) : OrthoPlaySize;
        }

        /// <summary>
        /// Size actually applied for a serialized/demo size: iso on always uses the iso size
        /// (a serialized 6 in a scene cannot leak into the iso camera).
        /// </summary>
        public static float EffectiveOrtho(float serialized, float aspect)
        {
            return IsoConfig.Enabled ? IsoConfig.OrthoSizeForAspect(aspect) : serialized;
        }

        public static CameraViewService Instance { get; private set; }

        [SerializeField] Camera targetCamera;
        [SerializeField] [Min(0.1f)] float orthographicSize = OrthoPlaySize;

        /// <summary>Effective size this service applies (see <see cref="EffectiveOrtho"/>).</summary>
        public float OrthographicSize
        {
            get
            {
                var cam = targetCamera != null ? targetCamera : GetComponent<Camera>();
                return EffectiveOrtho(orthographicSize, cam != null ? CameraViewMath.ResolveAspect(cam) : IsoConfig.TargetAspect);
            }
        }

        public Camera TargetCamera => targetCamera != null ? targetCamera : GetComponent<Camera>();

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
            targetCamera.orthographicSize = EffectiveOrtho(orthographicSize, CameraViewMath.ResolveAspect(targetCamera));
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

        /// <summary>
        /// Logic position inside the screen view. Iso off: the ortho rect (unchanged). Iso on: the
        /// view quad in logic space (<see cref="IsoProjection.ViewQuadInLogic(Camera, Vector2[])"/>).
        /// </summary>
        public bool IsInCameraView(Vector3 worldPos)
        {
            if (!IsoConfig.Enabled)
                return CameraViewMath.ContainsInclusive(GetViewRect(), worldPos);
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
                return false;
            IsoProjection.ViewQuadInLogic(cam, _quad);
            return ViewSpace.QuadContains(_quad, new Vector2(worldPos.x, worldPos.y));
        }

        readonly Vector2[] _quad = new Vector2[4];

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
