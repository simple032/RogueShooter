using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Combat;
using RogueShooter.Demo;
using RogueShooter.Player;
using RogueShooter.Spawning;

namespace RogueShooter.Art
{
    /// <summary>
    /// ACTION_SPEC_P1 clip player. Samples 12fps-named frames when PNGs exist;
    /// otherwise binds idle. Hurt does not interrupt roll. Death holds last frame.
    /// </summary>
    public class EntityAnimView : MonoBehaviour
    {
        [SerializeField] bool player = true;
        [SerializeField] string kindId = EnemyKindIds.Normal;

        SpriteRenderer _sr;
        Vector3 _last;
        EntityAnimState _state = EntityAnimState.Idle;
        EntityAnimState _clipState = EntityAnimState.Idle;
        float _clipStart;
        float _facing = 1f;
        string _bound;
        string _dir = "s";
        string _grip;
        int _attackCycleSeen = -1;
        int _naturalOrder;
        int _sortingOverride;
        float _movingUntil;

        /// <summary>Non-AI fallback: counts as walking above this speed (u/s), held MoveHoldSeconds.</summary>
        public const float WalkSpeedThreshold = 0.3f;

        /// <summary>
        /// Sorting-order override (0 = none). Re-applied after every frame bind, because
        /// JianHaiSprites.Bind resets the order from the art id. Stage1 uses it to lift the player above
        /// a still-covered room's mask while it stands in that doorway.
        /// </summary>
        public int SortingOverride
        {
            get => _sortingOverride;
            set => _sortingOverride = value;
        }

        /// <summary>Clip restarts caused by a new attack cycle (tests).</summary>
        public int AttackRestarts { get; private set; }
        /// <summary>Time the current clip started (tests).</summary>
        public float ClipStart => _clipStart;

        public EntityAnimState State => _state;
        public float Facing => _facing;
        public string KindId => kindId;

        public void Configure(bool isPlayer)
        {
            Configure(isPlayer, EnemyKindIds.Normal);
        }

        public void Configure(bool isPlayer, string enemyKindId)
        {
            player = isPlayer;
            kindId = string.IsNullOrEmpty(enemyKindId) ? EnemyKindIds.Normal : enemyKindId;
            Ensure();
            _clipStart = Time.time;
            BindClip(player ? EntityAnimState.Idle : EntityAnimState.Idle, 0, "s");
        }

        public void SetState(EntityAnimState state)
        {
            _state = state;
        }

        void Awake()
        {
            Ensure();
            _last = transform.position;
            _clipStart = Time.time;
        }

        void LateUpdate()
        {
            Ensure();
            Vector3 p = transform.position;
            Vector3 d = p - _last;
            d.z = 0f;
            _last = p;
            if (d.x > 0.002f)
                _facing = 1f;
            else if (d.x < -0.002f)
                _facing = -1f;

            Vector3 face = new Vector3(_facing, 0f, 0f);
            if (player)
                TickPlayer(d, ref face);
            else
                TickEnemy(d, ref face);

            _dir = ActionSpecP1.Cardinal(face.x, face.y);
            ActionClipDef clip = player
                ? EntityAnimCatalog.PlayerClip(_state)
                : EntityAnimCatalog.EnemyClip(_state, kindId);
            bool restart = _state != _clipState;
            // Attack/cast: replay from frame 0 at the start of EVERY attack cycle (windup), not only
            // on the state change — otherwise the one-shot clip holds its last frame (mage cast_07)
            // while the AI keeps firing every interval.
            int cycle = AttackCycleNow();
            if (cycle != _attackCycleSeen)
            {
                if (!restart && IsAttackState(_state) && _attackCycleSeen >= 0)
                {
                    restart = true;
                    AttackRestarts++;
                }
                _attackCycleSeen = cycle;
            }

            if (restart)
            {
                _clipState = _state;
                _clipStart = Time.time;
            }

            int frame = FrameFor(clip, _state);
            BindClip(_state, frame, _dir);
            if (_sr != null)
            {
                int want = _sortingOverride != 0 ? _sortingOverride : _naturalOrder;
                if (_sr.sortingOrder != want)
                    _sr.sortingOrder = want;
                // Only skip flip when east/west frames exist. walk_s (and missing n/e/w)
                // falls back to south art — ACTION 缺向先镜像.
                bool bakedSide = _bound != null
                    && (_bound.IndexOf("_e_", System.StringComparison.Ordinal) >= 0
                        || _bound.IndexOf("_w_", System.StringComparison.Ordinal) >= 0);
                _sr.flipX = !bakedSide && _facing < 0f;
            }
        }

        static bool IsAttackState(EntityAnimState s)
        {
            return s == EntityAnimState.Attack || s == EntityAnimState.Cast;
        }

        int AttackCycleNow()
        {
            if (player)
                return 0;
            var ai = GetComponent<MobFourStateAi>();
            return ai != null ? ai.AttackCycle : 0;
        }

        /// <summary>Velocity-based moving test with a short hold (no single-frame displacement threshold).</summary>
        bool MovingByVelocity(Vector3 delta)
        {
            float dt = Time.deltaTime;
            if (dt > 0.00001f && delta.magnitude / dt >= WalkSpeedThreshold)
                _movingUntil = Time.time + MobFourStateAi.MoveHoldSeconds;
            return Time.time <= _movingUntil;
        }

