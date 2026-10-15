using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Vision;

namespace RogueShooter.Demo
{
    /// <summary>Independent sliding-door sample room. No production map references.</summary>
    [ExecuteAlways]
    public sealed class DoorSlideArtSample : MonoBehaviour
    {
        public Texture2D WoodTexture;
        public int Selected;
        public bool AutoDemo;
        public SlidingDoorSampleView[] SlidingDoors { get; private set; }
        public Stage1HingedDoorView WideComparison { get; private set; }
        public Transform Walker { get; private set; }
        public BoxCollider2D WideBlocker { get; private set; }
        readonly List<Object> _owned = new List<Object>();
        readonly Dictionary<Color, Material> _colors = new Dictionary<Color, Material>();
        Transform _generated;
        Mesh _quad;
        float _wideTarget, _demoClock;
        static readonly string[] Labels = { "正面窄双滑门 · 4u", "东侧双滑门 · 8u", "正面单滑门 · 2.5u", "西侧双滑门 · 8u", "宽双开门对照 · 8u" };

        void OnEnable() { if (WoodTexture != null) Rebuild(); }
        void OnDisable() { Release(); }

        public void Rebuild()
        {
            Release();
            var root = new GameObject("Sliding door sample generated"); root.transform.SetParent(transform, false); _generated = root.transform;
            var room = Node("SLIDE_ROOM", 0, 0, 28, 22);
            var nodes = new[] { room, Node("N_WIDE", -7, 15, 8, 8), Node("N_NARROW", 6, 15, 4, 8),
                Node("E_HALL", 18, 0, 8, 8), Node("W_HALL", -18, 0, 8, 8), Node("S_SINGLE", 0, -15, 2.5f, 8) };
            Stage1IsoArt.BuildFloors(new Stage1Maze { Seed = 42, TemplateId = "SlidingStudy", Nodes = nodes, Edges = new MazeEdge[0] }, _generated);
            NorthWall(-14, -11, 11); NorthWall(-3, 4, 11); NorthWall(8, 14, 11);
            SouthWall(-14, -1.25f, -11); SouthWall(1.25f, 14, -11);
            SideWall(-14, -11, -4); SideWall(-14, 4, 11); SideWall(14, -11, -4); SideWall(14, 4, 11);
            NorthHall(-11, -3, 11, 19); NorthHall(4, 8, 11, 19);
            SouthHall(-1.25f, 1.25f, -19, -11);
            EastWestHall(14, 22); EastWestHall(-22, -14);
            SlidingDoors = new SlidingDoorSampleView[4];
            SlidingDoors[0] = Door("Narrow front sliding door", new Vector3(6, 11), 'N', 4, false);
            SlidingDoors[1] = Door("East sliding door", new Vector3(14, 0), 'E', 8, false);
            SlidingDoors[2] = Door("Single front sliding door", new Vector3(0, -11), 'S', 2.5f, true);
            SlidingDoors[3] = Door("West sliding door", new Vector3(-14, 0), 'W', 8, false);
            var comparison = new GameObject("Wide hinged comparison"); comparison.transform.SetParent(_generated, false);
            comparison.transform.localPosition = new Vector3(-7, 11);
            WideComparison = comparison.AddComponent<Stage1HingedDoorView>(); WideComparison.Initialize('N', 8); WideComparison.SetPose(0);
            WideBlocker = comparison.AddComponent<BoxCollider2D>(); WideBlocker.size = new Vector2(8, .28f); _wideTarget = 0;
            Decorate(room);
            var player = new GameObject("Sample walker"); player.transform.SetParent(_generated, false);
            var rb = player.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.AddComponent<CircleCollider2D>().radius = Stage1Coin.BodyRadius;
            player.AddComponent<SpriteRenderer>(); player.AddComponent<SortingGroup>().sortingOrder = 0;
            player.AddComponent<Stage1IsoActor>().Bind("archer"); player.AddComponent<PlayerMotor2D>().Configure(6);
            Walker = player.transform;
            foreach (var item in _generated.GetComponentsInChildren<Transform>(true)) item.gameObject.hideFlags = HideFlags.DontSave;
            Focus(Selected);
        }

        static MazeNode Node(string id, float x, float y, float w, float h)
        {
            return new MazeNode { Id = id, Kind = MazeNodeKind.Start, Center = new MazeVec2(x, y), Width = w, Height = h, NeighborIds = new string[0] };
        }

