using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Boss;
using RogueShooter.Combat;

namespace RogueShooter.Player
{
    /// <summary>
    /// Flying charge arrow. Visual = jh_proj_arrow_fly (faces +X, program rotates).
    /// Charge-string tip stays jh_fx_charge_arrow_tip. Stops on wall/door or first mob/boss.
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        Vector3 _dir;
        float _speed;
        float _maxRange;
        float _traveled;
        float _dmg;
        ChargeShotKind _kind;
        float _held;
        IList<string> _owned;
        Transform _owner;
        bool _dead;

        public bool Alive => !_dead && isActiveAndEnabled;

        public static ArrowProjectile Spawn(
            Vector3 origin,
            Vector3 direction,
            float damage,
            ChargeShotKind kind,
            float heldSeconds,
            IList<string> ownedRewards,
            Transform owner,
            float maxRange)
        {
            var go = new GameObject("Arrow_" + kind);
            go.transform.position = origin;
            JianHaiBind.ApplyTo(go, JianHaiArtCatalog.ArrowFlight);
            CollisionVolume.Add(go, CollisionLayer.Projectile, false, ProjectileRules.ArrowHitRadius, ProjectileRules.ArrowHitRadius);
            go.AddComponent<ProjectileTrail>().Configure(
                JianHaiArtCatalog.FxTipIdle, 0.045f, 0.14f, 0.38f);
            var arrow = go.AddComponent<ArrowProjectile>();
            arrow.Launch(origin, direction, damage, kind, heldSeconds, ownedRewards, owner, maxRange);
            return arrow;
        }

        public void Launch(
            Vector3 origin,
            Vector3 direction,
            float damage,
            ChargeShotKind kind,
            float heldSeconds,
            IList<string> ownedRewards,
            Transform owner,
            float maxRange)
        {
            transform.position = origin;
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.right;
            _dir = direction.normalized;
            float ang = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
            _speed = ProjectileRules.ArrowSpeed;
            _maxRange = maxRange > 0.01f ? maxRange : ProjectileRules.ArrowMaxRange;
            _traveled = 0f;
            _dmg = damage;
            _kind = kind;
            _held = heldSeconds;
            _owned = ownedRewards;
            _owner = owner;
            _dead = false;
            Debug.Log("[Arrow] fire kind=" + kind
                      + " dmg=" + damage.ToString("0.0")
                      + " speed=" + _speed.ToString("0.00")
                      + " range≤" + _maxRange.ToString("0.00")
                      + " art=" + JianHaiArtCatalog.ArrowFlight);
        }

        void Update()
        {
            if (_dead)
                return;
            float step = _speed * Time.deltaTime;
            Vector3 from = transform.position;
            CollisionHit block = CollisionWorld.Trace(
                from.x, from.y, _dir.x, _dir.y, step + 0.02f, ProjectileRules.ArrowHitRadius,
                CollisionLayer.Wall | CollisionLayer.Door, transform);
            if (block.Hit)
            {
                transform.position = new Vector3(block.X, block.Y, from.z);
                Despawn(block.Layer == CollisionLayer.Door ? "door" : "wall");
                return;
            }

            CollisionHit victim = CollisionWorld.Trace(
                from.x, from.y, _dir.x, _dir.y, step + ProjectileRules.ArrowHitRadius,
                ProjectileRules.ArrowHitRadius,
                CollisionLayer.Mob, _owner);
            if (victim.Hit)
            {
                transform.position = new Vector3(victim.X, victim.Y, from.z);
                HitNearest(from);
                return;
            }

            if (HitNearest(from))
                return;

            transform.position = from + _dir * step;
            _traveled += step;
            if (_traveled >= _maxRange)
                Despawn("range");
        }

        bool HitNearest(Vector3 origin)
        {
            MobFourStateAi best = null;
            float bestD = ProjectileRules.ArrowHitRadius + 0.35f;
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                MobFourStateAi mob = all[i];
                if (mob == null || !mob.isActiveAndEnabled)
                    continue;
                Vector3 d = mob.transform.position - transform.position;
                d.z = 0f;
                float dist = d.magnitude;
                if (dist <= bestD)
                {
                    bestD = dist;
                    best = mob;
                }
            }

            BossFightDriver boss = BossFightDriver.Live;
            if (boss != null && boss.FightStarted && !boss.FightSettled)
            {
                float bd = Vector3.Distance(transform.position, boss.transform.position);
                if (bd <= bestD)
                {
                    PlayerCharge ownerCharge = _owner != null ? _owner.GetComponent<PlayerCharge>() : null;
                    if (ownerCharge != null)
                        ownerCharge.ResolveArrowHitBoss(boss, origin, _dir, _dmg, _kind, _held);
                    else
                        ChargeShotImpact.ApplyToBoss(boss, origin, _dir, _dmg, _kind, _held, _owned);
                    Despawn("hit");
                    return true;
                }
            }

            if (best == null)
                return false;
            // A's rules (reward ModifyOutgoing / pierce / lifesteal) live on the owner's PlayerCharge.
            PlayerCharge charge = _owner != null ? _owner.GetComponent<PlayerCharge>() : null;
            if (charge != null)
                charge.ResolveArrowHitMob(best, origin, _dir, _dmg, _kind, _held);
            else
                ChargeShotImpact.ApplyToMob(best, origin, _dir, _dmg, _kind, _held, _owned);
            Despawn("hit");
            return true;
        }

        void Despawn(string reason)
        {
            if (_dead)
                return;
            _dead = true;
            Debug.Log("[Arrow] despawn " + reason + " traveled=" + _traveled.ToString("0.00"));
            Destroy(gameObject);
        }
    }
}
