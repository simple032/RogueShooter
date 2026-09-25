using UnityEngine;
using RogueShooter.Combat;
using RogueShooter.Vision;

namespace RogueShooter.Player
{
    /// <summary>
    /// Space / LeftShift dodge roll. Table lives in DodgeRules (draft, unlocked).
    /// Cancels charge and shot recovery. No fire while rolling. No stamina.
    /// </summary>
    [DefaultExecutionOrder(35)]
    public class PlayerDodge : MonoBehaviour
    {
        float _rollStart = -999f;
        Vector3 _dir = Vector3.right;
        PlayerCharge _charge;
        PlayerMotor2D _motor;
        Rigidbody2D _body;

        public bool IsRolling => DodgeRules.RollActive(Time.time, _rollStart);
        public bool IsInvulnerable => DodgeRules.IFrameActive(Time.time, _rollStart);
        public bool OnCooldown => DodgeRules.OnCooldown(Time.time, _rollStart);
        /// <summary>Logic-space unit roll direction.</summary>
        public Vector3 RollDir => _dir;

        /// <summary>
        /// Roll direction in logic space (unit): input (screen WASD → logic), else motor facing,
        /// else previous, else +X. Iso off: same as the previous inline code.
        /// </summary>
        public static Vector3 RollDirFrom(Vector2 rawInput, Vector3 lastFacing, Vector3 previous, bool iso)
        {
            Vector3 dir = previous;
            Vector2 input = ViewSpace.InputToLogic(rawInput, iso);
            if (input.sqrMagnitude > 0.01f)
            {
                input.Normalize();
                dir = new Vector3(input.x, input.y, 0f);
            }
            else if (lastFacing.sqrMagnitude > 0.01f)
            {
                dir = lastFacing;
            }

            dir.z = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.right;
            return dir.normalized;
        }
        public float LastRollStart => _rollStart;

        void Awake()
        {
            _charge = GetComponent<PlayerCharge>();
            _motor = GetComponent<PlayerMotor2D>();
            _body = GetComponent<Rigidbody2D>();
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

            if (_body == null)
                _body = GetComponent<Rigidbody2D>();
            if (_body != null)
            {
                // A's painted Stage1 player is a Rigidbody2D: roll via velocity so the
                // Walls TilemapCollider2D / door BoxCollider2D still stop it.
                _body.velocity = new Vector2(_dir.x, _dir.y) * DodgeRules.RollSpeed;
                return;
            }

            float step = DodgeRules.RollSpeed * Time.deltaTime;
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

        /// <summary>
        /// Charge, atk 后摇, and recover do not block the roll — DodgeRules cancel flags.
        /// </summary>
        public static bool RecoveryBlocksRoll()
        {
            return !DodgeRules.CancelCharge && !DodgeRules.CancelShotRecovery;
        }

        bool WantDodge()
        {
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift);
        }

        void Begin()
        {
            Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _dir = RollDirFrom(raw, _motor != null ? _motor.LastFacing : Vector3.zero, _dir, ViewSpace.IsoOn);
            _rollStart = Time.time;
            if (_charge == null)
                _charge = GetComponent<PlayerCharge>();
            if (_charge != null)
                _charge.CancelIntoRoll();
            Debug.Log("[Dodge] start dir=" + _dir
                      + " dur=" + DodgeRules.DurationSeconds.ToString("0.00")
                      + "s iframe=" + DodgeRules.IFrameStartSeconds.ToString("0.00")
                      + "-" + DodgeRules.IFrameEndSeconds.ToString("0.00")
                      + "s (len=" + DodgeRules.IFrameSeconds.ToString("0.00")
                      + " draft-unlocked) cd=" + DodgeRules.CooldownSeconds.ToString("0.00")
                      + "s dist=" + DodgeRules.Distance.ToString("0.00")
                      + " cancelCharge=" + (DodgeRules.CancelCharge ? 1 : 0)
                      + " cancelRecover=" + (DodgeRules.CancelShotRecovery ? 1 : 0)
                      + " blockFire=" + (DodgeRules.BlockFireWhileRolling ? 1 : 0)
                      + " stamina=" + DodgeRules.StaminaCost.ToString("0")
                      + " ACTION_SPEC_P1/DodgeRules");
        }
    }
}
