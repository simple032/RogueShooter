using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Boss;
using RogueShooter.Build;
using RogueShooter.Demo;
using RogueShooter.Maze;
using RogueShooter.Vision;

namespace RogueShooter.Player
{
    /// <summary>
    /// Hold-to-charge bow. Ring full at 0.70s; no arrow under 0.30s; 0.30s can fire.
    /// Weak under 0.4s x0.50; weak-spot 0.68-0.72s. After a shot, 0.2s recovery.
    /// Movement x0.5 while charging.
    /// </summary>
    public class PlayerCharge : MonoBehaviour
    {
        [SerializeField] float hitRange = 8f;

        float _held;
        bool _charging;
        bool _mid;
        bool _green;
        bool _exited;
        float _recoverUntil;
        ChargeFxView _fx;
        GuaranteedCritActive _guaranteed;
        IList<string> _ownedRewards;
        Stage1Maze _maze;

        public bool IsCharging => _charging;
        public bool InRecovery => Time.time < _recoverUntil;
        public float HeldSeconds => _held;
        /// <summary>Where the bow is aimed right now (mouse). Updated while charging.</summary>
        public Vector3 AimDir { get; private set; }
        public ChargeShotKind LastShot { get; private set; }
        public float LastDamage { get; private set; }
        public int WallStops { get; private set; }
        public int PathHits { get; private set; }
        public ShotRead CurrentRead => ShotRead.From(_ownedRewards);

        public void BindOwnedRewards(IList<string> owned)
        {
            _ownedRewards = owned;
        }

        public float DamageFor(ChargeShotKind kind, ShotRead read)
        {
            float dmg = ChargeShotRules.DamageWithBuild(kind, read.DamageProduct);
            if (kind == ChargeShotKind.Crit)
                dmg *= RewardStatHooks.ProductMul(_ownedRewards, "crit_damage");
            return dmg;
        }

        public void BindMaze(Stage1Maze maze)
        {
            _maze = maze;
        }

        void Awake()
        {
            _fx = GetComponent<ChargeFxView>();
            if (_fx == null)
                _fx = gameObject.AddComponent<ChargeFxView>();
            _guaranteed = GetComponent<GuaranteedCritActive>();
        }

        void Update()
        {
            if (RunPause.IsPaused || Down())
            {
                if (_charging)
                    CancelCharge();
                return;
            }

            bool hold = Input.GetMouseButton(0) || Input.GetKey(KeyCode.C);
            if (!_charging)
            {
                if (hold && Time.time >= _recoverUntil && !RollLocked())
                    BeginCharge();
                return;
            }

            if (!hold)
            {
                ReleaseCharge();
                return;
            }

            _held += Time.deltaTime;
            AimDir = AimDirection();
            ShotRead read = CurrentRead;
            float p = read.Progress(_held);
            if (_fx != null)
                _fx.SetChargeProgress(p, _green);
            if (!_mid && _held >= ChargeFxHooks.MidAt)
            {
                _mid = true;
                ChargeFxHooks.ChargeMid();
                Debug.Log("[ChargeFx] OnChargeMid");
            }

            if (!_green && _held >= read.GreenEnter)
            {
                _green = true;
                ChargeFxHooks.ChargeEnterGreen();
                Debug.Log("[ChargeFx] OnChargeEnterGreen");
            }

            if (_green && !_exited && _held > read.GreenExit)
            {
                _exited = true;
                _green = false;
                ChargeFxHooks.ChargeExitGreen();
                Debug.Log("[ChargeFx] OnChargeExitGreen");
            }
        }

        void BeginCharge()
        {
            _charging = true;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            LastShot = ChargeShotKind.None;
            LastDamage = 0f;
            if (_fx != null)
                _fx.SetChargeProgress(0f, false);
        }

        void ReleaseCharge()
        {
            if (RollLocked())
            {
                CancelCharge();
                LastShot = ChargeShotKind.None;
                LastDamage = 0f;
                return;
            }

            float held = _held;
            _charging = false;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            Fire(held);
        }

        /// <summary>Test/helper: resolve and fire as if released after heldSeconds.</summary>
        public ChargeShotKind FireAtHeld(float heldSeconds)
        {
            return FireAtHeld(heldSeconds, Vector3.zero);
        }

