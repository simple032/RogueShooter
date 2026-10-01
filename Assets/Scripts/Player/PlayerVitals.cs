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
        float _baseMax;

        public float MaxHp => maxHp;
        public float Hp { get; private set; }
        public bool IsDown => Hp <= 0.001f;
        public float LastHitDamage { get; private set; }
        public string LastHitKind { get; private set; }

        public void Configure(float max)
        {
            maxHp = max > 1f ? max : EnemyDamageCatalog.PlayerMaxHpRef;
            _baseMax = maxHp;
            Hp = maxHp;
            LastHitDamage = 0f;
            LastHitKind = null;
        }

        /// <summary>Catalog max_hp product, from the configured base. Does not stack on itself.</summary>
        public void ApplyRewardMax(float mul)
        {
            float m = mul > 0.01f ? mul : 1f;
            float basis = _baseMax > 1f ? _baseMax : maxHp;
            float next = basis * m;
            float gained = next - maxHp;
            maxHp = next;
            if (gained > 0f && Hp > 0f)
                Hp = Mathf.Min(maxHp, Hp + gained);
        }

        void Awake()
        {
            if (Hp <= 0f)
                Configure(maxHp);
        }

        public void ApplyHit(float amount, string kindId)
        {
            if (RunPause.RunSettled)
                return;
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
