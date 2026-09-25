using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Demo;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;
using RogueShooter.Combat;
using RogueShooter.Art;

namespace RogueShooter.Ai
{
    /// <summary>
    /// Four-state AI plus Spec v0.5 behaviors (bang, orbs, S2+ lunge, shield).
    /// State machine is unchanged: Patrol → Alert → Chase → Attack → Disengage.
    /// </summary>
    public class MobFourStateAi : MonoBehaviour
    {
        static readonly List<MobFourStateAi> Live = new List<MobFourStateAi>();

        readonly MobAiBrain _brain = new MobAiBrain();
        readonly List<MageOrbProjectile> _orbs = new List<MageOrbProjectile>();
        Transform _player;
        Vector3 _home;
        Vector3 _facing = Vector3.right;
        float _patrolSpeed = 1.35f;
        float _chaseSpeed = 3.6f;
        float _disengageSpeed = 2.4f;
        float _patrolRadius = 1.8f;
        float _patrolT;
        bool _pendingDamage;
        TextMesh _label;
        SpriteRenderer _sr;
        Color _base = new Color(0.86f, 0.28f, 0.24f);
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
        bool _elite;
        bool _playScale;
        MobBangMarker _bang;
        MobShieldVisual _shieldVis;
        bool _shieldRaised;
        bool _shieldBroken;
        float _shieldRaiseAt = -1f;
        bool _lunging;
        float _lungeLeft;
        float _lungeCd;
        Vector3 _lungeDir;
        Vector3 _knockDir;
        float _knockLeft;
        float _knockSpeed;
        float _rootUntil;

        float _hurtUntil;
        int _attackCycle;
        float _lastMovedAt = -999f;
        bool _dead;
        float _corpseUntil = -1f;

        /// <summary>
        /// Walk/idle hold: the mob counts as moving for this long after its last real displacement
        /// while it intends to move, so a slow patrol at high fps (tiny per-frame steps) or a
        /// one-frame wall stall does not flip walk↔idle.
        /// </summary>
        public const float MoveHoldSeconds = 0.15f;
        /// <summary>Corpse stays for the death clip plus this hold, then the object is destroyed.</summary>
        public const float CorpseLingerSeconds = 0.25f;

        public MobAiState State => _brain.State;
        public float DistToPlayer { get; private set; }
        public string DisplayName => name;
        public float LastDealtDamage => _lastDealt;
        public bool IsStaggered => Time.time < _staggerUntil;
        public bool InWindup => _inWindup;
        public bool IsHurting => Time.time < _hurtUntil;
        public StageId Stage => _stage;
        public bool ShieldRaised => _shieldRaised;
        public bool Elite => _elite;
        public Vector3 Facing => _facing;
        public string KindId => CurrentKindId();
        public bool IsKnocking => _knockLeft > 0.001f;
        public bool IsRooted => Time.time < _rootUntil;
        /// <summary>+1 every time a windup (telegraph → hit/orb) starts; the anim view restarts atk/cast on change.</summary>
        public int AttackCycle => _attackCycle;
        /// <summary>Movement intent this frame (patrol/alert/chase/disengage step requested and made recently).</summary>
        public bool IsMoving => !_dead && Time.time - _lastMovedAt <= MoveHoldSeconds;
        public bool IsDead => _dead;
        /// <summary>Time the corpse object is destroyed (−1 while alive).</summary>
        public float CorpseUntil => _corpseUntil;

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
            _elite = elite;
            _home = transform.position;
            _lastKnown = _home;
            _patrolT = UnityEngine.Random.Range(0f, 6.28f);
            float detect = 5.5f;
            float mul = 1.6f;
            float alert = 1.2f;
            var profile = EnemyKindCatalog.ForKind(CurrentKindId());
            float attack = profile.AttackRange > 0.05f ? profile.AttackRange : 0.7f;
            if (data != null)
            {
                if (data.mobDetectRadius > 0f) detect = data.mobDetectRadius;
                if (data.mobDisengageMul > 0f) mul = data.mobDisengageMul;
                if (data.mobAlertSeconds > 0f) alert = data.mobAlertSeconds;
                if (data.mobPatrolRadius > 0f) _patrolRadius = data.mobPatrolRadius;
            }

            // L5: Patrol → Alert radius is a logic distance from L5Rules (melee 8u / ranged 10u),
            // independent of the camera size; replaces the lock-CSV mobDetectRadius (5.5).
            detect = DetectRadiusFor(profile.RangedOrb, attack);

