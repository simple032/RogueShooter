using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Combat;

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
        public Vector3 LastFacing => _lastFacing;
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
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            _lastInput = input;
            if (input.sqrMagnitude > 0.01f)
                _lastFacing = new Vector3(input.x, input.y, 0f);
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
