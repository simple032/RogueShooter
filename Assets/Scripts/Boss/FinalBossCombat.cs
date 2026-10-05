using System;
using System.Collections.Generic;

namespace RogueShooter.Boss
{
    public struct BossHitEvent
    {
        public float Damage;
        public string Kind;
        public BossMoveId Move;
        public int Group;
    }

    struct BossShot
    {
        public float X;
        public float Y;
        public float DirX;
        public float DirY;
        public float Speed;
        public float Radius;
        public float Left;
        public int Group;
        public int MoveIndex;
        public BossMoveId Move;
        public bool Alive;
    }

    struct WalkSample
    {
        public float Time;
        public float Dx;
        public float Dy;
        public float Dt;
    }

    /// <summary>
    /// Spatial execution for the one boss clock on <see cref="BossBrain"/>.
    /// Projectiles, fans, charge and leap are resolved here. This is not a second move machine.
    /// Provisional: no official frames. The driver may draw the logical shots.
    /// </summary>
    public sealed class FinalBossCombat
    {
        readonly BossBrain _brain;
        readonly List<BossShot> _shots = new List<BossShot>();
        readonly List<BossHitEvent> _hits = new List<BossHitEvent>();
        readonly List<long> _hitKeys = new List<long>();
        readonly List<WalkSample> _walk = new List<WalkSample>();
        readonly Random _rng;

        int _seenIndex = -1;
        int _shotsFired;
        bool _hitThisMove;
        bool _chargeDone;
        bool _leapLocked;
        bool _hasPlayer;
        bool _p1FirstRanged;
        bool _p1FirstCharge;
        bool _p2FirstRanged;
        float _aimX = 1f;
        float _aimY;
        float _leapX;
        float _leapY;
        float _leapStartX;
        float _leapStartY;
        float _dashEndX;
        float _dashEndY;

        public bool AllowActions = true;
        public bool AutoSelect = true;
        public bool AutoMove = true;
        public bool AcceptWalkSample = true;
        public bool BodyAndCrownInView = true;
        public bool PlayerInvulnerable;
        public bool HasArena;
        public float ArenaMinX;
        public float ArenaMinY;
        public float ArenaMaxX;
        public float ArenaMaxY;

        public float FacingX { get; private set; }
        public float FacingY { get; private set; }
        public float BossX { get; private set; }
        public float BossY { get; private set; }
        public float PlayerX { get; private set; }
        public float PlayerY { get; private set; }
        public int DamageApplications { get; private set; }
        public float DamageToPlayer { get; private set; }
        public int ArrowsFired { get; private set; }
        public int LastVolleyCount { get; private set; }
        public float LastVolleyTime { get; private set; }
        public int LastVolleyMoveIndex { get; private set; }
        public float LastVolleyAngle0 { get; private set; }
        public float LastVolleyAngle1 { get; private set; }
        public float LastVolleyAngle2 { get; private set; }
        public int LiveShots { get { return CountShots(); } }
        public float LeapTargetX { get { return _leapX; } }
        public float LeapTargetY { get { return _leapY; } }
        public bool LeapTargetLocked { get { return _leapLocked; } }

        public float MaxHp { get { return _brain != null ? _brain.MaxHp : FinalBossRules.MaxHp; } }
        public float Hp { get { return _brain != null ? _brain.Hp : 0f; } }
        public BossPhase Phase { get { return _brain != null ? _brain.Phase : BossPhase.IdleOutside; } }
        public BossMoveId CurrentMove { get { return _brain != null ? _brain.CurrentMove : BossMoveId.None; } }
        public BossMoveStep Step { get { return _brain != null ? _brain.MoveStep : BossMoveStep.Idle; } }
        public int P1RangedUses { get { return _brain != null ? _brain.P1RangedUses : 0; } }
        public bool ReadyToSelect { get { return _brain != null && _brain.ReadyToSelect; } }