            var stub = GetComponent<StubEnemy>();
            string kind = stub != null ? stub.KindId : "E1";
            if (playScale)
                ApplyPlaySpeed(kind);
            else
                ApplyKindSpeed(kind);

            _brain.Configure(detect, mul, alert, attack);
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
            {
                _base = KindTint(kind, _sr.color);
                _sr.color = _base;
            }

            _pressure = GetComponent<EnemyPressureState>();
            if (_player != null)
                _playerVitals = _player.GetComponent<PlayerVitals>();
            _windupLeft = 0f;
            _cooldownLeft = 0f;
            _inWindup = false;
            _lastDealt = 0f;
            _shieldRaised = false;
            _shieldBroken = false;
            _shieldRaiseAt = -1f;
            _lunging = false;
            _lungeCd = 0f;
            _knockLeft = 0f;
            _rootUntil = 0f;
            _hurtUntil = 0f;
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
            CollisionVolume.Add(gameObject, CollisionLayer.Mob, false, CollisionRules.MobHalfX, CollisionRules.MobHalfY);
            EntityAnimView.Add(gameObject, false);
            Debug.Log($"[MobAI] {name} Patrol home={_home} stage={StageIdUtil.Label(_stage)} kind={kind} " +
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
            _pendingDamage = true;
            _hurtUntil = Time.time + ActionSpecP1.EnemyHurt(CurrentKindId()).Duration;
            if (_player != null)
                _lastKnown = _player.position;
        }

        public void ApplyWeakSpotStagger(float seconds)
        {
            float dur = seconds > 0.01f ? seconds : ChargeShotRules.WeakSpotStaggerSeconds;
            _staggerUntil = Time.time + dur;
            _inWindup = false;
            _windupLeft = 0f;
            _lunging = false;
            SetBang(false);
            NotifyDamaged();
            if (_shieldRaised)
                ShatterShield();
            if (_sr != null)
                _sr.color = new Color(0.92f, 0.92f, 0.88f);
            if (_label != null)
                _label.text = "STAGGER";
            Debug.Log($"[MobAI] {name} weak-spot stagger {dur:0.00}s");
        }

