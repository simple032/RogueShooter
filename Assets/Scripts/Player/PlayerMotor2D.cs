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
        }

        void Update()
        {
            if (RunPause.IsPaused)
                return;
            if (_charge == null)
                _charge = GetComponent<PlayerCharge>();
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            transform.position += (Vector3)(input * CurrentSpeed * Time.deltaTime);
        }
    }
}
