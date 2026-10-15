using UnityEngine;
using RogueShooter.Demo;
using RogueShooter.Player;

namespace RogueShooter.Art
{
    /// <summary>Target-local confirmation from resolved arrow hits, never from release/charge.</summary>
    public sealed class ArrowImpactFeedback : MonoBehaviour
    {
        static Material _material;
        readonly LineRenderer[] _lines = new LineRenderer[8];
        Transform _root;
        Vector3 _incoming;
        float _age, _duration;
        public string LastKind { get; private set; }
        public bool Active { get; private set; }
        public int ConfirmedHits { get; private set; }

        public static void Confirm(GameObject target, Vector3 origin, bool weak, bool blocked)
        {
            var view = target.GetComponent<ArrowImpactFeedback>();
            if (view == null) view = target.AddComponent<ArrowImpactFeedback>();
            RogueShooter.Audio.JianHaiAudio.Emit(weak?"jh_hit_weakspot":blocked?"jh_hit_shield_block":"jh_hit_body",view);
            view.Begin(origin, weak, blocked);
        }

        void Ensure()
        {
            if (_root != null) return;
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
            _root = new GameObject("ArrowHitConfirmation").transform;
            _root.SetParent(transform, false);
            _root.localPosition = new Vector3(0f, 1.02f, 0f);
            for (int i = 0; i < _lines.Length; i++)
            {
                var go = new GameObject(i < 2 ? "WeakDiamond" : "ContactSpark");
                go.transform.SetParent(_root, false);
                var l = go.AddComponent<LineRenderer>();
                l.sharedMaterial = _material; l.useWorldSpace = false;
                // Mobs draw in the Entity layer; default-layer lines would hide under
                // the floor and the body and the hit confirmation would never show.
                l.sortingLayerName = RogueShooter.Art.JianHaiArtCatalog.LayerEntity;
                l.sortingOrder = 6; l.numCapVertices = 2;
                l.enabled = false; _lines[i] = l;
            }
        }

        void Begin(Vector3 origin, bool weak, bool blocked)
        {
            Ensure(); _incoming = origin - transform.position; _incoming.z = 0f;
            if (_incoming.sqrMagnitude < .001f) _incoming = Vector3.left;
            _incoming.Normalize(); _age = 0f;
            LastKind = weak ? "weak" : blocked ? "block" : "body";
            _duration = weak ? .45f : blocked ? .18f : .12f;
            Active = true; ConfirmedHits++; Draw();
        }

        void Update()
        {
            if (!Active) return;
            var body = GetComponent<StubEnemy>();
            if (body != null && body.IsDead) { Clear(); return; }
            if (RunPause.IsPaused) return;
            _age += Time.deltaTime;
            if (_age >= _duration) { Clear(); return; }
            Draw();
        }

        void Draw()
        {
            float t = Mathf.Clamp01(_age / _duration), fade = 1f-t;
            bool weak = LastKind == "weak", block = LastKind == "block";
            Color c = weak ? new Color(.45f, 1f, .72f, fade)
                : block ? new Color(.65f,.82f,1f,fade) : new Color(1f,.83f,.55f,fade);
            for (int i = 0; i < _lines.Length; i++)
            {
                var l = _lines[i]; l.startColor = l.endColor = c;
                l.startWidth = l.endWidth = weak ? .045f*fade : .025f*fade;
                if (i < 2)
                {
                    l.enabled = weak; if (!weak) continue;
                    float r = i == 0 ? .20f + .04f*t : .24f + .4f*t;
                    l.positionCount = 5;
                    l.SetPosition(0,new Vector3(0,r*1.15f,0));
                    l.SetPosition(1,new Vector3(r,0,0));
                    l.SetPosition(2,new Vector3(0,-r*1.15f,0));
                    l.SetPosition(3,new Vector3(-r,0,0));
                    l.SetPosition(4,new Vector3(0,r*1.15f,0));
                    if (i == 0) l.startColor = l.endColor = new Color(.85f,1f,.9f,Mathf.Clamp01(1f-_age/.16f));
                }
                else
                {
                    l.enabled = true; l.positionCount = 2;
                    float angle = (block ? -65f+(i-2)*26f : (i-2)*60f)*Mathf.Deg2Rad;
                    Vector3 axis = block ? _incoming : Vector3.right;
                    Vector3 dir = new Vector3(axis.x*Mathf.Cos(angle)-axis.y*Mathf.Sin(angle),axis.x*Mathf.Sin(angle)+axis.y*Mathf.Cos(angle),0f);
                    float travel = .1f + t*(weak ? .5f : .23f);
                    Vector3 center = block ? _incoming*.25f : Vector3.zero;
                    l.SetPosition(0,center+dir*travel);
                    l.SetPosition(1,center+dir*(travel+.08f*fade));
                }
            }
        }

        void Clear() { Active = false; foreach (var l in _lines) if (l != null) l.enabled = false; }
        void OnDisable() { Clear(); }
    }
}
