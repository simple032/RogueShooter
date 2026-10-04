using System;

namespace RogueShooter.Boss
{
    /// <summary>
    /// 终局 Boss 开招。贴脸改播近战且不生成弹体；更远仍走原四招。
    /// 近战表现先占在前摇、出手、恢复这三段上，秒数用草案，不绑新图。
    /// 不改玩家手感，也不新增无敌。无敌只读调用方给出的既有标记。
    /// </summary>
    public sealed class FinalBossCombat
    {
        static readonly FinalBossMoveId[] P1Moves =
        {
            FinalBossMoveId.NailBow,
            FinalBossMoveId.ShieldCharge
        };

        static readonly FinalBossMoveId[] P2Moves =
        {
            FinalBossMoveId.TripleArrow,
            FinalBossMoveId.RingBolt
        };

        float _moveT;
        float _beatDamage;
        int _beatHits;
        bool _beatWhiff;
        int _beatProjectiles;
        bool _volleySpawned;
        bool _hitResolved;
        int _p1;
        int _p2;
        FinalBossMoveId _replaced;

        public float MaxHp { get { return FinalBossRules.MaxHp; } }
        public float Hp { get; private set; }
        public FinalBossPhase Phase { get; private set; }
        public FinalBossMoveId CurrentMove { get; private set; }
        public FinalBossStep Step { get; private set; }
        public FinalBossMoveId ReplacedMove { get { return _replaced; } }
        public float MoveTime { get { return _moveT; } }
        public float BeatDamage { get { return _beatDamage; } }
        public int BeatHits { get { return _beatHits; } }
        public bool BeatWhiff { get { return _beatWhiff; } }
        public int BeatProjectiles { get { return _beatProjectiles; } }
        public int ProjectilesAlive { get; private set; }
        public int ProjectilesSpawned { get; private set; }
        public float DamageToPlayer { get; private set; }
        public FinalBossMoveId LastBeatMove { get; private set; }
        public float LastBeatDamage { get; private set; }
        public int LastBeatHits { get; private set; }
        public bool LastBeatWhiff { get; private set; }
        public int LastBeatProjectiles { get; private set; }
        public FinalBossMoveId LastReplacedMove { get; private set; }
        public int P1Index { get { return _p1; } }
        public int P2Index { get { return _p2; } }
        public float FacingX { get; private set; }
        public float FacingY { get; private set; }
        public float BossX { get; private set; }
        public float BossY { get; private set; }
        public float PlayerX { get; private set; }
        public float PlayerY { get; private set; }
        public bool BodyAndCrownInView { get; set; }
        public bool PlayerInvulnerable { get; set; }

        public float Distance
        {
            get
            {
                float dx = PlayerX - BossX;
                float dy = PlayerY - BossY;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public FinalBossCombat()
        {
            Reset();
        }

        public void Reset()
        {
            Hp = FinalBossRules.MaxHp;
            Phase = FinalBossPhase.P1;
            CurrentMove = FinalBossMoveId.None;
            Step = FinalBossStep.Idle;
            _replaced = FinalBossMoveId.None;
            _moveT = 0f;
            _beatDamage = 0f;
            _beatHits = 0;
            _beatWhiff = false;
            _beatProjectiles = 0;
            _volleySpawned = false;
            _hitResolved = false;
            _p1 = 0;
            _p2 = 0;
            ProjectilesAlive = 0;
            ProjectilesSpawned = 0;
            DamageToPlayer = 0f;
            LastBeatMove = FinalBossMoveId.None;
            LastBeatDamage = 0f;
            LastBeatHits = 0;
            LastBeatWhiff = false;
            LastBeatProjectiles = 0;
            LastReplacedMove = FinalBossMoveId.None;
            FacingX = 0f;
            FacingY = -1f;
            BossX = 0f;
            BossY = 0f;
            PlayerX = 8f;
            PlayerY = 0f;
            BodyAndCrownInView = true;
            PlayerInvulnerable = false;
        }

        public void PlacePlayerAtDistance(float distance)
        {
            PlayerX = BossX + distance;
            PlayerY = BossY;
        }

        public void SetPlayerPosition(float x, float y)
        {
            PlayerX = x;
            PlayerY = y;
        }

        /// <summary>掉血可以切阶段。不取消正在放的招。</summary>
        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || Hp <= 0f)
                return;
            Hp -= amount;
            if (Hp < 0f)
                Hp = 0f;
            if (Hp <= 0f)
                return;
            if (Phase == FinalBossPhase.P1 && Hp <= MaxHp * BossBrain.Phase2HpFrac)
                Phase = FinalBossPhase.P2;
        }

