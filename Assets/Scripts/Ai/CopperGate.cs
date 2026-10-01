using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Spawning;

namespace RogueShooter.Ai
{
    /// <summary>
    /// Copper gate pulse. Position locks when the warning starts.
    /// Stage 1 does not spawn these. Rooms are not laid here.
    /// </summary>
    public class CopperGate : MonoBehaviour
    {
        public const float Width = 1.2f;
        public const float WarnSeconds = 1.2f;
        public const float ActiveSeconds = 0.6f;
        public const float RestSeconds = 5f;
        public const float Damage = 10f;

        enum Phase
        {
            Warn,
            Active,
            Rest,
            Stopped
        }

        Phase _phase = Phase.Stopped;
        float _left;
        readonly HashSet<string> _hit = new HashSet<string>();

        public Vector3 LockedPosition { get; private set; }
        public string PhaseName { get; private set; }
        public int Pulses { get; private set; }
        public bool RoomStopped => _phase == Phase.Stopped && PhaseName == "停";

        public static bool MaySpawn(StageId stage)
        {
            return stage != StageId.S1;
        }

        public static bool CoversPoint(Vector3 gate, Vector3 point)
        {
            Vector3 d = gate - point;
            d.z = 0f;
            return d.magnitude <= Width * 0.5f;
        }

        public bool TryPlace(Vector3 pos, Vector3[] forbidden)
        {
            if (forbidden != null)
            {
                for (int i = 0; i < forbidden.Length; i++)
                {
                    if (CoversPoint(pos, forbidden[i]))
                        return false;
                }
            }

            LockedPosition = pos;
            transform.position = pos;
            Pulses = 0;
            BeginWarn();
            return true;
        }

        public void StopForClear()
        {
            _phase = Phase.Stopped;
            _left = 0f;
            PhaseName = "停";
            _hit.Clear();
        }

        public float Simulate(float dt, Vector3 person, string personId)
        {
            if (_phase == Phase.Stopped || dt <= 0f)
                return 0f;
            transform.position = LockedPosition;
            float dealt = 0f;
            float budget = dt;
            int guard = 0;
            while (budget > 0.0001f && guard++ < 8 && _phase != Phase.Stopped)
            {
                float slice = _left < budget ? _left : budget;
                if (slice < 0f)
                    slice = 0f;
                if (_phase == Phase.Active && Overlaps(person) && _hit.Add(personId ?? ""))
                    dealt += Damage;
                _left -= slice;
                budget -= slice;
                if (_left > 0.0001f)
                    break;
                Advance();
            }

            return dealt;
        }

        bool Overlaps(Vector3 person)
        {
            return CoversPoint(LockedPosition, person);
        }

        void BeginWarn()
        {
            _phase = Phase.Warn;
            _left = WarnSeconds;
            PhaseName = "预警";
            _hit.Clear();
            Pulses++;
            transform.position = LockedPosition;
        }

        void Advance()
        {
            if (_phase == Phase.Warn)
            {
                _phase = Phase.Active;
                _left = ActiveSeconds;
                PhaseName = "生效";
                _hit.Clear();
                return;
            }

            if (_phase == Phase.Active)
            {
                _phase = Phase.Rest;
                _left = RestSeconds;
                PhaseName = "休息";
                _hit.Clear();
                return;
            }

            if (_phase == Phase.Rest)
                BeginWarn();
        }
    }
}
