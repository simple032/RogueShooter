using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Demo;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Tracks time-pressure attr on a stub. Unengaged follows wall clock;
    /// engage locks the mul at that moment.
    /// </summary>
    public class EnemyPressureState : MonoBehaviour
    {
        SpawnBandClock _clock;
        float _segmentHpMul = 1f;
        float _baselineHpMul = 1f;
        float _attrMul = 1f;
        bool _locked;
        StubEnemy _stub;

        public float AttrMul => _attrMul;
        public bool Locked => _locked;
        public string PhaseId => TimePressure.PhaseId(_clock != null ? _clock.WallMinutes : 0f);

        public void Bind(SpawnBandClock clock, float segmentHpMul, float baselineHpMul)
        {
            _clock = clock;
            _segmentHpMul = segmentHpMul > 0.01f ? segmentHpMul : 1f;
            _baselineHpMul = baselineHpMul > 0.01f ? baselineHpMul : 1f;
            _stub = GetComponent<StubEnemy>();
            _locked = false;
            ApplyMinutes(_clock != null ? _clock.WallMinutes : 0f, log: true);
        }

        public void SyncUnengaged()
        {
            if (_locked || _clock == null)
                return;
            ApplyMinutes(_clock.WallMinutes, log: false);
        }

        public void LockEngage()
        {
            if (_locked)
                return;
            if (_clock != null)
                ApplyMinutes(_clock.WallMinutes, log: false);
            _locked = true;
            float t = _clock != null ? _clock.WallMinutes : 0f;
            Debug.Log($"[TimePressure] {name} LOCK attr={_attrMul:0.000} phase={TimePressure.PhaseId(t)} t={t:0.00}′");
        }

        void ApplyMinutes(float minutes, bool log)
        {
            _attrMul = TimePressure.AttrMul(minutes);
            float hp = _segmentHpMul * _attrMul;
            float visual = Mathf.Clamp(hp / _baselineHpMul, 0.85f, 1.6f);
            if (_stub != null)
                _stub.SetPressureScale(visual);
            if (log)
            {
                Debug.Log($"[TimePressure] {name} APPLY attr={_attrMul:0.000} int={TimePressure.IntervalMul(minutes):0.000} " +
                          $"phase={TimePressure.PhaseId(minutes)} t={minutes:0.00}′");
            }
        }
    }
}
