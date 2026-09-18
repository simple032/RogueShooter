using UnityEngine;

namespace RogueShooter.Art
{
    /// <summary>
    /// A-primary / B-weak charge FX. No charge bar, no crit-window HUD.
    /// </summary>
    public class ChargeFxView : MonoBehaviour
    {
        const float ReticleAlpha = 0.6f;
        const float ColdAlpha = 0.55f;
        const float PulseOn = 0.07f;
        const float PulseOff = 0.07f;
        const float CritSeconds = 2f / 30f;

        Transform _root;
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

        void OnEnable()
        {
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
        }

        void Awake()
        {
            EnsureSlots();
            HideAll();
        }

        void LateUpdate()
        {
            if (_pulsesLeft > 0 && _pulse != null)
            {
                _pulsePhase += Time.deltaTime;
                float slice = _pulseShow ? PulseOn : PulseOff;
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
            if (_root != null)
                return;

            var go = new GameObject("ChargeFx");
            _root = go.transform;
            _root.SetParent(transform, false);
            _root.localPosition = Vector3.zero;
            float entity = JianHaiArtCatalog.EntityStubWorldScale;
            float inv = entity > 0.01f ? 1f / entity : 1f;
            _root.localScale = new Vector3(inv, inv, 1f);

            _string = MakeSlot("String", new Vector3(0f, 0.55f, -0.02f));
            _pulse = MakeSlot("Pulse", new Vector3(0f, 0.55f, -0.03f));
            _bow = MakeSlot("BowEdge", new Vector3(0.04f, 0.50f, -0.02f));
            _tip = MakeSlot("Tip", new Vector3(0.10f, 0.42f, -0.02f));
            _crit = MakeSlot("CritFlash", new Vector3(0f, 0.52f, -0.04f));
            _reticle = MakeSlot("Reticle", new Vector3(0f, 0.48f, -0.05f));
        }

        SpriteRenderer MakeSlot(string name, Vector3 localPos)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root, false);
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
    }
}
