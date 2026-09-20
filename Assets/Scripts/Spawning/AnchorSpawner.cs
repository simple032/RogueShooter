using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Demo;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Tries each bound anchor; spawns only when off-view (see SpawnViewGate).
    /// Cluster stubs start in Patrol — no ForceChase.
    /// </summary>
    public class AnchorSpawner : MonoBehaviour
    {
        [SerializeField] float retryInterval = 0.35f;

        SpawnAnchor[] _anchors = System.Array.Empty<SpawnAnchor>();
        GameObject _stubPrefab;
        Transform _player;
        readonly HashSet<string> _spawned = new HashSet<string>();
        readonly Dictionary<string, string> _reason = new Dictionary<string, string>();
        readonly HashSet<string> _loggedSkip = new HashSet<string>();
        float _timer;
        bool _configured;

        public IReadOnlyCollection<string> SpawnedIds => _spawned;

        public void Bind(SpawnAnchor[] anchors, GameObject stubPrefab, float interval)
        {
            Bind(anchors, stubPrefab, interval, null);
        }

        public void Bind(SpawnAnchor[] anchors, GameObject stubPrefab, float interval, Transform player)
        {
            _anchors = anchors ?? System.Array.Empty<SpawnAnchor>();
            _stubPrefab = stubPrefab;
            _player = player;
            retryInterval = Mathf.Max(0.05f, interval);
            _configured = false;
        }

        public void ArmAndEvaluate(string phase)
        {
            _configured = true;
            EvaluateAll(phase);
        }

        public bool WasSpawned(SpawnAnchor anchor)
        {
            return anchor != null && _spawned.Contains(anchor.AnchorId);
        }

        public string ReasonFor(SpawnAnchor anchor)
        {
            if (anchor == null)
                return "";
            return _reason.TryGetValue(anchor.AnchorId, out string reason) ? reason : "";
        }

        void Update()
        {
            if (!_configured)
                return;
            _timer += Time.deltaTime;
            if (_timer < retryInterval)
                return;
            _timer = 0f;
            EvaluateAll("tick");
        }

        public void EvaluateAll(string phase)
        {
            if (_anchors == null)
                return;
            for (int i = 0; i < _anchors.Length; i++)
                TrySpawn(_anchors[i], phase);
        }

        void TrySpawn(SpawnAnchor anchor, string phase)
        {
            if (anchor == null || _spawned.Contains(anchor.AnchorId))
                return;

            if (SpawnViewGate.ShouldSkipSpawn(anchor.WorldPosition))
            {
                _reason[anchor.AnchorId] = "SKIP in-view";
                if (_loggedSkip.Add(anchor.AnchorId))
                    Debug.Log($"[SpawnViewGate] {anchor.AnchorId} at {anchor.WorldPosition} SKIP ({phase}) — in view or edge buffer pad={SpawnViewGate.EffectivePad:0.00}");
                return;
            }

            int room = SpawnScreenCap.Remaining;
            if (room <= 0)
            {
                _reason[anchor.AnchorId] = "SKIP screen-cap";
                Debug.Log($"[SpawnScreenCap] {anchor.AnchorId} SKIP ({phase}) live={SpawnScreenCap.LiveCount()}/{SpawnScreenCap.MaxLive}");
                return;
            }

            _spawned.Add(anchor.AnchorId);
            int n = Mathf.Min(SpawnCluster.RollCount(), room);
            _reason[anchor.AnchorId] = "SPAWN off-view cluster=" + n;
            Debug.Log($"[SpawnViewGate] {anchor.AnchorId} at {anchor.WorldPosition} SPAWN ({phase}) — outside view+edge pad={SpawnViewGate.EffectivePad:0.00}");
            Debug.Log($"[SpawnCluster] {anchor.AnchorId} count={n} radius={SpawnCluster.Radius:0.00} ({phase})");

            if (_stubPrefab == null)
                return;

            for (int i = 0; i < n; i++)
            {
                Vector3 pos = SpawnCluster.Offset(anchor.WorldPosition, i, n);
                GameObject stub = Instantiate(_stubPrefab, pos, Quaternion.identity);
                stub.name = n > 1 ? $"Stub_{anchor.AnchorId}_{i}" : "Stub_" + anchor.AnchorId;
                stub.SetActive(true);
                var enemy = stub.GetComponent<StubEnemy>();
                if (enemy == null)
                    enemy = stub.AddComponent<StubEnemy>();
                enemy.ConfigureKind("E1", 1);
                var ai = stub.GetComponent<MobFourStateAi>();
                if (ai == null)
                    ai = stub.AddComponent<MobFourStateAi>();
                ai.Configure(null, _player);
                ai.ApplyKindSpeed(enemy.KindId);
                Debug.Log($"[MobAI] {stub.name} spawn→Patrol");
            }
        }
    }
}
