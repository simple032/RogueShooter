using UnityEngine;
using RogueShooter.Balance;

namespace RogueShooter.Player
{
    /// <summary>
    /// Top-down WASD / arrow movement. No combat.
    /// </summary>
    public class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] float speed = MoveSpeeds.Player;

        PlayerCharge _charge;
        Rigidbody2D _body;

        public float BaseSpeed => speed;
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
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            if (!physics)
            {
                transform.position += (Vector3)(input * CurrentSpeed * dt);
                return;
            }
            _body.velocity = input * CurrentSpeed;
        }
    }
}
