using UnityEngine;

namespace RogueShooter.Art
{
    /// <summary>
    /// A-primary charge FX on the bow/player: string glow, bow edge, warm tip, crit flash.
    /// B reticle lives on <see cref="AimReticle"/> (mouse), never on the player root.
    /// No charge bar. Pulse×2 fits Spec §7.5 green (72–84% of 0.90s). Visual ≤ 0.8u.
    /// </summary>
    public class ChargeFxView : MonoBehaviour
    {
        const float ColdAlpha = 0.55f;
        const float CritSeconds = 2f / 30f;

        Transform _root;
        SpriteRenderer _string;
        SpriteRenderer _pulse;
        SpriteRenderer _bow;
        SpriteRenderer _tip;
        SpriteRenderer _crit;

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
        }

        public void HideAll()
        {
            SetActive(_string, false);
            SetActive(_pulse, false);
            SetActive(_bow, false);
            SetActive(_tip, false);
            SetActive(_crit, false);
            _pulsesLeft = 0;
        }

        void EnsureSlots()
        {
            if (_root != null)
                return;

            var go = new GameObject("ChargeFxA");
            _root = go.transform;
            _root.SetParent(transform, false);
            _root.localPosition = Vector3.zero;
            _root.localScale = Vector3.one;

            _string = MakeSlot("String", new Vector3(0f, 0.12f, -0.02f));
            _pulse = MakeSlot("Pulse", new Vector3(0f, 0.12f, -0.03f));
            _bow = MakeSlot("BowEdge", new Vector3(0.02f, 0.06f, -0.02f));
            _tip = MakeSlot("Tip", new Vector3(0.06f, -0.02f, -0.02f));
            _crit = MakeSlot("CritFlash", new Vector3(0f, 0.10f, -0.04f));
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
            float s = JianHaiArtCatalog.VisualScaleForCap(artId);
            sr.transform.localScale = new Vector3(s, s, 1f);
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

        static float PulseSlice()
        {
            return Mathf.Max(0.016f, ChargeFxHooks.GreenWindowSeconds * 0.25f);
        }
    }
}
