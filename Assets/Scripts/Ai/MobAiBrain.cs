using System;

namespace RogueShooter.Ai
{
    public enum MobAiState
    {
        Patrol,
        Alert,
        Chase,
        Disengage
    }

    /// <summary>
    /// Patrol → Alert → Chase → Disengage → Patrol.
    /// Detect / disengage radii come from config. Outside detect, chase is not started.
    /// </summary>
    public sealed class MobAiBrain
    {
        public float DetectRadius { get; private set; }
        public float DisengageMul { get; private set; }
        public float AlertSeconds { get; private set; }
        public MobAiState State { get; private set; }

        public float DisengageRadius
        {
            get { return DetectRadius * DisengageMul; }
        }

        float _alertT;
        bool _forceChaseFromDamage;

        public void Configure(float detectRadius, float disengageMul, float alertSeconds)
        {
            DetectRadius = detectRadius > 0.01f ? detectRadius : 5.5f;
            DisengageMul = disengageMul > 0.01f ? disengageMul : 1.6f;
            AlertSeconds = alertSeconds < 0f ? 0f : alertSeconds;
            Reset();
        }

        public void Reset()
        {
            State = MobAiState.Patrol;
            _alertT = 0f;
            _forceChaseFromDamage = false;
        }

        public MobAiState Tick(float distToPlayer, bool damaged, float dt, bool atHome)
        {
            if (damaged)
                EnterAlert(true);

            switch (State)
            {
                case MobAiState.Patrol:
                    if (distToPlayer <= DetectRadius)
                        EnterAlert(false);
                    break;

                case MobAiState.Alert:
                    _alertT += dt < 0f ? 0f : dt;
                    if (_alertT >= AlertSeconds)
                    {
                        bool inDetect = distToPlayer <= DetectRadius;
                        bool damageChase = _forceChaseFromDamage && distToPlayer <= DisengageRadius;
                        if (inDetect || damageChase)
                            SetState(MobAiState.Chase);
                        else
                            SetState(MobAiState.Patrol);
                    }
                    break;

                case MobAiState.Chase:
                    if (distToPlayer > DisengageRadius)
                        SetState(MobAiState.Disengage);
                    break;

                case MobAiState.Disengage:
                    if (distToPlayer <= DetectRadius)
                        EnterAlert(false);
                    else if (atHome)
                        SetState(MobAiState.Patrol);
                    break;
            }

            return State;
        }

        void EnterAlert(bool fromDamage)
        {
            _forceChaseFromDamage = fromDamage || _forceChaseFromDamage;
            if (State == MobAiState.Alert)
                return;
            _alertT = 0f;
            SetState(MobAiState.Alert);
        }

        void SetState(MobAiState next)
        {
            if (State == next)
                return;
            if (next == MobAiState.Patrol)
                _forceChaseFromDamage = false;
            if (next == MobAiState.Chase)
                _forceChaseFromDamage = false;
            State = next;
        }
    }
}
