using System;

namespace RogueShooter.Boss
{
    public enum BossPhase
    {
        IdleOutside,
        Entering,
        DoorSealed,
        P1,
        P2,
        Defeated
    }

    public enum BossMoveId
    {
        None,
        StraightShot,
        WarningCharge,
        TripleShot,
        RingBurst
    }

    public enum BossMoveStep
    {
        Idle,
        Windup,
        Active,
        Recovery
    }

    /// <summary>
    /// Independent BOSS fight SM (W3-01/02). Not MobAiBrain / patrol tree.
    /// Enter → seal door → P1 moves → HP≤50% → P2 moves → HP≤0 → Defeated.
    /// W3-02: MaxHP locked on enter from Build×time CSV; cross-seg only bumps DmgMul.
    /// </summary>
    public sealed class BossBrain
    {
        public const float DefaultMaxHp = 3850f;
        public const float Phase2HpFrac = 0.5f;

        public float MaxHp { get; private set; }
        public float Hp { get; private set; }
        public bool HpLocked { get; private set; }
        public string EnterAnchorId { get; private set; }
        public int EnterBuild { get; private set; }
        public int EnterTimeTier { get; private set; }
        public float EnterDmgMul { get; private set; }
        public float LiveDmgMul { get; private set; }
        public BossPhase Phase { get; private set; }
        public bool DoorClosed { get; private set; }
        public BossMoveId CurrentMove { get; private set; }
        public BossMoveStep MoveStep { get; private set; }
        public int MoveIndex { get; private set; }
        public int MovesCompleted { get; private set; }
        public bool UsesMobAi { get { return false; } }

        float _stepT;
        float _windup;
        float _active;
        float _recovery;
        int _p1Pick;
        int _p2Pick;

        static readonly BossMoveId[] P1Moves =
        {
            BossMoveId.StraightShot,
            BossMoveId.WarningCharge
        };

        static readonly BossMoveId[] P2Moves =
        {
            BossMoveId.TripleShot,
            BossMoveId.RingBurst
        };

        public void Configure(float maxHp)
        {
            MaxHp = maxHp > 1f ? maxHp : DefaultMaxHp;
            Reset();
        }

        public void Reset()
        {
            Hp = MaxHp > 1f ? MaxHp : DefaultMaxHp;
            if (MaxHp < 1f)
                MaxHp = DefaultMaxHp;
            HpLocked = false;
            EnterAnchorId = "";
            EnterBuild = 0;
            EnterTimeTier = 0;
            EnterDmgMul = 1f;
            LiveDmgMul = 1f;
            Phase = BossPhase.IdleOutside;
            DoorClosed = false;
            CurrentMove = BossMoveId.None;
            MoveStep = BossMoveStep.Idle;
            MoveIndex = 0;
            MovesCompleted = 0;
            _stepT = 0f;
            _p1Pick = 0;
            _p2Pick = 0;
            ClearTiming();
        }

        /// <summary>W3-02: lock MaxHP / enter dmg from scale snapshot. Call before NotifyEnter.</summary>
        public void LockHpOnEnter(BossScaleSnapshot snap)
        {
            if (HpLocked)
                return;
            MaxHp = snap.MaxHp > 1f ? snap.MaxHp : DefaultMaxHp;
            Hp = MaxHp;
            HpLocked = true;
            EnterAnchorId = snap.AnchorId ?? "";
            EnterBuild = snap.BuildCount;
            EnterTimeTier = snap.TimeTier;
            EnterDmgMul = snap.DmgMul > 0.01f ? snap.DmgMul : 1f;
            LiveDmgMul = EnterDmgMul;
        }

        /// <summary>
        /// Mid-fight time cross: refresh damage mul only; MaxHP/Hp stay locked.
        /// </summary>
        public void NotifyTimeCross(float wallMinutes)
        {
            if (!HpLocked)
                return;
            float next = BossScaleTable.DmgMulFor(EnterBuild, wallMinutes);
            if (next > 0.01f)
                LiveDmgMul = next;
        }

        /// <summary>Player crossed BOSS room threshold.</summary>
        public void NotifyEnter()
        {
            if (Phase != BossPhase.IdleOutside)
                return;
            Phase = BossPhase.Entering;
            DoorClosed = false;
        }

