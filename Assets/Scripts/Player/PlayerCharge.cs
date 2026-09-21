using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Art;
using RogueShooter.Combat;
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

            var dodge = GetComponent<PlayerDodge>();
            if (dodge != null && dodge.IsRolling)
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

            if (!_fullPose && _held >= ActionSpecP1.ChargeFullPoseSeconds)
            {
                _fullPose = true;
                ChargeFxHooks.ChargeFull();
                Debug.Log("[ChargeFx] OnChargeFull pose t=" + ActionSpecP1.ChargeFullPoseSeconds.ToString("0.00"));
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
                      $"recover={ChargeShotRules.RecoverSeconds:0.00}s OnFire@{onFire:0.000}s " +
                      $"(weak×{ChargeShotRules.WeakMul:0.00} full×{ChargeShotRules.FullMul:0.00} crit×{ChargeShotRules.CritMul:0.00})");
            return kind;
        }

        void TickQueuedFire()
        {
            if (!_pendingFire || Time.time < _fireAt)
                return;
            _pendingFire = false;
            Debug.Log("[ActionSpec] OnFire frame=_01 art=" + ActionSpecP1.PlayerFire.Root);
            Vector3 origin = transform.position;
            Vector3 aim = _queuedDir.sqrMagnitude > 0.0001f ? _queuedDir : AimDirection();
            ArrowProjectile.Spawn(origin + aim * 0.45f, aim, _queuedDmg, _queuedKind, _queuedHeld, _ownedRewards, transform, hitRange);
        }

        public Vector3 AimDirectionPublic()
        {
            return AimDirection();
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

        public void CancelChargePublic()
        {
            CancelCharge();
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
