using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Demo;
using RogueShooter.Player;
using RogueShooter.Maze;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Ai
{
    /// <summary>
    /// Four-state AI plus shield lunge, orbs, and shield raise.
    /// State machine is unchanged: Patrol → Alert → Chase → Attack → Disengage.
    /// Normals do not lunge.
    /// </summary>
    public class MobFourStateAi : MonoBehaviour
    {
        static readonly List<MobFourStateAi> Live = new List<MobFourStateAi>();

        readonly MobAiBrain _brain = new MobAiBrain();
        readonly List<MageOrbProjectile> _orbs = new List<MageOrbProjectile>();
        Transform _player;
        Stage1Maze _maze;
        MazeNode _roomBounds;
        bool _roomActivated;
        public void ActivateRoom() { _roomActivated=true; }
        public bool RoomCombatAllowed => _roomBounds==null || (_roomActivated && _player!=null && _roomBounds.Contains(_player.position.x,_player.position.y,0f));
        Vector3 _meleeDirection=Vector3.right;
        public Vector3 MeleeDirection => _meleeDirection;
        IReadOnlyList<GameObject> _mazeDoors;
        public void BindMaze(Stage1Maze maze, string roomId, IReadOnlyList<GameObject> doors = null)
        {
            _maze = maze; _roomBounds = maze != null ? maze.Find(roomId) : null; _mazeDoors = doors; _roomActivated=false;
            _navigation = _roomBounds != null ? new RoomMobNavigation(_roomBounds) : null;
            MoveTo(transform.position);
        }
        void MoveTo(Vector3 next)
        {
            if (_roomBounds != null)
            {
                const float inset = Stage1Coin.BodyRadius + Stage1Maze.WalkInset;
                next.x = Mathf.Clamp(next.x, _roomBounds.Center.X-_roomBounds.Width*.5f+inset, _roomBounds.Center.X+_roomBounds.Width*.5f-inset);
                next.y = Mathf.Clamp(next.y, _roomBounds.Center.Y-_roomBounds.Height*.5f+inset, _roomBounds.Center.Y+_roomBounds.Height*.5f-inset);
            }
            transform.position = MazeCollision.Slide(_maze,_mazeDoors,transform.position,next,.3f);
        }
        RoomMobNavigation _navigation;
        public string NavigationAction { get; private set; }
        Vector3 _home;
        Vector3 _facing = Vector3.right;
        float _patrolSpeed = 1.35f;
        float _chaseSpeed = 3.6f;
        float _disengageSpeed = 2.4f;
        float _patrolRadius = 1.8f;
        float _patrolT;
        bool _pendingDamage;
        SpriteRenderer _sr;
        EnemyPressureState _pressure;
        PlayerVitals _playerVitals;
        float _windupLeft;
        float _cooldownLeft;
        bool _inWindup;
        float _lastDealt;
        float _staggerUntil;
        bool _wasStaggered;
        StageId _stage = StageId.S1;
        float _atkOverride;
        float _baseAtk;
        bool _pressureSettled;
        bool _elite;
        bool _playScale;
        MobBangMarker _bang;
        MobShieldVisual _shieldVis;
        bool _shieldRaised;
        bool _shieldBroken;
        float _shieldRaiseAt = -1f;
        enum ShieldLungePhase
        {
            None,
            Track,
            Lock,
            Dash,
            Recover
        }

        bool _lunging;
        float _lungeCd;
        float _lungePhaseLeft;
        float _lungeTravel;
        bool _lungeHit;
        ShieldLungePhase _lungePhase;
        Vector3 _lungeDir;
        public bool LungeCommitted => _lunging && _lungePhase != ShieldLungePhase.Recover;
        public bool IsLunging => _lunging;
        public void SetSpawnPosition(Vector3 p){MoveTo(p);_home=transform.position;}
        public string LungePhase { get; private set; }
        public int LungeHits { get; private set; }
        public Vector3 LungeAim => _lungeDir;
        public Vector3 LungePreviewEnd
        {
            get
            {
                Vector3 end = transform.position + _lungeDir * EnemyCombatRules.ShieldLungeDistance;
                if (_roomBounds != null)
                {
                    float inset = Stage1Coin.BodyRadius + Stage1Maze.WalkInset;
                    end.x = Mathf.Clamp(end.x, _roomBounds.Center.X-_roomBounds.Width*.5f+inset, _roomBounds.Center.X+_roomBounds.Width*.5f-inset);
                    end.y = Mathf.Clamp(end.y, _roomBounds.Center.Y-_roomBounds.Height*.5f+inset, _roomBounds.Center.Y+_roomBounds.Height*.5f-inset);
                }
                float wall = MazeCollision.FirstBlockT(_maze, _mazeDoors, transform.position, end, .3f);
                return wall <= 1f ? Vector3.Lerp(transform.position, end, Mathf.Max(0f, wall-.001f)) : end;
            }
        }
        public float WindupProgress => _inWindup ? Mathf.Clamp01(1f - _windupLeft / AttackClipSeconds()) : 0f;
        public float LungeProgress
        {
            get
            {
                switch (_lungePhase)
                {
                    case ShieldLungePhase.Track: return Mathf.Clamp01(1f-_lungePhaseLeft/EnemyCombatRules.ShieldLungeTrackSeconds);
                    case ShieldLungePhase.Lock: return Mathf.Clamp01(1f-_lungePhaseLeft/(EnemyCombatRules.ShieldLungeWarnSeconds-EnemyCombatRules.ShieldLungeTrackSeconds));
                    case ShieldLungePhase.Dash: return Mathf.Clamp01(_lungeTravel/EnemyCombatRules.ShieldLungeDistance);
                    case ShieldLungePhase.Recover: return Mathf.Clamp01(1f-_lungePhaseLeft/EnemyCombatRules.ShieldLungeRecoverSeconds);
                    default: return 0f;
                }
            }
        }
        Vector3 _knockDir;
        float _knockLeft;
        float _knockSpeed;
        float _rootUntil;

        public MobAiState State => _brain.State;
        public bool InMeleeWindup
        {
            get
            {
                if (EnemyKindCatalog.ForKind(CurrentKindId()).RangedOrb)
                    return false;
                return _inWindup && !_lunging;
            }
        }

        /// <summary>Ranged cast windup: the mage raises the staff before the volley.</summary>
        public bool InCastWindup
        {
            get
            {
                if (!EnemyKindCatalog.ForKind(CurrentKindId()).RangedOrb)
                    return false;
                return _inWindup && !_lunging;
            }
        }

        public const float CultKeepOut = 5f;
        public const float CultOrbRange = 12f;

        public float DistToPlayer { get; private set; }
        public string KiteAction { get; private set; }
        public string DisplayName => name;
        public float LastDealtDamage => _lastDealt;
        public bool IsStaggered => Time.time < _staggerUntil;
        public StageId Stage => _stage;
        public bool ShieldRaised => _shieldRaised;
        public bool ShieldBroken => _shieldBroken;
        public int ShieldBreakSerial { get; private set; }
        public int ShieldBlockSerial { get; private set; }
        public float MeleeReleasedAt { get; private set; } = -100f;
        public float CastReleasedAt { get; private set; } = -100f;
        public bool Elite => _elite;
        public Vector3 Facing => _facing;
        public string KindId => CurrentKindId();
        public bool IsKnocking => _knockLeft > 0.001f;
        public bool IsRooted => Time.time < _rootUntil;

        public static IReadOnlyList<MobFourStateAi> All => Live;

        public float PatrolSpeed => _patrolSpeed;
        public float ChaseSpeed => _chaseSpeed;
        Vector3 _lastKnown;

        public void Configure(BalanceLockData data, Transform player)
        {
            Configure(data, player, StageId.S1, false, 0f, false);
        }

        public void Configure(BalanceLockData data, Transform player, StageId stage, bool playScale)
        {
            Configure(data, player, stage, playScale, 0f, false);
        }

        public void Configure(
            BalanceLockData data, Transform player, StageId stage, bool playScale, float atkOverride, bool elite)
        {
            _player = player;
            _stage = stage;
            _playScale = playScale;
            _atkOverride = atkOverride;
            _baseAtk = atkOverride > 0.01f ? atkOverride : EnemyKindCatalog.HitDamageStub(CurrentKindId());
            _pressureSettled = false;
            _elite = elite;
            _home = transform.position;
            _lastKnown = _home;
            _patrolT = UnityEngine.Random.Range(0f, 6.28f);
            float detect = 9.0f;
            float mul = 1.6f;
            float alert = 1.2f;
            var profile = EnemyKindCatalog.ForKind(CurrentKindId());
            float attack = profile.RangedOrb ? profile.AttackRange : EnemyCombatRules.MeleeStartRangeFor(CurrentKindId());
            if (data != null)
            {
                if (data.mobDetectRadius > 0f) detect = data.mobDetectRadius;
                if (data.mobDisengageMul > 0f) mul = data.mobDisengageMul;
                if (data.mobAlertSeconds > 0f) alert = data.mobAlertSeconds;
                if (data.mobPatrolRadius > 0f) _patrolRadius = data.mobPatrolRadius;
            }

            if (profile.RangedOrb && detect < attack + 0.5f)
                detect = attack + 1.0f;

            var stub = GetComponent<StubEnemy>();
            string kind = stub != null ? stub.KindId : "E1";
            if (kind == EnemyKindIds.CultMage)
            {
                attack = CultOrbRange;
                detect = CultOrbRange + 4f;
            }
            if (playScale)
                ApplyPlaySpeed(kind);
            else
                ApplyKindSpeed(kind);

            _brain.Configure(detect, mul, alert, attack);
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
                _sr.color = Color.white;

            _pressure = GetComponent<EnemyPressureState>();
            if (_player != null)
                _playerVitals = _player.GetComponent<PlayerVitals>();
            _windupLeft = 0f;
            _cooldownLeft = 0f;
            _inWindup = false;
            _lastDealt = 0f;
            _shieldRaised = false;
            _shieldBroken = false;
            ShieldBreakSerial = ShieldBlockSerial = 0;
            MeleeReleasedAt = CastReleasedAt = -100f;
            _shieldRaiseAt = -1f;
            ClearLungeMotion();
            _lungeCd = 0f;
            _knockLeft = 0f;
            _rootUntil = 0f;
            _orbs.Clear();
            EnsureLabel();
            _bang = GetComponent<MobBangMarker>();
            if (_bang == null)
                _bang = gameObject.AddComponent<MobBangMarker>();
            _bang.Ensure();
            _bang.SetVisible(false);
            if (profile.Shield)
            {
                _shieldVis = GetComponent<MobShieldVisual>();
                if (_shieldVis == null)
                    _shieldVis = gameObject.AddComponent<MobShieldVisual>();
                _shieldVis.Ensure();
            }

            ApplyVisual();
            RogueShooter.Demo.GameTrace.Log($"[MobAI] {name} Patrol home={_home} stage={StageIdUtil.Label(_stage)} kind={kind} " +
                      $"elite={_elite} detect={detect:0.0} alert={alert:0.00}s attack={attack:0.00} DRAFT");
        }

        public void ApplyKindSpeed(string kindId)
        {
            float v = MoveSpeeds.ForKind(kindId);
            _patrolSpeed = v;
            _chaseSpeed = v;
            _disengageSpeed = v;
        }

        public void ApplyPlaySpeed(string kindId)
        {
            float v = EnemyKindCatalog.WalkSpeed(kindId, false);
            _patrolSpeed = v;
            _chaseSpeed = v;
            _disengageSpeed = v;
        }

        public void ForceChase()
        {
            Debug.LogWarning($"[MobAI] {name} ForceChase ignored — spawn stays Patrol");
        }

        public void NotifyDamaged()
        {
            if (!RoomCombatAllowed) return;
            _pendingDamage = true;
            if (_player != null)
                _lastKnown = _player.position;
        }

        public void ApplyWeakSpotStagger(float seconds)
        {
            bool breakShield = _shieldRaised || LungeCommitted;
            float dur = seconds > 0.01f ? seconds : ChargeShotRules.WeakSpotStaggerSeconds;
            _staggerUntil = Time.time + dur;
            _inWindup = false;
            _windupLeft = 0f;
            ClearLungeMotion();
            SetBang(false);
            NotifyDamaged();
            if (breakShield)
                ShatterShield();
            if (_sr != null)
                _sr.color = Color.white;
            RogueShooter.Demo.GameTrace.Log($"[MobAI] {name} weak-spot stagger {dur:0.00}s");
        }

        /// <summary>
        /// Full-charge knockback along shot_away. DRAFT mid from CSV. Elite uses same species.
        /// Weak-spot uses ×1.5 distance and stacks with stagger (TickKnockback still runs).
        /// </summary>
        public void ApplyKnockback(Vector3 shotAway, float distance)
        {
            if (LungeCommitted) { NotifyDamaged(); return; }
            if (distance < 0.01f)
                return;
            shotAway.z = 0f;
            if (shotAway.sqrMagnitude < 0.0001f)
                shotAway = transform.position - (_player != null ? _player.position : transform.position);
            shotAway.z = 0f;
            if (shotAway.sqrMagnitude < 0.0001f)
                shotAway = _facing.sqrMagnitude > 0.0001f ? -_facing : Vector3.right;
            _knockDir = shotAway.normalized;
            float dur = FullChargeKnockback.SlideSeconds(distance);
            _knockLeft = dur;
            _knockSpeed = dur > 0.001f ? distance / dur : 0f;
            _rootUntil = 0f;
            ClearLungeMotion();
            NotifyDamaged();
            RogueShooter.Demo.GameTrace.Log("[Knockback] DRAFT_NOT_LOCKED kind=" + CurrentKindId()
                      + " dist=" + distance.ToString("0.00")
                      + " t=" + dur.ToString("0.00")
                      + "s return=" + FullChargeKnockback.ReturnSeconds.ToString("0.0")
                      + "s shield=" + (_shieldRaised ? 1 : 0)
                      + " elite=" + (_elite ? 1 : 0)
                      + " (same-species)");
        }

        /// <summary>Shield-raised full-charge body hit: 0 knockback, no-move root.</summary>
        public void ApplyRoot(float seconds)
        {
            if (LungeCommitted) { NotifyDamaged(); return; }
            float dur = seconds > 0.01f ? seconds : FullChargeKnockback.ShieldRaisedRootSeconds;
            _rootUntil = Time.time + dur;
            _knockLeft = 0f;
            ClearLungeMotion();
            NotifyDamaged();
            RogueShooter.Demo.GameTrace.Log("[Knockback] DRAFT_NOT_LOCKED kind=" + CurrentKindId()
                      + " root=" + dur.ToString("0.00") + "s kb=0 shield=1 elite="
                      + (_elite ? 1 : 0) + " (same-species)");
        }

        /// <summary>Shield front −50%; weak-spot unchanged. Returns applied damage.</summary>
        public int ModifyIncomingShot(Vector3 origin, bool weakSpot, int amount, out float staggerSeconds)
        {
            string kind = CurrentKindId();
            staggerSeconds = ChargeShotRules.WeakSpotStaggerSeconds;
            if (_shieldRaised && kind == EnemyKindIds.Shield)
                staggerSeconds = EnemyCombatRules.ShieldWeakSpotStaggerSeconds;
            if (!_shieldRaised)
                return amount < 1 ? 1 : amount;
            bool front = EnemyCombatRules.HitFromFront(
                _facing.x, _facing.y, origin.x, origin.y, transform.position.x, transform.position.y);
            float mul = EnemyCombatRules.IncomingDamageMul(true, front, weakSpot);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(amount * mul));
            if (weakSpot)
                ShatterShield();
            else if (front)
                ShieldBlockSerial++;
            return dmg;
        }

        public bool ForceDealHitForProof()
        {
            if (_playerVitals == null && _player != null)
                _playerVitals = _player.GetComponent<PlayerVitals>();
            string kind = CurrentKindId();
            float dmg = HitDamage();
            _lastDealt = dmg;
            if (_playerVitals == null)
                return false;
            _playerVitals.ApplyHit(dmg, kind,_player.position-transform.position);
            return true;
        }

        void OnEnable()
        {
            if (!Live.Contains(this))
                Live.Add(this);
            var stub = GetComponent<StubEnemy>();
            if (stub != null)
            {
                stub.Damaged -= NotifyDamaged;
                stub.Damaged += NotifyDamaged;
            }
        }

        void OnDisable()
        {
            Live.Remove(this);
            RogueShooter.Audio.JianHaiAudio.StopOwner(this);_audioWindup=null;
            var stub = GetComponent<StubEnemy>();
            if (stub != null)
                stub.Damaged -= NotifyDamaged;
        }

        void Update()
        {
            if (RunPause.IsPaused || _player == null)
                return;
            if (!RoomCombatAllowed) { _inWindup=false; _windupLeft=0f; _pendingDamage=false; SetBang(false); ClearLungeMotion(); return; }
            var body = GetComponent<StubEnemy>();
            if (body != null && body.IsDead)
            {
                SetBang(false);
                return;
            }

            if (_lungeCd > 0f)
                _lungeCd -= Time.deltaTime;

            TickKnockback(Time.deltaTime);

            if (IsStaggered)
            {
                _wasStaggered = true;
                return;
            }

            if (_wasStaggered)
            {
                _wasStaggered = false;
                ApplyVisual();
            }

            if (_pressure == null)
                _pressure = GetComponent<EnemyPressureState>();
            if (_pressure != null && _brain.State == MobAiState.Patrol)
                _pressure.SyncUnengaged();

            Vector3 delta = _player.position - transform.position;
            delta.z = 0f;
            DistToPlayer = delta.magnitude;
            if (DistToPlayer <= _brain.DetectRadius)
                _lastKnown = _player.position;
            if (AimsAtPlayer() && delta.sqrMagnitude > 0.0001f && _brain.State != MobAiState.Patrol)
                _facing = delta.normalized;

            TickShield();
            if (_lunging) { TickLunge(Time.deltaTime); return; }

            bool atHome = (transform.position - _home).sqrMagnitude <= 0.14f * 0.14f;
            MobAiState prev = _brain.State;
            bool dmg = _pendingDamage;
            _pendingDamage = false;
            _brain.Tick(DistToPlayer, dmg, Time.deltaTime, atHome);
            if (_brain.State != prev)
            {
                RogueShooter.Demo.GameTrace.Log($"[MobAI] {name} {prev} → {_brain.State} dist={DistToPlayer:0.00} " +
                          $"detect={_brain.DetectRadius:0.0} disengage={_brain.DisengageRadius:0.0}");
                LockPressureIfEngaged();
                ApplyVisual();
                if (_brain.State == MobAiState.Attack && prev != MobAiState.Attack)
                    BeginAttackCycle();
                if (_brain.State != MobAiState.Attack && (!_inWindup || DistToPlayer > _brain.DisengageRadius))
                {
                    _inWindup = false;
                    _windupLeft = 0f;
                    SetBang(false);
                }

                if (prev == MobAiState.Patrol && _brain.State == MobAiState.Alert)
                    ArmShield();
                if (_brain.State == MobAiState.Patrol || _brain.State == MobAiState.Disengage)
                    ResetShieldCycle();
            }

            if (IsKnocking)
                return;

            if (IsRooted)
            {
                if (_brain.State == MobAiState.Attack || _inWindup)
                    TickAttack(Time.deltaTime);
                return;
            }

            if (_lunging)
            {
                TickLunge(Time.deltaTime);
                return;
            }

            if (ReadyToLunge())
            {
                BeginLunge(delta);
                return;
            }

            if (_brain.State == MobAiState.Attack || _inWindup)
                TickAttack(Time.deltaTime);

            Move(delta, Time.deltaTime);
        }

        public void Simulate(float dt)
        {
            if (RunPause.IsPaused || _player == null || dt <= 0f || !RoomCombatAllowed)
                return;
            Vector3 delta = _player.position - transform.position;
            delta.z = 0f;
            DistToPlayer = delta.magnitude;
            if (DistToPlayer <= _brain.DetectRadius)
                _lastKnown = _player.position;
            if (_lunging)
            {
                TickLunge(dt);
                RefreshDist();
                return;
            }

            _brain.Tick(DistToPlayer, false, dt, false);
            if (ReadyToLunge())
            {
                BeginLunge(delta);
                RefreshDist();
                return;
            }

            Move(delta, dt);
            RefreshDist();
        }

        void ArmShield()
        {
            var p = EnemyKindCatalog.ForKind(CurrentKindId());
            if (!p.Shield || _shieldBroken || _shieldRaised)
                return;
            _shieldRaiseAt = Time.time + EnemyCombatRules.ShieldRaiseDelaySeconds;
        }

        void TickShield()
        {
            var p = EnemyKindCatalog.ForKind(CurrentKindId());
            if (!p.Shield || _shieldBroken || _shieldRaised)
                return;
            if (_shieldRaiseAt > 0f && Time.time >= _shieldRaiseAt)
            {
                _shieldRaised = true;
                RogueShooter.Audio.JianHaiAudio.Emit("jh_shield_raise",this);
                if (_shieldVis != null)
                    _shieldVis.SetRaised(true, _facing);
                RogueShooter.Demo.GameTrace.Log($"[Shield] {name} raised (DRAFT move×{EnemyCombatRules.ShieldMoveMul:0.00})");
                ApplyVisual();
            }
        }

        void ShatterShield()
        {
            if (!_shieldRaised && _shieldBroken)
                return;
            _shieldRaised = false;
            _shieldBroken = true;
            ShieldBreakSerial++;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_shield_break",this);
            _inWindup = false;
            _windupLeft = 0f;
            ClearLungeMotion();
            SetBang(false);
            _shieldRaiseAt = -1f;
            if (_shieldVis != null)
                _shieldVis.SetRaised(false, _facing);
            RogueShooter.Demo.GameTrace.Log($"[Shield] {name} shatter");
        }

        void ResetShieldCycle()
        {
            if (!EnemyKindCatalog.ForKind(CurrentKindId()).Shield || _shieldBroken)
                return;
            _shieldBroken = false;
            _shieldRaised = false;
            _shieldRaiseAt = -1f;
            if (_shieldVis != null)
                _shieldVis.SetRaised(false, _facing);
        }

        void BeginAttackCycle()
        {
            if (_lunging || _inWindup || _cooldownLeft > 0f) return;
            var profile = EnemyKindCatalog.ForKind(CurrentKindId());
            _cooldownLeft = 0f;
            if (profile.RangedOrb && LiveOrbCount() > 0)
            {
                _inWindup = false;
                return;
            }

            _inWindup = true;
            _windupLeft = AttackClipSeconds();
            SetBang(true);
        }

        float AttackClipSeconds()
        {
            string family = "skel";
            if (CurrentKindId() == EnemyKindIds.Dog)
                family = "dog";
            else if (EnemyKindCatalog.ForKind(CurrentKindId()).RangedOrb)
                family = "mage";
            return family == "mage" ? Stage1IsoActor.AttackClipSeconds(family) : EnemyCombatRules.MeleeWindupFor(CurrentKindId());
        }

        void TickAttack(float dt)
        {
            if (_lunging) return;
            if (_playerVitals == null && _player != null)
                _playerVitals = _player.GetComponent<PlayerVitals>();

            var profile = EnemyKindCatalog.ForKind(CurrentKindId());
            if (profile.RangedOrb && LiveOrbCount() > 0)
            {
                _inWindup = false;
                SetBang(false);
                return;
            }

            if (_cooldownLeft > 0f)
            {
                _cooldownLeft -= dt;
                if (_cooldownLeft > 0f)
                    return;
                if (profile.RangedOrb && LiveOrbCount() > 0)
                    return;
                _inWindup = true;
                _windupLeft = AttackClipSeconds();
                SetBang(true);
            }

            if (!_inWindup)
                return;

            _windupLeft -= dt;
            if (_windupLeft > 0f)
                return;

            _inWindup = false;
            SetBang(false);
            if (profile.RangedOrb)
            {
                if (!RunPause.CombatEnded && !RunPause.RunSettled)
                    FireOrbs(profile);
            }
            else
            {
                MeleeReleasedAt = Time.time;
                RogueShooter.Audio.JianHaiAudio.Emit(CurrentKindId()==EnemyKindIds.Dog?"jh_dog_attack":"jh_skel_swing",this);
                if (MeleeCovers(_player.position))
                    DealMeleeHit();
            }
            _cooldownLeft = profile.AttackIntervalSeconds;
        }

        public bool MeleeCovers(Vector3 point)
        {
            string kind=CurrentKindId();
            // Reuse the finite-sector/circle contact helper also used by the boss.
            return RogueShooter.Boss.FinalBossRules.InSector(transform.position,_meleeDirection,point,
                EnemyCombatRules.MeleeStrikeRangeFor(kind),EnemyCombatRules.MeleeStrikeAngleFor(kind),.32f)
                && MazeCollision.FirstBlockT(_maze,_mazeDoors,transform.position,point)>1f;
        }

        void DealMeleeHit()
        {
            float dmg = HitDamage();
            _lastDealt = dmg;
            if (_playerVitals != null)
                _playerVitals.ApplyHit(dmg, CurrentKindId(),_player.position-transform.position);
            else
                RogueShooter.Demo.GameTrace.Log($"[MobAI] {name} hit dmg={dmg:0.#} (no PlayerVitals)");
        }

        public int ActiveOrbCount=>LiveOrbCount();
        public MageOrbVolley LastVolley {get;private set;}
        public int VolleysFired {get;private set;}
        public bool VisibleForOrb()
        {var cam=Camera.main;if(cam==null)return false;var v=cam.WorldToViewportPoint(transform.position+Vector3.up*.8f);return v.z>0&&v.x>=.02f&&v.x<=.98f&&v.y>=.02f&&v.y<=.98f;}

        void FireOrbs(EnemyKindProfile profile)
        {
            if (_player == null || !RoomCombatAllowed || !VisibleForOrb() || LiveOrbCount()>0)
                return;
            if (MazeCollision.FirstBlockT(_maze, _mazeDoors, transform.position, _player.position) <= 1f)
                return;
            Vector3 dir = _player.position - transform.position;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.right;
            dir.Normalize();
            // The orb appears in front of the body, along the shot direction.
            Vector3 origin = transform.position + dir * 0.9f;
            float walk = _chaseSpeed > 0.01f ? _chaseSpeed : profile.WalkSpeedPlayStub;
            float speed = EnemyCombatRules.OrbSpeedForKind(CurrentKindId(), walk);
            Camera cam = Camera.main;
            float ortho = cam != null ? cam.orthographicSize : EnemyCombatRules.PlayOrthoSize;
            float aspect = cam != null ? CameraViewMath.ResolveAspect(cam) : EnemyCombatRules.DefaultAspect;
            float maxRange = CurrentKindId()==EnemyKindIds.CultMage ? CultOrbRange : EnemyCombatRules.OrbMaxRange(ortho, aspect);
            float dmg = HitDamage();
            int n = EnemyCombatRules.OrbCount(CurrentKindId(),_stage);
            LastVolley=new MageOrbVolley();VolleysFired++;
            CastReleasedAt = Time.time;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_mage_orb_release",this);
            for (int i = 0; i < n; i++)
            {
                float deg = 0f;
                if (n == 3)
                {
                    if (i == 0) deg = -EnemyCombatRules.GrandOrbSpreadDegrees;
                    else if (i == 2) deg = EnemyCombatRules.GrandOrbSpreadDegrees;
                }

                float ox, oy;
                EnemyCombatRules.RotateDeg(dir.x, dir.y, deg, out ox, out oy);
                Vector3 shot = new Vector3(ox, oy, 0f);
                var go = new GameObject("Orb_" + CurrentKindId());
                go.transform.position = origin;
                SpriteRenderer orbSprite = go.AddComponent<SpriteRenderer>();
                orbSprite.sortingOrder = 40;
                Sprite fly = Stage1IsoArt.MageOrbSprite(0);
                if (fly != null)
                {
                    orbSprite.sprite = fly;
                    float wide = fly.bounds.size.x;
                    if (wide > 0.01f && wide < 0.8f)
                    {
                        float fit = 0.8f / wide;
                        go.transform.localScale = new Vector3(fit, fit, 1f);
                    }
                }
                var orb = go.AddComponent<MageOrbProjectile>();
                _orbs.Add(orb);
                orb.BindMaze(_maze, _mazeDoors);
                orb.Launch(go.transform.position, new Vector3(ox, oy, 0f), speed, maxRange, dmg, _player, OnOrbDespawn, LastVolley);
            }

            _lastDealt = dmg;
            RogueShooter.Demo.GameTrace.Log($"[Orb] {name} fire n={n} speed={speed:0.00} range≤{maxRange:0.00} stage={_stage} sharedHit=true");
        }

        void OnOrbDespawn(MageOrbProjectile orb, string reason)
        {
            _orbs.Remove(orb);
            if(this!=null)RogueShooter.Demo.GameTrace.Log($"[Orb] {name} despawn {reason} live={LiveOrbCount()}");
        }

        int LiveOrbCount()
        {
            int n = 0;
            for (int i = _orbs.Count - 1; i >= 0; i--)
            {
                if (_orbs[i] == null || !_orbs[i].Alive)
                    _orbs.RemoveAt(i);
                else
                    n++;
            }

            return n;
        }

        void TickKnockback(float dt)
        {
            if (_knockLeft <= 0f)
                return;
            float step = _knockSpeed * dt;
            float max = _knockSpeed * _knockLeft;
            if (step > max)
                step = max;
            MoveTo(transform.position + _knockDir * step);
            _knockLeft -= dt;
            if (_knockLeft < 0f)
                _knockLeft = 0f;
        }

        bool AimsAtPlayer()
        {
            return !InMeleeWindup && Time.time-MeleeReleasedAt >= EnemyCombatRules.MeleeRecoveryFor(CurrentKindId()) && _lungePhase != ShieldLungePhase.Lock
                && _lungePhase != ShieldLungePhase.Dash
                && _lungePhase != ShieldLungePhase.Recover;
        }

        bool ReadyToLunge()
        {
            if (_lunging || _lungeCd > 0f || _shieldBroken)
                return false;
            if (!EnemyCombatRules.CanLunge(CurrentKindId(), _stage))
                return false;
            if (_brain.State != MobAiState.Alert && _brain.State != MobAiState.Chase)
                return false;
            if (DistToPlayer <= _brain.AttackRange)
                return false;
            return DistToPlayer <= EnemyCombatRules.ShieldLungeWarnRange;
        }

        void AimLunge(Vector3 toPlayer)
        {
            toPlayer.z = 0f;
            _lungeDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.right;
            _facing = _lungeDir;
        }

        void BeginLunge(Vector3 toPlayer)
        {
            _inWindup=false;_windupLeft=0;SetBang(false);
            _rootUntil=0;_knockLeft=0;
            _lunging = true;
            _lungeHit = false;
            _lungeTravel = 0f;
            _lungePhase = ShieldLungePhase.Track;
            _lungePhaseLeft = EnemyCombatRules.ShieldLungeTrackSeconds;
            LungePhase = "跟踪";
            RogueShooter.Audio.JianHaiAudio.Emit("jh_shield_dash_warn",this);
            AimLunge(toPlayer);
            RogueShooter.Demo.GameTrace.Log("[ShieldLunge] warn track=" + EnemyCombatRules.ShieldLungeTrackSeconds.ToString("0.00")
                      + " lock=" + (EnemyCombatRules.ShieldLungeWarnSeconds - EnemyCombatRules.ShieldLungeTrackSeconds).ToString("0.00"));
        }

        void TickLunge(float dt)
        {
            if (dt < 0f)
                dt = 0f;
            if (_lungePhase == ShieldLungePhase.Track)
            {
                Vector3 to = _player != null ? _player.position - transform.position : _lungeDir;
                AimLunge(to);
                _lungePhaseLeft -= dt;
                if (_lungePhaseLeft > 0f)
                    return;
                _lungePhase = ShieldLungePhase.Lock;
                _lungePhaseLeft = EnemyCombatRules.ShieldLungeWarnSeconds - EnemyCombatRules.ShieldLungeTrackSeconds;
                LungePhase = "锁定";
                RogueShooter.Audio.JianHaiAudio.Emit("jh_shield_dash_lock",this);
                return;
            }

            if (_lungePhase == ShieldLungePhase.Lock)
            {
                _lungePhaseLeft -= dt;
                if (_lungePhaseLeft > 0f)
                    return;
                _lungePhase = ShieldLungePhase.Dash;
                _lungeTravel = 0f;
                LungePhase = "冲";
                RogueShooter.Audio.JianHaiAudio.StopThreat(this);
                RogueShooter.Audio.JianHaiAudio.Emit("jh_shield_dash",this);
                return;
            }

            if (_lungePhase == ShieldLungePhase.Dash)
            {
                float remain = EnemyCombatRules.ShieldLungeDistance - _lungeTravel;
                float step = EnemyCombatRules.ShieldLungeSpeed * dt;
                if (step > remain)
                    step = remain;
                if (step < 0f)
                    step = 0f;
                Vector3 oldPosition=transform.position;
                MoveTo(oldPosition + _lungeDir * step);
                float travelled=Vector3.Distance(oldPosition,transform.position);
                _lungeTravel += travelled;
                bool blocked=travelled < step-.0001f;
                RefreshDist();
                Vector3 sweep=transform.position-oldPosition;
                Vector3 toBody=_player.position-oldPosition;toBody.z=0f;
                float t=sweep.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(toBody,sweep)/sweep.sqrMagnitude):0f;
                float sweptDistance=Vector3.Distance(_player.position,oldPosition+sweep*t);
                if (!_lungeHit && sweptDistance <= EnemyCombatRules.MeleeHitRadius && MazeCollision.FirstBlockT(_maze,_mazeDoors,transform.position,_player.position)>1f)
                {
                    _lungeHit = true;
                    LungeHits++;
                    float dmg = HitDamage();
                    _lastDealt = dmg;
                    if (_playerVitals != null)
                        _playerVitals.ApplyHit(dmg, CurrentKindId(),_player.position-transform.position);
                    RogueShooter.Demo.GameTrace.Log("[ShieldLunge] hit dmg=" + dmg.ToString("0.#") + " hits=" + LungeHits);
                }

                if (blocked || _lungeTravel >= EnemyCombatRules.ShieldLungeDistance - 0.0001f)
                {
                    _lungePhase = ShieldLungePhase.Recover;
                    _lungePhaseLeft = EnemyCombatRules.ShieldLungeRecoverSeconds;
                    LungePhase = "恢复";
                }

                return;
            }

            if (_lungePhase == ShieldLungePhase.Recover)
            {
                _lungePhaseLeft -= dt;
                if (_lungePhaseLeft <= 0f)
                    EndLunge();
            }
        }

        void RefreshDist()
        {
            if (_player == null)
                return;
            Vector3 left = _player.position - transform.position;
            left.z = 0f;
            DistToPlayer = left.magnitude;
        }

        void ClearLungeMotion()
        {
            RogueShooter.Audio.JianHaiAudio.Stop("jh_shield_dash_warn",this);
            RogueShooter.Audio.JianHaiAudio.Stop("jh_shield_dash_lock",this);
            _lunging = false;
            _lungePhase = ShieldLungePhase.None;
            _lungePhaseLeft = 0f;
            _lungeTravel = 0f;
            LungePhase = "";
        }

        void EndLunge()
        {
            ClearLungeMotion();
            _lungeCd = 0f;
            ApplyVisual();
        }

        float HitDamage()
        {
            if (_atkOverride > 0.01f)
                return _atkOverride;
            return EnemyKindCatalog.HitDamageStub(CurrentKindId());
        }

        string CurrentKindId()
        {
            var stub = GetComponent<StubEnemy>();
            return stub != null ? stub.KindId : "E1";
        }

        void LockPressureIfEngaged()
        {
            if (_pressure == null)
                return;
            if (_brain.State == MobAiState.Alert || _brain.State == MobAiState.Chase || _brain.State == MobAiState.Attack)
                _pressure.LockEngage();
        }

        int _kiteSide = 1;

        bool IsCultMage()
        {
            return CurrentKindId() == EnemyKindIds.CultMage;
        }

        void MoveCultMage(Vector3 toPlayer, float step)
        {
            Vector3 away = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.right;
            Vector3 dir;
            if (DistToPlayer < CultKeepOut)
            {
                KiteAction = "拉开";
                dir = -away;
            }
            else if (DistToPlayer > CultOrbRange)
            {
                KiteAction = "靠近";
                NavigateTo(_player.position,step);
                return;
            }
            else
            {
                KiteAction = "游走";
                dir = new Vector3(-away.y, away.x, 0f) * _kiteSide;
            }

            // Look ahead before walking into a floor cut or wall. Choose a legal
            // tangent when retreating is blocked, and reverse circling deliberately.
            if (_navigation != null)
            {
                dir=_navigation.KiteDirection(transform.position,dir,away,DistToPlayer<CultKeepOut);
                if(DistToPlayer>=CultKeepOut && DistToPlayer<=CultOrbRange && dir.sqrMagnitude>.001f)
                    _kiteSide=Vector3.Dot(dir,new Vector3(-away.y,away.x,0))>=0?1:-1;
            }
            Vector3 before=transform.position;
            MoveTo(before+dir*step);
            NavigationAction=(transform.position-before).sqrMagnitude>.000001f?"合法游走":"停步";
        }

        void NavigateTo(Vector3 target,float step)
        {
            Vector3 next=_navigation!=null?_navigation.NextStep(transform.position,target,step):Vector3.MoveTowards(transform.position,target,step);
            Vector3 before=transform.position;MoveTo(next);
            NavigationAction=(transform.position-before).sqrMagnitude>.000001f?(_navigation!=null&&_navigation.Detouring?"绕行":"行走"):"停步";
        }

        void Move(Vector3 toPlayer, float dt)
        {
            if (InMeleeWindup || Time.time-MeleeReleasedAt < EnemyCombatRules.MeleeRecoveryFor(CurrentKindId())) return;
            float mul = 1f;
            if (_shieldRaised)
            {
                float shielded = EnemyKindCatalog.WalkSpeed(CurrentKindId(), true);
                float baseWalk = _chaseSpeed > 0.01f ? _chaseSpeed : EnemyKindCatalog.WalkSpeed(CurrentKindId(), false);
                mul = baseWalk > 0.01f ? shielded / baseWalk : EnemyCombatRules.ShieldMoveMul;
            }
            switch (_brain.State)
            {
                case MobAiState.Patrol:
                    _patrolT += dt * 0.85f;
                    Vector3 patrol = _home + new Vector3(Mathf.Cos(_patrolT), Mathf.Sin(_patrolT), 0f) * _patrolRadius;
                    NavigateTo(patrol, _patrolSpeed * mul * dt);
                    break;
                case MobAiState.Alert:
                    if (IsCultMage())
                        MoveCultMage(toPlayer, _chaseSpeed * mul * dt);
                    else
                        NavigateTo(_lastKnown, _patrolSpeed * 0.75f * mul * dt);
                    break;
                case MobAiState.Chase:
                    if (IsCultMage())
                        MoveCultMage(toPlayer, _chaseSpeed * mul * dt);
                    else if (DistToPlayer > 0.2f)
                        NavigateTo(_player.position, _chaseSpeed * mul * dt);
                    break;
                case MobAiState.Attack:
                    if (_inWindup)
                        break;
                    if (IsCultMage())
                        MoveCultMage(toPlayer, _chaseSpeed * mul * dt);
                    else if (DistToPlayer > 0.2f)
                        NavigateTo(_player.position, _chaseSpeed * mul * dt);
                    break;
                case MobAiState.Disengage:
                    NavigateTo(_home, _disengageSpeed * mul * dt);
                    break;
            }
        }

        void EnsureLabel()
        {
        }

        public float Attack => HitDamage();

        /// <summary>Once, at engage. Same locked mul as HP.</summary>
        public void SettlePressure(float mul)
        {
            if (_pressureSettled)
                return;
            _pressureSettled = true;
            float m = mul > 0.01f ? mul : 1f;
            float baseAtk = _baseAtk > 0.01f ? _baseAtk : HitDamage();
            _baseAtk = baseAtk;
            _atkOverride = baseAtk * m;
        }

        string _audioWindup;
        void SetBang(bool on)
        {
            if(on && _audioWindup==null)
            {
                if (_player != null && !EnemyKindCatalog.ForKind(CurrentKindId()).RangedOrb) { var d=_player.position-transform.position; d.z=0; if(d.sqrMagnitude>.0001f) _meleeDirection=d.normalized; _facing=_meleeDirection; }
                _audioWindup=EnemyKindCatalog.ForKind(CurrentKindId()).RangedOrb?"jh_mage_cast":CurrentKindId()==EnemyKindIds.Dog?"jh_dog_windup":"jh_skel_windup";
                RogueShooter.Audio.JianHaiAudio.Emit(_audioWindup,this);
            }
            else if(!on && _audioWindup!=null){RogueShooter.Audio.JianHaiAudio.Stop(_audioWindup,this);_audioWindup=null;}
            if (_bang == null)
                return;
            // Melee uses a sector outline; ranged casts retain their original !.
            _bang.SetVisible(on && EnemyKindCatalog.ForKind(CurrentKindId()).RangedOrb);
        }

        void ApplyVisual()
        {
            if (_sr != null)
                _sr.color = Color.white;
        }

    }
}
