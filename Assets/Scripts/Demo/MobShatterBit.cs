using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>Death pops the current frame apart. No die clip.</summary>
    public sealed class MobShatterBit : MonoBehaviour
    {
        Vector2 _velocity;
        float _life = 0.35f;
        SpriteRenderer _renderer;

        public void Kick(Vector2 velocity)
        {
            _velocity = velocity;
            _renderer = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            _velocity *= 0.92f;
            _life -= Time.deltaTime;
            if (_renderer != null)
            {
                Color c = _renderer.color;
                c.a = Mathf.Clamp01(_life / 0.35f);
                _renderer.color = c;
            }

            if (_life <= 0f)
                Destroy(gameObject);
        }
    }
}
