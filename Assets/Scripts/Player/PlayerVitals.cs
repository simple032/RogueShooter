using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Combat;

namespace RogueShooter.Player
{
    /// <summary>
    /// Simple HP for N20 hit observation. MaxHP ref aligns with EnemyDamageCatalog (100).
    /// Hurt clip 0.25s (ACTION_SPEC); death holds die clip. Hurt does not cancel roll.
    /// </summary>
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] float maxHp = 100f;
        float _hurtUntil;

        public float MaxHp => maxHp;
        public float Hp { get; private set; }
        public float LastHitDamage { get; private set; }
        public string LastHitKind { get; private set; }
        public bool IsDead => Hp <= 0.001f;
        public bool IsHurting => !IsDead && Time.time < _hurtUntil;

        public void Configure(float max)
        {
            maxHp = max > 1f ? max : EnemyDamageCatalog.PlayerMaxHpRef;
            Hp = maxHp;
            LastHitDamage = 0f;
            LastHitKind = null;
            _hurtUntil = 0f;
        }

        void Awake()
        {
            if (Hp <= 0f)
                Configure(maxHp);
        }

        public static bool HitBlockedByIFrame(bool iframeActive)
        {
            return iframeActive;
        }

        public void ApplyHit(float amount, string kindId)
        {
            var dodge = GetComponent<PlayerDodge>();
            if (HitBlockedByIFrame(dodge != null && dodge.IsInvulnerable))
            {
                LastHitDamage = 0f;
                LastHitKind = "IFRAME";
                Debug.Log($"[PlayerVitals] iframe blocked kind={kindId} hp={Hp:0.#}/{MaxHp:0.#}");
                return;
            }

            float dmg = amount < 0f ? 0f : amount;
            LastHitDamage = dmg;
            LastHitKind = kindId;
            Hp = Mathf.Max(0f, Hp - dmg);
            if (Hp <= 0.001f)
            {
                Debug.Log($"[PlayerVitals] death kind={kindId} dmg={dmg:0.#}");
                return;
            }

            if (dodge != null && dodge.IsRolling)
            {
                Debug.Log($"[PlayerVitals] hit kind={kindId} dmg={dmg:0.#} hp={Hp:0.#}/{MaxHp:0.#} (roll; hurt clip skipped)");
                return;
            }

            _hurtUntil = Time.time + ActionSpecP1.PlayerHurt.Duration;
            Debug.Log($"[PlayerVitals] hit kind={kindId} dmg={dmg:0.#} hp={Hp:0.#}/{MaxHp:0.#}");
        }
    }
}
