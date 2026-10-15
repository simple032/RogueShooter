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
        float _levelBonus;
        float _rewardMaxMul = 1f;

        public float MaxHp => maxHp;
        public float Hp { get; private set; }
        public bool IsDown => Hp <= 0.001f;
        public float LastHitDamage { get; private set; }
        public string LastHitKind { get; private set; }
        public int DamageSerial { get; private set; }

        public void Configure(float max)
        {
            maxHp = max > 1f ? max : EnemyDamageCatalog.PlayerMaxHpRef;
            _baseMax = maxHp;
            _levelBonus = 0f; _rewardMaxMul = 1f;
            Hp = maxHp;
            LastHitDamage = 0f;
            LastHitKind = null;
        }

        /// <summary>Catalog max_hp product, from the configured base. Does not stack on itself.</summary>
        public void ApplyRewardMax(float mul)
        {
            float m = mul > 0.01f ? mul : 1f;
            _rewardMaxMul = m;
            float basis = (_baseMax > 1f ? _baseMax : maxHp) + _levelBonus;
            float next = basis * m;
            float gained = next - maxHp;
            maxHp = next;
            if (gained > 0f && Hp > 0f)
                Hp = Mathf.Min(maxHp, Hp + gained);
            else Hp = Mathf.Min(Hp, maxHp);
        }

        /// <summary>Level changes preserve the fraction of final HP, including bone armor.</summary>
        public void ApplyLevelBonus(float bonus)
        {
            float ratio = maxHp > .001f ? Mathf.Clamp01(Hp / maxHp) : 0f;
            _levelBonus = Mathf.Max(0f, bonus);
            maxHp = (_baseMax + _levelBonus) * _rewardMaxMul;
            Hp = ratio * maxHp;
        }

        void Awake()
        {
            if (Hp <= 0f)
                Configure(maxHp);
        }

        public void ApplyHit(float amount,string kindId)=>ApplyHit(amount,kindId,Vector3.zero);
        /// <summary>Impact direction is supplied by the real hit source; no added invulnerability.</summary>
        public void ApplyHit(float amount, string kindId,Vector3 impactAway)
        {
            if (RunPause.IsPaused || IsDown)
                return;
            float dmg = amount < 0f ? 0f : amount;
            LastHitDamage = dmg;
            LastHitKind = kindId;
            Hp = Mathf.Max(0f, Hp - dmg);
            if(dmg>0){DamageSerial++;RogueShooter.Audio.JianHaiAudio.Emit("jh_player_hurt",this);}
            if(dmg>0)GetComponent<ConditionalShotPerks>()?.NotifyHurt();
            if (!IsDown && dmg>0) GetComponent<PlayerMotor2D>()?.ApplyImpact(impactAway);
            if (IsDown)
                RunPause.EnterCombatEnd();
            RogueShooter.Demo.GameTrace.Log($"[PlayerVitals] hit kind={kindId} dmg={dmg:0.#} hp={Hp:0.#}/{MaxHp:0.#}");
        }

        public float Heal(float amount)
        {
            if (amount <= 0f || Hp <= 0f)
                return 0f;
            float before = Hp;
            Hp = Mathf.Min(maxHp, Hp + amount);
            float gained = Hp - before;
            if (gained > 0f)
                RogueShooter.Demo.GameTrace.Log($"[PlayerVitals] heal +{gained:0.#} hp={Hp:0.#}/{MaxHp:0.#}");
            return gained;
        }
    }
}