        SlidingDoorSampleView Door(string name, Vector3 position, char rim, float width, bool single)
        {
            var go = new GameObject(name); go.transform.SetParent(_generated, false); go.transform.localPosition = position;
            var door = go.AddComponent<SlidingDoorSampleView>(); door.Initialize(rim, width, single, WoodTexture); return door;
        }

        public void Focus(int index)
        {
            Selected = Mathf.Clamp(index, 0, 4);
            if (Walker == null) return;
            Transform gate = Selected == 4 ? WideComparison.transform : SlidingDoors[Selected].transform;
            char rim = Selected == 4 ? 'N' : SlidingDoors[Selected].Rim;
            Vector3 inward = rim == 'N' ? Vector3.down : rim == 'S' ? Vector3.up : rim == 'E' ? Vector3.left : Vector3.right;
            Walker.position = gate.position + inward * 2.5f + ((rim == 'E' || rim == 'W') ? Vector3.up * .75f : Vector3.zero);
            var rb = Walker.GetComponent<Rigidbody2D>(); rb.position = Walker.position; rb.velocity = Vector2.zero;
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow2D>();
                if (follow != null) { follow.SetFaceOn(16); follow.SetTarget(Walker); }
                else cam.transform.position = Walker.position + Vector3.back * 16;
            }
        }

        public void AnimateSelected(float value)
        {
            if (Selected == 4) { _wideTarget = Mathf.Clamp01(value); if (WideBlocker != null) WideBlocker.enabled = value < .98f || WideComparison.Open01 < .98f; }
            else if (SlidingDoors != null) SlidingDoors[Selected].AnimateTo(value);
        }

        public void SetSelectedPose(float value)
        {
            if (Selected == 4) { _wideTarget = value; WideComparison.SetPose(value); WideBlocker.enabled = value < .98f; }
            else SlidingDoors[Selected].SetPoseImmediate(value);
        }

        float CurrentPose => Selected == 4 ? WideComparison.Open01 : SlidingDoors[Selected].Open01;

        void Update()
        {
            if (!Application.isPlaying || SlidingDoors == null) return;
            for (int i = 0; i < 5; i++) if (Input.GetKeyDown(KeyCode.F1 + i)) Focus(i);
            if (Input.GetKeyDown(KeyCode.Alpha1)) AnimateSelected(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) AnimateSelected(.5f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) AnimateSelected(1);
            if (Input.GetKeyDown(KeyCode.Space)) AnimateSelected(CurrentPose > .5f ? 0 : 1);
            if (WideComparison != null)
            {
                WideComparison.SetPose(Mathf.MoveTowards(WideComparison.Open01, _wideTarget, Time.deltaTime / .65f));
                WideBlocker.enabled = _wideTarget < .98f || WideComparison.Open01 < .98f;
            }
            if (AutoDemo)
            {
                _demoClock += Time.deltaTime;
                if (_demoClock > 1.8f) { _demoClock = 0; AnimateSelected(CurrentPose > .5f ? 0 : 1); }
            }
        }

        void Decorate(MazeNode room)
        {
            var items = new List<GameObject>();
            Stage1IsoArt.BuildRoomShell(room, new Stage1Maze { Nodes = new[] { room }, Edges = new MazeEdge[0] }, _generated, items, 42, new List<GameObject>());
            foreach (var item in items)
                if (item != null && !(item.name.StartsWith("Brazier") || item.name == "Living flame" || item.name == "Warm wash" || item.name.StartsWith("Dressing"))) DestroyImmediate(item);
        }

        void NorthHall(float left, float right, float bottom, float top)
        {
            SideWall(left, bottom, top, true); SideWall(right, bottom, top, false); NorthWall(left, right, top);
        }
        void SouthHall(float left, float right, float bottom, float top)
        {
            SideWall(left, bottom, top, true); SideWall(right, bottom, top, false); SouthWall(left, right, bottom);
        }
        void EastWestHall(float left, float right)
        {
            NorthWall(left, right, 4); SouthWall(left, right, -4);
            SideWall(left == 14 ? right : left, -4, 4);
        }