        public void ApplyDamage(float amount)
        {
            if (Phase == BossPhase.IdleOutside || Phase == BossPhase.Defeated)
                return;
            if (amount <= 0f)
                return;
            Hp -= amount;
            if (Hp < 0f)
                Hp = 0f;

            if (Hp <= 0f)
            {
                Phase = BossPhase.Defeated;
                CurrentMove = BossMoveId.None;
                MoveStep = BossMoveStep.Idle;
                ClearTiming();
                return;
            }

            if (Phase == BossPhase.P1 && Hp <= MaxHp * Phase2HpFrac)
                EnterPhase2();
        }

        /// <summary>Spec §4 弱点命中硬直: drop current windup/active/recovery.</summary>
        public void InterruptCurrentMove()
        {
            if (Phase == BossPhase.Defeated || Phase == BossPhase.IdleOutside)
                return;
            CurrentMove = BossMoveId.None;
            MoveStep = BossMoveStep.Idle;
            ClearTiming();
        }

        public BossPhase Tick(float dt)
        {
            if (dt < 0f)
                dt = 0f;

            switch (Phase)
            {
                case BossPhase.Entering:
                    // Seal immediately on first tick after enter.
                    DoorClosed = true;
                    Phase = BossPhase.DoorSealed;
                    break;

                case BossPhase.DoorSealed:
                    Phase = BossPhase.P1;
                    BeginNextMove();
                    break;

                case BossPhase.P1:
                case BossPhase.P2:
                    TickMove(dt);
                    break;
            }

            return Phase;
        }

        void EnterPhase2()
        {
            Phase = BossPhase.P2;
            CurrentMove = BossMoveId.None;
            MoveStep = BossMoveStep.Idle;
            ClearTiming();
            BeginNextMove();
        }

        void TickMove(float dt)
        {
            if (MoveStep == BossMoveStep.Idle)
            {
                BeginNextMove();
                return;
            }

            _stepT += dt;
            switch (MoveStep)
            {
                case BossMoveStep.Windup:
                    if (_stepT >= _windup)
                        EnterStep(BossMoveStep.Active, _active);
                    break;
                case BossMoveStep.Active:
                    if (_stepT >= _active)
                        EnterStep(BossMoveStep.Recovery, _recovery);
                    break;
                case BossMoveStep.Recovery:
                    if (_stepT >= _recovery)
                    {
                        MovesCompleted++;
                        MoveStep = BossMoveStep.Idle;
                        CurrentMove = BossMoveId.None;
                        ClearTiming();
                        BeginNextMove();
                    }
                    break;
            }
        }

        void BeginNextMove()
        {
            BossMoveId move;
            if (Phase == BossPhase.P1)
            {
                move = P1Moves[_p1Pick % P1Moves.Length];
                _p1Pick++;
            }
            else if (Phase == BossPhase.P2)
            {
                move = P2Moves[_p2Pick % P2Moves.Length];
                _p2Pick++;
            }
            else
                return;

            CurrentMove = move;
            MoveIndex++;
            TimingsFor(move, out _windup, out _active, out _recovery);
            EnterStep(BossMoveStep.Windup, _windup);
        }

        void EnterStep(BossMoveStep step, float duration)
        {
            MoveStep = step;
            _stepT = 0f;
            if (duration < 0f)
                duration = 0f;
        }

        void ClearTiming()
        {
            _stepT = 0f;
            _windup = 0f;
            _active = 0f;
            _recovery = 0f;
        }

        static void TimingsFor(BossMoveId move, out float windup, out float active, out float recovery)
        {
            // §10.1 策划设计表 defaults.
            switch (move)
            {
                case BossMoveId.StraightShot:
                    windup = 0.5f;
                    active = 0.4f;
                    recovery = 0.3f;
                    break;
                case BossMoveId.WarningCharge:
                    windup = 0.7f;
                    active = 0.35f;
                    recovery = 0.5f;
                    break;
                case BossMoveId.TripleShot:
                    windup = 0.4f;
                    active = 0.45f;
                    recovery = 0.4f;
                    break;
                case BossMoveId.RingBurst:
                    windup = 0.8f;
                    active = 0.35f;
                    recovery = 0.6f;
                    break;
                default:
                    windup = 0.3f;
                    active = 0.2f;
                    recovery = 0.2f;
                    break;
            }
        }

        public string Snapshot()
        {
            return string.Format(
                "phase={0} door={1} hp={2:0}/{3:0} lock={4} anchor={5} B={6} T={7} dmg={8:0.00}→{9:0.00} move={10}/{11} done={12} mobAi={13}",
                Phase, DoorClosed, Hp, MaxHp, HpLocked, EnterAnchorId, EnterBuild, EnterTimeTier,
                EnterDmgMul, LiveDmgMul, CurrentMove, MoveStep, MovesCompleted, UsesMobAi);
        }
    }
}
