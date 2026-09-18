using UnityEngine;

namespace RogueShooter.Player
{
    /// <summary>
    /// Top-down WASD / arrow movement. No combat.
    /// </summary>
    public class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] float speed = 7f;

        public void Configure(float moveSpeed)
        {
            speed = Mathf.Max(0.1f, moveSpeed);
        }

        void Update()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            transform.position += (Vector3)(input * speed * Time.deltaTime);
        }
    }
}
