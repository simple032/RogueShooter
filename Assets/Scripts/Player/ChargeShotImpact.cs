using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Boss;
using RogueShooter.Combat;
using RogueShooter.Demo;

namespace RogueShooter.Player
{
    /// <summary>Applies charge-shot damage / knockback / stagger to a resolved target.</summary>
    public static class ChargeShotImpact
    {
        public static bool ApplyToMob(
            MobFourStateAi mob,
            Vector3 origin,
            Vector3 shotAway,
            float damage,
            ChargeShotKind kind,
            float heldSeconds,
            IList<string> ownedRewards)
        {
            if (mob == null)
                return false;
            bool weak = kind == ChargeShotKind.Crit;
            bool raised = mob.ShieldRaised;
            int amount = Mathf.Max(1, Mathf.RoundToInt(damage));
            float stagger = ChargeShotRules.WeakSpotStaggerSeconds;
            amount = mob.ModifyIncomingShot(origin, weak, amount, out stagger);
            var stub = mob.GetComponent<StubEnemy>();
            if (stub != null)
                stub.TakeDamage(amount);
            else
                mob.NotifyDamaged();
            if (weak)
                mob.ApplyWeakSpotStagger(stagger);
            if (FullChargeKnockback.Applies(kind, heldSeconds, ChargeProfile.FromOwned(ownedRewards).Full))
            {
                if (FullChargeKnockback.RootsOnBodyHit(mob.KindId, raised, weak))
                    mob.ApplyRoot(FullChargeKnockback.ShieldRaisedRootSeconds);
                else
                {
                    float kb = FullChargeKnockback.HitDistance(
                        mob.KindId, raised, false, weak, ownedRewards);
                    mob.ApplyKnockback(shotAway, kb);
                }
            }

            float dist = Vector3.Distance(origin, mob.transform.position);
            Debug.Log($"[ChargeShot] hit {mob.name} kind={kind} dmg={amount:0.0} dist={dist:0.00} " +
                      $"held={heldSeconds:0.00} shieldFront={(mob.ShieldRaised ? 1 : 0)} stagger={stagger:0.00}s" +
                      $" zhenshi×{KnockbackRewardDraft.DistPctProduct(ownedRewards):0.00}");
            return true;
        }

        public static bool ApplyToBoss(
            BossFightDriver boss,
            Vector3 origin,
            Vector3 shotAway,
            float damage,
            ChargeShotKind kind,
            float heldSeconds,
            IList<string> ownedRewards)
        {
            if (boss == null || !boss.FightStarted || boss.FightSettled)
                return false;
            boss.DealDamage(damage);
            bool bossWeak = kind == ChargeShotKind.Crit;
            if (bossWeak)
                boss.ApplyWeakSpotStagger(ChargeShotRules.WeakSpotStaggerSeconds);
            if (FullChargeKnockback.Applies(kind, heldSeconds, ChargeProfile.FromOwned(ownedRewards).Full))
            {
                float kb = FullChargeKnockback.HitDistance(
                    null, false, true, bossWeak, ownedRewards);
                boss.ApplyKnockback(shotAway, kb);
            }

            float dist = Vector3.Distance(origin, boss.transform.position);
            Debug.Log($"[ChargeShot] hit BOSS kind={kind} dmg={damage:0.0} dist={dist:0.00} " +
                      $"hp={boss.Brain.Hp:0}/{boss.Brain.MaxHp:0}");
            return true;
        }
    }
}
