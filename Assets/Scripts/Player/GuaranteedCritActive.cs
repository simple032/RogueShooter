using UnityEngine;

namespace RogueShooter.Player
{
    /// <summary>
    /// crit2 凝神窥机: Q — 15s force 命中弱点 (ChargeShotKind.Crit ×2.0). CD 120s.
    /// Still needs ≥ min charge; does not auto-fill weak-spot window.
    /// </summary>
    public class GuaranteedCritActive : MonoBehaviour
    {
        public const KeyCode DefaultKey = KeyCode.Q;
        public const float DurationSeconds = 15f;
        public const float CooldownSeconds = 120f;

        float _buffLeft;
        float _cdLeft;

        public bool IsReady => _cdLeft <= 0f && _buffLeft <= 0f;
        public bool IsActive => _buffLeft > 0f;
        public float BuffLeft => _buffLeft;
        public float CooldownLeft => _cdLeft;

        void Update()
        {
            if (RunPause.IsPaused)
                return;

            if (_buffLeft > 0f)
            {
                float prev = _buffLeft;
                _buffLeft -= Time.deltaTime;
                if (prev > 0f && _buffLeft <= 0f)
                {
                    _buffLeft = 0f;
                    Debug.Log("[凝神窥机] WINDOW END");
                }
            }

            if (_cdLeft > 0f)
                _cdLeft -= Time.deltaTime;

            if (Input.GetKeyDown(DefaultKey))
                TryActivate();
        }

        public bool TryActivate()
        {
            if (_buffLeft > 0f)
                return false;
            if (_cdLeft > 0f)
                return false;

            _buffLeft = DurationSeconds;
            _cdLeft = CooldownSeconds;
            Debug.Log($"[凝神窥机] ACTIVE {DurationSeconds:0}s CD={CooldownSeconds:0}s");
            return true;
        }

        public bool TryForceCrit(ChargeShotKind resolved, out ChargeShotKind forced)
        {
            forced = resolved;
            if (_buffLeft <= 0f || resolved == ChargeShotKind.None)
                return false;

            forced = ChargeShotKind.Crit;
            return true;
        }

        public void DebugSetBuff(float seconds)
        {
            _buffLeft = Mathf.Max(0f, seconds);
        }

        public void DebugSetCooldown(float seconds)
        {
            _cdLeft = Mathf.Max(0f, seconds);
        }
    }
}
