using UnityEngine;

namespace RogueShooter.Art
{
    /// <summary>A narrow rim preserves the player's silhouette without changing body depth.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class PlayerReadability : MonoBehaviour
    {
        SpriteRenderer _body, _rim;
        Material _material;
        bool _crowded;

        void Awake()
        {
            _body = GetComponent<SpriteRenderer>();
            var shader = Resources.Load<Shader>("JianHaiShaders/PlayerRim");
            if (_body == null || shader == null) return;
            _material = new Material(shader);
            _material.SetFloat("_OutlinePixels", 3f);
            var child = new GameObject("Player silhouette rim");
            child.transform.SetParent(transform, false);
            _rim = child.AddComponent<SpriteRenderer>();
            _rim.sharedMaterial = _material;
            _rim.sortingLayerName = JianHaiArtCatalog.LayerFx;
            _rim.sortingOrder = -5;
            _rim.spriteSortPoint = SpriteSortPoint.Pivot;
            _rim.color = new Color(1f, .87f, .60f, .28f);
        }

        void LateUpdate()
        {
            var hero=GetComponent<RogueShooter.Iso.HeroMeshyPresentation>();if(hero!=null&&hero.RenderedBody!=null){_body=hero.RenderedBody;if(_rim!=null)_rim.transform.localPosition=hero.VisualOffset;}
            if (_rim == null || _body == null) return;
            _rim.sprite = _body.sprite;
            _rim.flipX = _body.flipX; _rim.flipY = _body.flipY;
            bool crowded = false;
            var enemies = RogueShooter.Ai.MobFourStateAi.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled || (enemy.transform.position-transform.position).sqrMagnitude > 9f) continue;
                var body = enemy.GetComponent<SpriteRenderer>();
                if (body != null && body.enabled && _body.bounds.Intersects(body.bounds)) { crowded = true; break; }
            }
            var boss = RogueShooter.Boss.BossFightDriver.Live;
            if (!crowded && boss != null && boss.FightStarted && !boss.FightSettled)
            {
                var body = boss.Combat!=null?boss.Combat.BodyRenderer:boss.GetComponent<SpriteRenderer>();
                crowded = body != null && _body.bounds.Intersects(body.bounds);
            }
            if (crowded != _crowded)
            {
                _crowded = crowded;
                _material.SetFloat("_OutlinePixels", crowded ? 5f : 3f);
                _rim.color = new Color(1f, .87f, .60f, crowded ? .9f : .28f);
            }
            var vitals = GetComponent<RogueShooter.Player.PlayerVitals>();
            _rim.enabled = _body.enabled && (vitals == null || !vitals.IsDown);
        }
        void OnDisable() { if (_rim != null) _rim.enabled = false; }
        void OnDestroy() { if (_material != null) Destroy(_material); }
    }
}
