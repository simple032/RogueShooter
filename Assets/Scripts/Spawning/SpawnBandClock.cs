using UnityEngine;
using RogueShooter.Balance;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Wall-clock spawn bands Z1 0–3′ / Z2 3–6′ / Z3 6–8′. Demo can scale or jump.
    /// </summary>
    public class SpawnBandClock : MonoBehaviour
    {
        [SerializeField] float clockScale = 20f;
        [SerializeField] bool paused;

        BalanceLockData _lock;
        float _wallSeconds;
        string _bandId = "";
        int _waveIndex;
        float _bandEnteredWallSeconds;

        public float ClockScale => clockScale;
        public bool Paused => paused;
        public float WallSeconds => _wallSeconds;
        public float WallMinutes => _wallSeconds / 60f;
        public string BandId => string.IsNullOrEmpty(_bandId) ? "Pre" : _bandId;
        public int WaveIndex => _waveIndex;
        public int WaveCount { get; private set; }
        public float TWaveMidSeconds { get; private set; }

        public event System.Action<string, string> BandChanged;

        public void Bind(BalanceLockData data, float scale)
        {
            _lock = data;
            clockScale = Mathf.Max(0.01f, scale);
            _wallSeconds = 0f;
            ApplyBand(forceEvent: false);
        }

        public void SetPaused(bool value) => paused = value;

        public void SetClockScale(float scale) => clockScale = Mathf.Max(0.01f, scale);

        public void JumpToBand(string bandId)
        {
            if (_lock == null)
                return;
            ClockBand band = _lock.GetClockBand(bandId);
            if (band == null)
            {
                ClockBand z3 = _lock.GetClockBand("Z3");
                _wallSeconds = z3 != null ? z3.endMin * 60f : 8f * 60f;
            }
            else
            {
                _wallSeconds = band.startMin * 60f;
            }

            ApplyBand(forceEvent: true);
        }

        public void JumpToMinutes(float minutes)
        {
            _wallSeconds = Mathf.Max(0f, minutes) * 60f;
            ApplyBand(forceEvent: true);
        }

        public void ResetClock()
        {
            _wallSeconds = 0f;
            ApplyBand(forceEvent: true);
        }

        void Update()
        {
            if (_lock == null || paused)
                return;
            _wallSeconds += Time.deltaTime * clockScale;
            ApplyBand(forceEvent: false);
            TickWaves();
        }

        void ApplyBand(bool forceEvent)
        {
            if (_lock == null)
                return;

            ClockBand band = _lock.ClockBandAtMinutes(WallMinutes);
            string nextId = band != null ? band.id : "Pre";
            if (!forceEvent && nextId == _bandId)
                return;

            string previous = _bandId;
            _bandId = nextId;
            _bandEnteredWallSeconds = _wallSeconds;
            WaveSpec waves = _lock.GetWave(nextId);
            WaveCount = waves != null ? waves.count : 0;
            TWaveMidSeconds = waves != null ? waves.tWaveMidSeconds : 0f;
            _waveIndex = WaveCount > 0 ? 1 : 0;
            BandChanged?.Invoke(previous, _bandId);
        }

        void TickWaves()
        {
            if (WaveCount < 2 || _waveIndex >= WaveCount || TWaveMidSeconds <= 0f)
                return;
            if (_wallSeconds - _bandEnteredWallSeconds >= TWaveMidSeconds)
            {
                _waveIndex = WaveCount;
                Debug.Log($"[SpawnBand] {BandId} wave {_waveIndex}/{WaveCount} (t_wave mid {TWaveMidSeconds:0}s)");
            }
        }
    }
}