        /// <summary>
        /// Full-charge knockback along shot_away. DRAFT mid from CSV. Elite uses same species.
        /// Weak-spot uses ×1.5 distance and stacks with stagger (TickKnockback still runs).
        /// </summary>
        public void ApplyKnockback(Vector3 shotAway, float distance)
        {
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
            _lunging = false;
            NotifyDamaged();
            Debug.Log("[Knockback] DRAFT_NOT_LOCKED kind=" + CurrentKindId()
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
            float dur = seconds > 0.01f ? seconds : FullChargeKnockback.ShieldRaisedRootSeconds;
            _rootUntil = Time.time + dur;
            _knockLeft = 0f;
            _lunging = false;
            NotifyDamaged();
            if (_label != null)
                _label.text = "ROOT";
            Debug.Log("[Knockback] DRAFT_NOT_LOCKED kind=" + CurrentKindId()
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
            _playerVitals.ApplyHit(dmg, kind);
            return true;
        }

        void OnEnable()
        {
            var stub = GetComponent<StubEnemy>();
            if (stub != null && stub.IsDead)
                _dead = true;
            if (!_dead && !Live.Contains(this))
                Live.Add(this);
            if (stub != null)
            {
                stub.Damaged -= NotifyDamaged;
                stub.Damaged += NotifyDamaged;
                stub.Died -= OnStubDied;
                stub.Died += OnStubDied;
            }
        }

        void OnDisable()
        {
            Live.Remove(this);
            var stub = GetComponent<StubEnemy>();
            if (stub != null)
            {
                stub.Damaged -= NotifyDamaged;
                stub.Died -= OnStubDied;
            }
        }

        /// <summary>
        /// Death: leave Live (arrow / hitscan / strike / screen-cap all iterate Live), drop the mob
        /// collision volume so arrows and bodies pass through, hide the state label + bang, then
        /// destroy the object once the death clip has played.
        /// </summary>
        void OnStubDied(StubEnemy stub)
        {
            if (_dead)
                return;
            _dead = true;
            Live.Remove(this);
            _inWindup = false;
            _lunging = false;
            _knockLeft = 0f;
            SetBang(false);
            if (_label != null)
                _label.gameObject.SetActive(false);
            if (_shieldVis != null)
                _shieldVis.SetRaised(false, _facing);
            var vol = GetComponent<CollisionVolume>();
            if (vol != null)
                vol.enabled = false;
            var box = GetComponent<BoxCollider2D>();
            if (box != null)
                box.enabled = false;
            _corpseUntil = Time.time + ActionSpecP1.EnemyDeath(CurrentKindId()).Duration + CorpseLingerSeconds;
            Debug.Log($"[MobAI] {name} died → collision off, corpse until +{_corpseUntil - Time.time:0.00}s");
        }

        void Update()
        {
            if (RunPause.IsPaused)
                return;

            if (_dead)
            {
                if (_corpseUntil > 0f && Time.time >= _corpseUntil)
                    Destroy(gameObject);
                return;
            }

            var stub = GetComponent<StubEnemy>();
            if (stub != null && stub.IsDead)
            {
                OnStubDied(stub);
                return;
            }

            if (_player == null)
                return;

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
            if (_brain.State != MobAiState.Patrol)
                _facing = FacingToward(delta, _facing);

            TickShield();

            bool atHome = (transform.position - _home).sqrMagnitude <= 0.14f * 0.14f;
            MobAiState prev = _brain.State;
            bool dmg = _pendingDamage;
            _pendingDamage = false;
            _brain.Tick(DistToPlayer, dmg, Time.deltaTime, atHome);
            if (_brain.State != prev)
            {
                Debug.Log($"[MobAI] {name} {prev} → {_brain.State} dist={DistToPlayer:0.00} " +
                          $"detect={_brain.DetectRadius:0.0} disengage={_brain.DisengageRadius:0.0}");
                LockPressureIfEngaged();
                ApplyVisual();
                if (_brain.State == MobAiState.Attack && prev != MobAiState.Attack)
                    BeginAttackCycle();
                if (_brain.State != MobAiState.Attack)
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
                if (_label != null)
                    _label.text = "ROOT";
                if (_brain.State == MobAiState.Attack)
                    TickAttack(Time.deltaTime);
                return;
            }

            if (_lunging)
            {
                TickLunge(Time.deltaTime);
                return;
            }

            if (EnemyCombatRules.CanLunge(CurrentKindId(), _stage)
                && _brain.State == MobAiState.Chase
                && DistToPlayer <= EnemyCombatRules.LungeRangeStub
                && _lungeCd <= 0f)
            {
                BeginLunge(delta);
                return;
            }

            if (_brain.State == MobAiState.Attack)
                TickAttack(Time.deltaTime);

            Move(delta);
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
                if (_shieldVis != null)
                    _shieldVis.SetRaised(true, _facing);
                Debug.Log($"[Shield] {name} raised (DRAFT move×{EnemyCombatRules.ShieldMoveMul:0.00})");
                ApplyVisual();
            }
        }

        void ShatterShield()
        {
            if (!_shieldRaised && _shieldBroken)
                return;
            _shieldRaised = false;
            _shieldBroken = true;
            _shieldRaiseAt = -1f;
            if (_shieldVis != null)
                _shieldVis.SetRaised(false, _facing);
            Debug.Log($"[Shield] {name} shatter");
        }

        void ResetShieldCycle()
        {
            if (!EnemyKindCatalog.ForKind(CurrentKindId()).Shield)
                return;
            _shieldBroken = false;
            _shieldRaised = false;
            _shieldRaiseAt = -1f;
            if (_shieldVis != null)
                _shieldVis.SetRaised(false, _facing);
        }

        void BeginAttackCycle()
        {
            var profile = EnemyKindCatalog.ForKind(CurrentKindId());
            _cooldownLeft = 0f;
            if (profile.RangedOrb && LiveOrbCount() > 0)
            {
                _inWindup = false;
                return;
            }

            TryStartWindup(profile);
        }

        /// <summary>L5 Patrol → Alert radius (logic u). Ranged keeps the legacy "≥ attack + 1" guard.</summary>
        public static float DetectRadiusFor(bool ranged, float attackRange)
        {
            float detect = L5Rules.AggroFor(ranged);
            if (ranged && detect < attackRange + 0.5f)
                detect = attackRange + 1.0f;
            return detect;
        }

        /// <summary>Logic-space unit facing toward <paramref name="delta"/>; keeps previous (normalized) when zero.</summary>
        public static Vector3 FacingToward(Vector3 delta, Vector3 previous)
        {
            delta.z = 0f;
            if (delta.sqrMagnitude > 0.0001f)
                return delta.normalized;
            previous.z = 0f;
            return previous.sqrMagnitude > 0.0001f ? previous.normalized : Vector3.right;
        }

        /// <summary>L5 hard rule: a ranged caster fires only from inside the screen view quad.</summary>
        public static bool RangedMayFire(bool ranged, bool insideViewQuad)
        {
            return !ranged || !L5Rules.RangedFireRequiresInView || insideViewQuad;
        }

        /// <summary>Count of windups / fires refused by the view rule (probe / log).</summary>
        public int BlockedFireCount { get; private set; }

        bool FireBlockedByView(EnemyKindProfile profile)
        {
            if (RangedMayFire(profile.RangedOrb, ViewSpace.InViewQuad(transform.position)))
                return false;
            BlockedFireCount++;
            _inWindup = false;
            SetBang(false);
            _cooldownLeft = Mathf.Max(0.02f, L5Rules.BlockedFireRetrySeconds);
            return true;
        }

        void TryStartWindup(EnemyKindProfile profile)
        {
            if (FireBlockedByView(profile))
                return;
            StartWindup(profile);
        }

        void StartWindup(EnemyKindProfile profile)
        {
            _inWindup = true;
            _windupLeft = profile.WindupSeconds;
            _attackCycle++;
            SetBang(true);
        }

        void TickAttack(float dt)
        {
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
                TryStartWindup(profile);
                if (!_inWindup)
                    return;
            }

            if (!_inWindup)
                return;

            _windupLeft -= dt;
            if (_windupLeft > 0f)
                return;

            if (FireBlockedByView(profile))
                return;
            _inWindup = false;
            SetBang(false);
            if (profile.RangedOrb)
                FireOrbs(profile);
            else
                DealMeleeHit();
            _cooldownLeft = profile.AttackIntervalSeconds;
        }