        /// <summary>Same as FireAtHeld, along a world aim. Zero uses the mouse.</summary>
        public ChargeShotKind FireAtHeld(float heldSeconds, Vector3 aim)
        {
            if (Down())
            {
                CancelCharge();
                LastShot = ChargeShotKind.None;
                LastDamage = 0f;
                return ChargeShotKind.None;
            }

            _charging = false;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            return Fire(heldSeconds, aim);
        }

        ChargeShotKind Fire(float heldSeconds)
        {
            return Fire(heldSeconds, Vector3.zero);
        }

        ChargeShotKind Fire(float heldSeconds, Vector3 aim)
        {
            if (RunPause.IsPaused)
            {
                CancelCharge();
                LastShot = ChargeShotKind.None;
                LastDamage = 0f;
                return ChargeShotKind.None;
            }

            ShotRead read = CurrentRead;
            ChargeShotKind kind = read.Resolve(heldSeconds);
            if (_guaranteed == null)
                _guaranteed = GetComponent<GuaranteedCritActive>();
            if (_guaranteed != null && _guaranteed.TryForceCrit(kind, out ChargeShotKind forced))
                kind = forced;

            float dmg = DamageFor(kind, read);
            LastShot = kind;
            LastDamage = dmg;
            float p = read.Progress(heldSeconds);

            if (kind == ChargeShotKind.None)
            {
                Debug.Log($"[ChargeShot] NO_SHOT held={heldSeconds:0.000}s p={p:0.00} (<{ChargeShotRules.MinChargeSeconds:0.00}s)");
                if (_fx != null)
                    _fx.HideAll();
                return kind;
            }

            if (kind == ChargeShotKind.Crit)
            {
                ChargeFxHooks.CritConfirm();
                Debug.Log("[ChargeFx] OnCritConfirm");
            }
            else if (_fx != null)
                _fx.HideAll();

            _recoverUntil = Time.time + ChargeShotRules.RecoverSeconds;
            Debug.Log($"[ChargeShot] {kind} dmg={dmg:0.0} held={heldSeconds:0.000}s p={p:0.00} " +
                      $"recover={ChargeShotRules.RecoverSeconds:0.00}s " +
                      $"(weak×{ChargeShotRules.WeakMul:0.00} full×{ChargeShotRules.FullMul:0.00} crit×{ChargeShotRules.CritMul:0.00}) " +
                      $"read dmg×{read.DamageProduct:0.00} charge={read.ChargeSeconds:0.000} " +
                      $"window={read.GreenEnter:0.000}-{read.GreenExit:0.000}");

            LaunchArrow(dmg, kind, heldSeconds, read, aim);
            return kind;
        }

        bool RollLocked()
        {
            PlayerRoll roll = GetComponent<PlayerRoll>();
            return roll != null && roll.LocksActions;
        }

        bool Down()
        {
            PlayerVitals vitals = GetComponent<PlayerVitals>();
            return vitals != null && vitals.IsDown;
        }

