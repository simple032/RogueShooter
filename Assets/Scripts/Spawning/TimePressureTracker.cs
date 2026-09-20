using UnityEngine;
using RogueShooter.Balance;

namespace RogueShooter.Spawning
{
    /// <summary>Logs wall-clock pressure band switches P1→P2→P3→P4.</summary>
    public class TimePressureTracker : MonoBehaviour
    {
        SpawnBandClock _clock;
        string _phase = "";

        public string Phase => string.IsNullOrEmpty(_phase) ? "" : _phase;

        public void Bind(SpawnBandClock clock)
        {
            _clock = clock;
            _phase = "";
            Tick(force: true);
        }

        void Update() => Tick(force: false);

        void Tick(bool force)
        {
            if (_clock == null)
                return;
            string next = TimePressure.PhaseId(_clock.WallMinutes);
            if (!force && next == _phase)
                return;
            string previous = _phase;
            _phase = next;
            float t = _clock.WallMinutes;
            string line =
                $"[TimePressure] {Fmt(previous)} → {next} ({TimePressure.PhaseLabel(_clock.WallMinutes)}) at {t:0.00}′ " +
                $"attr={TimePressure.AttrMul(t):0.000} int={TimePressure.IntervalMul(t):0.000}";
            Debug.Log(line);
        }

        static string Fmt(string id) => string.IsNullOrEmpty(id) ? "—" : id;
    }
}