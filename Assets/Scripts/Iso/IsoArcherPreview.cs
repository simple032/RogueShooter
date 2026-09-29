using UnityEngine;

namespace RogueShooter.Iso
{
    public sealed class IsoArcherPreview : MonoBehaviour
    {
        [System.Serializable]
        public struct Clip
        {
            public string Name;
            public Sprite[] Frames;
            public float FramesPerSecond;
        }

        public Clip[] Clips;
        public float ChargeHold = 0.45f;
        public int ReleaseFrame = 1;
        public Sprite[] ArrowFrames;
        public SpriteRenderer Arrow;
        public float ArrowSpeed = 8f;
        public float ArrowRange = 4.5f;
        public string Facing = "s";

        int _clip;
        int _frame;
        float _time;
        bool _armed;
        bool _flying;
        float _flyTime;
        Vector3 _origin;
        Vector2 _dir;
        float _angle;
        SpriteRenderer _renderer;

        public void ShowFirst()
        {
            _clip = 0;
            _time = 0f;
            _frame = 0;
            _armed = false;
            _flying = false;
            ApplyFrame();
            if (Arrow != null)
                Arrow.enabled = false;
        }

        void OnEnable()
        {
            _renderer = GetComponent<SpriteRenderer>();
            ShowFirst();
        }

        void Update()
        {
            if (!Application.isPlaying || Clips == null || Clips.Length == 0)
                return;
            _time += Time.deltaTime;
            Clip clip = Clips[_clip];
            float duration = clip.Frames.Length / Mathf.Max(0.01f, clip.FramesPerSecond);
            float hold = clip.Name == "charge" ? ChargeHold : 0f;
            if (_time >= duration + hold)
            {
                _time = 0f;
                _clip = (_clip + 1) % Clips.Length;
                clip = Clips[_clip];
                if (clip.Name == "atk")
                    _armed = true;
            }

            ApplyFrame();
            if (_armed && clip.Name == "atk" && _frame >= ReleaseFrame)
            {
                _armed = false;
                Launch();
            }

            TickArrow();
        }

        void ApplyFrame()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (Clips == null || Clips.Length == 0 || _renderer == null)
                return;
            Clip clip = Clips[_clip];
            if (clip.Frames == null || clip.Frames.Length == 0)
                return;
            int frame = Mathf.FloorToInt(_time * clip.FramesPerSecond);
            if (frame < 0)
                frame = 0;
            if (frame >= clip.Frames.Length)
                frame = clip.Frames.Length - 1;
            _frame = frame;
            if (_renderer.sprite == clip.Frames[frame])
                return;
            _renderer.sprite = clip.Frames[frame];
            var lit = GetComponent<IsoLitSprite>();
            if (lit != null)
                lit.Apply();
        }

        void Launch()
        {
            if (Arrow == null || ArrowFrames == null || ArrowFrames.Length == 0)
                return;
            _dir = FacingDir(Facing);
            _angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            Vector2 bow = Rotate(new Vector2(-0.28f, 0.9f), _angle - (-90f));
            _origin = transform.position + new Vector3(bow.x, bow.y, 0f);
            _flyTime = 0f;
            _flying = true;
            Arrow.enabled = true;
            Arrow.sortingOrder = 5;
            Arrow.transform.position = _origin;
            Arrow.transform.rotation = Quaternion.Euler(0f, 0f, _angle);
            Arrow.sprite = ArrowFrames[0];
        }

        void TickArrow()
        {
            if (Arrow == null)
                return;
            if (!_flying)
            {
                Arrow.enabled = false;
                return;
            }

            _flyTime += Time.deltaTime;
            float dist = ArrowSpeed * _flyTime;
            Arrow.transform.position = _origin + new Vector3(_dir.x, _dir.y, 0f) * dist;
            Arrow.transform.rotation = Quaternion.Euler(0f, 0f, _angle);
            int frame = Mathf.FloorToInt(_flyTime * 12f) % ArrowFrames.Length;
            if (frame < 0)
                frame = 0;
            if (Arrow.sprite != ArrowFrames[frame])
                Arrow.sprite = ArrowFrames[frame];
            if (dist >= ArrowRange)
                _flying = false;
        }

        public static Vector2 FacingDir(string facing)
        {
            switch (facing)
            {
                case "e": return Vector2.right;
                case "ne": return new Vector2(0.70710678f, 0.70710678f);
                case "n": return Vector2.up;
                case "nw": return new Vector2(-0.70710678f, 0.70710678f);
                case "w": return Vector2.left;
                case "sw": return new Vector2(-0.70710678f, -0.70710678f);
                case "s": return Vector2.down;
                case "se": return new Vector2(0.70710678f, -0.70710678f);
                default: return Vector2.down;
            }
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
