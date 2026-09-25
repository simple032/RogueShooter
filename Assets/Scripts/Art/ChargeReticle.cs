using UnityEngine;

namespace RogueShooter.Art
{
    /// <summary>
    /// Fig1 charge aim: center dot + diamond + 4 cardinal ticks + thick ring
    /// that fills clockwise from 12 o'clock. Fig2 scale: compact in-camera
    /// (outer radius ~half a character head, ≤ previous 0.72u assembly).
    /// World-space; caller places via ScreenToWorld. Not a player child.
    /// Weak-spot band: faint arc at ChargeShotRules.WeakSpotEnterPct–ExitPct of the ring (progress is
    /// held / current full charge, so the band stays at 76%–84% under 疾张). 凝神窥机 active →
    /// core + ring recolour (code only, no PNG).
    /// </summary>
    public class ChargeReticle : MonoBehaviour
    {
        public const float OuterRadius = 0.22f;
        public const float RingThickness = 0.048f;
        const float TickInner = 0.045f;
        const float TickOuter = 0.125f;
        const float Diamond = 0.026f;
        const float Dot = 0.009f;
        const int RingSegments = 56;
        const int Sorting = 40;

        static readonly Color CoreColor = new Color(0.82f, 0.79f, 0.72f, 0.95f);
        static readonly Color RingIdle = new Color(0.78f, 0.76f, 0.70f, 0.95f);
        static readonly Color RingWarm = new Color(0.83f, 0.64f, 0.36f, 0.98f);
        static readonly Color RingGreen = new Color(0.55f, 0.78f, 0.62f, 1f);
        static readonly Color BandColor = new Color(0.55f, 0.78f, 0.62f, 0.35f);
        public static readonly Color FocusColor = new Color(1f, 0.84f, 0.50f, 1f);

        LineRenderer _diamond;
        LineRenderer _dot;
        LineRenderer[] _ticks;
        MeshFilter _ringFilter;
        MeshRenderer _ringRend;
        Mesh _ringMesh;
        MeshRenderer _bandRend;
        Mesh _bandMesh;
        Material _bandMat;
        bool _focus;
        Material _lineMat;
        Material _ringMat;
        float _progress;
        bool _green;
        bool _coreOn;
        bool _ringOn;

        public float Progress => _progress;
        public bool FocusTinted => _focus;
        /// <summary>Band arc as ring fractions (tests).</summary>
        public static float BandStart => RogueShooter.Player.ChargeShotRules.WeakSpotEnterPct;
        public static float BandEnd => RogueShooter.Player.ChargeShotRules.WeakSpotExitPct;

        public void SetCoreVisible(bool on)
        {
            _coreOn = on;
            ApplyVisibility();
        }

        public void SetRingVisible(bool on)
        {
            _ringOn = on;
            ApplyVisibility();
        }

        public void SetProgress(float progress01, bool greenWindow)
        {
            _progress = Mathf.Clamp01(progress01);
            _green = greenWindow;
            RebuildRing();
            ApplyVisibility();
        }

        void Awake()
        {
            BuildCore();
            BuildRing();
            BuildBand();
            SetCoreVisible(true);
            SetRingVisible(false);
            SetProgress(0f, false);
        }

        void OnDestroy()
        {
            if (_ringMesh != null)
                Destroy(_ringMesh);
            if (_lineMat != null)
                Destroy(_lineMat);
            if (_ringMat != null)
                Destroy(_ringMat);
            if (_bandMesh != null)
                Destroy(_bandMesh);
            if (_bandMat != null)
                Destroy(_bandMat);
        }

        void LateUpdate()
        {
            bool focus = RogueShooter.Player.GuaranteedCritActive.AnyActive;
            if (focus == _focus)
                return;
            _focus = focus;
            Color core = focus ? FocusColor : CoreColor;
            SetLineColor(_diamond, core);
            SetLineColor(_dot, core);
            if (_ticks != null)
                for (int i = 0; i < _ticks.Length; i++)
                    SetLineColor(_ticks[i], core);
            RebuildRing();
        }

        static void SetLineColor(LineRenderer lr, Color c)
        {
            if (lr == null)
                return;
            lr.startColor = c;
            lr.endColor = c;
        }

        void BuildBand()
        {
            var go = new GameObject("WeakSpotBand");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            _bandRend = go.AddComponent<MeshRenderer>();
            _bandMesh = new Mesh { name = "ChargeWeakSpotBand" };
            mf.sharedMesh = _bandMesh;
            _bandMat = MakeMat();
            _bandMat.color = BandColor;
            _bandRend.sharedMaterial = _bandMat;
            _bandRend.sortingLayerName = JianHaiArtCatalog.LayerUi;
            _bandRend.sortingOrder = Sorting - 1;
            _bandRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _bandRend.receiveShadows = false;
            BuildArc(_bandMesh, BandStart, BandEnd, OuterRadius - RingThickness * 0.5f, OuterRadius + RingThickness * 0.5f, BandColor);
        }

