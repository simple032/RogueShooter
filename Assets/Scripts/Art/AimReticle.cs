using UnityEngine;

namespace RogueShooter.Art
{
    /// <summary>
    /// QA-UX-002: hide OS cursor; combat reticle follows mouse in world.
    /// Charge B (idle/green open-close) stacks on this reticle, never on the player root.
    /// World visual ≤ <see cref="JianHaiArtCatalog.MaxFxWorldUnits"/>. No Canvas, no charge bar.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class AimReticle : MonoBehaviour
    {
        const float ReticleAlpha = 0.6f;
        const float SightAlpha = 0.5f;
        const float ClosedMul = 0.82f;

        public static AimReticle Instance { get; private set; }

        SpriteRenderer _sight;
        SpriteRenderer _b;
        bool _hidOs;
        bool _osVisible;
        CursorLockMode _osLock;

        public static AimReticle Ensure()
        {
            if (Instance != null)
                return Instance;
            var go = new GameObject("AimReticle");
            return go.AddComponent<AimReticle>();
        }

        void Awake()
        {
            Instance = this;
            transform.SetParent(null, true);
            HideOsCursor();
            EnsureSlots();
            ShowSight();
            HideB();
        }

        void OnEnable()
        {
            Instance = this;
            HideOsCursor();
            ChargeFxHooks.OnChargeMid += OnChargeMid;
            ChargeFxHooks.OnChargeEnterGreen += OnChargeEnterGreen;
            ChargeFxHooks.OnChargeExitGreen += OnChargeExitGreen;
            ChargeFxHooks.OnCritConfirm += OnCritConfirm;
        }

        void OnDisable()
        {
            ChargeFxHooks.OnChargeMid -= OnChargeMid;
            ChargeFxHooks.OnChargeEnterGreen -= OnChargeEnterGreen;
            ChargeFxHooks.OnChargeExitGreen -= OnChargeExitGreen;
            ChargeFxHooks.OnCritConfirm -= OnCritConfirm;
            RestoreOsCursor();
            if (Instance == this)
                Instance = null;
        }

        void LateUpdate()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None;
            FollowMouse();
        }

        public void ResetChargeB()
        {
            HideB();
            ShowSight();
        }

        void OnChargeMid()
        {
            ShowSight();
            ShowB(JianHaiArtCatalog.ReticleChargeIdle, false);
        }

        void OnChargeEnterGreen()
        {
            ShowSight();
            ShowB(JianHaiArtCatalog.ReticleChargeGreen, true);
        }

        void OnChargeExitGreen()
        {
            ShowSight();
            ShowB(JianHaiArtCatalog.ReticleChargeIdle, false);
        }

        void OnCritConfirm()
        {
            HideB();
            ShowSight();
        }

        void FollowMouse()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            var plane = new Plane(Vector3.forward, Vector3.zero);
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            float dist;
            if (!plane.Raycast(ray, out dist))
                return;
            Vector3 p = ray.GetPoint(dist);
            p.z = 0f;
            transform.position = p;
        }

        void HideOsCursor()
        {
            if (_hidOs)
                return;
            _osVisible = Cursor.visible;
            _osLock = Cursor.lockState;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None;
            _hidOs = true;
        }

        void RestoreOsCursor()
        {
            if (!_hidOs)
                return;
            Cursor.visible = _osVisible;
            Cursor.lockState = _osLock;
            _hidOs = false;
        }

        void EnsureSlots()
        {
            if (_sight != null)
                return;
            _sight = MakeSlot("CombatSight", -0.04f);
            _b = MakeSlot("ChargeB", -0.06f);
        }

        SpriteRenderer MakeSlot(string name, float z)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(0f, 0f, z);
            child.transform.localScale = Vector3.one;
            return child.AddComponent<SpriteRenderer>();
        }

        void ShowSight()
        {
            EnsureSlots();
            BindCapped(_sight, JianHaiArtCatalog.ReticleChargeIdle, 1, new Color(1f, 1f, 1f, SightAlpha), 1f);
        }

        void ShowB(string artId, bool open)
        {
            EnsureSlots();
            BindCapped(_b, artId, 2, new Color(1f, 1f, 1f, ReticleAlpha), open ? 1f : ClosedMul);
        }

        void HideB()
        {
            if (_b == null)
                return;
            _b.enabled = false;
            _b.gameObject.SetActive(false);
        }

        static void BindCapped(SpriteRenderer sr, string artId, int order, Color color, float mul)
        {
            if (sr == null)
                return;
            JianHaiSprites.Bind(sr, artId);
            sr.sortingOrder = order;
            sr.color = color;
            float s = JianHaiArtCatalog.VisualScaleForCap(artId) * mul;
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.enabled = true;
            sr.gameObject.SetActive(true);
        }
    }
}