        void TickPlayer(Vector3 delta, ref Vector3 face)
        {
            var dodge = GetComponent<PlayerDodge>();
            var charge = GetComponent<PlayerCharge>();
            var motor = GetComponent<PlayerMotor2D>();
            var vitals = GetComponent<PlayerVitals>();
            if (motor != null && motor.LastFacing.sqrMagnitude > 0.0001f)
                face = motor.LastFacing;
            if (dodge != null && dodge.IsRolling)
                face = dodge.RollDir;
            else if (charge != null && (charge.IsCharging || charge.IsFiring))
                face = charge.AimDirectionPublic();

            if (vitals != null && vitals.IsDead)
                _state = EntityAnimState.Death;
            else if (dodge != null && dodge.IsRolling)
                _state = EntityAnimState.Dodge;
            else if (vitals != null && vitals.IsHurting)
                _state = EntityAnimState.Hurt;
            else if (charge != null && charge.IsFiring)
                _state = EntityAnimState.Attack;
            else if (charge != null && charge.IsCharging)
                _state = EntityAnimState.Charge;
            else if (motor != null && motor.HasMoveInput)
                _state = EntityAnimState.Walk;
            else if (MovingByVelocity(delta))
                _state = EntityAnimState.Walk;
            else
                _state = EntityAnimState.Idle;
        }

        void TickEnemy(Vector3 delta, ref Vector3 face)
        {
            var ai = GetComponent<MobFourStateAi>();
            var stub = GetComponent<StubEnemy>();
            // StubEnemy owns the kind (MobFourStateAi.Configure re-adds this view with the default kind).
            if (stub != null && !string.IsNullOrEmpty(stub.KindId) && stub.KindId != kindId)
                kindId = stub.KindId;
            if (ai != null && ai.Facing.sqrMagnitude > 0.0001f)
                face = ai.Facing;
            if (stub != null && stub.IsDead)
            {
                _state = EntityAnimState.Death;
                return;
            }

            if (ai != null && ai.IsHurting)
            {
                _state = EntityAnimState.Hurt;
                return;
            }

            if (ai == null)
            {
                _state = MovingByVelocity(delta) ? EntityAnimState.Walk : EntityAnimState.Idle;
                return;
            }

            switch (ai.State)
            {
                case MobAiState.Alert:
                    _state = EntityAnimState.Alert;
                    break;
                case MobAiState.Chase:
                    _state = EntityAnimState.Chase;
                    break;
                case MobAiState.Attack:
                    // Mage attacks are casts (jh_enemy_mage_cast); melee/dog use atk.
                    _state = ActionSpecP1.IsMageKind(kindId) ? EntityAnimState.Cast : EntityAnimState.Attack;
                    break;
                case MobAiState.Disengage:
                    _state = EntityAnimState.Walk;
                    break;
                default:
                    // Patrol: movement intent from the AI (held MoveHoldSeconds), not per-frame displacement.
                    _state = ai.IsMoving ? EntityAnimState.Walk : EntityAnimState.Idle;
                    break;
            }
        }

        int FrameFor(ActionClipDef clip, EntityAnimState state)
        {
            _grip = null;
            if (player && state == EntityAnimState.Charge)
            {
                var charge = GetComponent<PlayerCharge>();
                float held = charge != null ? charge.HeldSeconds : 0f;
                ChargeProfile prof = charge != null ? charge.Profile : ChargeProfile.Base;
                // Stage 1→2 grip slot: grip_1to2_01/02 when delivered, otherwise skipped (warn once).
                _grip = EntityAnimCatalog.GripArt(held, prof);
                return ActionSpecP1.ChargePoseFrame(held, prof);
            }

            return clip.FrameAt(Time.time - _clipStart);
        }

        /// <summary>Last bound art id (tests / debug).</summary>
        public string BoundArt => _bound;

        void BindClip(EntityAnimState state, int frame, string dir)
        {
            ActionClipDef clip = player
                ? EntityAnimCatalog.PlayerClip(state)
                : EntityAnimCatalog.EnemyClip(state, kindId);
            string art = player && state == EntityAnimState.Charge && _grip != null
                ? _grip
                : EntityAnimCatalog.ResolveFrame(clip.Root, dir, frame, player, kindId);
            // null = nothing on disk: keep the current sprite rather than a generated red block.
            if (_sr == null || string.IsNullOrEmpty(art) || art == _bound)
                return;
            _bound = art;
            JianHaiSprites.Bind(_sr, art);
            _naturalOrder = _sr.sortingOrder;
        }

        void Ensure()
        {
            if (_sr == null)
            {
                _sr = GetComponent<SpriteRenderer>();
                if (_sr != null)
                    _naturalOrder = _sr.sortingOrder;
            }
        }

        public static EntityAnimView Add(GameObject go, bool isPlayer)
        {
            return Add(go, isPlayer, EnemyKindIds.Normal);
        }

        public static EntityAnimView Add(GameObject go, bool isPlayer, string enemyKindId)
        {
            var view = go.GetComponent<EntityAnimView>();
            if (view == null)
                view = go.AddComponent<EntityAnimView>();
            view.Configure(isPlayer, enemyKindId);
            return view;
        }
    }
}
