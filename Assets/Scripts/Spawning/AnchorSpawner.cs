using System.Collections.Generic;
using UnityEngine;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Tries each bound anchor; skips any whose position is inside the live camera rect.
    /// </summary>
    public class AnchorSpawner : MonoBehaviour
    {
        [SerializeField] float retryInterval = 0.35f;

        SpawnAnchor[] _anchors = System.Array.Empty<SpawnAnchor>();
        GameObject _stubPrefab;
        readonly HashSet<string> _spawned = new HashSet<string>();
        readonly Dictionary<string, string> _reason = new Dictionary<string, string>();
        readonly HashSet<string> _loggedSkip = new HashSet<string>();
        float _timer;
        bool _configured;

        public IReadOnlyCollection<string> SpawnedIds => _spawned;

        public void Bind(SpawnAnchor[] anchors, GameObject stubPrefab, float interval)
        {
            _anchors = anchors ?? System.Array.Empty<SpawnAnchor>();
            _stubPrefab = stubPrefab;
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
                    Debug.Log($"[SpawnViewGate] {anchor.AnchorId} at {anchor.WorldPosition} SKIP ({phase}) — inside camera rect");
                return;
            }

            _spawned.Add(anchor.AnchorId);
            _reason[anchor.AnchorId] = "SPAWN out-of-view";
            Debug.Log($"[SpawnViewGate] {anchor.AnchorId} at {anchor.WorldPosition} SPAWN ({phase}) — outside camera rect");

            if (_stubPrefab != null)
            {
                GameObject stub = Instantiate(_stubPrefab, anchor.WorldPosition, Quaternion.identity);
                stub.name = "Stub_" + anchor.AnchorId;
                stub.SetActive(true);
            }
        }
    }
}
