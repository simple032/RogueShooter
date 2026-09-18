using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Demo;
using RogueShooter.Player;

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

        public MobAiState State => _brain.State;
        public float DistToPlayer { get; private set; }
        public string DisplayName => name;

        public static IReadOnlyList<MobFourStateAi> All => Live;

        public void Configure(BalanceLockData data, Transform player)
        {
            _player = player;
            _home = transform.position;
            _patrolT = UnityEngine.Random.Range(0f, 6.28f);
            float detect = 5.5f;
            float mul = 1.6f;
            float alert = 0.4f;
            if (data != null)
            {
                if (data.mobDetectRadius > 0f) detect = data.mobDetectRadius;
                if (data.mobDisengageMul > 0f) mul = data.mobDisengageMul;
                alert = data.mobAlertSeconds;
                if (data.mobPatrolSpeed > 0f) _patrolSpeed = data.mobPatrolSpeed;
                if (data.mobChaseSpeed > 0f) _chaseSpeed = data.mobChaseSpeed;
                if (data.mobDisengageSpeed > 0f) _disengageSpeed = data.mobDisengageSpeed;
                if (data.mobPatrolRadius > 0f) _patrolRadius = data.mobPatrolRadius;
            }

            _brain.Configure(detect, mul, alert);
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
                _base = _sr.color;
            EnsureLabel();
            ApplyVisual();
        }

        public void NotifyDamaged()
        {
            _pendingDamage = true;
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

            Vector3 delta = _player.position - transform.position;
            delta.z = 0f;
            DistToPlayer = delta.magnitude;
            bool atHome = (transform.position - _home).sqrMagnitude <= 0.14f * 0.14f;
            MobAiState prev = _brain.State;
            bool dmg = _pendingDamage;
            _pendingDamage = false;
            _brain.Tick(DistToPlayer, dmg, Time.deltaTime, atHome);
            if (_brain.State != prev)
            {
                Debug.Log($"[MobAI] {name} {prev} → {_brain.State} dist={DistToPlayer:0.00} " +
                          $"detect={_brain.DetectRadius:0.0} disengage={_brain.DisengageRadius:0.0}");
                ApplyVisual();
            }

            Move(delta);
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
                    break;
                case MobAiState.Chase:
                    if (DistToPlayer > 0.2f)
                        transform.position += toPlayer.normalized * (_chaseSpeed * dt);
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
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
                _label.font = font;
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