        void NorthWall(float left, float right, float y)
        {
            Solid("Masonry wall", left, y, right - left, 1.45f, new Color(.20f, .24f, .28f), -16);
            for (int row = 0; row < 3; row++)
            {
                Solid("Stone horizontal joint", left, y + row * .48f, right - left, .035f, new Color(.10f, .13f, .16f), -15);
                for (float x = left + (row % 2 == 0 ? 1.2f : 2.4f); x < right; x += 2.4f)
                    Solid("Stone vertical joint", x, y + row * .48f, .025f, .46f, new Color(.10f, .13f, .16f), -15);
            }
            Solid("Wall cornice", left, y + 1.45f, right - left, .18f, new Color(.27f, .31f, .35f), -14);
            Solid("Wall foot", left, y - .16f, right - left, .18f, new Color(.11f, .14f, .17f), -12);
            Collision("Wall collision", new Vector2((left + right) * .5f, y), new Vector2(right - left, .28f));
        }
        void SouthWall(float left, float right, float y)
        {
            Solid("Low plinth", left, y - .45f, right - left, .45f, new Color(.16f, .20f, .24f), -11);
            Solid("Low plinth edge", left, y - .08f, right - left, .10f, new Color(.29f, .33f, .36f), -10);
            Collision("Plinth collision", new Vector2((left + right) * .5f, y), new Vector2(right - left, .28f));
        }
        void SideWall(float x, float bottom, float top, bool? west = null)
        {
            float outside = (west ?? (x < 0)) ? x - .55f : x;
            Solid("Cutaway wall", outside, bottom, .55f, top - bottom, new Color(.20f, .24f, .28f), -30);
            Solid("Cutaway highlight", outside, bottom, .08f, top - bottom, new Color(.30f, .34f, .37f), -29);
            for (float y = bottom; y < top; y += 1) Solid("Cutaway joint", outside, y, .55f, .035f, new Color(.10f, .13f, .16f), -28);
            Collision("Side wall collision", new Vector2(x, (bottom + top) * .5f), new Vector2(.28f, top - bottom));
        }

        void Collision(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name); go.transform.SetParent(_generated, false); go.transform.localPosition = position;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        void Solid(string name, float x, float y, float width, float height, Color color, int order)
        {
            if (_quad == null)
            {
                _quad = new Mesh { name = "Sample wall quad" }; _owned.Add(_quad);
                _quad.vertices = new[] { Vector3.zero, Vector3.right, new Vector3(1, 1, 0), Vector3.up };
                _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; _quad.RecalculateBounds();
            }
            Material mat;
            if (!_colors.TryGetValue(color, out mat))
            {
                mat = new Material(Shader.Find("Sprites/Default")) { color = color, mainTexture = Texture2D.whiteTexture };
                _colors.Add(color, mat); _owned.Add(mat);
            }
            var go = new GameObject(name); go.transform.SetParent(_generated, false);
            go.transform.localPosition = new Vector3(x, y); go.transform.localScale = new Vector3(width, height, 1);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat; renderer.sortingOrder = order;
        }

        void Release()
        {
            if (_generated != null) DestroyImmediate(_generated.gameObject);
            _generated = null; SlidingDoors = null; WideComparison = null; Walker = null; WideBlocker = null;
            foreach (var item in _owned) if (item != null) DestroyImmediate(item);
            _owned.Clear(); _colors.Clear(); _quad = null;
        }

        void OnGUI()
        {
            if (!Application.isPlaying || SlidingDoors == null) return;
            GUI.Box(new Rect(12, 12, 285, 208), "嵌墙滑门样板 · " + Labels[Selected]);
            for (int i = 0; i < 5; i++) if (GUI.Button(new Rect(24, 40 + i * 25, 260, 23), "F" + (i + 1) + "  " + Labels[i])) Focus(i);
            if (GUI.Button(new Rect(24, 169, 76, 24), "关门 · 1")) AnimateSelected(0);
            if (GUI.Button(new Rect(107, 169, 76, 24), "半开 · 2")) AnimateSelected(.5f);
            if (GUI.Button(new Rect(190, 169, 94, 24), "全开 · 3")) AnimateSelected(1);
            AutoDemo = GUI.Toggle(new Rect(24, 197, 100, 20), AutoDemo, "自动开关");
            GUI.Label(new Rect(126, 197, 160, 20), "WASD移动 / 空格开关");
        }
    }
}
