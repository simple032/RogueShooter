using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Placeholder enemy. Combat is a stub; four-state AI lives on MobFourStateAi.
    /// </summary>
    public class StubEnemy : MonoBehaviour
    {
        [SerializeField] float pulse = 2.4f;
        [SerializeField] int hitPoints = 1;

        Vector3 _baseScale;
        float _pressureScale = 1f;
        string _kindId = "E1";
        bool _dead;

        public string KindId => _kindId;
        public bool IsDead => _dead;
        public event System.Action Damaged;
        public event System.Action<StubEnemy> Died;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        bool _elite;

        public bool Elite => _elite;

        public void ConfigureKind(string kindId, int hp = 1)
        {
            ConfigureKind(kindId, hp, false);
        }

        public void ConfigureKind(string kindId, int hp, bool elite)
        {
            _kindId = string.IsNullOrEmpty(kindId) ? "E1" : kindId;
            hitPoints = hp < 1 ? 1 : hp;
            _elite = elite;
            _dead = false;
            if (elite)
                _pressureScale = 1.18f;
        }

        public void SetPressureScale(float scale)
        {
            _pressureScale = scale > 0.01f ? scale : 1f;
        }

        public void TakeDamage(int amount)
        {
            if (_dead)
                return;
            Damaged?.Invoke();
            hitPoints -= amount < 1 ? 1 : amount;
            if (hitPoints > 0)
                return;
            _dead = true;
            Died?.Invoke(this);
        }

        void Update()
        {
            if (_dead)
                return;
            float s = 1f + 0.08f * Mathf.Sin(Time.time * pulse);
            transform.localScale = _baseScale * _pressureScale * s;
        }
    }
}