        public void Tick(float dt)
        {
            if (dt < 0f)
                dt = 0f;
            if (Hp <= 0f)
                return;

            if (Step == FinalBossStep.Idle)
                TryOpen();
            if (Step == FinalBossStep.Idle)
                return;

            float prev = _moveT;
            _moveT += dt;
            AdvanceMove(prev, _moveT);
            if (Step == FinalBossStep.Idle)
                TryOpen();
        }

        void TryOpen()
        {
            if (Step != FinalBossStep.Idle || ProjectilesAlive > 0 || Hp <= 0f)
                return;

            FinalBossOpenChoice choice = FinalBossRules.Choose(Distance, BodyAndCrownInView);
            if (choice == FinalBossOpenChoice.Hold)
                return;

            FinalBossMoveId ranged = Peek();
            AdvancePointer();
            if (choice == FinalBossOpenChoice.Melee)
                Begin(FinalBossMoveId.Melee, ranged);
            else
                Begin(ranged, FinalBossMoveId.None);
        }

        FinalBossMoveId Peek()
        {
            if (Phase == FinalBossPhase.P2)
                return P2Moves[_p2 % P2Moves.Length];
            return P1Moves[_p1 % P1Moves.Length];
        }

        void AdvancePointer()
        {
            if (Phase == FinalBossPhase.P2)
                _p2++;
            else
                _p1++;
        }

        void Begin(FinalBossMoveId move, FinalBossMoveId replaced)
        {
            CurrentMove = move;
            _replaced = replaced;
            Step = FinalBossStep.Windup;
            _moveT = 0f;
            _beatDamage = 0f;
            _beatHits = 0;
            _beatWhiff = false;
            _beatProjectiles = 0;
            _volleySpawned = false;
            _hitResolved = false;
            ProjectilesAlive = 0;
            LockFacing();
        }

        void LockFacing()
        {
            float dx = PlayerX - BossX;
            float dy = PlayerY - BossY;
            float mag = (float)Math.Sqrt(dx * dx + dy * dy);
            if (mag < 0.00001f)
            {
                FacingX = 0f;
                FacingY = -1f;
                return;
            }

            FacingX = dx / mag;
            FacingY = dy / mag;
        }

        void AdvanceMove(float prev, float now)
        {
            float windup;
            float active;
            float recovery;
            FinalBossRules.Timing(CurrentMove, out windup, out active, out recovery);
            float activeEnd = windup + active;
            float total = activeEnd + recovery;
            float hitStart = CurrentMove == FinalBossMoveId.Melee
                ? FinalBossRules.MeleeHitTimeSeconds
                : windup;
            float hitEnd = hitStart + active;

            if (CurrentMove != FinalBossMoveId.Melee && !_volleySpawned && now >= windup)
            {
                _volleySpawned = true;
                int n = FinalBossRules.ProjectileVolley(CurrentMove);
                _beatProjectiles = n;
                ProjectilesAlive = n;
                ProjectilesSpawned += n;
            }

            if (CurrentMove == FinalBossMoveId.Melee && !_hitResolved && now >= hitStart && prev < hitEnd)
            {
                bool inRange = Distance <= FinalBossRules.MeleeTriggerDistance;
                if (inRange && !PlayerInvulnerable)
                    ResolveHit();
                else if (now >= hitEnd)
                    ResolveWhiff();
            }

            if (now >= total)
            {
                FinishBeat();
                return;
            }

            if (now >= activeEnd)
            {
                ProjectilesAlive = 0;
                Step = FinalBossStep.Recovery;
                return;
            }

            if (now >= windup)
            {
                Step = FinalBossStep.Active;
                return;
            }

            Step = FinalBossStep.Windup;
        }

        void ResolveHit()
        {
            _hitResolved = true;
            _beatWhiff = false;
            _beatHits = FinalBossRules.MeleeHits;
            _beatDamage = FinalBossRules.MeleeDamage;
            DamageToPlayer += FinalBossRules.MeleeDamage;
        }

        void ResolveWhiff()
        {
            _hitResolved = true;
            _beatWhiff = true;
            _beatHits = 0;
            _beatDamage = 0f;
        }

        void FinishBeat()
        {
            LastBeatMove = CurrentMove;
            LastBeatDamage = _beatDamage;
            LastBeatHits = _beatHits;
            LastBeatWhiff = _beatWhiff;
            LastBeatProjectiles = _beatProjectiles;
            LastReplacedMove = _replaced;
            ProjectilesAlive = 0;
            CurrentMove = FinalBossMoveId.None;
            _replaced = FinalBossMoveId.None;
            Step = FinalBossStep.Idle;
            _moveT = 0f;
        }
    }
}
