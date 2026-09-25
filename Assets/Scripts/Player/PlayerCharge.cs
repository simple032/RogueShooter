using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Boss;
using RogueShooter.Build;
using RogueShooter.Combat;
using RogueShooter.Demo;
using RogueShooter.Vision;

namespace RogueShooter.Player
{
    /// <summary>
    /// Hold-to-charge bow. Ring full at ChargeProfile.Full (0.70s at 0B; 疾张 shortens it);
    /// fire if held over 0.2s; weak under 60% of full x0.50; weak-spot 76%–84% of full
    /// (+鸿运 on the upper bound). After a shot, 0.2s recovery (not scaled).
    /// Movement x0.5 while charging. Release queues an ArrowProjectile at the
    /// ActionSpecP1 OnFire frame; damage resolves on arrow impact (PR#10 port) through
    /// the reward-aware HitMob / boss path below (A's rules: ModifyOutgoing, pierce, lifesteal).
    /// Dodge roll cancels charge / pending fire / recovery (DodgeRules).
    /// </summary>
    public class PlayerCharge : MonoBehaviour
    {
        [SerializeField] float hitRange = ProjectileRules.ArrowMaxRange;

        float _held;
        bool _charging;
        bool _mid;
        bool _green;
        bool _exited;
        bool _fullPose;
        float _recoverUntil;
        float _atkUntil;
        bool _pendingFire;
        float _fireAt;
        float _queuedDmg;
        ChargeShotKind _queuedKind;
        float _queuedHeld;
        Vector3 _queuedDir;
        ChargeFxView _fx;
        GuaranteedCritActive _guaranteed;
        IList<string> _ownedRewards;

        public bool IsCharging => _charging;
        public bool IsFiring => Time.time < _atkUntil;
        public bool InRecovery => Time.time < _recoverUntil;
        public float HeldSeconds => _held;
        /// <summary>Current full charge / window from owned rewards (疾张 charge_time, 鸿运 crit_window).</summary>
        public ChargeProfile Profile => ChargeProfile.FromOwned(_ownedRewards);
        public ChargeShotKind LastShot { get; private set; }
        public float LastDamage { get; private set; }

        public void BindOwnedRewards(IList<string> owned)
        {
            _ownedRewards = owned;
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
            var dodge = GetComponent<PlayerDodge>();
            if (dodge != null && dodge.IsRolling)
            {
                CancelIntoRoll();
                return;
            }

            TickQueuedFire();
            if (RunPause.IsPaused)
            {
                if (_charging)
                    CancelCharge();
                return;
            }

            var vitals = GetComponent<PlayerVitals>();
            if (vitals != null && vitals.IsDead)
            {
                if (_charging)
                    CancelCharge();
                _pendingFire = false;
                return;
            }

            bool hold = Input.GetMouseButton(0) || Input.GetKey(KeyCode.C);
            if (!_charging)
            {
                if (hold && Time.time >= _recoverUntil)
                    BeginCharge();
                return;
            }

            if (!hold)
            {
                ReleaseCharge();
                return;
            }

            _held += Time.deltaTime;
            ChargeProfile prof = Profile;
            float p = prof.Progress(_held);
            if (_fx != null)
                _fx.SetChargeProgress(p, _green);
            if (!_mid && _held >= ChargeFxHooks.MidAt)
            {
                _mid = true;
                ChargeFxHooks.ChargeMid();
                Debug.Log("[ChargeFx] OnChargeMid");
            }

            if (!_green && !_exited && prof.InWindow(_held))
            {
                _green = true;
                ChargeFxHooks.ChargeEnterGreen();
                Debug.Log("[ChargeFx] OnChargeEnterGreen");
            }

            if (_green && !_exited && _held - ChargeShotRules.EdgeEpsilon > prof.WindowExit)
            {
                _exited = true;
                _green = false;
                ChargeFxHooks.ChargeExitGreen();
                Debug.Log("[ChargeFx] OnChargeExitGreen");
            }

            if (!_fullPose && prof.Reached(_held))
            {
                _fullPose = true;
                ChargeFxHooks.ChargeFull();
                Debug.Log("[ChargeFx] OnChargeFull t=" + prof.Full.ToString("0.000") + "s");
            }
        }

        void BeginCharge()
        {
            _charging = true;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            _fullPose = false;
            LastShot = ChargeShotKind.None;
            LastDamage = 0f;
            if (_fx != null)
                _fx.SetChargeProgress(0f, false);
            ChargeFxHooks.ChargeStart();
            Debug.Log("[ChargeFx] OnChargeStart");
        }

