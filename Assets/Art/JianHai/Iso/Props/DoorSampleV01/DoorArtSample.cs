using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Player;

namespace RogueShooter.Demo
{
    /// <summary>Independent north-facing doorway art study. No production gate references.</summary>
    [ExecuteAlways]
    public sealed class DoorArtSample : MonoBehaviour
    {
        public Texture2D LeafTexture;
        [Range(0f, 1f)] public float PreviewOpen01;
        public float OpeningSeconds = 0.65f;
        public bool AutoDemo;
        public const float ClearWidth = 8f;
        public const float GateY = 3f;
        public const float LeafHeight = 2.25f;
        public const float OpenDegrees = 100f;
        public BoxCollider2D ClosedBlocker { get; private set; }
        public Transform Walker { get; private set; }
        public float EffectiveClearWidth => PreviewOpen01 >= 0.999f ? ClearWidth : 0f;

        readonly List<Object> _owned = new List<Object>();
        Mesh[] _faces, _tops, _edges, _shadows;
        MeshRenderer[] _faceRenderers, _topRenderers, _edgeRenderers;
        Material _wood;
        float _target, _shown = -1f, _demoClock;
        bool _animating;
        Transform _generated;

        void OnEnable() { if (LeafTexture != null) Rebuild(); }
        void OnDisable() { Release(); }

        public void Rebuild()
        {
            Release();
            var root = new GameObject("Door sample generated");
            root.transform.SetParent(transform, false);
            _generated = root.transform;
            var room = new MazeNode { Id = "SAMPLE", Kind = MazeNodeKind.Normal,
                Center = new MazeVec2(0f, -8f), Width = 28f, Height = 22f,
                NeighborIds = new[] { "HALL" } };
            var hall = new MazeNode { Id = "HALL", Kind = MazeNodeKind.Start,
                Center = new MazeVec2(0f, 20f), Width = 8f, Height = 12f,
                NeighborIds = new[] { "SAMPLE" } };
            var edge = new MazeEdge { FromId = "SAMPLE", ToId = "HALL",
                From = new MazeVec2(0f, GateY), To = new MazeVec2(0f, 14f),
                Width = ClearWidth, Length = 11f, Points = new[] { new MazeVec2(0f, GateY), new MazeVec2(0f, 14f) } };
            var maze = new Stage1Maze { Seed = 42, TemplateId = "DoorStudy",
                Nodes = new[] { room, hall }, Edges = new[] { edge } };
            Stage1IsoArt.BuildFloors(maze, _generated);
            var shell = new List<GameObject>();
            var oldGates = new List<GameObject>();
            Stage1IsoArt.BuildRoomShell(room, maze, _generated, shell, 42, oldGates);
            foreach (var old in oldGates) { old.SetActive(false); DestroyImmediate(old); }
            foreach (var item in shell)
                if (item != null && (item.name == "North cornice SAMPLE" || item.name == "North cornice shadow SAMPLE"))
                    DestroyImmediate(item);
            SolidRect("Left wall cornice", -14.15f, GateY + 1.45f, 9.5f, .19f,
                new Color(.27f, .31f, .35f), -14);
            SolidRect("Right wall cornice", 4.65f, GateY + 1.45f, 9.5f, .19f,
                new Color(.27f, .31f, .35f), -14);

            // Stone jambs sit outside the full eight-unit opening.
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s < 0 ? -4.65f : 4.05f;
                for (int row = 0; row < 5; row++)
                {
                    float variation = (row % 2 == 0 ? 0.015f : -0.008f);
                    SolidRect("Jamb stone", x, GateY + row * .48f, .6f, .45f,
                        new Color(.24f + variation, .28f + variation, .31f + variation), -5);
                    SolidRect("Jamb bevel", x, GateY + row * .48f + .40f, .6f, .045f,
                        new Color(.33f, .37f, .40f), -4);
                }
                SolidRect("Jamb foot", x - .08f, GateY - .16f, .76f, .18f,
                    new Color(.13f, .16f, .19f), -3);
                SolidRect("Jamb cap", x - .07f, GateY + 2.4f, .74f, .12f,
                    new Color(.32f, .36f, .39f), -3);
                float hx = s * 4f - .075f;
                for (int h = 0; h < 2; h++)
                {
                    SolidRect("Fixed hinge plate", hx, GateY + .48f + h * 1.13f, .15f, .30f,
                        new Color(.10f, .12f, .14f), 6);
                    SolidRect("Hinge knuckle", hx + .04f, GateY + .53f + h * 1.13f, .065f, .20f,
                        new Color(.28f, .31f, .33f), 7);
                }
            }
            SolidRect("Thin rear lintel", -4.65f, GateY + 2.46f, 9.3f, .13f,
                new Color(.24f, .28f, .31f), -25);
            SolidRect("Threshold contact shadow", -4.08f, GateY - .26f, 8.16f, .34f,
                new Color(0f, 0f, 0f, .22f), -705);
            for (int i = 0; i < 8; i++)
            {
                float x = -4f + i;
                SolidRect("Threshold stone", x + .015f, GateY - .16f, .97f, .26f,
                    new Color(.28f + (i % 3) * .01f, .31f + (i % 3) * .01f, .33f + (i % 3) * .01f), -700);
                SolidRect("Threshold worn edge", x + .04f, GateY - .14f, .9f, .035f,
                    new Color(.36f, .38f, .39f), -699);
            }

