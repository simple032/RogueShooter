using UnityEngine;
using RogueShooter.Demo;

namespace RogueShooter.Ai
{
    /// <summary>Placeholder 感叹号 / bang during attack windup. Art stub; VFX outsourced.</summary>
    public class MobBangMarker : MonoBehaviour
    {
        TextMesh _tm;
        SpriteRenderer _dot;
        bool _on;

        public void Ensure()
        {
            if (_tm != null)
                return;
            var go = new GameObject("BangMark");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            _tm = go.AddComponent<TextMesh>();
            _tm.text = "!";
            _tm.anchor = TextAnchor.LowerCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.characterSize = 0.22f;
            _tm.fontSize = 32;
            _tm.color = new Color(1f, 0.92f, 0.15f);
            BuiltinUiFont.Apply(_tm);
            var dot = new GameObject("BangDot");
            dot.transform.SetParent(go.transform, false);
            dot.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            dot.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
            _dot = DemoPrimitives.AddSprite(dot, new Color(1f, 0.85f, 0.1f, 0.95f), 20);
            SetVisible(false);
        }

        public void SetVisible(bool on)
        {
            _on = on;
            if (_tm != null)
                _tm.gameObject.SetActive(on);
        }

        public bool Visible => _on;
    }
}
