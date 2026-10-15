using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RogueShooter.Demo
{
    /// <summary>Production version of the oak/iron door study. Floor-space hinges
    /// stay fixed; height is projected separately for each wall orientation.</summary>
    public sealed class Stage1HingedDoorView : MonoBehaviour
    {
        public const string TextureResource = "JianHaiDoor/door_oak_iron_leaf_v01";
        public float Open01 { get; private set; } = 1f;
        public char Rim { get; private set; }
        public float ClearWidth { get; private set; }
        public const float OpeningSeconds = .65f;
        readonly List<Object> _owned = new List<Object>();
        readonly Mesh[] _faces = new Mesh[2], _tops = new Mesh[2], _edges = new Mesh[2], _shadows = new Mesh[2];
        readonly Transform[] _leaves = new Transform[2];
        Vector3 _tangent, _inward, _height;
        Material _stone, _wood, _iron, _edge, _shadow;

        public void Initialize(char rim, float width)
        {
            Rim = rim; ClearWidth = width;
            bool side = rim == 'E' || rim == 'W';
            _tangent = side ? Vector3.up : Vector3.right;
            _inward = rim == 'N' ? Vector3.down : rim == 'S' ? Vector3.up
                : rim == 'E' ? Vector3.left : Vector3.right;
            _height = side ? new Vector3(rim == 'E' ? .75f : -.75f, 1.65f, 0) : Vector3.up * 2.25f;
            var texture = Resources.Load<Texture2D>(TextureResource);
            if (texture == null) Debug.LogError("Door texture missing: Resources/" + TextureResource, this);
            _stone = MakeMaterial("Door stone", Texture2D.whiteTexture, Color.white);
            _wood = MakeMaterial("Door oak and iron", texture, Color.white);
            _iron = MakeMaterial("Door top iron", Texture2D.whiteTexture, new Color(.24f, .27f, .29f));
            _edge = MakeMaterial("Door thickness", Texture2D.whiteTexture, new Color(.13f, .16f, .18f));
            _shadow = MakeMaterial("Door contact", Texture2D.whiteTexture, new Color(0, 0, 0, .24f));
            BuildFrame();
            for (int i = 0; i < 2; i++)
            {
                var leaf = new GameObject(i == 0 ? "Left hinged leaf" : "Right hinged leaf");
                leaf.transform.SetParent(transform, false); _leaves[i] = leaf.transform;
                // Group pivot follows the ground footprint, rather than the tall face's centre.
                leaf.AddComponent<SortingGroup>().sortingOrder = 0;
                _faces[i] = MakeQuad("Oak face", _wood, 0, leaf.transform);
                _tops[i] = MakeQuad("Top iron thickness", _iron, 1, leaf.transform);
                _edges[i] = MakeQuad("Free edge thickness", _edge, 2, leaf.transform);
                _shadows[i] = MakeQuad("Moving contact shadow", _shadow, -710, transform);
            }
            SetPose(1f);
        }

        public void SetPose(float open01)
        {
            Open01 = Mathf.Clamp01(open01);
            if (_faces[0] == null) return;
            float angle = Open01 * 100f * Mathf.Deg2Rad;
            float w = ClearWidth * .5f - .015f;
            for (int i = 0; i < 2; i++)
            {
                float dir = i == 0 ? 1f : -1f;
                Vector3 foot = _tangent * (i == 0 ? -ClearWidth * .5f : ClearWidth * .5f);
                Vector3 end = foot + _tangent * (dir * w * Mathf.Cos(angle)) + _inward * (w * Mathf.Sin(angle) * .45f);
                Vector3 pivot = (foot + end) * .5f;
                _leaves[i].localPosition = pivot;
                Vector3 a = foot - pivot, b = end - pivot;
                Vector3 depth = _height.normalized * .11f + _tangent * (.035f * dir);
                WriteQuad(_faces[i], a, b, b + _height, a + _height);
                WriteQuad(_tops[i], a + _height, b + _height, b + _height + depth, a + _height + depth);
                WriteQuad(_edges[i], b, b + depth, b + _height + depth, b + _height);
                Vector3 shadowOffset = new Vector3(.12f, -.28f, 0);
                WriteQuad(_shadows[i], foot, end, end + shadowOffset, foot + shadowOffset);
                float shade = .88f - Open01 * .13f;
                var color = new Color(.62f * shade, .67f * shade, .72f * shade);
                _faces[i].colors = new[] { color, color, color, color };
            }
        }

        void BuildFrame()
        {
            // Combine static stone courses into two meshes, not one renderer per stone.
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s < 0 ? -ClearWidth * .5f - .65f : ClearWidth * .5f + .05f;
                for (int row = 0; row < 5; row++)
                {
                    float v = row % 2 == 0 ? .015f : -.008f;
                    AddRect(vertices, colors, triangles, x, row * .48f / 2.25f, .6f, .45f / 2.25f, new Color(.24f + v, .28f + v, .31f + v));
                    AddRect(vertices, colors, triangles, x, (row * .48f + .40f) / 2.25f, .6f, .045f / 2.25f, new Color(.33f, .37f, .40f));
                }
                AddRect(vertices, colors, triangles, x - .08f, -.16f / 2.25f, .76f, .18f / 2.25f, new Color(.13f, .16f, .19f));
                AddRect(vertices, colors, triangles, x - .07f, 2.4f / 2.25f, .74f, .12f / 2.25f, new Color(.32f, .36f, .39f));
                for (int h = 0; h < 2; h++)
                    AddRect(vertices, colors, triangles, s * ClearWidth * .5f - .075f, (.48f + h * 1.13f) / 2.25f, .15f, .30f / 2.25f, new Color(.10f, .12f, .14f));
            }
            AddRect(vertices, colors, triangles, -ClearWidth * .5f - .65f, 2.46f / 2.25f, ClearWidth + 1.3f, .13f / 2.25f, new Color(.24f, .28f, .31f));
            var frame = MakeQuad("Stone jambs and lintel", _stone, -5, transform);
            frame.Clear(); frame.SetVertices(vertices); frame.SetColors(colors); frame.SetTriangles(triangles, 0); frame.RecalculateBounds();
            var threshold = MakeQuad("Worn stone threshold", _stone, -700, transform);
            var a = -_tangent * (ClearWidth * .5f) - _inward * .10f;
            var b = _tangent * (ClearWidth * .5f) - _inward * .10f;
            WriteQuad(threshold, a, b, b + _inward * .26f, a + _inward * .26f);
            var c = VertexColor(new Color(.29f, .32f, .34f)); threshold.colors = new[] { c, c, c, c };
        }

        void AddRect(List<Vector3> vertices, List<Color> colors, List<int> indices, float x, float y, float w, float h, Color color)
        {
            int i = vertices.Count; var a = _tangent * x + _height * y;
            vertices.Add(a); vertices.Add(a + _tangent * w); vertices.Add(a + _tangent * w + _height * h); vertices.Add(a + _height * h);
            for (int j = 0; j < 4; j++) colors.Add(VertexColor(color));
            indices.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        Material MakeMaterial(string name, Texture texture, Color color)
        {
            var material = new Material(Shader.Find("Sprites/Default")) { name = name, mainTexture = texture, color = color };
            _owned.Add(material); return material;
        }

        // Material colour properties are converted by Unity; vertex colours are not.
        static Color VertexColor(Color color)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
        }

        Mesh MakeQuad(string name, Material material, int order, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name }; mesh.MarkDynamic(); _owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return mesh;
        }

        static void WriteQuad(Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            mesh.vertices = new[] { a, b, c, d };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            foreach (var item in _owned)
                if (item != null) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            _owned.Clear();
        }
    }
}