            _wood = Material("Door oak and iron", Color.white, LeafTexture);
            var top = Material("Door top iron", new Color(.24f, .27f, .29f), Texture2D.whiteTexture);
            var side = Material("Door thickness", new Color(.13f, .16f, .18f), Texture2D.whiteTexture);
            var shadow = Material("Door contact", new Color(0f, 0f, 0f, .24f), Texture2D.whiteTexture);
            _faces = new Mesh[2]; _tops = new Mesh[2]; _edges = new Mesh[2]; _shadows = new Mesh[2];
            _faceRenderers = new MeshRenderer[2];
            _topRenderers = new MeshRenderer[2]; _edgeRenderers = new MeshRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                string leaf = i == 0 ? "Left" : "Right";
                _faces[i] = Quad(leaf + " hinged face", _wood, 2, out _faceRenderers[i]);
                MeshRenderer unused;
                _tops[i] = Quad(leaf + " top thickness", top, 3, out _topRenderers[i]);
                _edges[i] = Quad(leaf + " edge thickness", side, 4, out _edgeRenderers[i]);
                _shadows[i] = Quad(leaf + " moving contact shadow", shadow, -710, out unused);
            }
            var blocker = new GameObject("Closed opening blocker");
            blocker.transform.SetParent(_generated, false);
            blocker.transform.localPosition = new Vector3(0f, GateY, 0f);
            ClosedBlocker = blocker.AddComponent<BoxCollider2D>();
            ClosedBlocker.size = new Vector2(ClearWidth, .28f);
            WallCollider("Left wall collision", -9f, 10f);
            WallCollider("Right wall collision", 9f, 10f);
            CreateWalker();
            SetPoseImmediate(PreviewOpen01);
            foreach (var t in _generated.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
        }

