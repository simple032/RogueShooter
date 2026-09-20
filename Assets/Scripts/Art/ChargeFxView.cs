using UnityEngine;
using RogueShooter.Vision;

namespace RogueShooter.Art
{
    /// <summary>
    /// A-primary (string glow / bow edge / warm tip) + B-weak reticle open/close.
    /// No charge bar, no crit-window HUD. Pulse×2 fits Spec §7.5 green (72–84% of 0.90s).
    /// Reticle follows mouse via ScreenToWorldPoint; ChargeFx is not a player-child offset.
    /// </summary>
    public class ChargeFxView : MonoBehaviour
    {
        const float ReticleAlpha = 0.6f;
        const float ColdAlpha = 0.55f;
        const float ReticleClosed = 0.82f;
        const float CritSeconds = 2f / 30f;
        const float ReticleWorldOpen = 0.72f;
        const float ReticleNativeWorld = 48f / 32f;

        Transform _body;
        Transform _reticleRoot;
        SpriteRenderer _string;
        SpriteRenderer _pulse;
        SpriteRenderer _bow;
        SpriteRenderer _tip;
        SpriteRenderer _crit;
        SpriteRenderer _reticle;

        int _pulsesLeft;
        float _pulsePhase;
        bool _pulseShow;
        float _critUntil;
        bool _reticleOpen;

        void OnEnable()
        {
            Cursor.visible = false;
            ChargeFxHooks.OnChargeMid += OnChargeMid;
            ChargeFxHooks.OnChargeEnterGreen += OnChargeEnterGreen;
            ChargeFxHooks.OnChargeExitGreen += OnChargeExitGreen;
            ChargeFxHooks.OnCritConfirm += OnCritConfirm;
        }

        void OnDisable()
        {
            Cursor.visible = true;
            ChargeFxHooks.OnChargeMid -= OnChargeMid;
            ChargeFxHooks.OnChargeEnterGreen -= OnChargeEnterGreen;
            ChargeFxHooks.OnChargeExitGreen -= OnChargeExitGreen;
            ChargeFxHooks.OnCritConfirm -= OnCritConfirm;
        }

        void OnDestroy()
        {
            if (_body != null)
                Destroy(_body.gameObject);
            if (_reticleRoot != null)
                Destroy(_reticleRoot.gameObject);
        }

        void Awake()
        {
            EnsureSlots();
            HideAll();
        }

        void LateUpdate()
        {
            FollowPlayerBody();
            FollowMouseReticle();

            if (_pulsesLeft > 0 && _pulse != null)
            {
                _pulsePhase += Time.deltaTime;
                float slice = PulseSlice();
                if (_pulsePhase >= slice)
                {
                    _pulsePhase = 0f;
                    if (_pulseShow)
                    {
                        _pulsesLeft--;
                        _pulseShow = false;
                        SetActive(_pulse, false);
                    }
                    else if (_pulsesLeft > 0)
                    {
                        _pulseShow = true;
                        Show(_pulse, JianHaiArtCatalog.FxStringPulse, 30, Color.white);
                    }
                }
            }

            if (_crit != null && _crit.enabled && Time.time >= _critUntil)
                SetActive(_crit, false);
        }

        void OnChargeMid()
        {
            EnsureSlots();
            Show(_string, JianHaiArtCatalog.FxStringCold, 29, new Color(1f, 1f, 1f, ColdAlpha));
            Show(_tip, JianHaiArtCatalog.FxTipIdle, 30, Color.white);
            Show(_reticle, JianHaiArtCatalog.ReticleChargeIdle, 0, new Color(1f, 1f, 1f, ReticleAlpha));
            SetReticleOpen(false);
            SetActive(_pulse, false);
            SetActive(_bow, false);
            SetActive(_crit, false);
            _pulsesLeft = 0;
        }

        void OnChargeEnterGreen()
        {
            EnsureSlots();
            Show(_string, JianHaiArtCatalog.FxStringGlow, 30, Color.white);
            Show(_bow, JianHaiArtCatalog.FxBowEdge, 30, Color.white);
            Show(_tip, JianHaiArtCatalog.FxTipWarm, 30, Color.white);
            Show(_reticle, JianHaiArtCatalog.ReticleChargeGreen, 0, new Color(1f, 1f, 1f, ReticleAlpha));
            SetReticleOpen(true);
            _pulsesLeft = 2;
            _pulsePhase = 0f;
            _pulseShow = true;
            Show(_pulse, JianHaiArtCatalog.FxStringPulse, 30, Color.white);
        }

