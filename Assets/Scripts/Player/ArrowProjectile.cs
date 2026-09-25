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
    /// Charge-string tip stays jh_fx_charge_arrow_tip. Stops on wall/door (shaft radius) or on the mob volume it traces into; damage goes to that mob.
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

        readonly HashSet<MobFourStateAi> _hitMobs = new HashSet<MobFourStateAi>();
        bool _bossHit;

        /// <summary>Mobs this arrow already resolved damage on (tests).</summary>
        public int HitCount => _hitMobs.Count + (_bossHit ? 1 : 0);

        void Update()
        {
            if (_dead)
                return;
            float remaining = _speed * Time.deltaTime;
            // One frame can resolve several contacts (pierce): walk the segment in order.
            for (int guard = 0; guard < 8 && remaining > 0f && !_dead; guard++)
            {
                Vector3 from = transform.position;
                CollisionHit block = CollisionWorld.Trace(
                    from.x, from.y, _dir.x, _dir.y, remaining + 0.02f, ProjectileRules.ArrowBlockRadius,
                    CollisionLayer.Wall | CollisionLayer.Door, transform);
                float blockD = block.Hit ? block.Distance : float.MaxValue;

                float mobD;
                MobFourStateAi mob = SweepMob(from, _dir, remaining, _hitMobs, out mobD);
                float bossD = float.MaxValue;
                BossFightDriver boss = null;
                if (!_bossHit)
                    boss = SweepBoss(from, _dir, remaining, out bossD);
                if (boss == null)
                    bossD = float.MaxValue;
                if (mob == null)
                    mobD = float.MaxValue;

                if (blockD <= mobD && blockD <= bossD && block.Hit)
                {
                    transform.position = new Vector3(block.X, block.Y, from.z);
                    _traveled += blockD;
                    Despawn(block.Layer == CollisionLayer.Door ? "door" : "wall");
                    return;
                }

                if (boss != null && bossD <= mobD)
                {
                    Advance(from, bossD, ref remaining);
                    _bossHit = true;
                    ResolveBoss(boss, from);
                    Despawn("hit boss");
                    return;
                }

                if (mob != null)
                {
                    Advance(from, mobD, ref remaining);
                    bool pierce = ResolveMob(mob, from);
                    if (!pierce)
                    {
                        Despawn("hit");
                        return;
                    }

                    continue; // pierce: keep tracing behind, this mob is excluded now
                }

                Advance(from, remaining, ref remaining);
            }

            if (!_dead && _traveled >= _maxRange)
                Despawn("range");
        }

        void Advance(Vector3 from, float d, ref float remaining)
        {
            transform.position = from + _dir * d;
            _traveled += d;
            remaining -= d;
            if (remaining < 0f)
                remaining = 0f;
        }

        /// <summary>
        /// First live mob whose collision volume (inflated by ArrowHitRadius) the segment enters.
        /// Uses the same AABB as CollisionWorld, so a body-size change moves stop point and hit together.
        /// Dead mobs are not in MobFourStateAi.All and have their volume disabled.
        /// </summary>
        public static MobFourStateAi SweepMob(Vector3 from, Vector3 dir, float maxDist,
            ICollection<MobFourStateAi> exclude, out float dist)
        {
            dist = float.MaxValue;
            MobFourStateAi best = null;
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                MobFourStateAi mob = all[i];
                if (mob == null || !mob.isActiveAndEnabled || mob.IsDead)
                    continue;
                if (exclude != null && exclude.Contains(mob))
                    continue;
                CollisionVolume vol = mob.GetComponent<CollisionVolume>();
                if (vol == null || !vol.isActiveAndEnabled)
                    continue;
                float d = CollisionSpace.SweepDistance(from.x, from.y, dir.x, dir.y,
                    vol.WorldAabb.Inflated(ProjectileRules.ArrowHitRadius));
                if (d < 0f || d > maxDist || d >= dist)
                    continue;
                dist = d;
                best = mob;
            }

            return best;
        }

        static BossFightDriver SweepBoss(Vector3 from, Vector3 dir, float maxDist, out float dist)
        {
            dist = float.MaxValue;
            BossFightDriver boss = BossFightDriver.Live;
            if (boss == null || !boss.FightStarted || boss.FightSettled)
                return null;
            // Boss has no CollisionVolume: keep its proximity disc, swept along the segment.
            float r = ProjectileRules.ArrowHitRadius + 0.35f;
            Vector3 to = boss.transform.position - from;
            to.z = 0f;
            float along = Vector3.Dot(to, dir);
            float perp2 = to.sqrMagnitude - along * along;
            if (perp2 > r * r)
                return null;
            float d = along - Mathf.Sqrt(Mathf.Max(0f, r * r - perp2));
            if (d < 0f)
                d = to.sqrMagnitude <= r * r ? 0f : -1f;
            if (d < 0f || d > maxDist)
                return null;
            dist = d;
            return boss;
        }

        /// <summary>
        /// Single settlement path for every shot kind (weak / full / crit / focus): the mob the trace hit
        /// takes the damage. First contact = full packet; with 穿透 (pierce_back) the arrow keeps going and
        /// the next different mob takes damage × pierceAdd as a pierce packet, then the arrow stops.
        /// Returns true when the arrow should keep flying.
        /// </summary>
        bool ResolveMob(MobFourStateAi mob, Vector3 origin)
        {
            bool first = _hitMobs.Count == 0;
            _hitMobs.Add(mob);
            PlayerCharge charge = _owner != null ? _owner.GetComponent<PlayerCharge>() : null;
            float pierceAdd = charge != null ? charge.PierceBackAdd : 0f;
            if (charge != null)
                charge.ResolveArrowContact(mob, origin, _dir, first ? _dmg : _dmg * pierceAdd, _kind, _held, !first);
            else
                ChargeShotImpact.ApplyToMob(mob, origin, _dir, _dmg, _kind, _held, _owned);
            Debug.Log("[Arrow] hit " + mob.name + (first ? "" : " (pierce)") + " kind=" + _kind
                      + " at d=" + _traveled.ToString("0.00"));
            return first && pierceAdd > 0f;
        }

        void ResolveBoss(BossFightDriver boss, Vector3 origin)
        {
            PlayerCharge ownerCharge = _owner != null ? _owner.GetComponent<PlayerCharge>() : null;
            if (ownerCharge != null)
                ownerCharge.ResolveArrowHitBoss(boss, origin, _dir, _dmg, _kind, _held);
            else
                ChargeShotImpact.ApplyToBoss(boss, origin, _dir, _dmg, _kind, _held, _owned);
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
