using System;
using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Balance;
using RogueShooter.Build;
using RogueShooter.Demo;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Periodic stub spawns: off-view only, six-class group roll (N12), map-phase cooldown.
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
        Transform _player;
        RunBuildState _build;
        System.Action<StubEnemy> _onEnemyKilled;
        Slot[] _slots = System.Array.Empty<Slot>();
        readonly HashSet<string> _spawned = new HashSet<string>();
        readonly Dictionary<string, int> _lastPhase = new Dictionary<string, int>();
        readonly List<string> _log = new List<string>();
        System.Random _rng = new System.Random();
        float _timer;

        public IReadOnlyCollection<string> SpawnedIds => _spawned;
        public IReadOnlyList<string> RecentLog => _log;
        public SpawnClassId LastClass { get; private set; }
        public string LastGroupLine { get; private set; }

        public void Bind(BalanceLockData data, SpawnBandClock clock, Slot[] slots, GameObject stubPrefab, Transform player)
        {
            Bind(data, clock, slots, stubPrefab, player, null, null);
        }

        public void Bind(
            BalanceLockData data,
            SpawnBandClock clock,
            Slot[] slots,
            GameObject stubPrefab,
            Transform player,
            System.Action<StubEnemy> onEnemyKilled)
        {
            Bind(data, clock, slots, stubPrefab, player, onEnemyKilled, null);
        }

        public void Bind(
            BalanceLockData data,
            SpawnBandClock clock,
            Slot[] slots,
            GameObject stubPrefab,
            Transform player,
            System.Action<StubEnemy> onEnemyKilled,
            RunBuildState build)
        {
            _lock = data;
            _clock = clock;
            _slots = slots ?? System.Array.Empty<Slot>();
            _stubPrefab = stubPrefab;
            _player = player;
            _onEnemyKilled = onEnemyKilled;
            _build = build;
            _timer = 0f;
            _rng = new System.Random(Environment.TickCount);
            _lastPhase.Clear();
            LastGroupLine = "";
            if (_clock != null)
            {
                _clock.BandChanged -= OnBandChanged;
                _clock.BandChanged += OnBandChanged;
            }
        }

        public void BindBuild(RunBuildState build)
        {
            _build = build;
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
            if (seg == null)
                return 0f;
            float timeInt = TimePressure.IntervalMul(_clock.WallMinutes);
            return seg.baseIntervalSeconds * seg.intervalMul * timeInt;
        }

        public bool WasSpawned(string id) => _spawned.Contains(id);

        public bool TryGetLastPhase(string id, out int phaseIndex)
        {
            return _lastPhase.TryGetValue(id, out phaseIndex);
        }

        public void EvaluateNamed(string id, Vector3 pos, string phase)
        {
            TryPlace(id, pos, _clock != null ? _clock.BandId : "Z1", phase);
        }

        /// <summary>
        /// One-shot 死路 MobWave. Existing kinds + SpawnStub four-state AI + existing HP scale.
        /// Count clamped to 1–3 (design lock). Bypasses in-view skip. No new AI / TTK / CSV.
        /// </summary>
        public int SpawnEventWave(string id, Vector3 pos, string band)
        {
            return SpawnEventWave(id, pos, band, 1, 3);
        }

        public int SpawnEventWave(string id, Vector3 pos, string band, int countMin, int countMax)
        {
            if (countMin < 1)
                countMin = 1;
            if (countMax < countMin)
                countMax = countMin;
            if (string.IsNullOrEmpty(band))
                band = _clock != null ? _clock.BandId : "Z2";
            if (string.IsNullOrEmpty(band) || band == "Pre")
                band = "Z2";

            SpawnClassId cls = SpawnWaveCatalog.ResolveClass(band, _build);
            SpawnGroupDef[] pool = SpawnWaveCatalog.GroupsFor(cls);
            SpawnGroupDef group = pool != null && pool.Length > 0 ? pool[0] : default(SpawnGroupDef);
            int want = SpawnWaveCatalog.TotalCount(group);
            if (want <= 0)
            {
                SpawnGroupDef[] fallback = SpawnWaveCatalog.GroupsFor(SpawnClassId.S1Pre);
                if (fallback != null && fallback.Length > 0)
                {
                    group = fallback[0];
                    want = SpawnWaveCatalog.TotalCount(group);
                    cls = SpawnClassId.S1Pre;
                }
            }

            if (want > countMax)
                want = countMax;
            if (want < countMin)
                want = countMin;

            if (group.Members == null || group.Members.Length == 0 || _stubPrefab == null)
            {
                Debug.Log("[DeadEnd] MobWave " + id + " skip — no group/prefab");
                return 0;
            }

            float t = _clock != null ? _clock.WallMinutes : 0f;
            int build = _build != null ? _build.BuildCount : 0;
            float tm = SpawnWaveCatalog.TimeMul(t);
            float bm = SpawnWaveCatalog.BuildMul(build);
            LastClass = cls;
            LastGroupLine = SpawnWaveCatalog.FormatGroup(group);
            Debug.Log("[DeadEnd] MobWave " + id + " " + SpawnWaveCatalog.ClassLabel(cls)
                      + " → " + LastGroupLine + " n=" + want + " (1–3) Tm=" + tm.ToString("0.00")
                      + " Bm=" + bm.ToString("0.00") + " ai=MobFourStateAi existing scale");

            int spawned = 0;
            for (int m = 0; m < group.Members.Length && spawned < want; m++)
            {
                SpawnMember mem = group.Members[m];
                for (int k = 0; k < mem.Count && spawned < want; k++)
                {
                    Vector3 stubPos = SpawnCluster.Offset(pos, spawned, want);
                    SpawnStub(id, stubPos, band, spawned, want, mem.KindId, tm, bm);
                    spawned++;
                }
            }

            while (spawned < want)
            {
                string kind = group.Members[0].KindId;
                if (string.IsNullOrEmpty(kind))
                    kind = "E1";
                Vector3 stubPos = SpawnCluster.Offset(pos, spawned, want);
                SpawnStub(id, stubPos, band, spawned, want, kind, tm, bm);
                spawned++;
            }

            return spawned;
        }

        void TrySpawnOne()
        {
            string band = _clock.BandId;
            if (band == "Pre")
                return;

            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (TryPlace(slot.Id, slot.Position, band, "tick"))
                    return;
            }
        }

        bool TryPlace(string id, Vector3 pos, string band, string phase)
        {
            int cur = SpawnPhaseCooldown.PhaseIndex(band);
            if (_lastPhase.TryGetValue(id, out int last) && SpawnPhaseCooldown.IsBlocked(last, band))
            {
                string cool = $"[SpawnCooldown] {id} SKIP phase={band}({cur}) last={last} ({phase}) — this+next blocked";
                if (!_log.Contains(cool))
                {
                    Remember(cool);
                    Debug.Log(cool);
                }
                return false;
            }

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

            if (cur < 0)
                return false;

            int room = SpawnScreenCap.Remaining;
            if (room <= 0)
            {
                string cap = $"[SpawnScreenCap] {id} SKIP ({phase}) live={SpawnScreenCap.LiveCount()}/{SpawnScreenCap.MaxLive}";
                if (!_log.Contains(cap))
                {
                    Remember(cap);
                    Debug.Log(cap);
                }
                return false;
            }

            SpawnClassId cls = SpawnWaveCatalog.ResolveClass(band, _build);
            SpawnGroupDef group = SpawnWaveCatalog.RollGroup(cls, _rng);
            int want = SpawnWaveCatalog.TotalCount(group);
            if (want <= 0)
                return false;
            int n = Mathf.Min(want, room);
            float t = _clock != null ? _clock.WallMinutes : 0f;
            int build = _build != null ? _build.BuildCount : 0;
            float tm = SpawnWaveCatalog.TimeMul(t);
            float bm = SpawnWaveCatalog.BuildMul(build);
            LastClass = cls;
            LastGroupLine = SpawnWaveCatalog.FormatGroup(group);

            _spawned.Add(id);
            _lastPhase[id] = cur;
            string line = $"[SpawnCooldown] {id} SPAWN phase={band}({cur}) last={cur} ({phase}) interval={CurrentInterval():0.00}s";
            Remember(line);
            Debug.Log(line);
            Debug.Log($"[SpawnClass] {SpawnWaveCatalog.ClassLabel(cls)} → group {LastGroupLine} Tm={tm:0.00} Bm={bm:0.00} B={build} t={t:0.00}′");

            int spawned = 0;
            if (group.Members != null)
            {
                for (int m = 0; m < group.Members.Length && spawned < n; m++)
                {
                    SpawnMember mem = group.Members[m];
                    for (int k = 0; k < mem.Count && spawned < n; k++)
                    {
                        Vector3 stubPos = SpawnCluster.Offset(pos, spawned, n);
                        SpawnStub(id, stubPos, band, spawned, n, mem.KindId, tm, bm);
                        spawned++;
                    }
                }
            }

            return true;
        }

        void SpawnStub(string id, Vector3 pos, string band, int index, int total, string kindId, float tm, float bm)
        {
            if (_stubPrefab == null)
                return;

            GameObject stub = Instantiate(_stubPrefab, pos, Quaternion.identity);
            stub.name = total > 1 ? $"Stub_{id}_{index}_{kindId}" : "Stub_" + id + "_" + kindId;
            SegmentMul seg = _lock != null ? _lock.GetSegment(band) : null;
            SegmentMul z1 = _lock != null ? _lock.GetSegment("Z1") : null;
            float segHp = seg != null ? seg.hpMul : 1f;
            float baselineHp = z1 != null ? z1.hpMul : 1f;
            var pressure = stub.GetComponent<EnemyPressureState>();
            if (pressure == null)
                pressure = stub.AddComponent<EnemyPressureState>();
            pressure.Bind(_clock, segHp, baselineHp);
            var enemy = stub.GetComponent<StubEnemy>();
            if (enemy == null)
                enemy = stub.AddComponent<StubEnemy>();
            int hp = SpawnWaveCatalog.ScaledHp(kindId, tm, bm, SpawnWaveCatalog.SixBagHpMul);
            enemy.ConfigureKind(kindId, hp);
            if (_onEnemyKilled != null)
            {
                enemy.Died -= _onEnemyKilled;
                enemy.Died += _onEnemyKilled;
            }
            var sr = stub.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = BandColor(band);
            stub.SetActive(true);
            var ai = stub.GetComponent<MobFourStateAi>();
            if (ai == null)
                ai = stub.AddComponent<MobFourStateAi>();
            ai.Configure(_lock, _player);
            ai.ApplyKindSpeed(enemy.KindId);
            Debug.Log($"[MobAI] {stub.name} spawn→Patrol hp={hp} kind={kindId} Tm×Bm={tm:0.00}×{bm:0.00}");
        }

        // Keep overload for any legacy callers.
        void SpawnStub(string id, Vector3 pos, string band, int index, int total)
        {
            float t = _clock != null ? _clock.WallMinutes : 0f;
            float tm = SpawnWaveCatalog.TimeMul(t);
            float bm = SpawnWaveCatalog.BuildMul(_build != null ? _build.BuildCount : 0);
            SpawnStub(id, pos, band, index, total, "E1", tm, bm);
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
            while (_log.Count > 16)
                _log.RemoveAt(0);
        }

        static string Fmt(string id) => string.IsNullOrEmpty(id) ? "—" : id;
    }
}
