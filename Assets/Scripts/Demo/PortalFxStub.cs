using UnityEngine;
using RogueShooter.Art;

namespace RogueShooter.Demo
{
    /// <summary>
    /// High-contrast ground portal stub at a spawn point. Outsourced art can replace this.
    /// Must be readable in Play on dark floors (cyan ring + magenta core + pulse).
    /// </summary>
    public class PortalFxStub : MonoBehaviour
    {
        Vector3 _baseScale = Vector3.one;
        SpriteRenderer[] _srs;

        public static GameObject SpawnAt(string name, Vector3 world, Transform parent)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "PortalFx" : name);
            go.transform.SetParent(parent, false);
            world.z = 0.05f;
            go.transform.position = world;
            var fx = go.AddComponent<PortalFxStub>();
            fx.Build();
            return go;
        }

        void Build()
        {
            AddPlate("Ring", Vector3.zero, new Vector2(1.70f, 1.70f),
                new Color(0.05f, 1f, 1f, 0.95f), 10);
            AddPlate("Band", Vector3.zero, new Vector2(1.22f, 1.22f),
                new Color(1f, 0.12f, 0.82f, 0.95f), 11);
            AddPlate("Core", Vector3.zero, new Vector2(0.48f, 0.48f),
                new Color(1f, 0.95f, 0.20f, 1f), 12);

            var label = new GameObject("Bang");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            var tm = label.AddComponent<TextMesh>();
            tm.text = "!";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.22f;
            tm.fontSize = 42;
            tm.color = new Color(1f, 0.95f, 0.15f, 1f);
            BuiltinUiFont.Apply(tm);

            _srs = GetComponentsInChildren<SpriteRenderer>();
            _baseScale = Vector3.one;
        }

        void AddPlate(string name, Vector3 local, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            DemoPrimitives.AddSprite(go, color, order);
            JianHaiBind.SetLayer(go, JianHaiArtCatalog.LayerFx, order);
        }

        void Update()
        {
            float pulse = 1f + 0.16f * Mathf.Sin(Time.time * 10f);
            transform.localScale = _baseScale * pulse;
            if (_srs == null)
                return;
            float blink = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(Time.time * 14f));
            for (int i = 0; i < _srs.Length; i++)
            {
                if (_srs[i] == null)
                    continue;
                Color c = _srs[i].color;
                c.a = blink;
                _srs[i].color = c;
            }
        }
    }
}
