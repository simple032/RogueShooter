using UnityEngine;
using RogueShooter.Balance;

namespace RogueShooter.Player
{
    /// <summary>
    /// Simple HP for N20 hit observation. MaxHP ref aligns with EnemyDamageCatalog (100).
    /// </summary>
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] float maxHp = 100f;

        public float MaxHp => maxHp;
        public float Hp { get; private set; }
        public float LastHitDamage { get; private set; }
        public string LastHitKind { get; private set; }

        public void Configure(float max)
        {
            maxHp = max > 1f ? max : EnemyDamageCatalog.PlayerMaxHpRef;
            Hp = maxHp;
            LastHitDamage = 0f;
            LastHitKind = null;
        }

        void Awake()
        {
            if (Hp <= 0f)
                Configure(maxHp);
        }

        public void ApplyHit(float amount, string kindId)
        {
            float dmg = amount < 0f ? 0f : amount;
            LastHitDamage = dmg;
            LastHitKind = kindId;
            Hp = Mathf.Max(0f, Hp - dmg);
            Debug.Log($"[PlayerVitals] hit kind={kindId} dmg={dmg:0.#} hp={Hp:0.#}/{MaxHp:0.#}");
        }

        public float Heal(float amount)
        {
            if (amount <= 0f || Hp <= 0f)
                return 0f;
            float before = Hp;
            Hp = Mathf.Min(maxHp, Hp + amount);
            float gained = Hp - before;
            if (gained > 0f)
                Debug.Log($"[PlayerVitals] heal +{gained:0.#} hp={Hp:0.#}/{MaxHp:0.#}");
            return gained;
        }
    }
}