        void LaunchArrow(float damage, ChargeShotKind kind, float heldSeconds, ShotRead read, Vector3 aim)
        {
            RewardStatHooks.SyncClock(Time.time);
            Vector3 origin = transform.position;
            if (aim.sqrMagnitude < 0.01f)
                aim = AimDirection();
            aim.z = 0f;
            if (aim.sqrMagnitude < 0.01f)
                aim = Vector3.right;
            aim.Normalize();
            // The arrow leaves from in front of the body, along the shot direction.
            Vector3 muzzle = origin + aim * 0.9f;
            Vector3 land = muzzle + aim * hitRange;
            land.z = muzzle.z;
            float locked = damage;
            float ring = read.ChargeSeconds;
            float pierce = RewardStatHooks.PierceBackAdd(_ownedRewards);
            int hits = 0;
            var seen = new HashSet<int>();
            Stage1Maze maze = _maze;
            Vector3 shotOrigin = origin;
            Vector3 shotAim = aim;

            // Draw the bow-release pose while the arrow flies.
            var actor = GetComponent<Stage1IsoActor>();
            if (actor != null)
                actor.TryBeginAttack(null);

            var go = new GameObject("Arrow");
            var fly = go.AddComponent<ArrowFly>();
            fly.Launch(muzzle, land, (a, b) =>
            {
                float wallT = WallDistance(maze, a, b);
                if (maze != null && !maze.OpenAt(a.x, a.y))
                {
                    WallStops++;
                    Debug.Log("[ChargeShot] wall");
                    return ArrowPathStop.Wall;
                }

                float bodyT;
                MobFourStateAi mob;
                BossFightDriver boss;
                if (!ClosestBody(a, b, wallT, seen, out mob, out boss, out bodyT))
                {
                    if (wallT < 1.5f)
                    {
                        WallStops++;
                        Debug.Log("[ChargeShot] wall");
                        return ArrowPathStop.Wall;
                    }

                    return ArrowPathStop.Clear;
                }

                bool piercePacket = hits > 0;
                float packet = piercePacket ? locked * pierce : locked;
                if (mob != null)
                {
                    seen.Add(mob.GetInstanceID());
                    float dealt = HitMob(mob, packet, kind, heldSeconds, shotOrigin, shotAim, piercePacket, ring);
                    ApplyLifesteal(dealt);
                }
                else if (boss != null)
                {
                    seen.Add(boss.GetInstanceID());
                    HitBoss(boss, packet, kind, heldSeconds, shotAim, piercePacket, ring, bodyT);
                }

                hits++;
                PathHits++;
                bool more = !piercePacket && pierce > 0.0001f && wallT > 1.5f;
                return more ? ArrowPathStop.Clear : ArrowPathStop.Hit;
            }, () => Debug.Log($"[ChargeShot] miss kind={kind}"));
        }

        static float WallDistance(Stage1Maze maze, Vector3 a, Vector3 b)
        {
            if (maze == null)
                return 2f;
            float len = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                float x = Mathf.Lerp(a.x, b.x, u);
                float y = Mathf.Lerp(a.y, b.y, u);
                if (!maze.OpenAt(x, y))
                {
                    if (i == 1 && maze.OpenAt(a.x, a.y))
                        return 0.5f / n;
                    return (i - 1) / (float)n;
                }
            }

            return 2f;
        }

        static bool ClosestBody(Vector3 a, Vector3 b, float wallT, HashSet<int> seen,
            out MobFourStateAi mob, out BossFightDriver boss, out float bodyT)
        {
            mob = null;
            boss = null;
            bodyT = wallT;
            float radius = Stage1Coin.BodyRadius;
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                MobFourStateAi candidate = all[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;
                if (seen != null && seen.Contains(candidate.GetInstanceID()))
                    continue;
                StubEnemy stub = candidate.GetComponent<StubEnemy>();
                if (stub != null && stub.IsDead)
                    continue;
                float dist;
                float t = SegmentT(candidate.transform.position, a, b, out dist);
                if (dist > radius || t >= wallT - 0.0001f)
                    continue;
                if (t < bodyT)
                {
                    bodyT = t;
                    mob = candidate;
                    boss = null;
                }
            }

            BossFightDriver live = BossFightDriver.Live;
            if (live != null && live.FightStarted && !live.FightSettled
                && live.gameObject.activeInHierarchy
                && (seen == null || !seen.Contains(live.GetInstanceID())))
            {
                float dist;
                float t = SegmentT(live.transform.position, a, b, out dist);
                if (dist <= radius && t < wallT - 0.0001f && t < bodyT)
                {
                    bodyT = t;
                    mob = null;
                    boss = live;
                }
            }

            return mob != null || boss != null;
        }

        static float SegmentT(Vector3 point, Vector3 a, Vector3 b, out float dist)
        {
            Vector3 ab = b - a;
            ab.z = 0f;
            Vector3 ap = point - a;
            ap.z = 0f;
            float len2 = ab.sqrMagnitude;
            float t = 0f;
            if (len2 > 0.000001f)
                t = (ap.x * ab.x + ap.y * ab.y) / len2;
            if (t < 0f)
                t = 0f;
            if (t > 1f)
                t = 1f;
            Vector3 q = a + (b - a) * t;
            q.z = 0f;
            point.z = 0f;
            dist = Vector3.Distance(point, q);
            return t;
        }

