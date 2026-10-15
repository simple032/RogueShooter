using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RogueShooter.Demo
{
    /// <summary>Shared production/sample sliding-door view. Crops translated panels at the
    /// wall slots, preserving source dimensions and UVs throughout the motion.</summary>
    public class Stage1SlidingDoorView : MonoBehaviour
    {
        public float Open01 { get; private set; }
        public float ClearWidth { get; private set; }
        public char Rim { get; private set; }
        public bool SingleLeaf { get; private set; }
        public bool Animating { get; private set; }
        public BoxCollider2D ClosedBlocker { get; private set; }
        public float VisibleGap => ClearWidth * Open01;
        public bool Passable => _target >= .98f && Open01 >= .98f;
        public const float OpeningSeconds = .65f;

        /// <summary>Supports the main map's transform movement as well as physics actors.
        /// Sweeps the actor radius so a long frame or roll cannot skip the closed band.</summary>
        public bool BlocksMovement(Vector2 from, Vector2 to, float radius)
        {
            if (ClosedBlocker == null || !ClosedBlocker.enabled) return false;
            Vector2 center = (Vector2)ClosedBlocker.transform.position + ClosedBlocker.offset;
            Vector2 half = ClosedBlocker.size * .5f + Vector2.one * radius;
            Vector2 start = from - center, delta = to - from;
            float enter = 0f, exit = 1f;
            return ClipAxis(start.x, delta.x, half.x, ref enter, ref exit)
                && ClipAxis(start.y, delta.y, half.y, ref enter, ref exit);
        }

        static bool ClipAxis(float start, float delta, float half, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < .00001f) return Mathf.Abs(start) <= half;
            float a = (-half - start) / delta, b = (half - start) / delta;
            enter = Mathf.Max(enter, Mathf.Min(a, b));
            exit = Mathf.Min(exit, Mathf.Max(a, b));
            return enter <= exit;
        }
        readonly List<Object> _owned = new List<Object>();
        Vector3 _tangent, _height, _inward;
        float _target, _from, _elapsed, _duration;
        Material _wood, _solid, _top, _shadow;
        Panel[] _panels;

        sealed class Panel
        {
            public float Start, End;
            public Transform Root;
            public Mesh Wood, Metal, Top, Shadow;
            public MeshRenderer WoodRenderer, MetalRenderer, TopRenderer, ShadowRenderer;
        }

        public void Initialize(char rim, float width, bool singleLeaf, Texture2D texture)
        {
            Rim = rim; ClearWidth = width; SingleLeaf = singleLeaf;
            bool side = rim == 'E' || rim == 'W';
            _tangent = side ? Vector3.up : Vector3.right;
            _height = side ? new Vector3(rim == 'E' ? .65f : -.65f, 1.45f, 0) : Vector3.up * 2.25f;
            _inward = rim == 'N' ? Vector3.down : rim == 'S' ? Vector3.up : rim == 'E' ? Vector3.left : Vector3.right;
            _wood = Material("Slide door wood", texture, new Color(.70f, .73f, .76f));
            _solid = Material("Slide door stone and iron", Texture2D.whiteTexture, Color.white);
            _top = Material("Slide door top thickness", Texture2D.whiteTexture, new Color(.24f, .27f, .29f));
            _shadow = Material("Slide door contact", Texture2D.whiteTexture, new Color(0, 0, 0, .22f));
            BuildFrame();
            _panels = new Panel[singleLeaf ? 1 : 2];
            for (int i = 0; i < _panels.Length; i++)
            {
                var panel = new Panel { Start = singleLeaf || i == 0 ? -width * .5f : 0,
                    End = singleLeaf || i == 1 ? width * .5f : 0 };
                var go = new GameObject(singleLeaf ? "Single sliding leaf" : i == 0 ? "Lower or left sliding leaf" : "Upper or right sliding leaf");
                go.transform.SetParent(transform, false); panel.Root = go.transform;
                go.AddComponent<SortingGroup>().sortingOrder = 0;
                panel.Wood = Mesh("Cropped wood", _wood, 0, panel.Root, out panel.WoodRenderer);
                panel.Metal = Mesh("Cropped iron straps and latch", _solid, 1, panel.Root, out panel.MetalRenderer);
                panel.Top = Mesh("Cropped top thickness", _top, 2, panel.Root, out panel.TopRenderer);
                panel.Shadow = Mesh("Cropped ground contact", _shadow, -710, transform, out panel.ShadowRenderer);
                _panels[i] = panel;
            }
            var blocker = new GameObject("Passage blocker"); blocker.transform.SetParent(transform, false);
            ClosedBlocker = blocker.AddComponent<BoxCollider2D>();
            ClosedBlocker.size = side ? new Vector2(.28f, width) : new Vector2(width, .28f);
            SetPoseImmediate(0);
        }

        public void SetPoseImmediate(float value)
        {
            _target = Mathf.Clamp01(value); Animating = false; Draw(_target);
        }

        public void AnimateTo(float value)
        {
            _from = Open01; _target = Mathf.Clamp01(value); _elapsed = 0;
            _duration = Mathf.Max(.08f, Mathf.Abs(_target - _from) * OpeningSeconds);
            Animating = !Mathf.Approximately(_target, Open01);
            RefreshBlocker();
        }

        protected void Update()
        {
            if (!Animating || !Application.isPlaying || Time.deltaTime <= 0) return;
            _elapsed += Time.deltaTime; float t = Mathf.Clamp01(_elapsed / _duration);
            Draw(Mathf.Lerp(_from, _target, Mathf.SmoothStep(0, 1, t)));
            if (t >= 1) Animating = false;
        }

        void RefreshBlocker() { if (ClosedBlocker != null) ClosedBlocker.enabled = !Passable; }

        void Draw(float value)
        {
            Open01 = Mathf.Clamp01(value);
            if (_panels == null) return;
            float half = ClearWidth * .5f;
            for (int i = 0; i < _panels.Length; i++)
            {
                Panel p = _panels[i]; float sourceWidth = p.End - p.Start;
                float shift = (SingleLeaf || i == 0 ? -1 : 1) * sourceWidth * Open01;
                float left = Mathf.Max(-half, p.Start + shift), right = Mathf.Min(half, p.End + shift);
                bool visible = right - left > .001f;
                p.WoodRenderer.enabled = p.MetalRenderer.enabled = p.TopRenderer.enabled = p.ShadowRenderer.enabled = visible;
                if (!visible) { p.Wood.Clear(); p.Metal.Clear(); p.Top.Clear(); p.Shadow.Clear(); continue; }
                Vector3 pivot = _tangent * ((left + right) * .5f); p.Root.localPosition = pivot;
                var a = _tangent * left - pivot; var b = _tangent * right - pivot;
                float u0 = (left - shift - p.Start) / sourceWidth, u1 = (right - shift - p.Start) / sourceWidth;
                // Only the central oak region is used; straps/latches have fixed geometry.
                bool side = Rim == 'E' || Rim == 'W';
                Vector2 UV(float u, float v) { return side ? new Vector2(Mathf.Lerp(.16f, .62f, v), Mathf.Lerp(.34f, .66f, u))
                    : new Vector2(Mathf.Lerp(.16f, .62f, u), Mathf.Lerp(.34f, .66f, v)); }
                Write(p.Wood, a, b, b + _height, a + _height, new[] { UV(u0, 0), UV(u1, 0), UV(u1, 1), UV(u0, 1) });
                Vector3 depth = _height.normalized * .10f;
                Write(p.Top, a + _height, b + _height, b + _height + depth, a + _height + depth);
                var sa = _tangent * left; var sb = _tangent * right;
                var contact = _inward * .12f;
                Write(p.Shadow, sa, sb, sb + contact, sa + contact);
                DrawMetal(p, shift, pivot, side);
            }
            RefreshBlocker();
        }

        void DrawMetal(Panel p, float shift, Vector3 pivot, bool side)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            var iron = new Color(.10f, .13f, .15f); var bevel = new Color(.29f, .32f, .34f);
            void Rect(float x, float y, float w, float h, Color color)
            {
                float left = Mathf.Max(-ClearWidth * .5f, x + shift);
                float right = Mathf.Min(ClearWidth * .5f, x + shift + w);
                if (right - left < .001f) return;
                Add(v, c, t, _tangent * left + _height * y - pivot, _tangent * (right - left), _height * h, color);
            }
            float width = p.End - p.Start;
            Rect(p.Start, 0, width, .045f, iron); Rect(p.Start, .955f, width, .045f, iron);
            Rect(p.Start, 0, .09f, 1, iron); Rect(p.End - .09f, 0, .09f, 1, iron);
            Rect(p.Start, .96f, width, .015f, bevel);
            if (side)
            {
                // Edge-on timber gate: two short cross straps, no frontal ring handle.
                for (int k = 1; k <= 2; k++)
                {
                    Rect(p.Start + width * k / 3f - .06f, 0, .12f, 1, iron);
                    Rect(p.Start + width * k / 3f + .01f, .05f, .035f, .9f, bevel);
                }
            }
            else
            {
                foreach (float y in new[] { .30f, .67f })
                {
                    Rect(p.Start, y, width, .055f, iron);
                    Rect(p.Start, y + .042f, width, .012f, bevel);
                    foreach (float x in new[] { p.Start + .18f, p.End - .23f }) Rect(x, y + .012f, .065f, .025f, bevel);
                }
                float latch = SingleLeaf || p.Start < 0 ? p.End - .26f : p.Start + .12f;
                Rect(latch, .46f, .14f, .12f, iron); Rect(latch + .025f, .49f, .09f, .045f, bevel);
            }
            p.Metal.Clear(); p.Metal.SetVertices(v); p.Metal.SetColors(c); p.Metal.SetTriangles(t, 0); p.Metal.RecalculateBounds();
        }

        void BuildFrame()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            void Rect(float x, float y, float w, float h, Color color) { Add(v, c, t, _tangent * x + _height * y, _tangent * w, _height * h, color); }
            float half = ClearWidth * .5f;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s < 0 ? -half - .64f : half + .04f;
                for (int row = 0; row < 5; row++)
                {
                    float shade = row % 2 == 0 ? .01f : -.008f;
                    Rect(x, row * .213f, .60f, .20f, new Color(.24f + shade, .28f + shade, .31f + shade));
                    Rect(x, row * .213f + .177f, .60f, .022f, new Color(.31f, .35f, .38f));
                }
                Rect(x - .06f, -.06f, .72f, .08f, new Color(.15f, .18f, .21f));
                Rect(x - .04f, 1.06f, .68f, .05f, new Color(.29f, .33f, .36f));
            }
            Rect(-half - .64f, 1.085f, ClearWidth + 1.28f, .055f, new Color(.24f, .28f, .31f));
            MeshRenderer renderer; var frame = Mesh("Stone jambs and lintel", _solid, -5, transform, out renderer);
            frame.SetVertices(v); frame.SetColors(c); frame.SetTriangles(t, 0); frame.RecalculateBounds();
            v.Clear(); c.Clear(); t.Clear();
            foreach (float x in new[] { -half - .04f, half + .005f })
            {
                Add(v, c, t, _tangent * x, _tangent * .035f, _height * 1.045f, new Color(.07f, .09f, .11f));
                Add(v, c, t, _tangent * (x - .025f), _tangent * .022f, _height * 1.045f, new Color(.28f, .31f, .33f));
            }
            var slots = Mesh("Recessed wall slots and guide rails", _solid, 0, transform, out renderer);
            slots.SetVertices(v); slots.SetColors(c); slots.SetTriangles(t, 0); slots.RecalculateBounds();
            var floor = Mesh("Worn stone threshold", _solid, -700, transform, out renderer);
            Vector3 a = -_tangent * half - _inward * .1f; Vector3 b = _tangent * half - _inward * .1f;
            Write(floor, a, b, b + _inward * .26f, a + _inward * .26f);
            var color = Linear(new Color(.28f, .31f, .33f)); floor.colors = new[] { color, color, color, color };
        }

        static Color Linear(Color c) { return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c; }
        static void Add(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 width, Vector3 height, Color color)
        {
            int i = v.Count; v.Add(a); v.Add(a + width); v.Add(a + width + height); v.Add(a + height);
            for (int j = 0; j < 4; j++) c.Add(Linear(color));
            t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        Material Material(string name, Texture texture, Color color)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { name = name, mainTexture = texture, color = color };
            _owned.Add(m); return m;
        }

        Mesh Mesh(string name, Material material, int order, Transform parent, out MeshRenderer renderer)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name }; mesh.MarkDynamic(); _owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return mesh;
        }

        static void Write(Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2[] uv = null)
        {
            mesh.vertices = new[] { a, b, c, d }; mesh.uv = uv ?? new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
        }

        protected void OnDestroy()
        {
            foreach (var item in _owned) if (item != null) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            _owned.Clear();
        }
    }
}
