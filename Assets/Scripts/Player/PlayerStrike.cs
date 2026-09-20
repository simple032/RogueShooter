using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Boss;

namespace RogueShooter.Player
{
    /// <summary>
    /// Stub melee: F damages the nearest four-state mob in range (triggers Alert/Chase).
    /// </summary>
    public class PlayerStrike : MonoBehaviour
    {
        [SerializeField] float range = 1.85f;

        public void Configure(float strikeRange)
        {
            if (strikeRange > 0.05f)
                range = strikeRange;
        }

        void Update()
        {
            if (RunPause.IsPaused)
                return;
            if (!Input.GetKeyDown(KeyCode.F))
                return;

            MobFourStateAi best = null;
            float bestD = range;
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                MobFourStateAi mob = all[i];
                if (mob == null || !mob.isActiveAndEnabled)
                    continue;
                float d = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.y),
                    new Vector2(mob.transform.position.x, mob.transform.position.y));
                if (d <= bestD)
                {
                    bestD = d;
                    best = mob;
                }
            }

            BossFightDriver boss = BossFightDriver.Live;
            float bossD = float.MaxValue;
            bool bossInRange = boss != null && boss.FightStarted && !boss.FightSettled;
            if (bossInRange)
            {
                bossD = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.y),
                    new Vector2(boss.transform.position.x, boss.transform.position.y));
                bossInRange = bossD <= range;
            }

            if (best == null && !bossInRange)
            {
                Debug.Log("[Strike] miss (no mob in " + range.ToString("0.00") + ")");
                return;
            }

            if (bossInRange && (best == null || bossD <= bestD))
            {
                boss.DealDamage(1f);
                Debug.Log("[Strike] hit BOSS dist=" + bossD.ToString("0.00")
                    + " hp=" + boss.Brain.Hp.ToString("0") + "/" + boss.Brain.MaxHp.ToString("0"));
                return;
            }

            var stub = best.GetComponent<RogueShooter.Demo.StubEnemy>();
            if (stub != null)
                stub.TakeDamage(1);
            else
                best.NotifyDamaged();
            Debug.Log("[Strike] hit " + best.name + " dist=" + bestD.ToString("0.00") + " → Alert/Chase");
        }
    }
}
