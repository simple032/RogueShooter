using System;

namespace RogueShooter.Ai
{
    public enum MobAiState
    {
        Patrol,
        Alert,
        Chase,
        Attack,
        Disengage
    }

    /// <summary>
    /// Patrol → Alert → Chase → Attack → Disengage → Patrol.
    /// Fresh enemies use base vision; alert/damage remembers combat until the wider leash is left.
    /// </summary>
    public sealed class MobAiBrain
    {
        public float DetectRadius { get; private set; }
        public float DisengageMul { get; private set; }
        public float AlertSeconds { get; private set; }
        public float AttackRange { get; private set; }
        public MobAiState State { get; private set; }

        public float DisengageRadius
        {
            get { return System.Math.Max(18f, DetectRadius * System.Math.Max(2f, DisengageMul)); }
        }

        float _alertT;
        bool _engaged;
        // Engaged re-alert floor: hit mobs remember combat across the wider leash.
        // 15.5 keeps the +6.5 headroom over the 9.0 base detect (was 12 over 5.5).
        public float AlertRadius => _engaged ? System.Math.Max(15.5f, DetectRadius) : DetectRadius;

        public void Configure(float detectRadius, float disengageMul, float alertSeconds)
        {
            Configure(detectRadius, disengageMul, alertSeconds, 0.7f);
        }

        public void Configure(float detectRadius, float disengageMul, float alertSeconds, float attackRange)
        {
            DetectRadius = detectRadius > 0.01f ? detectRadius : 9.0f;
            DisengageMul = disengageMul > 0.01f ? disengageMul : 1.6f;
            AlertSeconds = alertSeconds < 0f ? 0f : alertSeconds;
            AttackRange = attackRange > 0.05f ? attackRange : 0.7f;
            Reset();
        }

        public void Reset()
        {
            State = MobAiState.Patrol;
            _alertT = 0f;
            _engaged = false;
        }

        public MobAiState Tick(float distToPlayer, bool damaged, float dt, bool atHome)
        {
            // Already in reach: swing or cast now. Do not sit in Alert at contact range.
            if (distToPlayer <= AttackRange)
            {
                _engaged = true;
                SetState(MobAiState.Attack);
                return State;
            }

            if (damaged)
            {
                _engaged = true;
                if (State == MobAiState.Patrol || State == MobAiState.Disengage) EnterAlert();
            }

            switch (State)
            {
                case MobAiState.Patrol:
                    // 视野外不追：only Alert when player enters detect.
                    if (distToPlayer <= DetectRadius)
                        EnterAlert();
                    break;

                case MobAiState.Alert:
                    _alertT += dt < 0f ? 0f : dt;
                    if (_alertT >= AlertSeconds)
                    {
                        if (distToPlayer <= DisengageRadius)
                            SetState(MobAiState.Chase);
                        else
                            SetState(MobAiState.Disengage);
                    }
                    break;

                case MobAiState.Chase:
                    if (distToPlayer > DisengageRadius)
                        SetState(MobAiState.Disengage);
                    else if (distToPlayer <= AttackRange)
                        SetState(MobAiState.Attack);
                    break;

                case MobAiState.Attack:
                    if (distToPlayer > DisengageRadius)
                        SetState(MobAiState.Disengage);
                    else if (distToPlayer > AttackRange)
                        SetState(MobAiState.Chase);
                    break;

                case MobAiState.Disengage:
                    if (distToPlayer <= AlertRadius)
                        EnterAlert();
                    else if (atHome)
                    { _engaged = false; SetState(MobAiState.Patrol); }
                    break;
            }

            return State;
        }

        void EnterAlert()
        {
            if (State == MobAiState.Alert)
                return;
            _alertT = 0f;
            _engaged = true;
            SetState(MobAiState.Alert);
        }

        void SetState(MobAiState next)
        {
            if (State == next)
                return;
            State = next;
        }
    }
}
