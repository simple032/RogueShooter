using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Spawning;

namespace RogueShooter.Iso
{
    /// <summary>
    /// Sample-room cult mage. One orb leaves the staff and flies. It does not sit beside the caster.
    /// Speed is the combat cult-mage orb speed.
    /// </summary>
    public sealed class IsoMageCastPreview : MonoBehaviour
    {
        public Sprite[] CastFrames;
        public Sprite[] FlyFrames;
        public Sprite SpawnFrame;
        public SpriteRenderer Orb;
        public int ReleaseFrame = 3;
        public float FramesPerSecond = 12f;
        public Vector2 StaffOffset = new Vector2(0.44f, 1.22f);

        SpriteRenderer _renderer;
        float _time;
        bool _shot;
        bool _flying;
        float _flown;
        float _flyTime;
        Vector3 _dir;
        float _speed;
        float _range;

        void OnEnable()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _time = 0f;
            _shot = false;
            _flying = false;
            if (Orb != null)
                Orb.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Application.isPlaying || CastFrames == null || CastFrames.Length == 0)
                return;
            _time += Time.deltaTime;
            float duration = CastFrames.Length / Mathf.Max(0.01f, FramesPerSecond);
            if (_time >= duration)
            {
                _time -= duration;
                if (!_flying)
                    _shot = false;
            }

            int frame = Mathf.FloorToInt(_time * FramesPerSecond);
            if (frame >= CastFrames.Length)
                frame = CastFrames.Length - 1;
            if (frame < 0)
                frame = 0;
            SetSprite(_renderer, CastFrames[frame]);
            if (!_shot && frame >= ReleaseFrame)
            {
                _shot = true;
                Launch();
            }

            TickOrb();
        }

        void Launch()
        {
            if (Orb == null)
                return;
            _dir = Vector3.down;
            _speed = EnemyCombatRules.OrbSpeedForKind(EnemyKindIds.CultMage, EnemyCombatRules.WalkMageStub);
            Camera cam = Camera.main;
            float ortho = cam != null && cam.orthographic ? cam.orthographicSize : EnemyCombatRules.PlayOrthoSize;
            float aspect = cam != null && cam.aspect > 0.01f ? cam.aspect : EnemyCombatRules.DefaultAspect;
            _range = EnemyCombatRules.OrbMaxRange(ortho, aspect);
            _flown = 0f;
            _flyTime = 0f;
            _flying = true;
            Orb.gameObject.SetActive(true);
            Orb.sortingOrder = 5;
            Orb.transform.position = transform.position + new Vector3(StaffOffset.x, StaffOffset.y, 0f);
            if (SpawnFrame != null)
                SetSprite(Orb, SpawnFrame);
        }

        void TickOrb()
        {
            if (!_flying || Orb == null)
                return;
            float step = _speed * Time.deltaTime;
            Orb.transform.position += _dir * step;
            _flown += step;
            _flyTime += Time.deltaTime;
            if (FlyFrames != null && FlyFrames.Length > 0 && _flyTime >= 1f / Mathf.Max(0.01f, FramesPerSecond))
            {
                int fly = Mathf.FloorToInt(_flyTime * FramesPerSecond) % FlyFrames.Length;
                if (fly < 0)
                    fly = 0;
                SetSprite(Orb, FlyFrames[fly]);
            }

            if (_flown >= _range)
            {
                _flying = false;
                Orb.gameObject.SetActive(false);
            }
        }

        static void SetSprite(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer == null || sprite == null || renderer.sprite == sprite)
                return;
            renderer.sprite = sprite;
            var lit = renderer.GetComponent<IsoLitSprite>();
            if (lit != null)
                lit.Apply();
        }
    }
}
