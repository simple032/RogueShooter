using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Demo;
using RogueShooter.Player;
using RogueShooter.Spawning;

namespace RogueShooter.Ai
{
    /// <summary>
    /// World-space four-state AI for SpawnBand stub enemies.
    /// </summary>
    public class MobFourStateAi : MonoBehaviour
    {
        static readonly List<MobFourStateAi> Live = new List<MobFourStateAi>();

        readonly MobAiBrain _brain = new MobAiBrain();
        Transform _player;
        Vector3 _home;
        float _patrolSpeed = 1.35f;
        float _chaseSpeed = 3.6f;
        float _disengageSpeed = 2.4f;
        float _patrolRadius = 1.8f;
        float _patrolT;
        bool _pendingDamage;
        TextMesh _label;
        SpriteRenderer _sr;
        Color _base = new Color(0.86f, 0.28f, 0.24f);
        EnemyPressureState _pressure;
        PlayerVitals _playerVitals;
        float _windupLeft;
        float _cooldownLeft;
        bool _inWindup;
        float _lastDealt;

        public MobAiState State => _brain.State;
        public float DistToPlayer { get; private set; }
        public string DisplayName => name;
        public float LastDealtDamage => _lastDealt;

        /// <summary>Proof helper: deal one recommend hit immediately (same path as Attack windup end).</summary>
        public bool ForceDealHitForProof()
        {
            if (_playerVitals == null && _player != null)
                _playerVitals = _player.GetComponent<PlayerVitals>();
            string kind = CurrentKindId();
            float dmg = EnemyDamageCatalog.HitDamage(kind);
            _lastDealt = dmg;
            if (_playerVitals == null)
                return false;
            _playerVitals.ApplyHit(dmg, kind);
            return true;
        }

        public static IReadOnlyList<MobFourStateAi> All => Live;

        public float PatrolSpeed => _patrolSpeed;
        public float ChaseSpeed => _chaseSpeed;
        Vector3 _lastKnown;

        public void Configure(BalanceLockData data, Transform player)
        {
            _player = player;
            _home = transform.position;
            _lastKnown = _home;
            _patrolT = UnityEngine.Random.Range(0f, 6.28f);
            float detect = 5.5f;
            float mul = 1.6f;
            float alert = 1.2f;
            float attack = 0.7f;
            if (data != null)
            {
                if (data.mobDetectRadius > 0f) detect = data.mobDetectRadius;
                if (data.mobDisengageMul > 0f) mul = data.mobDisengageMul;
                if (data.mobAlertSeconds > 0f) alert = data.mobAlertSeconds;
                if (data.mobPatrolRadius > 0f) _patrolRadius = data.mobPatrolRadius;
            }

            var stub = GetComponent<StubEnemy>();
            ApplyKindSpeed(stub != null ? stub.KindId : "E1");

            _brain.Configure(detect, mul, alert, attack);
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
                _base = _sr.color;
            _pressure = GetComponent<EnemyPressureState>();
            if (_player != null)
                _playerVitals = _player.GetComponent<PlayerVitals>();
            _windupLeft = 0f;
            _cooldownLeft = 0f;
            _inWindup = false;
            _lastDealt = 0f;
            EnsureLabel();
            ApplyVisual();
            Debug.Log($"[MobAI] {name} Patrol home={_home} detect={detect:0.0} alert={alert:0.00}s attack={attack:0.00}");
        }

        public void ApplyKindSpeed(string kindId)
        {
            float v = MoveSpeeds.ForKind(kindId);
            _patrolSpeed = v;
            _chaseSpeed = v;
            _disengageSpeed = v;
        }

        public void ForceChase()
        {
            // Kept for debug only. Normal spawn must start Patrol (视野外不追).
            Debug.LogWarning($"[MobAI] {name} ForceChase ignored — spawn stays Patrol");
        }

        public void NotifyDamaged()
        {
            _pendingDamage = true;
            if (_player != null)
                _lastKnown = _player.position;
        }

        void OnEnable()
        {
            if (!Live.Contains(this))
                Live.Add(this);
            var stub = GetComponent<StubEnemy>();
            if (stub != null)
            {
                stub.Damaged -= NotifyDamaged;
                stub.Damaged += NotifyDamaged;
            }
        }

        void OnDisable()
        {
            Live.Remove(this);
            var stub = GetComponent<StubEnemy>();
            if (stub != null)
                stub.Damaged -= NotifyDamaged;
        }