        void DealMeleeHit()
        {
            float dmg = HitDamage();
            _lastDealt = dmg;
            if (_playerVitals != null)
                _playerVitals.ApplyHit(dmg, CurrentKindId());
            else
                Debug.Log($"[MobAI] {name} hit dmg={dmg:0.#} (no PlayerVitals)");
            Debug.Log("[ActionSpec] OnHitOpen kind=" + CurrentKindId()
                      + " clip=" + ActionSpecP1.EnemyAttack(CurrentKindId()).Root);
        }

        void FireOrbs(EnemyKindProfile profile)
        {
            if (_player == null)
                return;
            Vector3 origin = transform.position;
            Vector3 dir = _player.position - origin;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.right;
            dir.Normalize();
            float walk = _chaseSpeed > 0.01f ? _chaseSpeed : profile.WalkSpeedPlayStub;
            float speed = EnemyCombatRules.OrbSpeedForKind(CurrentKindId(), walk);
            // L5: logic range 12u / flight 1.0s (not camera width × 0.7).
            float maxRange = EnemyCombatRules.OrbMaxRange(speed);
            float dmg = HitDamage();
            int n = EnemyCombatRules.OrbCount(CurrentKindId());
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
                Color col = CurrentKindId() == EnemyKindIds.GrandMage
                    ? new Color(0.25f, 0.95f, 1f)
                    : new Color(0.95f, 0.2f, 0.95f);
                var orb = MageOrbProjectile.SpawnVisual(origin + new Vector3(ox, oy, 0f) * 0.35f, col);
                orb.gameObject.name = "Orb_" + CurrentKindId();
                _orbs.Add(orb);
                orb.Launch(orb.transform.position, new Vector3(ox, oy, 0f), speed, maxRange, dmg, _player, OnOrbDespawn);
            }

            _lastDealt = dmg;
            Debug.Log($"[Orb] {name} fire n={n} speed={speed:0.00} range≤{maxRange:0.00} (orb=player×2, L5 range/flight)");
            Debug.Log("[ActionSpec] OnOrbSpawn kind=" + CurrentKindId()
                      + " clip=" + ActionSpecP1.EnemyAttack(CurrentKindId()).Root);
        }

        void OnOrbDespawn(MageOrbProjectile orb, string reason)
        {
            _orbs.Remove(orb);
            if (this == null)
                return; // caster corpse already destroyed; the orb outlived it.
            Debug.Log($"[Orb] {name} despawn {reason} live={LiveOrbCount()}");
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
            Shift(_knockDir * step);
            _knockLeft -= dt;
            if (_knockLeft < 0f)
                _knockLeft = 0f;
        }

        void BeginLunge(Vector3 toPlayer)
        {
            _lunging = true;
            _lungeLeft = EnemyCombatRules.LungeDistanceStub;
            _lungeDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.right;
            if (_label != null)
                _label.text = "LUNGE";
            Debug.Log($"[Lunge] {name} start dist={DistToPlayer:0.00} DRAFT dmg=[{EnemyCombatRules.LungeDamageMinEasyStub},{EnemyCombatRules.LungeDamageMaxEasyStub}]");
        }

