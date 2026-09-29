using UnityEngine;

namespace RogueShooter.Iso
{
    public sealed class IsoBrazierFire : MonoBehaviour
    {
        public Sprite[] Frames;
        public float FramesPerSecond = 12f;
        SpriteRenderer _renderer;
        float _time;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (Frames == null || Frames.Length == 0 || _renderer == null)
                return;
            _time += Time.deltaTime;
            int frame = Mathf.FloorToInt(_time * FramesPerSecond) % Frames.Length;
            if (frame < 0)
                frame = 0;
            if (_renderer.sprite != Frames[frame])
            {
                _renderer.sprite = Frames[frame];
                var lit = GetComponent<IsoLitSprite>();
                if (lit != null)
                    lit.Apply();
            }
        }
    }
}
