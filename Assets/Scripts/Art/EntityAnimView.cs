using UnityEngine;
using RogueShooter.Combat;

namespace RogueShooter.Art
{
    /// <summary>
    /// Faces movement / aim and binds idle art. Walk/dodge/atk clips are not in
    /// repo yet — Resolve* falls back to idle. See EntityAnimCatalog.GapNote().
    /// </summary>
    public class EntityAnimView : MonoBehaviour
    {
        [SerializeField] bool player = true;

        SpriteRenderer _sr;
        Vector3 _last;
        EntityAnimState _state = EntityAnimState.Idle;
        float _facing = 1f;
        string _bound;

        public EntityAnimState State => _state;
        public float Facing => _facing;

        public void Configure(bool isPlayer)
        {
            player = isPlayer;
            Ensure();
            Bind(player ? EntityAnimCatalog.ResolvePlayer(EntityAnimState.Idle)
                : EntityAnimCatalog.ResolveEnemy(EntityAnimState.Idle));
        }

        public void SetState(EntityAnimState state)
        {
            _state = state;
        }

        void Awake()
        {
            Ensure();
            _last = transform.position;
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

            if (player)
            {
                var dodge = GetComponent<RogueShooter.Player.PlayerDodge>();
                var charge = GetComponent<RogueShooter.Player.PlayerCharge>();
                var motor = GetComponent<RogueShooter.Player.PlayerMotor2D>();
                if (dodge != null && dodge.IsRolling)
                    _state = EntityAnimState.Dodge;
                else if (charge != null && charge.IsCharging)
                    _state = EntityAnimState.Charge;
                else if (motor != null && motor.HasMoveInput)
                    _state = EntityAnimState.Walk;
                else if (d.sqrMagnitude > 0.0004f)
                    _state = EntityAnimState.Walk;
                else
                    _state = EntityAnimState.Idle;
            }
            else
            {
                var ai = GetComponent<RogueShooter.Ai.MobFourStateAi>();
                if (ai != null && ai.State == RogueShooter.Ai.MobAiState.Attack)
                    _state = EntityAnimState.Attack;
                else if (d.sqrMagnitude > 0.0004f)
                    _state = EntityAnimState.Walk;
                else
                    _state = EntityAnimState.Idle;
            }

            string art = player
                ? EntityAnimCatalog.ResolvePlayer(_state)
                : EntityAnimCatalog.ResolveEnemy(_state);
            Bind(art);
            if (_sr != null)
                _sr.flipX = _facing < 0f;
        }

        void Ensure()
        {
            if (_sr == null)
                _sr = GetComponent<SpriteRenderer>();
        }

        void Bind(string artId)
        {
            if (_sr == null || artId == _bound)
                return;
            _bound = artId;
            JianHaiSprites.Bind(_sr, artId);
        }

        public static EntityAnimView Add(GameObject go, bool isPlayer)
        {
            var view = go.GetComponent<EntityAnimView>();
            if (view == null)
                view = go.AddComponent<EntityAnimView>();
            view.Configure(isPlayer);
            return view;
        }
    }
}
