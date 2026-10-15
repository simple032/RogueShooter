using UnityEngine;

namespace JianHai.ArtSamples
{
    /// <summary>
    /// Cycles one character through its adopted action clips. About 8 fps, looping.
    /// </summary>
    public sealed class S1AdoptedClipPreview : MonoBehaviour
    {
        [System.Serializable]
        public struct Clip
        {
            public string Name;
            public Sprite[] Frames;
        }

        public Clip[] Clips;
        public float FramesPerSecond = 8f;

        SpriteRenderer _renderer;
        int _clip;
        int _frame;
        float _time;

        void OnEnable()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _clip = 0;
            _frame = 0;
            _time = 0f;
            Apply();
        }

        void Update()
        {
            if (!Application.isPlaying || Clips == null || Clips.Length == 0)
                return;
            float fps = FramesPerSecond > 0.01f ? FramesPerSecond : 8f;
            _time += Time.deltaTime;
            while (_time >= 1f / fps)
            {
                _time -= 1f / fps;
                Advance();
            }
            Apply();
        }

        void Advance()
        {
            int count = FrameCount(_clip);
            if (count == 0)
            {
                _clip = (_clip + 1) % Clips.Length;
                _frame = 0;
                return;
            }
            _frame++;
            if (_frame < count)
                return;
            _frame = 0;
            _clip = (_clip + 1) % Clips.Length;
        }

        int FrameCount(int index)
        {
            if (Clips == null || index < 0 || index >= Clips.Length || Clips[index].Frames == null)
                return 0;
            return Clips[index].Frames.Length;
        }

        void Apply()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null || FrameCount(_clip) == 0 || _frame >= FrameCount(_clip))
                return;
            Sprite sprite = Clips[_clip].Frames[_frame];
            if (sprite != null && _renderer.sprite != sprite)
                _renderer.sprite = sprite;
        }
    }
}