        static void BuildArc(Mesh mesh, float from01, float to01, float inner, float outer, Color col)
        {
            int segs = Mathf.Max(2, Mathf.RoundToInt((to01 - from01) * RingSegments * 2f));
            var verts = new Vector3[(segs + 1) * 2];
            var colors = new Color[verts.Length];
            var tris = new int[segs * 6];
            for (int i = 0; i <= segs; i++)
            {
                float t = Mathf.Lerp(from01, to01, i / (float)segs);
                float rad = (90f - t * 360f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
                colors[i * 2] = col;
                colors[i * 2 + 1] = col;
            }

            for (int i = 0; i < segs; i++)
            {
                int vi = i * 2;
                int ti = i * 6;
                tris[ti] = vi; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 3;
                tris[ti + 3] = vi; tris[ti + 4] = vi + 3; tris[ti + 5] = vi + 2;
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        void BuildCore()
        {
            _lineMat = MakeMat();
            _diamond = MakeLine("Diamond", 5, 0.012f);
            _diamond.loop = true;
            _diamond.SetPositions(new[]
            {
                new Vector3(0f, Diamond, 0f),
                new Vector3(Diamond, 0f, 0f),
                new Vector3(0f, -Diamond, 0f),
                new Vector3(-Diamond, 0f, 0f),
                new Vector3(0f, Diamond, 0f)
            });

            _dot = MakeLine("Dot", 8, 0.010f);
            _dot.loop = true;
            var dot = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                dot[i] = new Vector3(Mathf.Cos(a) * Dot, Mathf.Sin(a) * Dot, 0f);
            }

            _dot.SetPositions(dot);

            _ticks = new LineRenderer[4];
            Vector2[] axes = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            string[] names = { "TickN", "TickE", "TickS", "TickW" };
            for (int i = 0; i < 4; i++)
            {
                LineRenderer lr = MakeLine(names[i], 2, 0.011f);
                Vector3 d = new Vector3(axes[i].x, axes[i].y, 0f);
                lr.SetPosition(0, d * TickInner);
                lr.SetPosition(1, d * TickOuter);
                _ticks[i] = lr;
            }
        }

        void BuildRing()
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(transform, false);
            _ringFilter = go.AddComponent<MeshFilter>();
            _ringRend = go.AddComponent<MeshRenderer>();
            _ringMesh = new Mesh { name = "ChargeRing" };
            _ringFilter.sharedMesh = _ringMesh;
            _ringMat = MakeMat();
            _ringRend.sharedMaterial = _ringMat;
            _ringRend.sortingLayerName = JianHaiArtCatalog.LayerUi;
            _ringRend.sortingOrder = Sorting;
            _ringRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringRend.receiveShadows = false;
            _ringRend.allowOcclusionWhenDynamic = false;
        }

        void RebuildRing()
        {
            if (_ringMesh == null)
                return;

            float fill = _progress;
            Color col = _green ? RingGreen : (_focus ? FocusColor : (_progress > 0.001f ? RingWarm : RingIdle));
            if (_ringMat != null)
                _ringMat.color = col;

            if (fill <= 0.001f)
            {
                _ringMesh.Clear();
                return;
            }

            int segs = RingSegments;
            int shown = fill >= 0.999f ? segs : Mathf.Max(1, Mathf.RoundToInt(fill * segs));
            float inner = Mathf.Max(0.02f, OuterRadius - RingThickness * 0.5f);
            float outer = OuterRadius + RingThickness * 0.5f;

            int vertCount = (shown + 1) * 2;
            var verts = new Vector3[vertCount];
            var colors = new Color[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[shown * 6];

            for (int i = 0; i <= shown; i++)
            {
                float t = fill >= 0.999f ? i / (float)segs : i / (float)shown * fill;
                float deg = 90f - t * 360f;
                float rad = deg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                int vi = i * 2;
                verts[vi] = dir * inner;
                verts[vi + 1] = dir * outer;
                colors[vi] = col;
                colors[vi + 1] = col;
                uvs[vi] = new Vector2(0f, t);
                uvs[vi + 1] = new Vector2(1f, t);
            }

            for (int i = 0; i < shown; i++)
            {
                int vi = i * 2;
                int ti = i * 6;
                tris[ti] = vi;
                tris[ti + 1] = vi + 1;
                tris[ti + 2] = vi + 3;
                tris[ti + 3] = vi;
                tris[ti + 4] = vi + 3;
                tris[ti + 5] = vi + 2;
            }

            _ringMesh.Clear();
            _ringMesh.vertices = verts;
            _ringMesh.colors = colors;
            _ringMesh.uv = uvs;
            _ringMesh.triangles = tris;
            _ringMesh.RecalculateBounds();
        }

        void ApplyVisibility()
        {
            SetLine(_diamond, _coreOn);
            SetLine(_dot, _coreOn);
            if (_ticks != null)
            {
                for (int i = 0; i < _ticks.Length; i++)
                    SetLine(_ticks[i], _coreOn);
            }

            if (_ringRend != null)
                _ringRend.enabled = _ringOn && _progress > 0.001f;
            if (_bandRend != null)
                _bandRend.enabled = _ringOn;
        }

        LineRenderer MakeLine(string name, int points, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMat;
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.positionCount = points;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.startColor = CoreColor;
            lr.endColor = CoreColor;
            lr.sortingLayerName = JianHaiArtCatalog.LayerUi;
            lr.sortingOrder = Sorting + 1;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false;
            return lr;
        }

        static void SetLine(LineRenderer lr, bool on)
        {
            if (lr != null)
                lr.enabled = on;
        }

        static Material MakeMat()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");
            var mat = new Material(shader);
            mat.color = Color.white;
            if (mat.HasProperty("_MainTex"))
                mat.mainTexture = Texture2D.whiteTexture;
            return mat;
        }
    }
}
