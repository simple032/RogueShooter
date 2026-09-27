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
        int _clip;
        float _time;
        SpriteRenderer _renderer;

        public void ShowFirst()
        {
            _clip = 0;
            _time = 0f;
            ApplyFrame();
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
            Clip clip = Clips[_clip];
            if (clip.Frames == null || clip.Frames.Length == 0 || clip.FramesPerSecond <= 0f)
                return;
            _time += Time.deltaTime;
            float duration = clip.Frames.Length / clip.FramesPerSecond;
            if (_time >= duration)
            {
                _time -= duration;
                _clip = (_clip + 1) % Clips.Length;
                clip = Clips[_clip];
            }

            ApplyFrame();
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
            if (_renderer.sprite == clip.Frames[frame])
                return;
            _renderer.sprite = clip.Frames[frame];
            var lit = GetComponent<IsoLitSprite>();
            if (lit != null)
                lit.Apply();
        }
    }
}
