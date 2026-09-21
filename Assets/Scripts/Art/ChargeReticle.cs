using UnityEngine;

namespace RogueShooter.Art
{
    /// <summary>
    /// Fig1 charge aim: center dot + diamond + 4 cardinal ticks + thick ring
    /// that fills clockwise from 12 o'clock. Fig2 scale: compact in-camera
    /// (outer radius ~half a character head, ≤ previous 0.72u assembly).
    /// World-space; caller places via ScreenToWorld. Not a player child.
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

        LineRenderer _diamond;
        LineRenderer _dot;
        LineRenderer[] _ticks;
        MeshFilter _ringFilter;
        MeshRenderer _ringRend;
        Mesh _ringMesh;
        Material _lineMat;
        Material _ringMat;
        float _progress;
        bool _green;
        bool _coreOn;
        bool _ringOn;

        public float Progress => _progress;

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
            Color col = _green ? RingGreen : (_progress > 0.001f ? RingWarm : RingIdle);
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
