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
            int frame = FrameFor(clip, _state);
            if (_state != _clipState)
            {
                _clipState = _state;
                _clipStart = Time.time;
                frame = FrameFor(clip, _state);
            }

            BindClip(_state, frame, _dir);
            if (_sr != null)
            {
                bool directional = _bound != null
                    && (_bound.IndexOf("_e_", System.StringComparison.Ordinal) >= 0
                        || _bound.IndexOf("_w_", System.StringComparison.Ordinal) >= 0
                        || _bound.IndexOf("_n_", System.StringComparison.Ordinal) >= 0
                        || _bound.IndexOf("_s_", System.StringComparison.Ordinal) >= 0);
                _sr.flipX = !directional && _facing < 0f;
            }
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
            else if (delta.sqrMagnitude > 0.0004f)
                _state = EntityAnimState.Walk;
            else
                _state = EntityAnimState.Idle;
        }

        void TickEnemy(Vector3 delta, ref Vector3 face)
        {
            var ai = GetComponent<MobFourStateAi>();
            var stub = GetComponent<StubEnemy>();
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
                _state = delta.sqrMagnitude > 0.0004f ? EntityAnimState.Walk : EntityAnimState.Idle;
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
                    _state = EntityAnimState.Attack;
                    break;
                case MobAiState.Disengage:
                    _state = EntityAnimState.Walk;
                    break;
                default:
                    _state = delta.sqrMagnitude > 0.0004f ? EntityAnimState.Walk : EntityAnimState.Idle;
                    break;
            }
        }

        int FrameFor(ActionClipDef clip, EntityAnimState state)
        {
            if (player && state == EntityAnimState.Charge)
            {
                var charge = GetComponent<PlayerCharge>();
                float held = charge != null ? charge.HeldSeconds : 0f;
                return ActionSpecP1.ChargePoseFrame(held);
            }

            return clip.FrameAt(Time.time - _clipStart);
        }

        void BindClip(EntityAnimState state, int frame, string dir)
        {
            ActionClipDef clip = player
                ? EntityAnimCatalog.PlayerClip(state)
                : EntityAnimCatalog.EnemyClip(state, kindId);
            string fallback = player
                ? EntityAnimCatalog.PlayerIdle
                : EntityAnimCatalog.ResolveEnemyIdle(kindId);
            string art = JianHaiSprites.ResolveClipArt(clip.Root, dir, frame, fallback);
            if (_sr == null || art == _bound)
                return;
            _bound = art;
            JianHaiSprites.Bind(_sr, art);
        }

        void Ensure()
        {
            if (_sr == null)
                _sr = GetComponent<SpriteRenderer>();
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
