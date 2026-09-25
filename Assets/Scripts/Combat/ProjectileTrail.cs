using UnityEngine;
using RogueShooter.Art;

namespace RogueShooter.Combat
{
    /// <summary>
    /// Short fading JianHai marks along arrow / orb flight. Not a VFX budget table.
    /// </summary>
    public class ProjectileTrail : MonoBehaviour
    {
        [SerializeField] string artId;
        [SerializeField] float interval = 0.045f;
        [SerializeField] float markLife = 0.16f;
        [SerializeField] float markScale = 0.40f;
        float _acc;

        public void Configure(string id, float spawnInterval, float life, float scale)
        {
            artId = id;
            interval = spawnInterval;
            markLife = life;
            markScale = scale;
        }

        void Update()
        {
            _acc += Time.deltaTime;
            if (_acc < interval)
                return;
            _acc = 0f;
            SpawnMark(transform.position);
        }

        void SpawnMark(Vector3 pos)
        {
            string id = string.IsNullOrEmpty(artId) ? JianHaiArtCatalog.FxStringGlow : artId;
            var go = new GameObject("Trail_" + id);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * markScale;
            var sr = go.AddComponent<SpriteRenderer>();
            JianHaiSprites.Bind(sr, id);
            sr.color = new Color(1f, 1f, 1f, 0.42f);
            sr.sortingOrder = 8;
            var fade = go.AddComponent<ProjectileTrailMark>();
            fade.Life = markLife;
        }
    }

    public class ProjectileTrailMark : MonoBehaviour
    {
        public float Life = 0.16f;
        float _t;
        SpriteRenderer _sr;
        Color _start;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
                _start = _sr.color;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float u = Life < 0.0001f ? 1f : Mathf.Clamp01(_t / Life);
            if (_sr != null)
            {
                Color c = _start;
                c.a = _start.a * (1f - u);
                _sr.color = c;
            }

            if (_t >= Life)
                Destroy(gameObject);
        }
    }
}