        void TickLunge(float dt)
        {
            float step = EnemyCombatRules.LungeSpeedStub * dt;
            if (step > _lungeLeft)
                step = _lungeLeft;
            Shift(_lungeDir * step);
            _lungeLeft -= step;
            if (DistToPlayer <= EnemyCombatRules.LungeContactRadiusStub)
            {
                float dmg = EnemyPoolDraft.LungeDamageMinEasy
                    + (EnemyPoolDraft.LungeDamageMaxEasy - EnemyPoolDraft.LungeDamageMinEasy) * 0.5f;
                _lastDealt = dmg;
                if (_playerVitals != null)
                    _playerVitals.ApplyHit(dmg, CurrentKindId() + "_thrust");
                Debug.Log($"[Lunge] {name} hit dmg={dmg:0.#} (DRAFT easy mid of [30,40])");
                EndLunge();
                return;
            }

            if (_lungeLeft <= 0f)
                EndLunge();
        }

        void EndLunge()
        {
            _lunging = false;
            _lungeCd = EnemyCombatRules.LungeCooldownStub;
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

        void Move(Vector3 toPlayer)
        {
            float dt = Time.deltaTime;
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
                    ShiftTowards(patrol, _patrolSpeed * mul * dt);
                    break;
                case MobAiState.Alert:
                    ShiftTowards(_lastKnown, _patrolSpeed * 0.75f * mul * dt);
                    break;
                case MobAiState.Chase:
                    if (DistToPlayer > 0.2f)
                        Shift(toPlayer.normalized * (_chaseSpeed * mul * dt));
                    break;
                case MobAiState.Attack:
                    break;
                case MobAiState.Disengage:
                    ShiftTowards(_home, _disengageSpeed * mul * dt);
                    break;
            }
        }

        void Shift(Vector3 delta)
        {
            Vector3 before = transform.position;
            CollisionWorld.TryMove(
                transform,
                CollisionRules.MobHalfX,
                CollisionRules.MobHalfY,
                delta.x, delta.y);
            Vector3 moved = transform.position - before;
            moved.z = 0f;
            if (moved.sqrMagnitude > 1e-10f && !IsKnocking)
                _lastMovedAt = Time.time;
        }

        void ShiftTowards(Vector3 target, float maxDelta)
        {
            Vector3 next = Vector3.MoveTowards(transform.position, target, maxDelta);
            Shift(next - transform.position);
        }

        void EnsureLabel()
        {
            if (_label != null)
                return;
            var go = new GameObject("AiState");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            _label = go.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.characterSize = 0.14f;
            _label.fontSize = 24;
            _label.color = Color.white;
            BuiltinUiFont.Apply(_label);
        }

        void SetBang(bool on)
        {
            if (_bang == null)
                return;
            _bang.SetVisible(on);
            if (on && _label != null)
                _label.text = "!";
        }

        void ApplyVisual()
        {
            if (_label != null)
            {
                if (IsRooted)
                    _label.text = "ROOT";
                else if (_shieldRaised)
                    _label.text = "SHIELD";
                else if (_elite)
                    _label.text = "ELITE " + _brain.State.ToString().ToUpperInvariant();
                else
                    _label.text = _brain.State.ToString().ToUpperInvariant();
            }

            if (_sr == null)
                return;
            if (_shieldRaised)
            {
                _sr.color = new Color(0.40f, 0.70f, 1f);
                return;
            }

            switch (_brain.State)
            {
                case MobAiState.Alert:
                    _sr.color = new Color(0.98f, 0.86f, 0.22f);
                    break;
                case MobAiState.Chase:
                    _sr.color = new Color(1f, 0.18f, 0.12f);
                    break;
                case MobAiState.Attack:
                    _sr.color = new Color(1f, 0.05f, 0.35f);
                    break;
                case MobAiState.Disengage:
                    _sr.color = new Color(0.95f, 0.55f, 0.18f);
                    break;
                default:
                    _sr.color = _base;
                    break;
            }
        }

        static Color KindTint(string kind, Color fallback)
        {
            switch (kind)
            {
                case EnemyKindIds.Dog: return new Color(0.95f, 0.55f, 0.18f);
                case EnemyKindIds.CultMage: return new Color(0.78f, 0.22f, 0.85f);
                case EnemyKindIds.Shield: return new Color(0.35f, 0.55f, 0.92f);
                case EnemyKindIds.GrandMage: return new Color(0.20f, 0.82f, 0.95f);
                default: return fallback;
            }
        }
    }
}
