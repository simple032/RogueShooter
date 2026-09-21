using UnityEngine;
using RogueShooter.Vision;

namespace RogueShooter.Art
{
    /// <summary>
    /// Charge aim: fig1 diamond+ticks+clockwise ring (the reticle IS the charge).
    /// A string-glow / bow / tip stay weak secondary. No progress-bar HUD.
    /// Reticle follows mouse via ScreenToWorldPoint; not parented under the player.
    /// </summary>
    public class ChargeFxView : MonoBehaviour
    {
        const float ColdAlpha = 0.28f;
        const float GlowAlpha = 0.38f;
        const float CritSeconds = 2f / 30f;

        Transform _body;
        ChargeReticle _reticle;
        SpriteRenderer _string;
        SpriteRenderer _pulse;
        SpriteRenderer _bow;
        SpriteRenderer _tip;
        SpriteRenderer _crit;

        int _pulsesLeft;
        float _pulsePhase;
        bool _pulseShow;
        float _critUntil;
        float _chargeProgress;
        bool _green;
        bool _charging;

        public void SetChargeProgress(float progress01, bool greenWindow)
        {
            EnsureSlots();
            _charging = true;
            _chargeProgress = Mathf.Clamp01(progress01);
            _green = greenWindow;
            if (_reticle != null)
            {
                _reticle.SetCoreVisible(true);
                _reticle.SetRingVisible(true);
                _reticle.SetProgress(_chargeProgress, _green);
            }
        }

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
            if (_reticle != null)
                Destroy(_reticle.gameObject);
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
                        Show(_pulse, JianHaiArtCatalog.FxStringPulse, 30, new Color(1f, 1f, 1f, 0.22f));
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
            Show(_tip, JianHaiArtCatalog.FxTipIdle, 30, new Color(1f, 1f, 1f, GlowAlpha));
            SetActive(_pulse, false);
            SetActive(_bow, false);
            SetActive(_crit, false);
            _pulsesLeft = 0;
        }

        void OnChargeEnterGreen()
        {
            EnsureSlots();
            Show(_string, JianHaiArtCatalog.FxStringGlow, 30, new Color(1f, 1f, 1f, GlowAlpha));
            Show(_bow, JianHaiArtCatalog.FxBowEdge, 30, new Color(1f, 1f, 1f, 0.32f));
            Show(_tip, JianHaiArtCatalog.FxTipWarm, 30, new Color(1f, 1f, 1f, GlowAlpha));
            _pulsesLeft = 2;
            _pulsePhase = 0f;
            _pulseShow = true;
            Show(_pulse, JianHaiArtCatalog.FxStringPulse, 30, new Color(1f, 1f, 1f, 0.22f));
            _green = true;
            if (_reticle != null && _charging)
                _reticle.SetProgress(_chargeProgress, true);
        }

        void OnChargeExitGreen()
        {
            EnsureSlots();
            SetActive(_string, false);
            SetActive(_pulse, false);
            SetActive(_bow, false);
            SetActive(_tip, false);
            _pulsesLeft = 0;
            _green = false;
            if (_reticle != null && _charging)
                _reticle.SetProgress(_chargeProgress, false);
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
            _pulsesLeft = 0;
            _charging = false;
            _chargeProgress = 0f;
            _green = false;
            if (_reticle != null)
            {
                _reticle.SetRingVisible(false);
                _reticle.SetProgress(0f, false);
                _reticle.SetCoreVisible(true);
            }
        }

        public void HideAll()
        {
            SetActive(_string, false);
            SetActive(_pulse, false);
            SetActive(_bow, false);
            SetActive(_tip, false);
            SetActive(_crit, false);
            _pulsesLeft = 0;
            _charging = false;
            _chargeProgress = 0f;
            _green = false;
            if (_reticle != null)
            {
                _reticle.SetRingVisible(false);
                _reticle.SetProgress(0f, false);
                _reticle.SetCoreVisible(true);
            }
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
                _reticle = reticleGo.AddComponent<ChargeReticle>();
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
            if (_reticle == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Vector3 world = CameraViewMath.ScreenToWorldOnPlayPlane(cam, Input.mousePosition);
            world.z = -0.05f;
            _reticle.transform.position = world;
        }

        static float PulseSlice()
        {
            return Mathf.Max(0.016f, ChargeFxHooks.GreenWindowSeconds * 0.25f);
        }
    }
}