        void ReleaseCharge()
        {
            float held = _held;
            _charging = false;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            _fullPose = false;
            Fire(held);
        }

        /// <summary>Test/helper: resolve and fire as if released after heldSeconds.</summary>
        public ChargeShotKind FireAtHeld(float heldSeconds)
        {
            _charging = false;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            _fullPose = false;
            return Fire(heldSeconds);
        }

        ChargeShotKind Fire(float heldSeconds)
        {
            ChargeProfile prof = Profile;
            ChargeShotKind kind = prof.Resolve(heldSeconds);
            if (_guaranteed == null)
                _guaranteed = GetComponent<GuaranteedCritActive>();
            bool focusGate = false;
            if (_guaranteed != null && _guaranteed.TryForceCrit(kind, heldSeconds, out ChargeShotKind forced))
            {
                // 凝神窥机: ≥FocusMinChargeSeconds → Crit (必中弱点); shorter → no shot.
                focusGate = forced == ChargeShotKind.None;
                kind = forced;
            }

            float dmg = ChargeShotRules.Damage(kind);
            LastShot = kind;
            LastDamage = dmg;
            float p = prof.Progress(heldSeconds);

            if (kind == ChargeShotKind.None)
            {
                float gate = focusGate ? GuaranteedCritActive.FocusMinChargeSeconds : ChargeShotRules.MinChargeSeconds;
                Debug.Log($"[ChargeShot] NO_SHOT held={heldSeconds:0.000}s p={p:0.00} (<{gate:0.00}s{(focusGate ? " 凝神窥机" : "")})");
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
            _atkUntil = Time.time + ActionSpecP1.PlayerFire.Duration;
            float onFire = ActionSpecP1.PlayerOnFireSeconds;
            if (onFire < 0.001f)
                onFire = 1f / ActionSpecP1.Fps;
            _queuedDmg = dmg;
            _queuedKind = kind;
            _queuedHeld = heldSeconds;
            _queuedDir = AimDirection();
            _pendingFire = true;
            _fireAt = Time.time + onFire;
            Debug.Log($"[ChargeShot] {kind} dmg={dmg:0.0} held={heldSeconds:0.000}s p={p:0.00} " +
                      $"full={prof.Full:0.000}s win={prof.WindowEnter:0.000}-{prof.WindowExit:0.000}s weakMax={prof.WeakMax:0.000}s " +
                      $"recover={ChargeShotRules.RecoverSeconds:0.00}s OnFire@{onFire:0.000}s " +
                      $"(weak×{ChargeShotRules.WeakMul:0.00} full×{ChargeShotRules.FullMul:0.00} crit×{ChargeShotRules.CritMul:0.00})");
            return kind;
        }

        void TickQueuedFire()
        {
            if (DodgeRules.BlockFireWhileRolling)
            {
                var dodge = GetComponent<PlayerDodge>();
                if (dodge != null && dodge.IsRolling)
                {
                    _pendingFire = false;
                    return;
                }
            }

            if (!_pendingFire || Time.time < _fireAt)
                return;
            _pendingFire = false;
            Debug.Log("[ActionSpec] OnFire frame=_01 art=" + ActionSpecP1.PlayerFire.Root);
            Vector3 origin = transform.position;
            Vector3 aim = _queuedDir.sqrMagnitude > 0.0001f ? _queuedDir : AimDirection();
            ArrowProjectile.Spawn(origin + aim * 0.45f, aim, _queuedDmg, _queuedKind, _queuedHeld,
                _ownedRewards, transform, hitRange);
        }

        /// <summary>Arrow impact on a mob: A's reward-aware hit + 穿透后排 + lifesteal.</summary>
        public void ResolveArrowHitMob(MobFourStateAi best, Vector3 origin, Vector3 aim,
            float damage, ChargeShotKind kind, float heldSeconds)
        {
            if (best == null)
                return;
            RewardStatHooks.SyncClock(Time.time);
            float dealt = HitMob(best, damage, kind, heldSeconds, origin, aim, false);
            float pierceAdd = RewardStatHooks.PierceBackAdd(_ownedRewards);
            if (pierceAdd > 0f)
            {
                MobFourStateAi back = FindBehind(best, aim);
                if (back != null)
                    dealt += HitMob(back, damage * pierceAdd, kind, heldSeconds, origin, aim, true);
            }
            ApplyLifesteal(dealt);
        }

        /// <summary>R14 穿透 back-row fraction (0 = no pierce). The flying arrow uses it per contact.</summary>
        public float PierceBackAdd => RewardStatHooks.PierceBackAdd(_ownedRewards);

        /// <summary>
        /// Flying-arrow contact: settle damage on exactly the mob the arrow's trace hit (reward
        /// ModifyOutgoing, shield, weak-spot stagger / knockback unless pierce packet, lifesteal).
        /// </summary>
        public float ResolveArrowContact(MobFourStateAi mob, Vector3 origin, Vector3 aim,
            float damage, ChargeShotKind kind, float heldSeconds, bool piercePacket)
        {
            if (mob == null || mob.IsDead || damage <= 0f)
                return 0f;
            RewardStatHooks.SyncClock(Time.time);
            float dealt = HitMob(mob, damage, kind, heldSeconds, origin, aim, piercePacket);
            ApplyLifesteal(dealt);
            return dealt;
        }

        MobFourStateAi FindBehind(MobFourStateAi first, Vector3 aim)
        {
            MobFourStateAi back = null;
            float backD = hitRange;
            Vector3 from = first.transform.position;
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                MobFourStateAi mob = all[i];
                if (mob == null || mob == first || !mob.isActiveAndEnabled || mob.IsDead)
                    continue;
                Vector3 to = mob.transform.position - from;
                to.z = 0f;
                float d = to.magnitude;
                if (d < 0.01f || d > backD)
                    continue;
                if (Vector3.Dot(aim, to.normalized) < 0.35f)
                    continue;
                backD = d;
                back = mob;
            }
            return back;
        }

