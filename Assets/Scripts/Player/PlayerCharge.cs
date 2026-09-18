using UnityEngine;
using RogueShooter.Art;

namespace RogueShooter.Player
{
    /// <summary>
    /// Stub hold-to-charge that only fires P0 FX hooks. No bar, no LOCK numbers.
    /// Hold Mouse0 or C. Release in the green window → OnCritConfirm.
    /// </summary>
    public class PlayerCharge : MonoBehaviour
    {
        const float ChargeSeconds = 2f;
        const float MidAt = 0.15f;
        const float GreenEnter = 0.72f;
        const float GreenExit = 0.88f;

        float _held;
        bool _charging;
        bool _mid;
        bool _green;
        bool _exited;
        ChargeFxView _fx;

        void Awake()
        {
            _fx = GetComponent<ChargeFxView>();
            if (_fx == null)
                _fx = gameObject.AddComponent<ChargeFxView>();
        }

        void Update()
        {
            if (RunPause.IsPaused)
            {
                if (_charging)
                    CancelCharge();
                return;
            }

            bool hold = Input.GetMouseButton(0) || Input.GetKey(KeyCode.C);
            if (!_charging)
            {
                if (hold)
                    BeginCharge();
                return;
            }

            if (!hold)
            {
                ReleaseCharge();
                return;
            }

            _held += Time.deltaTime;
            float p = Mathf.Clamp01(_held / ChargeSeconds);
            if (!_mid && p >= MidAt)
            {
                _mid = true;
                ChargeFxHooks.ChargeMid();
                Debug.Log("[ChargeFx] OnChargeMid");
            }

            if (!_green && p >= GreenEnter)
            {
                _green = true;
                ChargeFxHooks.ChargeEnterGreen();
                Debug.Log("[ChargeFx] OnChargeEnterGreen");
            }

            if (_green && !_exited && p >= GreenExit)
            {
                _exited = true;
                _green = false;
                ChargeFxHooks.ChargeExitGreen();
                Debug.Log("[ChargeFx] OnChargeExitGreen");
            }
        }

        void BeginCharge()
        {
            _charging = true;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
        }

        void ReleaseCharge()
        {
            bool crit = _green && !_exited;
            _charging = false;
            _held = 0f;
            if (crit)
            {
                ChargeFxHooks.CritConfirm();
                Debug.Log("[ChargeFx] OnCritConfirm");
            }
            else if (_fx != null)
                _fx.HideAll();
            _mid = false;
            _green = false;
            _exited = false;
        }

        void CancelCharge()
        {
            _charging = false;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            if (_fx != null)
                _fx.HideAll();
        }
    }
}