        void OnChargeExitGreen()
        {
            EnsureSlots();
            SetActive(_string, false);
            SetActive(_pulse, false);
            SetActive(_bow, false);
            SetActive(_tip, false);
            _pulsesLeft = 0;
            Show(_reticle, JianHaiArtCatalog.ReticleChargeIdle, 0, new Color(1f, 1f, 1f, ReticleAlpha));
            SetReticleOpen(false);
        }

        void OnCritConfirm()
        {
            EnsureSlots();
            Show(_crit, JianHaiArtCatalog.FxCritFlash, 31, Color.white);
            _critUntil = Time.time + CritSeconds;
            SetActive(_string, false);
            SetActive(_pulse, false);
            SetActive(_bow, false);
            SetActive(_tip, false);
            SetActive(_reticle, false);
            _pulsesLeft = 0;
        }

        public void HideAll()
        {
            SetActive(_string, false);
            SetActive(_pulse, false);
            SetActive(_bow, false);
            SetActive(_tip, false);
            SetActive(_crit, false);
            SetActive(_reticle, false);
            _pulsesLeft = 0;
        }

        void EnsureSlots()
        {
            if (_body != null && _reticle != null)
                return;

            if (_body == null)
            {
                var go = new GameObject("ChargeFx");
                _body = go.transform;
                _body.localScale = Vector3.one;
                _string = MakeSlot(_body, "String", new Vector3(0f, 0.55f, -0.02f));
                _pulse = MakeSlot(_body, "Pulse", new Vector3(0f, 0.55f, -0.03f));
                _bow = MakeSlot(_body, "BowEdge", new Vector3(0.04f, 0.50f, -0.02f));
                _tip = MakeSlot(_body, "Tip", new Vector3(0.10f, 0.42f, -0.02f));
                _crit = MakeSlot(_body, "CritFlash", new Vector3(0f, 0.52f, -0.04f));
            }

            if (_reticle == null)
            {
                var reticleGo = new GameObject("ChargeReticle");
                _reticleRoot = reticleGo.transform;
                _reticleRoot.localScale = Vector3.one;
                _reticle = MakeSlot(_reticleRoot, "Reticle", new Vector3(0f, 0f, -0.05f));
            }

            FollowPlayerBody();
            FollowMouseReticle();
        }

        static SpriteRenderer MakeSlot(Transform parent, string name, Vector3 localPos)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPos;
            child.transform.localScale = Vector3.one;
            return child.AddComponent<SpriteRenderer>();
        }

        static void Show(SpriteRenderer sr, string artId, int order, Color color)
        {
            if (sr == null)
                return;
            JianHaiSprites.Bind(sr, artId);
            sr.sortingOrder = order;
            sr.color = color;
            sr.enabled = true;
            sr.gameObject.SetActive(true);
        }

        static void SetActive(SpriteRenderer sr, bool on)
        {
            if (sr == null)
                return;
            sr.enabled = on;
            sr.gameObject.SetActive(on);
        }

        void SetReticleOpen(bool open)
        {
            _reticleOpen = open;
            ApplyReticleScale();
        }

        void ApplyReticleScale()
        {
            if (_reticle == null)
                return;
            float world = _reticleOpen ? ReticleWorldOpen : ReticleWorldOpen * ReticleClosed;
            float native = ReticleNativeWorld;
            if (_reticle.sprite != null)
            {
                Vector3 size = _reticle.sprite.bounds.size;
                native = Mathf.Max(size.x, size.y);
            }

            if (native < 0.01f)
                native = ReticleNativeWorld;
            float s = world / native;
            _reticle.transform.localScale = new Vector3(s, s, 1f);
        }

        void FollowPlayerBody()
        {
            if (_body == null)
                return;
            Vector3 p = transform.position;
            p.z = 0f;
            _body.position = p;
        }

        void FollowMouseReticle()
        {
            if (_reticleRoot == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Vector3 world = CameraViewMath.ScreenToWorldOnPlayPlane(cam, Input.mousePosition);
            world.z = -0.05f;
            _reticleRoot.position = world;
            ApplyReticleScale();
        }

        static float PulseSlice()
        {
            return Mathf.Max(0.016f, ChargeFxHooks.GreenWindowSeconds * 0.25f);
        }
    }
}
