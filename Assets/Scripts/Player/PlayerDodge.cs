using UnityEngine;
using RogueShooter.Combat;

namespace RogueShooter.Player
{
    /// <summary>
    /// Space / LeftShift dodge roll. Displacement, duration, i-frames, CD live in DodgeRules.
    /// Blocked by wall/door volumes. Cancels charge.
    /// </summary>
    [DefaultExecutionOrder(35)]
    public class PlayerDodge : MonoBehaviour
    {
        float _rollStart = -999f;
        Vector3 _dir = Vector3.right;
        PlayerCharge _charge;
        PlayerMotor2D _motor;

        public bool IsRolling => DodgeRules.RollActive(Time.time, _rollStart);
        public bool IsInvulnerable => DodgeRules.IFrameActive(Time.time, _rollStart);
        public bool OnCooldown => DodgeRules.OnCooldown(Time.time, _rollStart);
        public Vector3 RollDir => _dir;
        public float LastRollStart => _rollStart;

        void Awake()
        {
            _charge = GetComponent<PlayerCharge>();
            _motor = GetComponent<PlayerMotor2D>();
        }

        void Update()
        {
            if (RunPause.IsPaused)
                return;
            var vitals = GetComponent<PlayerVitals>();
            if (vitals != null && vitals.IsDead)
                return;
            if (!IsRolling && WantDodge() && CanStart())
                Begin();
            if (!IsRolling)
                return;

            float step = (DodgeRules.Distance / DodgeRules.DurationSeconds) * Time.deltaTime;
            CollisionWorld.TryMove(
                transform,
                CollisionRules.PlayerHalfX,
                CollisionRules.PlayerHalfY,
                _dir.x * step,
                _dir.y * step);
        }

        bool CanStart()
        {
            var vitals = GetComponent<PlayerVitals>();
            if (vitals != null && vitals.IsDead)
                return false;
            if (OnCooldown && _rollStart > -100f)
                return false;
            return true;
        }

        bool WantDodge()
        {
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift);
        }

        void Begin()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 0.01f)
            {
                input.Normalize();
                _dir = new Vector3(input.x, input.y, 0f);
            }
            else if (_motor != null && _motor.LastFacing.sqrMagnitude > 0.01f)
            {
                _dir = _motor.LastFacing;
            }

            _dir.z = 0f;
            if (_dir.sqrMagnitude < 0.0001f)
                _dir = Vector3.right;
            _dir.Normalize();
            _rollStart = Time.time;
            if (_charge == null)
                _charge = GetComponent<PlayerCharge>();
            if (_charge != null)
                _charge.CancelChargePublic();
            Debug.Log("[Dodge] start dir=" + _dir
                      + " dur=" + DodgeRules.DurationSeconds.ToString("0.00")
                      + "s iframe=" + DodgeRules.IFrameStartSeconds.ToString("0.00")
                      + "-" + DodgeRules.IFrameEndSeconds.ToString("0.00")
                      + "s (len=" + DodgeRules.IFrameSeconds.ToString("0.00")
                      + " suggested-unlocked) cd=" + DodgeRules.CooldownSeconds.ToString("0.00")
                      + "s dist=" + DodgeRules.Distance.ToString("0.00")
                      + " ACTION_SPEC_P1/DodgeRules");
        }
    }
}
