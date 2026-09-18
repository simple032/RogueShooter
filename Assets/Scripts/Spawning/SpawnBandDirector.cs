using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Periodic stub spawns using LOCK composed intervals, skip-in-view, and no-spawn cores.
    /// </summary>
    public class SpawnBandDirector : MonoBehaviour
    {
        public struct Slot
        {
            public string Id;
            public Vector3 Position;
            public string BandId;
        }

        BalanceLockData _lock;
        SpawnBandClock _clock;
        GameObject _stubPrefab;
        Slot[] _slots = System.Array.Empty<Slot>();
        readonly HashSet<string> _spawned = new HashSet<string>();
        readonly List<string> _log = new List<string>();
        float _timer;

        public IReadOnlyCollection<string> SpawnedIds => _spawned;
        public IReadOnlyList<string> RecentLog => _log;

        public void Bind(BalanceLockData data, SpawnBandClock clock, Slot[] slots, GameObject stubPrefab)
        {
            _lock = data;
            _clock = clock;
            _slots = slots ?? System.Array.Empty<Slot>();
            _stubPrefab = stubPrefab;
            _timer = 0f;
            if (_clock != null)
            {
                _clock.BandChanged -= OnBandChanged;
                _clock.BandChanged += OnBandChanged;
            }
        }

        void OnDestroy()
        {
            if (_clock != null)
                _clock.BandChanged -= OnBandChanged;
        }

        void OnBandChanged(string previous, string next)
        {
            _timer = 0f;
            string anchors = SwitchAnchorLine(next);
            string line = $"[SpawnBand] {Fmt(previous)} → {next} at {(_clock != null ? _clock.WallMinutes : 0f):0.00}′  {anchors}";
            Remember(line);
            Debug.Log(line);
        }

        string SwitchAnchorLine(string bandId)
        {
            if (_lock == null || _lock.switchAnchors == null)
                return "";
            switch (bandId)
            {
                case "Z2":
                    return "switch Anchor_S1_End@HubA north crossed; next Anchor_S2_End_α/β/γ";
                case "Z3":
                    return "switch Anchor_S2_End_* crossed; next Anchor_S3_End@Pre north";
                case "Pre":
                    return "switch Anchor_S3_End@Pre north — BOSS 不挂 SpawnBand";
                default:
                    return "switch idle until Anchor_S1_End@HubA north";
            }
        }

        void Update()
        {
            if (_lock == null || _clock == null)
                return;

            float interval = CurrentInterval();
            if (interval <= 0f)
                return;

            _timer += Time.deltaTime;
            if (_timer < interval)
                return;
            _timer = 0f;
            TrySpawnOne();
        }

        public float CurrentInterval()
        {
            if (_lock == null || _clock == null)
                return 0f;
            SegmentMul seg = _lock.GetSegment(_clock.BandId);
            TimeScaleMul time = _lock.TimeScaleAtMinutes(_clock.WallMinutes);
            if (seg == null || time == null)
                return 0f;
            return BalanceMath.ComposeSpawnInterval(seg, time);
        }

        public bool WasSpawned(string id) => _spawned.Contains(id);

        public void EvaluateNamed(string id, Vector3 pos, string phase)
        {
            TryPlace(id, pos, _clock != null ? _clock.BandId : "Z1", phase);
        }

        void TrySpawnOne()
        {
            string band = _clock.BandId;
            if (band == "Pre")
                return;

            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (slot.BandId != band)
                    continue;
                if (_spawned.Contains(slot.Id))
                    continue;
                if (TryPlace(slot.Id, slot.Position, band, "tick"))
                    return;
            }
        }

        bool TryPlace(string id, Vector3 pos, string band, string phase)
        {
            if (_spawned.Contains(id))
                return false;

            if (SpawnPlacementGate.ShouldSkipSpawn(pos, out string reason))
            {
                string skip = $"[SpawnPlacementGate] {id} at {pos} {reason} ({phase})";
                if (!_log.Contains(skip))
                {
                    Remember(skip);
                    Debug.Log(skip);
                }
                return false;
            }

            _spawned.Add(id);
            string line = $"[SpawnPlacementGate] {id} at {pos} SPAWN ({phase}) band={band} interval={CurrentInterval():0.00}s";
            Remember(line);
            Debug.Log(line);

            if (_stubPrefab != null)
            {
                GameObject stub = Instantiate(_stubPrefab, pos, Quaternion.identity);
                stub.name = "Stub_" + id;
                stub.SetActive(true);
                SegmentMul seg = _lock != null ? _lock.GetSegment(band) : null;
                TimeScaleMul time = _lock != null && _clock != null
                    ? _lock.TimeScaleAtMinutes(_clock.WallMinutes)
                    : null;
                float hp = BalanceMath.FinalHpMul(seg, time);
                SegmentMul z1 = _lock != null ? _lock.GetSegment("Z1") : null;
                float baselineHp = z1 != null ? z1.hpMul : 1f;
                stub.transform.localScale *= Mathf.Clamp(hp / Mathf.Max(0.01f, baselineHp), 0.85f, 1.6f);
                var sr = stub.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = BandColor(band);
            }

            return true;
        }

        static Color BandColor(string band)
        {
            switch (band)
            {
                case "Z2": return new Color(0.91f, 0.45f, 0.20f);
                case "Z3": return new Color(0.85f, 0.18f, 0.22f);
                default: return new Color(0.86f, 0.28f, 0.24f);
            }
        }

        void Remember(string line)
        {
            _log.Add(line);
            while (_log.Count > 12)
                _log.RemoveAt(0);
        }

        static string Fmt(string id) => string.IsNullOrEmpty(id) ? "—" : id;
    }
}
