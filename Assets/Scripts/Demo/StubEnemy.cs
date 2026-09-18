using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Placeholder enemy. Combat is a stub; four-state AI lives on MobFourStateAi.
    /// </summary>
    public class StubEnemy : MonoBehaviour
    {
        [SerializeField] float pulse = 2.4f;

        Vector3 _baseScale;
        public event System.Action Damaged;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void TakeDamage(int amount)
        {
            Damaged?.Invoke();
        }

        void Update()
        {
            float s = 1f + 0.08f * Mathf.Sin(Time.time * pulse);
            transform.localScale = _baseScale * s;
        }
    }
}
