using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Boss;
using RogueShooter.Demo;
using RogueShooter.Vision;

namespace RogueShooter.Player
{
    /// <summary>
    /// Hold-to-charge bow. Ring full at 0.70s; fire if held over 0.2s;
    /// weak under 0.4s x0.50; weak-spot 0.68-0.72s. After a shot, 0.2s recovery.
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

        public bool IsCharging => _charging;
        public bool InRecovery => Time.time < _recoverUntil;
        public float HeldSeconds => _held;
        public ChargeShotKind LastShot { get; private set; }
        public float LastDamage { get; private set; }

        void Awake()
        {
            _fx = GetComponent<ChargeFxView>();
            if (_fx == null)
                _fx = gameObject.AddComponent<ChargeFxView>();
            _guaranteed = GetComponent<GuaranteedCritActive>();
        }

        void Update()
        {
            if (RunPause.IsPaused)
            {
                if (_charging)
                    CancelCharge();
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
            float p = ChargeShotRules.Progress(_held);
            if (_fx != null)
                _fx.SetChargeProgress(p, _green);
            if (!_mid && _held >= ChargeFxHooks.MidAt)
            {
                _mid = true;
                ChargeFxHooks.ChargeMid();
                Debug.Log("[ChargeFx] OnChargeMid");
            }

            if (!_green && _held >= ChargeShotRules.GreenEnterSeconds)
            {
                _green = true;
                ChargeFxHooks.ChargeEnterGreen();
                Debug.Log("[ChargeFx] OnChargeEnterGreen");
            }

            if (_green && !_exited && _held > ChargeShotRules.GreenExitSeconds)
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
            _charging = false;
            _held = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            return Fire(heldSeconds);
        }

        ChargeShotKind Fire(float heldSeconds)
        {
            ChargeShotKind kind = ChargeShotRules.Resolve(heldSeconds);
            if (_guaranteed == null)
                _guaranteed = GetComponent<GuaranteedCritActive>();
            if (_guaranteed != null && _guaranteed.TryForceCrit(kind, out ChargeShotKind forced))
                kind = forced;

            float dmg = ChargeShotRules.Damage(kind);
            LastShot = kind;
            LastDamage = dmg;
            float p = ChargeShotRules.Progress(heldSeconds);

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
                      $"(weak×{ChargeShotRules.WeakMul:0.00} full×{ChargeShotRules.FullMul:0.00} crit×{ChargeShotRules.CritMul:0.00})");

            ApplyHit(dmg, kind);
            return kind;
        }

        void ApplyHit(float damage, ChargeShotKind kind)
        {
            Vector3 origin = transform.position;
            Vector3 aim = AimDirection();
            MobFourStateAi best = null;
            float bestDot = 0.35f;
            float bestD = hitRange;
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                MobFourStateAi mob = all[i];
                if (mob == null || !mob.isActiveAndEnabled)
                    continue;
                Vector3 to = mob.transform.position - origin;
                to.z = 0f;
                float d = to.magnitude;
                if (d < 0.01f || d > hitRange)
                    continue;
                float dot = Vector3.Dot(aim, to.normalized);
                if (dot < bestDot)
                    continue;
                if (d < bestD)
                {
                    bestD = d;
                    best = mob;
                }
            }

            BossFightDriver boss = BossFightDriver.Live;
            if (boss != null && boss.FightStarted && !boss.FightSettled)
            {
                Vector3 toBoss = boss.transform.position - origin;
                toBoss.z = 0f;
                float bossDist = toBoss.magnitude;
                bool pointBlank = bossDist < 0.01f;
                float bossDot = pointBlank ? 1f : Vector3.Dot(aim, toBoss.normalized);
                if (bossDist <= hitRange && bossDot >= 0.35f && (best == null || bossDist <= bestD))
                {
                    boss.DealDamage(damage);
                    if (kind == ChargeShotKind.Crit)
                        boss.ApplyWeakSpotStagger(ChargeShotRules.WeakSpotStaggerSeconds);
                    Debug.Log($"[ChargeShot] hit BOSS kind={kind} dmg={damage:0.0} dist={bossDist:0.00} " +
                              $"hp={boss.Brain.Hp:0}/{boss.Brain.MaxHp:0}");
                    return;
                }
            }

            if (best == null)
            {
                Debug.Log($"[ChargeShot] miss kind={kind}");
                return;
            }

            var stub = best.GetComponent<StubEnemy>();
            if (stub != null)
                stub.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage)));
            else
                best.NotifyDamaged();
            if (kind == ChargeShotKind.Crit)
                best.ApplyWeakSpotStagger(ChargeShotRules.WeakSpotStaggerSeconds);
            Debug.Log($"[ChargeShot] hit {best.name} kind={kind} dmg={damage:0.0} dist={bestD:0.00}");
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