        /// <summary>Arrow impact on the boss: A's reward-aware boss hit + lifesteal.</summary>
        public void ResolveArrowHitBoss(BossFightDriver boss, Vector3 origin, Vector3 aim,
            float damage, ChargeShotKind kind, float heldSeconds)
        {
            if (boss == null || !boss.FightStarted || boss.FightSettled)
                return;
            RewardStatHooks.SyncClock(Time.time);
            float bossDist = Vector3.Distance(origin, boss.transform.position);
            bool bossFull = boss.Brain != null && boss.Brain.Hp >= boss.Brain.MaxHp - 0.001f;
            float scaled = RewardStatHooks.ModifyOutgoing(_ownedRewards, damage, bossFull, Time.time);
            boss.DealDamage(scaled);
            ApplyLifesteal(scaled);
            bool bossWeak = kind == ChargeShotKind.Crit;
            if (bossWeak)
                boss.ApplyWeakSpotStagger(ChargeShotRules.WeakSpotStaggerSeconds);
            if (FullChargeKnockback.Applies(kind, heldSeconds, Profile.Full))
            {
                float kb = FullChargeKnockback.HitDistance(
                    null, false, true, bossWeak, _ownedRewards);
                boss.ApplyKnockback(aim, kb);
            }
            Debug.Log($"[ChargeShot] hit BOSS kind={kind} dmg={scaled:0.0} dist={bossDist:0.00} " +
                      $"hp={boss.Brain.Hp:0}/{boss.Brain.MaxHp:0}");
        }

        float HitMob(MobFourStateAi best, float damage, ChargeShotKind kind, float heldSeconds,
            Vector3 origin, Vector3 aim, bool piercePacket)
        {
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
            if (!piercePacket && FullChargeKnockback.Applies(kind, heldSeconds, Profile.Full))
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

        public Vector3 AimDirectionPublic()
        {
            return AimDirection();
        }

        Vector3 AimDirection()
        {
            Camera cam = Camera.main;
            if (cam != null)
                return AimFrom(ViewSpace.ScreenPixelToLogic(cam, Input.mousePosition), transform.position);

            return Vector3.right;
        }

        /// <summary>Logic-space unit aim from <paramref name="origin"/> to a logic ground point.</summary>
        public static Vector3 AimFrom(Vector3 logicTarget, Vector3 origin)
        {
            Vector3 dir = logicTarget - origin;
            dir.z = 0f;
            if (dir.sqrMagnitude > 0.01f)
                return dir.normalized;
            return Vector3.right;
        }

        public void CancelChargePublic()
        {
            CancelCharge();
        }

        /// <summary>Roll interrupt: drop charge, pending OnFire, and shot recovery.</summary>
        public void CancelIntoRoll()
        {
            if (DodgeRules.CancelCharge)
                CancelCharge();
            _pendingFire = false;
            if (DodgeRules.CancelShotRecovery)
            {
                _recoverUntil = 0f;
                _atkUntil = 0f;
            }
        }

        void CancelCharge()
        {
            _charging = false;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            _fullPose = false;
            if (_fx != null)
                _fx.HideAll();
        }
    }
}