        void Update()
        {
            if (RunPause.IsPaused || _player == null)
                return;

            if (_pressure == null)
                _pressure = GetComponent<EnemyPressureState>();
            if (_pressure != null && _brain.State == MobAiState.Patrol)
                _pressure.SyncUnengaged();

            Vector3 delta = _player.position - transform.position;
            delta.z = 0f;
            DistToPlayer = delta.magnitude;
            if (DistToPlayer <= _brain.DetectRadius)
                _lastKnown = _player.position;
            bool atHome = (transform.position - _home).sqrMagnitude <= 0.14f * 0.14f;
            MobAiState prev = _brain.State;
            bool dmg = _pendingDamage;
            _pendingDamage = false;
            _brain.Tick(DistToPlayer, dmg, Time.deltaTime, atHome);
            if (_brain.State != prev)
            {
                Debug.Log($"[MobAI] {name} {prev} → {_brain.State} dist={DistToPlayer:0.00} " +
                          $"detect={_brain.DetectRadius:0.0} disengage={_brain.DisengageRadius:0.0}");
                LockPressureIfEngaged();
                ApplyVisual();
                if (_brain.State == MobAiState.Attack && prev != MobAiState.Attack)
                    BeginAttackCycle();
                if (_brain.State != MobAiState.Attack)
                {
                    _inWindup = false;
                    _windupLeft = 0f;
                }
            }

            if (_brain.State == MobAiState.Attack)
                TickAttack(Time.deltaTime);

            Move(delta);
        }

        void BeginAttackCycle()
        {
            var def = EnemyDamageCatalog.ForKind(CurrentKindId());
            _cooldownLeft = 0f;
            _inWindup = true;
            _windupLeft = def.WindupRecommend;
        }

        void TickAttack(float dt)
        {
            if (_playerVitals == null && _player != null)
                _playerVitals = _player.GetComponent<PlayerVitals>();

            var def = EnemyDamageCatalog.ForKind(CurrentKindId());
            if (_cooldownLeft > 0f)
            {
                _cooldownLeft -= dt;
                if (_cooldownLeft > 0f)
                    return;
                _inWindup = true;
                _windupLeft = def.WindupRecommend;
            }

            if (!_inWindup)
                return;

            _windupLeft -= dt;
            if (_windupLeft > 0f)
                return;

            _inWindup = false;
            float dmg = EnemyDamageCatalog.HitDamage(CurrentKindId());
            _lastDealt = dmg;
            if (_playerVitals != null)
                _playerVitals.ApplyHit(dmg, CurrentKindId());
            else
                Debug.Log($"[MobAI] {name} hit dmg={dmg:0.#} (no PlayerVitals)");
            _cooldownLeft = def.AttackIntervalRecommend;
        }

        string CurrentKindId()
        {
            var stub = GetComponent<StubEnemy>();
            return stub != null ? stub.KindId : "E1";
        }

        void LockPressureIfEngaged()
        {
            if (_pressure == null)
                return;
            if (_brain.State == MobAiState.Alert || _brain.State == MobAiState.Chase || _brain.State == MobAiState.Attack)
                _pressure.LockEngage();
        }

        void Move(Vector3 toPlayer)
        {
            float dt = Time.deltaTime;
            switch (_brain.State)
            {
                case MobAiState.Patrol:
                    _patrolT += dt * 0.85f;
                    Vector3 patrol = _home + new Vector3(Mathf.Cos(_patrolT), Mathf.Sin(_patrolT), 0f) * _patrolRadius;
                    transform.position = Vector3.MoveTowards(transform.position, patrol, _patrolSpeed * dt);
                    break;
                case MobAiState.Alert:
                    transform.position = Vector3.MoveTowards(transform.position, _lastKnown, _patrolSpeed * 0.75f * dt);
                    break;
                case MobAiState.Chase:
                    if (DistToPlayer > 0.2f)
                        transform.position += toPlayer.normalized * (_chaseSpeed * dt);
                    break;
                case MobAiState.Attack:
                    break;
                case MobAiState.Disengage:
                    transform.position = Vector3.MoveTowards(transform.position, _home, _disengageSpeed * dt);
                    break;
            }
        }

        void EnsureLabel()
        {
            if (_label != null)
                return;
            var go = new GameObject("AiState");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            _label = go.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.characterSize = 0.14f;
            _label.fontSize = 24;
            _label.color = Color.white;
            BuiltinUiFont.Apply(_label);
        }

        void ApplyVisual()
        {
            if (_label != null)
                _label.text = _brain.State.ToString().ToUpperInvariant();
            if (_sr == null)
                return;
            switch (_brain.State)
            {
                case MobAiState.Alert:
                    _sr.color = new Color(0.98f, 0.86f, 0.22f);
                    break;
                case MobAiState.Chase:
                    _sr.color = new Color(1f, 0.18f, 0.12f);
                    break;
                case MobAiState.Attack:
                    _sr.color = new Color(1f, 0.05f, 0.35f);
                    break;
                case MobAiState.Disengage:
                    _sr.color = new Color(0.95f, 0.55f, 0.18f);
                    break;
                default:
                    _sr.color = _base;
                    break;
            }
        }
    }
}