        void HitBoss(BossFightDriver boss, float damage, ChargeShotKind kind, float heldSeconds,
            Vector3 aim, bool piercePacket, float ringFill, float along)
        {
            RewardStatHooks.SyncClock(Time.time);
            bool bossFull = boss.Brain != null && boss.Brain.Hp >= boss.Brain.MaxHp - 0.001f;
            float scaled = RewardStatHooks.ModifyOutgoing(_ownedRewards, damage, bossFull, Time.time);
            boss.DealDamage(scaled);
            ApplyLifesteal(scaled);
            bool bossWeak = kind == ChargeShotKind.Crit;
            if (bossWeak && !piercePacket)
                boss.ApplyWeakSpotStagger(ChargeShotRules.WeakSpotStaggerSeconds);
            if (!piercePacket && FullChargeKnockback.Applies(kind, heldSeconds, ringFill))
            {
                float kb = FullChargeKnockback.HitDistance(
                    null, false, true, bossWeak, _ownedRewards);
                boss.ApplyKnockback(aim, kb);
            }

            float hp = boss.Brain != null ? boss.Brain.Hp : 0f;
            float max = boss.Brain != null ? boss.Brain.MaxHp : 0f;
            Debug.Log($"[ChargeShot] hit BOSS kind={kind} dmg={scaled:0.0} along={along:0.00} " +
                      $"hp={hp:0}/{max:0}");
        }

        float HitMob(MobFourStateAi best, float damage, ChargeShotKind kind, float heldSeconds,
            Vector3 origin, Vector3 aim, bool piercePacket, float ringFill)
        {
            RewardStatHooks.SyncClock(Time.time);
            bool weak = kind == ChargeShotKind.Crit;
            bool raised = best.ShieldRaised;
            StubEnemy stub = best.GetComponent<StubEnemy>();
            bool full = stub != null && stub.IsFullHp;
            float scaled = RewardStatHooks.ModifyOutgoing(_ownedRewards, damage, full, Time.time);
            int amount = Mathf.Max(1, Mathf.RoundToInt(scaled));
            float stagger = ChargeShotRules.WeakSpotStaggerSeconds;
            amount = best.ModifyIncomingShot(origin, weak, amount, out stagger);
            if (stub != null)
                stub.TakeDamage(amount);
            else
                best.NotifyDamaged();
            if (weak && !piercePacket)
                best.ApplyWeakSpotStagger(stagger);
            if (!piercePacket && FullChargeKnockback.Applies(kind, heldSeconds, ringFill))
            {
                if (FullChargeKnockback.RootsOnBodyHit(best.KindId, raised, weak))
                    best.ApplyRoot(FullChargeKnockback.ShieldRaisedRootSeconds);
                else
                {
                    float kb = FullChargeKnockback.HitDistance(
                        best.KindId, raised, false, weak, _ownedRewards);
                    best.ApplyKnockback(aim, kb);
                }
            }

            Debug.Log($"[ChargeShot] hit {best.name} kind={kind} dmg={amount:0.0}" +
                      $" pierce={(piercePacket ? 1 : 0)} " +
                      $"held={heldSeconds:0.00} shieldFront={(best.ShieldRaised ? 1 : 0)} stagger={stagger:0.00}s" +
                      $" zhenshi×{KnockbackRewardDraft.DistPctProduct(_ownedRewards):0.00}");
            return amount;
        }

        void ApplyLifesteal(float damageDealt)
        {
            float heal = RewardStatHooks.LifestealHeal(_ownedRewards, damageDealt);
            if (heal <= 0f)
                return;
            PlayerVitals vitals = GetComponent<PlayerVitals>();
            if (vitals != null)
                vitals.Heal(heal);
        }

        Vector3 AimDirection()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 world = CameraViewMath.ScreenToWorldOnPlayPlane(cam, Input.mousePosition);
                Vector3 dir = world - transform.position;
                dir.z = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    return dir.normalized;
            }

            return Vector3.right;
        }

        void CancelCharge()
        {
            _charging = false;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            if (_fx != null)
                _fx.HideAll();
        }
    }
}
