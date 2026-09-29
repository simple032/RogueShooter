using UnityEngine;
using RogueShooter.Player;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Swaps an accepted iso actor between idle, walk and roll, eight facings.
    /// </summary>
    public sealed class Stage1IsoActor : MonoBehaviour
    {
        string _family = "skel";
        SpriteRenderer _renderer;
        Vector3 _last;
        string _facing = "s";
        float _clock;
        bool _moving;
        PlayerRoll _roll;

        public string Family
        {
            get { return _family; }
        }

        public string Facing
        {
            get { return _facing; }
        }

        public bool Moving
        {
            get { return _moving; }
        }

        public void Bind(string family)
        {
            _family = string.IsNullOrEmpty(family) ? "skel" : family;
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = 0;
            _renderer.color = Color.white;
            transform.localScale = Vector3.one;
            _last = transform.position;
            Apply();
        }

        void LateUpdate()
        {
            if (_roll == null)
                _roll = GetComponent<PlayerRoll>();

            if (_roll != null && _roll.IsRolling)
            {
                // True 8-direction roll: 8 frames at 20 fps, no walk frames mixed in.
                _facing = FacingFrom((Vector3)_roll.RollDirection);
                _moving = true;
                _last = transform.position;
                if (_renderer != null)
                {
                    int frame = Mathf.Clamp(Mathf.FloorToInt(_roll.RollElapsed * 20f), 0, 7);
                    Sprite sprite = Stage1IsoArt.RollSprite(_facing, frame);
                    if (sprite != null)
                        _renderer.sprite = sprite;
                    _renderer.flipX = false;
                }
                return;
            }

            Vector3 delta = transform.position - _last;
            delta.z = 0f;
            _moving = delta.sqrMagnitude > 0.0004f;
            if (_moving)
                _facing = FacingFrom(delta);
            _last = transform.position;
            _clock += Time.deltaTime * (_moving ? 8f : 4f);
            Apply();
        }

        void Apply()
        {
            if (_renderer == null)
                return;
            int frame = Mathf.FloorToInt(_clock);
            bool flip;
            Sprite sprite = Stage1IsoArt.ActorSprite(_family, _moving, _facing, frame, out flip);
            if (sprite != null)
                _renderer.sprite = sprite;
            _renderer.flipX = flip;
        }

        static string FacingFrom(Vector3 delta)
        {
            float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            int idx = Mathf.RoundToInt(ang / 45f);
            idx = (idx % 8 + 8) % 8;
            switch (idx)
            {
                case 0: return "e";
                case 1: return "ne";
                case 2: return "n";
                case 3: return "nw";
                case 4: return "w";
                case 5: return "sw";
                case 6: return "s";
                default: return "se";
            }
        }
    }
}
