using System;
using System.Collections.Generic;

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

    /// <summary>
    /// One final-boss clock. RingBurst stays in the enum so old ids do not shift, and it is never started.
    /// </summary>
    public enum BossMoveId
    {
        None = 0,
        StraightShot = 1,
        WarningCharge = 2,
        TripleShot = 3,
        RingBurst = 4,
        BurstShot = 5,
        ShieldBash = 6,
        LeapSlam = 7,
        CrossbowBash = 8
    }

    public enum BossMoveStep
    {
        Idle,
        Windup,
        Active,
        Recovery
    }

    public struct MoveSpan
    {
        public BossMoveId Move;
        public int Index;
        public float From;
        public float To;
        public bool Finished;

        public bool HasMove
        {
            get { return Move != BossMoveId.None && To > From; }
        }
    }

    /// <summary>
    /// Single final-boss state machine: door, HP, phase, skill clock, cooldowns, ranged counter.
    /// Spatial hits live on <see cref="FinalBossCombat"/>. There is no second move rotator.
    /// </summary>
    public sealed class BossBrain
    {
        public const float DefaultMaxHp = FinalBossRules.BaseHp;
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
        public int Phase2Transitions { get; private set; }
        public float ActionElapsed { get; private set; }
        public float CombatSeconds { get; private set; }
        public int P1RangedUses { get; private set; }
        public float TransitionLeft { get; private set; }
        public bool UsesMobAi { get { return false; } }
        public bool Transitioning { get { return TransitionLeft > 0.00001f; } }

        public float QuietLeft
        {
            get { return Math.Max(0f, _quietUntil - CombatSeconds); }
        }

        public BossMoveId NextP1Ranged
        {
            get { return P1RangedUses % 3 == 2 ? BossMoveId.BurstShot : BossMoveId.StraightShot; }
        }

        public bool ReadyToSelect
        {
            get
            {
                return !Transitioning
                    && MoveStep == BossMoveStep.Idle
                    && CurrentMove == BossMoveId.None
                    && QuietLeft <= 0.00001f
                    && (Phase == BossPhase.P1 || Phase == BossPhase.P2);
            }
        }

        public float StepElapsed
        {
            get
            {
                if (MoveStep == BossMoveStep.Active)
                    return ActionElapsed - _windup;
                if (MoveStep == BossMoveStep.Recovery)
                    return ActionElapsed - _windup - _active;
                return ActionElapsed;
            }
        }

        public bool GroundVulnerable
        {
            get
            {
                if (CurrentMove != BossMoveId.LeapSlam)
                    return true;
                return ActionElapsed < FinalBossRules.LeapLandTime - FinalBossRules.LeapAirSeconds
                    || ActionElapsed >= FinalBossRules.LeapLandTime;
            }
        }

        public bool BlocksDisplacement
        {
            get
            {
                if (MoveStep != BossMoveStep.Active)
                    return false;
                return CurrentMove == BossMoveId.WarningCharge || CurrentMove == BossMoveId.LeapSlam;
            }
        }

        float _windup;
        float _active;
        float _recovery;
        float _quietUntil;
        float _weakUntil;
        int _weakMoveIndex = -1;
        int _rangedConsumedIndex = -1;
        int _sameCount;
        BossMoveId _lastMove;
        bool _quietArmed;
        readonly Dictionary<BossMoveId, float> _cooldowns = new Dictionary<BossMoveId, float>();

        public void Configure(float maxHp)
        {
            MaxHp = maxHp > 1f ? maxHp : DefaultMaxHp;
            Reset();
        }

        public void Reset()
        {
            if (MaxHp < 1f)
                MaxHp = DefaultMaxHp;
            Hp = MaxHp;
            HpLocked = false;
            EnterAnchorId = "";
            EnterBuild = 0;
            EnterTimeTier = 0;
            EnterDmgMul = 1f;
            LiveDmgMul = 1f;
            Phase = BossPhase.IdleOutside;
            DoorClosed = false;
            ClearMove();
            MoveIndex = 0;
            MovesCompleted = 0;
            Phase2Transitions = 0;
            P1RangedUses = 0;
            CombatSeconds = 0f;
            TransitionLeft = 0f;
            _quietUntil = 0f;
            _weakUntil = 0f;
            _weakMoveIndex = -1;
            _rangedConsumedIndex = -1;
            _sameCount = 0;
            _lastMove = BossMoveId.None;
            _quietArmed = false;
            _cooldowns.Clear();
        }

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

        /// <summary>Refresh outgoing damage from the current pressure. Max HP stays locked.</summary>
        public void RefreshDamage(float pressure)
        {
            if (!HpLocked || pressure <= 0.01f)
                return;
            LiveDmgMul = FinalBossRules.OutgoingMultiplier(EnterBuild, pressure);
        }

        public void NotifyTimeCross(float wallMinutes)
        {
            // Damage no longer follows the discrete time tier. Call RefreshDamage(pressure).
        }

        public void NotifyEnter()
        {
            if (Phase != BossPhase.IdleOutside)
                return;
            Phase = BossPhase.Entering;
            DoorClosed = false;
        }

        public void ApplyDamage(float amount)
        {
            if (Phase == BossPhase.IdleOutside || Phase == BossPhase.Defeated || Transitioning)
                return;
            if (amount <= 0f)
                return;
            Hp -= amount;
            if (Hp < 0f)
                Hp = 0f;
            if (Hp <= 0f)
            {
                Phase = BossPhase.Defeated;
                ClearMove();
                _quietArmed = false;
                return;
            }

            if (Phase == BossPhase.P1 && Hp <= MaxHp * Phase2HpFrac)
                EnterPhase2();
        }

        public void DrainTransition(float unscaledDt)
        {
            if (unscaledDt < 0f)
                unscaledDt = 0f;
            if (TransitionLeft <= 0f)
                return;
            TransitionLeft -= unscaledDt;
            if (TransitionLeft < 0f)
                TransitionLeft = 0f;
        }

        public bool CanUse(BossMoveId move)
        {
            if (FinalBossRules.IsRetired(move) || move == BossMoveId.None)
                return false;
            bool valid = Phase == BossPhase.P1
                ? FinalBossRules.IsP1Ranged(move) || move == BossMoveId.WarningCharge || move == BossMoveId.ShieldBash
                : Phase == BossPhase.P2 && (move == BossMoveId.TripleShot || move == BossMoveId.LeapSlam || move == BossMoveId.CrossbowBash);
            if (!valid)
                return false;
            float until;
            BossMoveId key = FinalBossRules.CooldownKey(move);
            if (_cooldowns.TryGetValue(key, out until) && CombatSeconds + 0.00001f < until)
                return false;
            bool same = key == FinalBossRules.CooldownKey(_lastMove);
            if (same && (FinalBossRules.IsHeavy(move) || _sameCount >= 2))
                return false;
            return true;
        }

        public bool TryStart(BossMoveId move)
        {
            if (!ReadyToSelect || !CanUse(move))
                return false;
            if (FinalBossRules.IsP1Ranged(move) && move != NextP1Ranged)
                return false;
            BossSkill skill = FinalBossRules.Skill(move);
            _windup = skill.Windup;
            _active = skill.Active;
            _recovery = skill.Recovery;
            BossMoveId key = FinalBossRules.CooldownKey(move);
            _sameCount = key == FinalBossRules.CooldownKey(_lastMove) ? _sameCount + 1 : 1;
            _lastMove = move;
            CurrentMove = move;
            MoveIndex++;
            ActionElapsed = 0f;
            MoveStep = BossMoveStep.Windup;
            _cooldowns[key] = CombatSeconds + skill.Cooldown;
            return true;
        }

        public void NotifyRangedFired(BossMoveId move, int moveIndex)
        {
            if (Phase != BossPhase.P1 || !FinalBossRules.IsP1Ranged(move))
                return;
            if (_rangedConsumedIndex == moveIndex)
                return;
            _rangedConsumedIndex = moveIndex;
            P1RangedUses++;
        }

        public bool TryWeakSpot()
        {
            if (Transitioning || MoveStep != BossMoveStep.Recovery)
                return false;
            if (Phase != BossPhase.P1 && Phase != BossPhase.P2)
                return false;
            if (_weakMoveIndex == MoveIndex || CombatSeconds < _weakUntil)
                return false;
            _recovery += FinalBossRules.RecoveryExtension;
            _weakUntil = CombatSeconds + FinalBossRules.WeakCooldown;
            _weakMoveIndex = MoveIndex;
            return true;
        }

        public void InterruptCurrentMove()
        {
            TryWeakSpot();
        }

        public void EndChargeEarly()
        {
            if (CurrentMove != BossMoveId.WarningCharge || MoveStep != BossMoveStep.Active)
                return;
            _active = Math.Max(0f, ActionElapsed - _windup);
            MoveStep = BossMoveStep.Recovery;
        }

        public BossPhase Tick(float dt)
        {
            Tick(dt, true);
            return Phase;
        }

        public MoveSpan Tick(float dt, bool projectilesClear)
        {
            if (dt < 0f)
                dt = 0f;
            if (Phase == BossPhase.Entering)
            {
                DoorClosed = true;
                Phase = BossPhase.DoorSealed;
                return default(MoveSpan);
            }

            if (Phase == BossPhase.DoorSealed)
            {
                Phase = BossPhase.P1;
                if (Hp <= MaxHp * Phase2HpFrac)
                    EnterPhase2();
                return default(MoveSpan);
            }

            if ((Phase != BossPhase.P1 && Phase != BossPhase.P2) || Transitioning)
                return default(MoveSpan);

            CombatSeconds += dt;
            MoveSpan span = default(MoveSpan);
            if (CurrentMove != BossMoveId.None && MoveStep != BossMoveStep.Idle)
            {
                float from = ActionElapsed;
                ActionElapsed += dt;
                float end = _windup + _active + _recovery;
                bool finished = ActionElapsed >= end - 0.00001f;
                float to = finished ? end : ActionElapsed;
                span = new MoveSpan
                {
                    Move = CurrentMove,
                    Index = MoveIndex,
                    From = from,
                    To = to,
                    Finished = finished
                };
                if (!finished)
                {
                    if (ActionElapsed >= _windup + _active)
                        MoveStep = BossMoveStep.Recovery;
                    else if (ActionElapsed >= _windup)
                        MoveStep = BossMoveStep.Active;
                    else
                        MoveStep = BossMoveStep.Windup;
                }
                else
                {
                    MovesCompleted++;
                    ClearMove();
                    _quietArmed = true;
                }
            }

            if (_quietArmed && projectilesClear && MoveStep == BossMoveStep.Idle)
            {
                _quietArmed = false;
                _quietUntil = CombatSeconds + FinalBossRules.Quiet(Phase);
            }

            return span;
        }

        public void NoteProjectiles(bool clear)
        {
            if (_quietArmed && clear && MoveStep == BossMoveStep.Idle)
            {
                _quietArmed = false;
                _quietUntil = CombatSeconds + FinalBossRules.Quiet(Phase);
            }
        }

        public static void TimingsFor(BossMoveId move, out float windup, out float active, out float recovery)
        {
            BossSkill skill = FinalBossRules.Skill(move);
            windup = skill.Windup;
            active = skill.Active;
            recovery = skill.Recovery;
        }

        void EnterPhase2()
        {
            if (Phase == BossPhase.Defeated || Phase2Transitions > 0)
                return;
            Phase = BossPhase.P2;
            Phase2Transitions++;
            ClearMove();
            _quietArmed = false;
            _quietUntil = CombatSeconds;
            _lastMove = BossMoveId.None;
            _sameCount = 0;
            TransitionLeft = FinalBossRules.TransitionSeconds;
        }

        void ClearMove()
        {
            CurrentMove = BossMoveId.None;
            MoveStep = BossMoveStep.Idle;
            ActionElapsed = 0f;
            _windup = 0f;
            _active = 0f;
            _recovery = 0f;
        }

        public string Snapshot()
        {
            return string.Format(
                "phase={0} door={1} hp={2:0}/{3:0} lock={4} anchor={5} B={6} T={7} dmg={8:0.00}→{9:0.00} move={10}/{11} done={12} ranged={13} mobAi={14}",
                Phase, DoorClosed, Hp, MaxHp, HpLocked, EnterAnchorId, EnterBuild, EnterTimeTier,
                EnterDmgMul, LiveDmgMul, CurrentMove, MoveStep, MovesCompleted, P1RangedUses, UsesMobAi);
        }
    }
}
