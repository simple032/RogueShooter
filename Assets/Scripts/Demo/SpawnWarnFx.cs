using UnityEngine;
using RogueShooter.Art;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Placeholder L5 fallback spawn warning (red pulsing plate + "!" + countdown) shown at a fallback
    /// spawn cell for <see cref="RogueShooter.Vision.L5Rules.FallbackWarnFor"/> seconds before the unit
    /// appears. Art will replace this; the timing is the contract.
    /// </summary>
    public class SpawnWarnFx : MonoBehaviour
    {
        float _seconds;
        float _age;
        TextMesh _label;
        SpriteRenderer[] _srs;

        public float Seconds => _seconds;
        public float Remaining => Mathf.Max(0f, _seconds - _age);

        public static SpawnWarnFx SpawnAt(string name, Vector3 world, float seconds, Transform parent)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "SpawnWarnFx" : name);
            go.transform.SetParent(parent, false);
            world.z = 0.04f;
            go.transform.position = world;
            var fx = go.AddComponent<SpawnWarnFx>();
            fx._seconds = Mathf.Max(0f, seconds);
            fx.Build();
            return fx;
        }

        void Build()
        {
            AddPlate("WarnRing", new Vector2(1.9f, 1.9f), new Color(1f, 0.1f, 0.1f, 0.85f), 13);
            AddPlate("WarnCore", new Vector2(0.9f, 0.9f), new Color(1f, 0.85f, 0.1f, 0.95f), 14);
            var label = new GameObject("WarnLabel");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            _label = label.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.characterSize = 0.2f;
            _label.fontSize = 40;
            _label.color = new Color(1f, 0.25f, 0.2f, 1f);
            BuiltinUiFont.Apply(_label);
            _srs = GetComponentsInChildren<SpriteRenderer>();
            UpdateLabel();
        }

        void AddPlate(string name, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            DemoPrimitives.AddSprite(go, color, order);
            JianHaiBind.SetLayer(go, JianHaiArtCatalog.LayerFx, order);
        }

        void UpdateLabel()
        {
            if (_label != null)
                _label.text = "! " + Remaining.ToString("0.0");
        }

        void Update()
        {
            _age += Time.deltaTime;
            UpdateLabel();
            float pulse = 1f + 0.2f * Mathf.Sin(_age * 16f);
            transform.localScale = Vector3.one * pulse;
            if (_srs == null)
                return;
            float a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_age * 20f));
            for (int i = 0; i < _srs.Length; i++)
            {
                if (_srs[i] == null)
                    continue;
                Color c = _srs[i].color;
                c.a = a;
                _srs[i].color = c;
            }
        }
    }
}
