using UnityEngine;
using RogueShooter.Demo;

namespace RogueShooter.Ai
{
    /// <summary>Single ! above the rendered head during windup. No extra dot or attack-area graphics.</summary>
    public class MobBangMarker : MonoBehaviour
    {
        TextMesh _tm;
        
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
            _tm.characterSize = 0.34f;
            _tm.fontSize = 32;
            _tm.color = new Color(1f, .88f, .55f);
            BuiltinUiFont.Apply(_tm);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sortingLayerName="FX";renderer.sortingOrder=30;
            SetVisible(false);
        }

        public void SetVisible(bool on)
        {
            _on = on;
            if (_tm != null)
                _tm.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if(!_on||_tm==null)return;var body=GetComponent<SpriteRenderer>();
            if(body!=null)_tm.transform.position=new Vector3(transform.position.x,body.bounds.max.y+.08f,transform.position.z);
        }
        public bool Visible => _on;
    }
}
