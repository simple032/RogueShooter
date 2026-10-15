using UnityEngine;
using RogueShooter.Balance;

namespace RogueShooter.Player
{
    /// <summary>
    /// Top-down WASD / arrow movement. No combat.
    /// </summary>
    public class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] float speed = MoveSpeeds.Player;
        float _walkBase;
        RogueShooter.Maze.Stage1Maze _maze;
        System.Collections.Generic.IReadOnlyList<GameObject> _doors;
        public void BindMaze(RogueShooter.Maze.Stage1Maze maze,System.Collections.Generic.IReadOnlyList<GameObject> doors){_maze=maze;_doors=doors;}
        public event System.Action<Vector2> Walked;
        bool _physicsWalking;
        Vector2 _physicsLast;
        struct WalkSample { public float At,Dt; public Vector2 Delta; }
        readonly System.Collections.Generic.Queue<WalkSample> _walkSamples = new System.Collections.Generic.Queue<WalkSample>();
        Vector2 _latestWalk;
        void RecordWalk(Vector2 delta,float dt)
        {
            _latestWalk=delta;
            if(dt<=0 || delta.magnitude>CurrentSpeed*dt*1.1f+.001f) { _walkSamples.Clear();return; }
            _walkSamples.Enqueue(new WalkSample{At=Time.time,Dt=dt,Delta=delta});
            while(_walkSamples.Count>0&&Time.time-_walkSamples.Peek().At>.2f)_walkSamples.Dequeue();
        }
        /// <summary>Actual voluntary ground movement over the last .2s. Never includes roll/teleport.</summary>
        public Vector2 RecentWalkVelocity
        {
            get
            {
                if(RunPause.IsPaused||(_roll!=null&&_roll.LocksActions)||_latestWalk.sqrMagnitude<.000001f)return Vector2.zero;
                Vector2 sum=Vector2.zero;float duration=0;
                foreach(var s in _walkSamples)if(Time.time-s.At<=.2f){sum+=s.Delta;duration+=s.Dt;}
                return duration>0?sum/duration:Vector2.zero;
            }
        }
        void Move(Vector3 delta, bool walking = false)
        {
            Vector3 previous = transform.position;
            transform.position=RogueShooter.Maze.MazeCollision.Slide(_maze,_doors,previous,previous+delta,.32f);
            Vector2 actual = transform.position-previous;
            if (walking) { RecordWalk(actual,Time.deltaTime); if(actual.sqrMagnitude>.000001f)Walked?.Invoke(actual); }
        }

        public const float HitPushDistance=.45f,HitPushSeconds=.12f;
        Vector3 _hitPushDirection;float _hitPushLeft;
        public bool IsHitPushed=>_hitPushLeft>0;
        public void ApplyImpact(Vector3 away)
        {
            away.z=0;if(away.sqrMagnitude<.0001f)return;
            _hitPushDirection=away.normalized;_hitPushLeft=HitPushSeconds;
            _walkSamples.Clear();_latestWalk=Vector2.zero;
        }

        PlayerCharge _charge;
        PlayerRoll _roll;
        Rigidbody2D _body;

        public float BaseSpeed => speed;
        public float CurrentSpeed
        {
            get
            {
                float mul = _charge != null && _charge.IsCharging ? MoveSpeeds.ChargeMul : 1f;
                return speed * mul;
            }
        }

        public void Configure(float moveSpeed)
        {
            speed = moveSpeed > 0.0001f ? moveSpeed : MoveSpeeds.Player;
            _walkBase = speed;
        }

        /// <summary>Catalog move_speed product, from the configured walk speed.</summary>
        public void ApplyRewardSpeed(float mul)
        {
            float m = mul > 0.01f ? mul : 1f;
            float basis = _walkBase > 0.01f ? _walkBase : speed;
            speed = basis * m;
        }

        void Awake()
        {
            _charge = GetComponent<PlayerCharge>();
            _roll = GetComponent<PlayerRoll>();
            _body = GetComponent<Rigidbody2D>();
        }

        void Update()
        {
            if (_body != null)
                return;
            Step(Time.deltaTime, false);
        }

        void FixedUpdate()
        {
            if (_body == null)
                return;
            Step(Time.fixedDeltaTime, true);
        }

        void Step(float dt, bool physics)
        {
            PlayerVitals vitals = GetComponent<PlayerVitals>();
            if (RunPause.IsPaused || (vitals != null && vitals.IsDown))
            {
                if (physics)
                    _body.velocity = Vector2.zero;
                _physicsWalking = false; _physicsLast = transform.position;
                _walkSamples.Clear();_latestWalk=Vector2.zero;
                return;
            }
            if(_hitPushLeft>0)
            {
                // Consume only the remaining slice; repeated hits replace, never stack distance.
                float slice=Mathf.Min(Mathf.Max(0,dt),_hitPushLeft);_hitPushLeft-=slice;
                if(physics)_body.velocity=Vector2.zero;
                Move(_hitPushDirection*(HitPushDistance/HitPushSeconds)*slice);
                if(physics)_body.position=transform.position;
                _physicsWalking=false;_physicsLast=transform.position;_walkSamples.Clear();_latestWalk=Vector2.zero;
                return;
            }
            if (_charge == null)
                _charge = GetComponent<PlayerCharge>();
            if (_roll == null)
                _roll = GetComponent<PlayerRoll>();
            if (physics)
            {
                Vector2 actual = (Vector2)transform.position-_physicsLast;
                if (_physicsWalking && (_roll == null || !_roll.LocksActions))
                { RecordWalk(actual,dt);if(actual.sqrMagnitude>.000001f)Walked?.Invoke(actual); }
                else { _walkSamples.Clear();_latestWalk=Vector2.zero; }
                _physicsLast = transform.position; _physicsWalking = false;
            }
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            if (_roll != null && _roll.LocksActions)
            {
                _walkSamples.Clear();_latestWalk=Vector2.zero;
                // Clip the last movement slice to the remaining 0.40 seconds.
                // Owning the clock here makes component Update order irrelevant.
                float moveDt = _roll.AdvanceMotion(dt);
                if (!physics)
                    Move((Vector3)(_roll.RollDirection * PlayerRoll.MoveSpeed * moveDt));
                else
                    _body.velocity = _roll.RollDirection * PlayerRoll.MoveSpeed * (moveDt / dt);
                return;
            }
            if (!physics)
            {
                Move((Vector3)(input * CurrentSpeed * dt), true);
                return;
            }
            _body.velocity = input * CurrentSpeed;
            _physicsWalking = input.sqrMagnitude > .0001f;
        }
    }
}
