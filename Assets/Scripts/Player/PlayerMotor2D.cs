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
            _dodge = GetComponent<PlayerDodge>();
        }

        void Update()
        {
            if (RunPause.IsPaused)
                return;
            if (_charge == null)
                _charge = GetComponent<PlayerCharge>();
            if (_dodge == null)
                _dodge = GetComponent<PlayerDodge>();
            if (_dodge != null && _dodge.IsRolling)
                return;
            var vitals = GetComponent<PlayerVitals>();
            if (vitals != null && vitals.IsDead)
                return;
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            _lastInput = input;
            if (input.sqrMagnitude > 0.01f)
                _lastFacing = new Vector3(input.x, input.y, 0f);
            Vector2 delta = input * CurrentSpeed * Time.deltaTime;
            CollisionWorld.TryMove(
                transform,
                CollisionRules.PlayerHalfX,
                CollisionRules.PlayerHalfY,
                delta.x, delta.y);
        }
    }
}