        public float Distance
        {
            get
            {
                float dx = PlayerX - BossX;
                float dy = PlayerY - BossY;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public FinalBossCombat(BossBrain brain)
        {
            _brain = brain;
            _rng = new Random(42);
            FacingX = 0f;
            FacingY = -1f;
            _aimX = FacingX;
            _aimY = FacingY;
        }

        public void SetArena(float cx, float cy, float halfW, float halfH)
        {
            HasArena = true;
            ArenaMinX = cx - halfW;
            ArenaMaxX = cx + halfW;
            ArenaMinY = cy - halfH;
            ArenaMaxY = cy + halfH;
        }

        public void SetBossPosition(float x, float y)
        {
            BossX = x;
            BossY = y;
        }

        public void SetPlayerPosition(float x, float y)
        {
            PlacePlayer(x, y, 0f);
        }

        public void PlacePlayer(float x, float y, float dt)
        {
            if (_hasPlayer && AcceptWalkSample && dt > 0.000001f)
                NoteWalk(x - PlayerX, y - PlayerY, dt);
            _hasPlayer = true;
            PlayerX = x;
            PlayerY = y;
        }

        public void NoteWalk(float dx, float dy, float dt)
        {
            if (!AcceptWalkSample || dt <= 0.000001f)
                return;
            _walk.Add(new WalkSample
            {
                Time = _brain != null ? _brain.CombatSeconds : 0f,
                Dx = dx,
                Dy = dy,
                Dt = dt
            });
            TrimWalk();
        }

        public void SetFacing(float x, float y)
        {
            float mag = (float)Math.Sqrt(x * x + y * y);
            if (mag < 0.00001f)
                return;
            FacingX = x / mag;
            FacingY = y / mag;
            _aimX = FacingX;
            _aimY = FacingY;
        }

        public float ApplyPlayerShot(float amount, float dirX, float dirY, bool weak)
        {
            if (_brain == null || amount <= 0f)
                return 0f;
            if (_brain.Phase == BossPhase.IdleOutside || _brain.Phase == BossPhase.Defeated || _brain.Transitioning)
                return 0f;
            if (!_brain.GroundVulnerable)
                return 0f;
            BossPhase guard = _brain.Phase == BossPhase.P2 ? BossPhase.P2 : BossPhase.P1;
            float mul = FinalBossRules.IncomingMultiplier(guard, FacingX, FacingY, dirX, dirY, weak);
            int cuts = _brain.Phase2Transitions;
            float before = _brain.Hp;
            _brain.ApplyDamage(amount * mul);
            if (_brain.Phase == BossPhase.Defeated || _brain.Phase2Transitions > cuts)
                ClearShots();
            return before - _brain.Hp;
        }

        public bool TryStart(BossMoveId move)
        {
            if (_brain == null || !AllowActions)
                return false;
            return _brain.TryStart(move);
        }

        public void Simulate(float dt)
        {
            if (dt < 0f)
                dt = 0f;
            while (dt > 0.000001f)
            {
                float step = dt > FinalBossRules.LogicStep ? FinalBossRules.LogicStep : dt;
                dt -= step;
                TickSlice(step);
            }
        }

        public List<BossHitEvent> ConsumeHits()
        {
            var copy = new List<BossHitEvent>(_hits);
            _hits.Clear();
            return copy;
        }

        public void ClearShots()
        {
            _shots.Clear();
            if (_brain != null)
                _brain.NoteProjectiles(true);
        }

        void TickSlice(float dt)
        {
            if (_brain == null)
                return;
            if (!AllowActions || _brain.Transitioning)
                return;
            if (_brain.Phase != BossPhase.P1 && _brain.Phase != BossPhase.P2)
                return;
            bool clear = CountShots() == 0;
            MoveSpan span = _brain.Tick(dt, clear);

            if (span.HasMove)
                Resolve(span, dt);
            _brain.NoteProjectiles(CountShots() == 0);
            if (AutoSelect && _brain.ReadyToSelect && CountShots() == 0)
                Select();
            else if (AutoMove && _brain.CurrentMove == BossMoveId.None)
                Wander(dt);
        }

        void Resolve(MoveSpan span, float dt)
        {
            if (span.Index != _seenIndex)
            {
                _seenIndex = span.Index;
                _shotsFired = 0;
                _hitThisMove = false;
                _chargeDone = false;
                _leapLocked = false;
                _leapX = PlayerX;
                _leapY = PlayerY;
                _leapStartX = BossX;
                _leapStartY = BossY;
                _dashEndX = BossX;
                _dashEndY = BossY;
            }

            BossSkill skill = FinalBossRules.Skill(span.Move);
            float elapsed = span.To;
            Aim(span.Move, skill, elapsed, dt);

            if (span.Move == BossMoveId.LeapSlam && !_leapLocked && elapsed + 0.00001f >= skill.Lock)
            {
                _leapLocked = true;
                _leapStartX = BossX;
                _leapStartY = BossY;
                ClampLeap(PlayerX, PlayerY);
            }
            else if (span.Move == BossMoveId.LeapSlam && !_leapLocked)
            {
                ClampLeap(PlayerX, PlayerY);
            }

            if (span.Move == BossMoveId.WarningCharge && !_chargeDone && span.From < skill.Lock)
                AimDash(skill);

            FireIfDue(span, skill);
            if (span.Move == BossMoveId.WarningCharge)
                Dash(span, skill);
            if (span.Move == BossMoveId.LeapSlam)
                Leap(span, skill);
            if (FinalBossRules.IsMelee(span.Move))
                Melee(span, skill);
            FlyShots(dt);
        }

        void Aim(BossMoveId move, BossSkill skill, float elapsed, float dt)
        {
            int stage = move == BossMoveId.BurstShot ? _shotsFired : 0;
            bool pendingShot = move == BossMoveId.BurstShot
                ? stage < 3
                : skill.Projectiles > 0 && _shotsFired < 1;
            float lockAt = skill.Lock;
            if (move == BossMoveId.BurstShot)
                lockAt = skill.Lock + stage * skill.ShotInterval;

            bool tracking = false;
            if (pendingShot && elapsed + 0.00001f < lockAt)
                tracking = true;
            else if (move == BossMoveId.LeapSlam && !_leapLocked)
                tracking = true;
            else if (move == BossMoveId.WarningCharge && elapsed < skill.Lock)
                tracking = true;
            else if (FinalBossRules.IsMelee(move) && elapsed < skill.Lock)
                tracking = true;

            if (!tracking)
            {
                _aimX = FacingX;
                _aimY = FacingY;
                return;
            }

            float wantX;
            float wantY;
            if (move == BossMoveId.BurstShot)
            {
                float vx;
                float vy;
                WalkVelocity(out vx, out vy);
                float px;
                float py;
                FinalBossRules.PredictPoint(PlayerX, PlayerY, BossX, BossY, vx, vy, out px, out py);
                wantX = px - BossX;
                wantY = py - BossY;
            }
            else
            {
                wantX = PlayerX - BossX;
                wantY = PlayerY - BossY;
            }

            TurnToward(wantX, wantY, FinalBossRules.TurnSpeed(_brain.Phase) * dt);
            _aimX = FacingX;
            _aimY = FacingY;
        }

        void FireIfDue(MoveSpan span, BossSkill skill)
        {
            if (skill.Projectiles <= 0)
                return;
            int cap = span.Move == BossMoveId.BurstShot ? 3 : 1;
            while (_shotsFired < cap)
            {
                float fireAt = skill.Windup + _shotsFired * (span.Move == BossMoveId.BurstShot ? skill.ShotInterval : 0f);
                if (span.From > fireAt + 0.00001f || span.To + 0.00001f < fireAt)
                    break;
                if (_shotsFired == 0)
                    _brain.NotifyRangedFired(span.Move, span.Index);
                if (span.Move == BossMoveId.TripleShot)
                {
                    float baseAng = (float)Math.Atan2(_aimY, _aimX);
                    LastVolleyAngle0 = -15f;
                    LastVolleyAngle1 = 0f;
                    LastVolleyAngle2 = 15f;
                    for (int side = -1; side <= 1; side++)
                    {
                        float ang = baseAng + side * 15f * (float)(Math.PI / 180.0);
                        Spawn(span, (float)Math.Cos(ang), (float)Math.Sin(ang), 0, skill);
                    }

                    LastVolleyCount = 3;
                }
                else
                {
                    Spawn(span, _aimX, _aimY, _shotsFired, skill);
                    LastVolleyCount = 1;
                    LastVolleyAngle0 = 0f;
                    LastVolleyAngle1 = 0f;
                    LastVolleyAngle2 = 0f;
                }

                LastVolleyTime = fireAt;
                LastVolleyMoveIndex = span.Index;
                _shotsFired++;
            }
        }

        void Spawn(MoveSpan span, float dirX, float dirY, int group, BossSkill skill)
        {
            float mag = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
            if (mag < 0.00001f)
            {
                dirX = FacingX;
                dirY = FacingY;
                mag = 1f;
            }

            dirX /= mag;
            dirY /= mag;
            _shots.Add(new BossShot
            {
                X = BossX + dirX * 0.9f,
                Y = BossY + dirY * 0.9f,
                DirX = dirX,
                DirY = dirY,
                Speed = skill.Speed,
                Radius = skill.HitRadius,
                Left = FinalBossRules.ShotRange,
                Group = group,
                MoveIndex = span.Index,
                Move = span.Move,
                Alive = true
            });
            ArrowsFired++;
        }

        void FlyShots(float dt)
        {
            for (int i = 0; i < _shots.Count; i++)
            {
                BossShot shot = _shots[i];
                if (!shot.Alive)
                    continue;
                float step = shot.Speed * dt;
                if (step > shot.Left)
                    step = shot.Left;
                float nx = shot.X + shot.DirX * step;
                float ny = shot.Y + shot.DirY * step;
                if (!PlayerInvulnerable && SegmentHits(shot.X, shot.Y, nx, ny, PlayerX, PlayerY, FinalBossRules.PlayerRadius + shot.Radius))
                {
                    TryHit(shot.Move, shot.Group, shot.MoveIndex);
                    shot.Alive = false;
                }
                else
                {
                    shot.X = nx;
                    shot.Y = ny;
                    shot.Left -= step;
                    if (shot.Left <= 0.0001f || (HasArena && !Inside(nx, ny, 0.2f)))
                        shot.Alive = false;
                }

                _shots[i] = shot;
            }
        }

        void Dash(MoveSpan span, BossSkill skill)
        {
            if (_chargeDone)
                return;
            float from = span.From > skill.Windup ? span.From : skill.Windup;
            float to = span.To < skill.Windup + skill.Active ? span.To : skill.Windup + skill.Active;
            if (to <= from)
                return;
            float slice = to - from;
            float travel = skill.Speed * slice;
            float dx = _dashEndX - BossX;
            float dy = _dashEndY - BossY;
            float remain = (float)Math.Sqrt(dx * dx + dy * dy);
            float x0 = BossX;
            float y0 = BossY;
            if (remain <= travel || remain < 0.0001f)
            {
                BossX = _dashEndX;
                BossY = _dashEndY;
                _chargeDone = true;
                _brain.EndChargeEarly();
            }
            else
            {
                BossX += dx / remain * travel;
                BossY += dy / remain * travel;
            }

            if (!_hitThisMove && !PlayerInvulnerable
                && SegmentHits(x0, y0, BossX, BossY, PlayerX, PlayerY, skill.Radius + FinalBossRules.PlayerRadius))
            {
                _hitThisMove = true;
                TryHit(BossMoveId.WarningCharge, 0, span.Index);
            }
        }

        void AimDash(BossSkill skill)
        {
            float len = skill.Radius > 0f ? 7f : 7f;
            float endX = BossX + _aimX * len;
            float endY = BossY + _aimY * len;
            if (HasArena)
            {
                for (int i = 0; i < 40; i++)
                {
                    if (Inside(endX, endY, 1f))
                        break;
                    endX = BossX + (endX - BossX) * 0.9f;
                    endY = BossY + (endY - BossY) * 0.9f;
                }
            }

            _dashEndX = endX;
            _dashEndY = endY;
        }

        void Leap(MoveSpan span, BossSkill skill)
        {
            float air = span.To - skill.Windup;
            if (air < 0f)
                return;
            if (air < FinalBossRules.LeapAirSeconds)
            {
                float u = air / FinalBossRules.LeapAirSeconds;
                BossX = _leapStartX + (_leapX - _leapStartX) * u;
                BossY = _leapStartY + (_leapY - _leapStartY) * u;
                return;
            }

            BossX = _leapX;
            BossY = _leapY;
            if (_hitThisMove || PlayerInvulnerable)
                return;
            if (span.To < FinalBossRules.LeapLandTime || span.From > FinalBossRules.LeapHitEnd)
                return;
            float dx = PlayerX - _leapX;
            float dy = PlayerY - _leapY;
            float reach = skill.Radius + FinalBossRules.PlayerRadius;
            if (dx * dx + dy * dy <= reach * reach)
            {
                _hitThisMove = true;
                TryHit(BossMoveId.LeapSlam, 0, span.Index);
            }
        }

        void Melee(MoveSpan span, BossSkill skill)
        {
            float hitStart = skill.Windup;
            float hitEnd = skill.Windup + skill.Active;
            if (span.To < hitStart || span.From > hitEnd || _hitThisMove)
                return;
            bool inside = FinalBossRules.InSector(
                BossX, BossY, _aimX, _aimY, PlayerX, PlayerY, skill.Radius, skill.Angle, FinalBossRules.PlayerRadius);
            if (!inside || PlayerInvulnerable)
                return;
            _hitThisMove = true;
            TryHit(span.Move, 0, span.Index);
        }

        void TryHit(BossMoveId move, int group, int moveIndex)
        {
            long key = ((long)moveIndex << 8) + (uint)group;
            for (int i = 0; i < _hitKeys.Count; i++)
            {
                if (_hitKeys[i] == key)
                    return;
            }

            _hitKeys.Add(key);
            float amount = FinalBossRules.Skill(move).Damage * (_brain != null ? _brain.LiveDmgMul : 1f);
            DamageApplications++;
            DamageToPlayer += amount;
            _hits.Add(new BossHitEvent
            {
                Damage = amount,
                Kind = "BOSS_" + move,
                Move = move,
                Group = group
            });
        }

        void Select()
        {
            if (!BodyAndCrownInView)
                return;
            BossMoveId ranged = _brain.Phase == BossPhase.P1 ? _brain.NextP1Ranged : BossMoveId.TripleShot;
            BossMoveId melee = _brain.Phase == BossPhase.P1 ? BossMoveId.ShieldBash : BossMoveId.CrossbowBash;
            BossMoveId heavy = _brain.Phase == BossPhase.P1 ? BossMoveId.WarningCharge : BossMoveId.LeapSlam;
            if (Legal(melee) && _brain.TryStart(melee))
                return;

            bool firstRanged = _brain.Phase == BossPhase.P1 ? !_p1FirstRanged : !_p2FirstRanged;
            if (firstRanged && Legal(ranged) && _brain.TryStart(ranged))
            {
                NoteOpened(ranged);
                return;
            }

            if (_brain.Phase == BossPhase.P1 && _p1FirstRanged && !_p1FirstCharge && Legal(heavy) && _brain.TryStart(heavy))
            {
                NoteOpened(heavy);
                return;
            }

            bool rangedOk = Legal(ranged);
            bool heavyOk = Legal(heavy);
            BossMoveId pick = BossMoveId.None;
            if (rangedOk && heavyOk)
                pick = _rng.Next(100) < (_brain.Phase == BossPhase.P1 ? 60 : 55) ? ranged : heavy;
            else if (rangedOk)
                pick = ranged;
            else if (heavyOk)
                pick = heavy;
            if (pick != BossMoveId.None && _brain.TryStart(pick))
                NoteOpened(pick);
        }

        void NoteOpened(BossMoveId move)
        {
            if (_brain.Phase == BossPhase.P1 && FinalBossRules.IsP1Ranged(move))
                _p1FirstRanged = true;
            if (move == BossMoveId.WarningCharge)
                _p1FirstCharge = true;
            if (move == BossMoveId.TripleShot)
                _p2FirstRanged = true;
        }

        bool Legal(BossMoveId move)
        {
            if (_brain == null || !_brain.CanUse(move) || !BodyAndCrownInView)
                return false;
            if (FinalBossRules.IsP1Ranged(move) && move != _brain.NextP1Ranged)
                return false;
            BossSkill skill = FinalBossRules.Skill(move);
            float dist = Distance;
            if (dist < skill.Min || dist > skill.Max)
                return false;
            if (move == BossMoveId.WarningCharge)
            {
                float dx = _aimX;
                float dy = _aimY;
                if (dx * dx + dy * dy < 0.0001f)
                {
                    dx = PlayerX - BossX;
                    dy = PlayerY - BossY;
                }

                float mag = (float)Math.Sqrt(dx * dx + dy * dy);
                if (mag < 0.0001f)
                    return false;
                return true;
            }

            if (move == BossMoveId.LeapSlam)
                return true;
            return true;
        }

        void Wander(float dt)
        {
            float dist = Distance;
            float speed = FinalBossRules.MoveSpeed(_brain.Phase);
            float dx = PlayerX - BossX;
            float dy = PlayerY - BossY;
            if (dist >= 4.5f && dist <= 7f)
            {
                TurnToward(dx, dy, FinalBossRules.TurnSpeed(_brain.Phase) * dt);
                _aimX = FacingX;
                _aimY = FacingY;
                return;
            }

            float dir = dist > 7f ? 1f : -1f;
            if (dist < 0.001f)
            {
                dx = FacingX;
                dy = FacingY;
                dist = 1f;
            }

            float step = speed * dt;
            float nx = BossX + dir * dx / dist * step;
            float ny = BossY + dir * dy / dist * step;
            if (!HasArena || Inside(nx, ny, FinalBossRules.BodyRadius))
            {
                BossX = nx;
                BossY = ny;
            }

            TurnToward(PlayerX - BossX, PlayerY - BossY, FinalBossRules.TurnSpeed(_brain.Phase) * dt);
            _aimX = FacingX;
            _aimY = FacingY;
        }

        void ClampLeap(float x, float y)
        {
            if (HasArena)
            {
                float minX = ArenaMinX + 2.5f;
                float maxX = ArenaMaxX - 2.5f;
                float minY = ArenaMinY + 2.5f;
                float maxY = ArenaMaxY - 2.5f;
                if (minX > maxX)
                {
                    minX = (ArenaMinX + ArenaMaxX) * 0.5f;
                    maxX = minX;
                }

                if (minY > maxY)
                {
                    minY = (ArenaMinY + ArenaMaxY) * 0.5f;
                    maxY = minY;
                }

                if (x < minX) x = minX;
                if (x > maxX) x = maxX;
                if (y < minY) y = minY;
                if (y > maxY) y = maxY;
            }

            _leapX = x;
            _leapY = y;
        }

        void TurnToward(float tx, float ty, float maxDeg)
        {
            float tl = (float)Math.Sqrt(tx * tx + ty * ty);
            if (tl < 0.00001f)
                return;
            float fa = (float)Math.Atan2(FacingY, FacingX);
            float ta = (float)Math.Atan2(ty, tx);
            float delta = ta - fa;
            float pi = (float)Math.PI;
            while (delta > pi)
                delta -= pi * 2f;
            while (delta < -pi)
                delta += pi * 2f;
            float max = maxDeg * (float)(Math.PI / 180.0);
            if (delta > max)
                delta = max;
            if (delta < -max)
                delta = -max;
            float na = fa + delta;
            FacingX = (float)Math.Cos(na);
            FacingY = (float)Math.Sin(na);
        }

        void WalkVelocity(out float vx, out float vy)
        {
            TrimWalk();
            float dx = 0f;
            float dy = 0f;
            float dt = 0f;
            float now = _brain != null ? _brain.CombatSeconds : 0f;
            for (int i = 0; i < _walk.Count; i++)
            {
                if (now - _walk[i].Time > FinalBossRules.PredictWindow)
                    continue;
                dx += _walk[i].Dx;
                dy += _walk[i].Dy;
                dt += _walk[i].Dt > 0f ? _walk[i].Dt : 0.02f;
            }

            if (dt <= 0.00001f)
            {
                vx = 0f;
                vy = 0f;
                return;
            }

            vx = dx / dt;
            vy = dy / dt;
        }

        void TrimWalk()
        {
            float now = _brain != null ? _brain.CombatSeconds : 0f;
            int i = 0;
            while (i < _walk.Count)
            {
                if (now - _walk[i].Time > FinalBossRules.PredictWindow)
                    _walk.RemoveAt(i);
                else
                    i++;
            }
        }

        int CountShots()
        {
            int n = 0;
            for (int i = 0; i < _shots.Count; i++)
            {
                if (_shots[i].Alive)
                    n++;
            }

            return n;
        }

        bool Inside(float x, float y, float inset)
        {
            return x >= ArenaMinX + inset && x <= ArenaMaxX - inset
                && y >= ArenaMinY + inset && y <= ArenaMaxY - inset;
        }

        static bool SegmentHits(float x0, float y0, float x1, float y1, float cx, float cy, float radius)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float fx = x0 - cx;
            float fy = y0 - cy;
            float a = dx * dx + dy * dy;
            float c = fx * fx + fy * fy - radius * radius;
            if (a < 0.0000001f)
                return c <= 0f;
            float b = 2f * (fx * dx + fy * dy);
            float disc = b * b - 4f * a * c;
            if (disc < 0f)
                return false;
            float s = (float)Math.Sqrt(disc);
            float t1 = (-b - s) / (2f * a);
            float t2 = (-b + s) / (2f * a);
            if (t1 >= 0f && t1 <= 1f)
                return true;
            if (t2 >= 0f && t2 <= 1f)
                return true;
            return c <= 0f;
        }
    }
}
