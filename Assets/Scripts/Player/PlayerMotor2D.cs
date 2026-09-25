using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Combat;
using RogueShooter.Vision;

namespace RogueShooter.Player
{
    /// <summary>
    /// Top-down WASD / arrow movement. Slides on wall/door volumes.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] float speed = MoveSpeeds.Player;

        PlayerCharge _charge;
        Rigidbody2D _body;
        PlayerDodge _dodge;
        Vector3 _lastFacing = Vector3.right;
        Vector2 _lastInput;

        public float BaseSpeed => speed;
        public bool HasMoveInput => _lastInput.sqrMagnitude > 0.01f;
        /// <summary>Logic-space unit vector of the last meaningful move input.</summary>
        public Vector3 LastFacing => _lastFacing;

        /// <summary>
        /// Raw WASD (screen directions) → logic move vector, length ≤ 1. Iso off: identical to the
        /// previous code (normalize only when longer than 1). Iso on: via <see cref="ViewSpace.InputToLogic(Vector2, bool)"/>.
        /// </summary>
        public static Vector2 MoveInput(Vector2 raw, bool iso)
        {
            Vector2 input = ViewSpace.InputToLogic(raw, iso);
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            return input;
        }

        /// <summary>Facing update: logic unit vector of <paramref name="input"/>, else keep previous.</summary>
        public static Vector3 FacingFromInput(Vector2 input, Vector3 previous)
        {
            if (input.sqrMagnitude > 0.01f)
                return new Vector3(input.x, input.y, 0f).normalized;
            previous.z = 0f;
            return previous.sqrMagnitude > 0.0001f ? previous.normalized : Vector3.right;
        }
        public float CurrentSpeed
        {
            get
            {
                float mul = _charge != null && _charge.IsCharging ? MoveSpeeds.ChargeMul : 1f;
                return speed * mul;
            }
        }

        public void Configure(float moveSpeed)
        {
            speed = moveSpeed > 0.0001f ? moveSpeed : MoveSpeeds.Player;
        }

        void Awake()
        {
            _charge = GetComponent<PlayerCharge>();
            _body = GetComponent<Rigidbody2D>();
            _dodge = GetComponent<PlayerDodge>();
        }

        void Update()
        {
            if (_body != null)
                return;
            Step(Time.deltaTime, false);
        }

        void FixedUpdate()
        {
            if (_body == null)
                return;
            Step(Time.fixedDeltaTime, true);
        }

        void Step(float dt, bool physics)
        {
            if (RunPause.IsPaused)
            {
                if (physics)
                    _body.velocity = Vector2.zero;
                return;
            }
            if (_charge == null)
                _charge = GetComponent<PlayerCharge>();
            if (_dodge == null)
                _dodge = GetComponent<PlayerDodge>();
            if (_dodge != null && _dodge.IsRolling)
                return; // PlayerDodge drives position (and body velocity) while rolling.
            var vitals = GetComponent<PlayerVitals>();
            if (vitals != null && vitals.IsDead)
            {
                if (physics)
                    _body.velocity = Vector2.zero;
                return;
            }
            Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Vector2 input = MoveInput(raw, ViewSpace.IsoOn);
            _lastInput = input;
            _lastFacing = FacingFromInput(input, _lastFacing);
            if (physics)
            {
                // A: painted Stage1 — Rigidbody2D vs Walls TilemapCollider2D.
                _body.velocity = input * CurrentSpeed;
                return;
            }
            // No body (procedural skeleton): PR#10 AABB collision world.
            Vector2 delta = input * CurrentSpeed * dt;
            CollisionWorld.TryMove(
                transform,
                CollisionRules.PlayerHalfX,
                CollisionRules.PlayerHalfY,
                delta.x, delta.y);
        }
    }
}
