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
    /// Outside detect radius, chase is never started (视野外不追).
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
            get { return DetectRadius * DisengageMul; }
        }

        float _alertT;

        public void Configure(float detectRadius, float disengageMul, float alertSeconds)
        {
            Configure(detectRadius, disengageMul, alertSeconds, 0.7f);
        }

        public void Configure(float detectRadius, float disengageMul, float alertSeconds, float attackRange)
        {
            DetectRadius = detectRadius > 0.01f ? detectRadius : 5.5f;
            DisengageMul = disengageMul > 0.01f ? disengageMul : 1.6f;
            AlertSeconds = alertSeconds < 0f ? 0f : alertSeconds;
            AttackRange = attackRange > 0.05f ? attackRange : 0.7f;
            Reset();
        }

        public void Reset()
        {
            State = MobAiState.Patrol;
            _alertT = 0f;
        }

        public MobAiState Tick(float distToPlayer, bool damaged, float dt, bool atHome)
        {
            if (damaged)
                EnterAlert();

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
                        if (distToPlayer <= DetectRadius)
                            SetState(MobAiState.Chase);
                        else
                            SetState(MobAiState.Patrol);
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
                    if (distToPlayer <= DetectRadius)
                        EnterAlert();
                    else if (atHome)
                        SetState(MobAiState.Patrol);
                    break;
            }

            return State;
        }

        void EnterAlert()
        {
            if (State == MobAiState.Alert)
                return;
            _alertT = 0f;
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