        void WallCollider(string name, float x, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(_generated, false);
            go.transform.localPosition = new Vector3(x, GateY, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(width, .3f);
        }

        void CreateWalker()
        {
            var go = new GameObject("Player scale and passage reference");
            go.transform.SetParent(_generated, false); go.transform.localPosition = new Vector3(0f, .4f, 0f);
            var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = Stage1Coin.BodyRadius;
            go.AddComponent<SpriteRenderer>().sortingOrder = 10;
            go.AddComponent<Stage1IsoActor>().Bind("archer");
            go.AddComponent<PlayerMotor2D>().Configure(6f);
            Walker = go.transform;
        }

        public void SetPoseImmediate(float open01)
        {
            PreviewOpen01 = Mathf.Clamp01(open01); _target = PreviewOpen01; _animating = false;
            DrawPose();
        }

        public void AnimateTo(float open01)
        {
            _target = Mathf.Clamp01(open01); _animating = true;
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) AnimateTo(0f);
                if (Input.GetKeyDown(KeyCode.Alpha2)) AnimateTo(.5f);
                if (Input.GetKeyDown(KeyCode.Alpha3)) AnimateTo(1f);
                if (Input.GetKeyDown(KeyCode.Space)) AnimateTo(_target > .5f ? 0f : 1f);
                if (AutoDemo)
                {
                    _demoClock += Time.unscaledDeltaTime;
                    if (_demoClock >= 1.8f) { _demoClock = 0f; AnimateTo(_target > .5f ? 0f : 1f); }
                }
                if (_animating)
                {
                    PreviewOpen01 = Mathf.MoveTowards(PreviewOpen01, _target,
                        Time.unscaledDeltaTime / Mathf.Max(.1f, OpeningSeconds));
                    if (Mathf.Abs(PreviewOpen01 - _target) < .0001f) _animating = false;
                }
            }
            if (_faces != null && !Mathf.Approximately(_shown, PreviewOpen01)) DrawPose();
        }

        void DrawPose()
        {
            if (_faces == null) return;
            _shown = PreviewOpen01;
            float a = PreviewOpen01 * OpenDegrees * Mathf.Deg2Rad;
            float w = ClearWidth * .5f - .015f;
            for (int i = 0; i < 2; i++)
            {
                float dir = i == 0 ? 1f : -1f;
                Vector3 foot = new Vector3(i == 0 ? -4f : 4f, GateY, 0f);
                Vector3 end = foot + new Vector3(dir * w * Mathf.Cos(a), -w * Mathf.Sin(a) * .45f, 0f);
                Vector3 height = Vector3.up * LeafHeight;
                Vector3 depth = new Vector3(.035f * dir, .11f, 0f);
                WriteQuad(_faces[i], foot, end, end + height, foot + height);
                WriteQuad(_tops[i], foot + height, end + height, end + height + depth, foot + height + depth);
                WriteQuad(_edges[i], end, end + depth, end + height + depth, end + height);
                Vector3 shadowOffset = new Vector3(.12f, -.28f, 0f);
                WriteQuad(_shadows[i], foot, end, end + shadowOffset, foot + shadowOffset);
                float shade = .88f - PreviewOpen01 * .13f;
                var colors = new Color[4];
                for (int j = 0; j < 4; j++) colors[j] = new Color(.62f * shade, .67f * shade, .72f * shade, 1f);
                _faces[i].colors = colors;
            }
            if (ClosedBlocker != null) ClosedBlocker.enabled = PreviewOpen01 < .999f;
        }

        void LateUpdate()
        {
            if (_faces == null || Walker == null) return;
            float depth = (ClearWidth * .5f - .015f) * Mathf.Sin(PreviewOpen01 * OpenDegrees * Mathf.Deg2Rad) * .45f;
            int doorOrder = 100 - Mathf.RoundToInt((GateY - depth * .5f) * 10f);
            for (int i = 0; i < 2; i++)
            {
                _faceRenderers[i].sortingOrder = doorOrder;
                _topRenderers[i].sortingOrder = doorOrder + 1;
                _edgeRenderers[i].sortingOrder = doorOrder + 2;
            }
            var actor = Walker.GetComponent<SpriteRenderer>();
            if (actor != null) actor.sortingOrder = 100 - Mathf.RoundToInt(Walker.position.y * 10f);
        }

        Material Material(string name, Color color, Texture texture)
        {
            var mat = new Material(Shader.Find("Sprites/Default")) { name = name, color = color, mainTexture = texture };
            _owned.Add(mat); return mat;
        }

        Mesh Quad(string name, Material material, int order, out MeshRenderer renderer)
        {
            var go = new GameObject(name); go.transform.SetParent(_generated, false);
            var mesh = new Mesh { name = name }; mesh.MarkDynamic(); _owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            WriteQuad(mesh, Vector3.zero, Vector3.right, Vector3.one, Vector3.up);
            return mesh;
        }

        static void WriteQuad(Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool mirror = false)
        {
            mesh.vertices = new[] { a, b, c, d };
            mesh.uv = mirror ? new[] { new Vector2(1, 0), Vector2.zero, Vector2.up, Vector2.one }
                             : new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
        }

        void SolidRect(string name, float x, float y, float width, float height, Color color, int order)
        {
            MeshRenderer unused;
            var mesh = Quad(name, Material(name, color, Texture2D.whiteTexture), order, out unused);
            WriteQuad(mesh, new Vector3(x, y), new Vector3(x + width, y),
                new Vector3(x + width, y + height), new Vector3(x, y + height));
        }

        void Release()
        {
            if (_generated != null) DestroyImmediate(_generated.gameObject);
            _generated = null; _faces = null;
            foreach (var item in _owned) if (item != null) DestroyImmediate(item);
            _owned.Clear(); _shown = -1f;
        }

        void OnGUI()
        {
            if (!Application.isPlaying) return;
            GUI.Box(new Rect(12, 12, 390, 82), "门洞美术样板 · 北向双开门");
            if (GUI.Button(new Rect(24, 42, 84, 25), "关闭 · 1")) AnimateTo(0f);
            if (GUI.Button(new Rect(114, 42, 84, 25), "半开 · 2")) AnimateTo(.5f);
            if (GUI.Button(new Rect(204, 42, 84, 25), "全开 · 3")) AnimateTo(1f);
            AutoDemo = GUI.Toggle(new Rect(300, 45, 90, 25), AutoDemo, "自动演示");
            GUI.Label(new Rect(20, 72, 370, 20), "WASD 移动查看通行；空格切换开关");
        }
    }
}
