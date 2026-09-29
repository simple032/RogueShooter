using UnityEngine;
using RogueShooter.Demo;

namespace RogueShooter.Player
{
    /// <summary>
    /// Shift starts one roll along the current move direction (or the current
    /// facing when idle). Plays the archer roll frames at 20 fps for 8 frames.
    /// No invulnerability window: the GDD does not lock one.
    /// </summary>
    public sealed class PlayerRoll : MonoBehaviour
    {
        public const float FrameSeconds = 1f / 20f;
        public const int FrameCount = 8;
        public const float Duration = FrameSeconds * FrameCount;

        float _t = float.MaxValue;
        Vector2 _dir = Vector2.right;
        Stage1IsoActor _actor;

        public bool IsRolling
        {
            get { return _t < Duration; }
        }

        public Vector2 RollDirection
        {
            get { return _dir; }
        }

        public float RollElapsed
        {
            get { return _t; }
        }

        void Awake()
        {
            _actor = GetComponent<Stage1IsoActor>();
        }

        void Update()
        {
            if (RunPause.IsPaused)
                return;
            if (_actor == null)
                _actor = GetComponent<Stage1IsoActor>();

            if (_t < Duration)
            {
                _t += Time.deltaTime;
                return;
            }

            if (!Input.GetKeyDown(KeyCode.LeftShift) && !Input.GetKeyDown(KeyCode.RightShift))
                return;

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 0.01f)
                input.Normalize();
            else
                input = FacingVector(_actor);
            _dir = input;
            _t = 0f;
        }

        static Vector2 FacingVector(Stage1IsoActor actor)
        {
            if (actor == null)
                return Vector2.right;
            switch (actor.Facing)
            {
                case "n": return Vector2.up;
                case "ne": return new Vector2(0.7071f, 0.7071f);
                case "e": return Vector2.right;
                case "se": return new Vector2(0.7071f, -0.7071f);
                case "s": return Vector2.down;
                case "sw": return new Vector2(-0.7071f, -0.7071f);
                case "w": return Vector2.left;
                default: return new Vector2(-0.7071f, 0.7071f);
            }
        }
    }
}
