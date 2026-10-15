using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using RogueShooter.Iso;

namespace JianHai.ArtSamples
{
    // Rebuilds the three-room turn-corridor play scene with the S1 sample room's
    // continuous-masonry vocabulary. The play driver S1ThreeRoomsPlay binds objects
    // by name, so the anchor names and positions below must stay fixed.
    public static class S1ThreeRoomsBuilder
    {
        public const string Folder = "Assets/ArtSamples/S1MapRoom";
        public const string ScenePath = Folder + "/S1ThreeRooms_TurnCorridor.scene";
        const string Art = "Assets/Art/JianHai/Iso/";
        static Transform root, architecture;
        static Material wash;
        static Mesh softDisc, unitQuad, masonryFace;
        static Material continuousMasonry;
        static Tilemap map;
        static Grid grid;

        // The sample room renders walls above 80; S1ThreeRoomsPlay pins actors at
        // order 20, so the whole wall scheme is the sample's minus 180 and props sit
        // between the walls and the actors.
        const int CutawayOrder = -30, HighlightOrder = -29, JointOrder = -28, BayOrder = -16,
            CorniceOrder = -14, CorniceShadowOrder = -13, FootOrder = -12, PierOrder = -8,
            PierCapOrder = -7, PropOrder = 10, FlameOrder = 14, ActorOrder = 20,
            PlinthOrder = 670, PlinthTopOrder = 671, JambOrder = 630, GateOrder = 930,
            ThresholdOrder = -900, RubbleOrder = -700, FloorOrder = -1000;

        static readonly Color CutawayColor = new Color(.20f, .24f, .28f);
        static readonly Color HighlightColor = new Color(.30f, .34f, .37f);
        static readonly Color JointColor = new Color(.10f, .13f, .16f);
        static readonly Color CorniceColor = new Color(.27f, .31f, .35f);
        static readonly Color FootColor = new Color(.11f, .14f, .17f);
        static readonly Color PierColor = new Color(.24f, .28f, .32f);
        static readonly Color PierCapColor = new Color(.31f, .34f, .37f);
        static readonly Color PlinthColor = new Color(.16f, .20f, .24f);
        static readonly Color PlinthTopColor = new Color(.29f, .33f, .36f);
        static readonly Color JambColor = new Color(.34f, .33f, .29f);
        static readonly Color ThresholdColor = new Color(.26f, .26f, .24f);
        static readonly Color RouteColor = new Color(.90f, .88f, .83f);
        static readonly Color CourtColor = new Color(.90f, .87f, .79f);

        [MenuItem("JianHai/Art Samples/Build S1 Three Rooms Turn Corridor")]
        public static void MenuBuild() { Debug.Log(Build()); }

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before rebuilding the three-room sample.");
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (previous.isDirty) throw new InvalidOperationException("The current scene has unsaved changes. Save it before switching scenes.");
            LoadSharedAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("S1 three rooms turn corridor 28x22").transform;

            BuildFloor();
            architecture = Group("02 Architecture — consistent masonry");
            BuildNormalRoom();
            BuildCorridors();
            BuildChestRoom();
            BuildAltarRoom();

            var focal = Group("03 Focal objects — chest and altar");
            Focus("Chest focus", "Props/jh_iso_prop_chest_closed_00.png", new Vector3(36, 39, 0), .9f, .3f, .25f, -.10f, focal);
            Focus("Altar focus", "Props/jh_iso_prop_altar_00.png", new Vector3(36, 64, 0), 1.5f, .5f, .30f, -.15f, focal);

            var fire = Group("04 Fire and local warmth");
            Vector3[] normalBraziers = { new Vector3(-3, 7.1f, 0), new Vector3(3, 7.1f, 0), new Vector3(-2.5f, -9.3f, 0), new Vector3(2.5f, -9.3f, 0), new Vector3(-10.7f, 6.2f, 0) };
            Vector3[] chestBraziers = { new Vector3(33, 46.1f, 0), new Vector3(39, 46.1f, 0), new Vector3(33.5f, 29.7f, 0), new Vector3(38.5f, 29.7f, 0), new Vector3(25.3f, 45.2f, 0) };
            Vector3[] altarBraziers = { new Vector3(33, 68.1f, 0), new Vector3(39, 68.1f, 0), new Vector3(33.5f, 51.7f, 0), new Vector3(38.5f, 51.7f, 0), new Vector3(25.3f, 67.2f, 0) };
            foreach (var p in normalBraziers) Brazier(p, fire);
            foreach (var p in chestBraziers) Brazier(p, fire);
            foreach (var p in altarBraziers) Brazier(p, fire);

            var dressing = Group("05 Edge dressing — grouped rubble");
            Vector2[] normalRubble = { new Vector2(-11.5f, 8), new Vector2(10.5f, 8), new Vector2(-11.5f, -7), new Vector2(11, -8), new Vector2(-11, 3) };
            Vector2[] chestRubble = { new Vector2(24.5f, 47), new Vector2(46.5f, 47), new Vector2(24.5f, 32), new Vector2(47, 31), new Vector2(25, 42) };
            Vector2[] altarRubble = { new Vector2(24.5f, 69), new Vector2(46.5f, 69), new Vector2(24.5f, 54), new Vector2(47, 53), new Vector2(25, 64) };
            for (int c = 0; c < normalRubble.Length; c++) Rubble(c, normalRubble[c], dressing);
            for (int c = 0; c < chestRubble.Length; c++) Rubble(c + 5, chestRubble[c], dressing);
            for (int c = 0; c < altarRubble.Length; c++) Rubble(c + 10, altarRubble[c], dressing);

            var cast = Group("06 Scale and start — archer");
            var player = Prop("Archer scale 1", "Characters/Archer/jh_archer_idle_s_00.png", new Vector3(0, -3, 0), ActorOrder, cast);
            // Last shadow write leaves the shared Shadow.mat at the sample's (0,0,0,.33).
            Shadow(player.transform.position, .46f, .17f, .33f, player.transform, true);

            var gates = Group("07 Closed gates — cleared rooms open");
            Gate("Gate normal east", new Vector3(14, -2, 0), gates);
            Gate("Gate chest south", new Vector3(36, 28, 0), gates);
            Gate("Gate altar south", new Vector3(36, 50, 0), gates);

            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 6;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.04f, .04f, .06f);
            camGo.AddComponent<AudioListener>();

            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Could not save three-room scene");
            SceneView.RepaintAll();
            return "Saved " + ScenePath + "; rooms stay 28x22, anchors named for S1ThreeRoomsPlay, walls rebuilt in sample vocabulary below actor order 20.";
        }

        static Transform Group(string name) { var go = new GameObject(name); go.transform.SetParent(root, false); return go.transform; }

        static Sprite Load(string path)
        {
            string full = path.StartsWith("Assets/") ? path : Art + path;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(full);
            if (sprite == null) throw new Exception("Missing three-room source: " + full);
            return sprite;
        }

        static void LoadSharedAssets()
        {
            wash = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Generated/GroundWash.mat");
            if (wash == null) { wash = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(wash, Folder + "/Generated/GroundWash.mat"); }
            softDisc = Disc();
            unitQuad = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Generated/UnitQuad.asset");
            if (unitQuad == null)
            {
                unitQuad = new Mesh();
                unitQuad.vertices = new[] { Vector3.zero, Vector3.right, new Vector3(1, 1), Vector3.up };
                unitQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                unitQuad.RecalculateBounds();
                AssetDatabase.CreateAsset(unitQuad, Folder + "/Generated/UnitQuad.asset");
            }
            masonryFace = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Generated/MasonryFace.asset");
            if (masonryFace == null)
            {
                masonryFace = new Mesh();
                AssetDatabase.CreateAsset(masonryFace, Folder + "/Generated/MasonryFace.asset");
                FillMasonryFace(masonryFace);
            }
            continuousMasonry = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Generated/ContinuousMasonry.mat");
            if (continuousMasonry == null)
            {
                continuousMasonry = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(continuousMasonry, Folder + "/Generated/ContinuousMasonry.mat");
                continuousMasonry.mainTexture = Load("Tiles/S1/Wall/jh_iso_wall_s1_l_01.png").texture;
            }
        }

        // Opaque painted face inside the source wall tile, excluding the transparent silhouette.
        static void FillMasonryFace(Mesh m)
        {
            m.vertices = new[] { Vector3.zero, Vector3.right, new Vector3(1, 1), Vector3.up };
            m.uv = new[] { new Vector2(4f / 128, 1 - 180f / 256), new Vector2(60f / 128, 1 - 208f / 256), new Vector2(60f / 128, 1 - 56f / 256), new Vector2(4f / 128, 1 - 28f / 256) };
            m.colors = new[] { new Color(.75f, .80f, .86f), new Color(.75f, .80f, .86f), new Color(.75f, .80f, .86f), new Color(.75f, .80f, .86f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            EditorUtility.SetDirty(m);
        }

        static Tile MakeTile(string id, string file)
        {
            string path = Folder + "/Generated/" + id + ".asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t != null) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = Load(file); t.flags = TileFlags.None; t.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        static int Hash(int a, int b) { unchecked { return (a * 73856093 ^ b * 19349663 ^ 42 * 83492791) & int.MaxValue; } }

        static bool InRect(Vector3 p, float x0, float y0, float x1, float y1)
        {
            return p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1;
        }

        // One isometric tilemap carries all rooms and corridors; each area keeps the
        // sample's quiet-stone base with sparse wear, edge cracks and local inlays.
        static void BuildFloor()
        {
            var gridGo = new GameObject("01 Floor — quiet stone and local inlays"); gridGo.transform.SetParent(root, false);
            grid = gridGo.AddComponent<Grid>(); grid.cellLayout = GridLayout.CellLayout.Isometric; grid.cellSize = new Vector3(1, .5f, 1);
            var go = new GameObject("Editable ground tiles"); go.transform.SetParent(gridGo.transform, false);
            map = go.AddComponent<Tilemap>(); go.AddComponent<TilemapRenderer>().sortingOrder = FloorOrder;
            var clean = MakeTile("QuietStone", "Tiles/S1/Floor/jh_iso_floor_s1_room_00.png");
            var worn = MakeTile("WornStone", "Tiles/S1/Floor/jh_iso_floor_s1_room_01.png");
            var crack = MakeTile("BrokenEdge", "Tiles/S1/Floor/jh_iso_floor_s1_room_02.png");
            int painted = 0;
            for (int u = -80; u <= 200; u++) for (int v = -80; v <= 200; v++)
            {
                var cell = new Vector3Int(u, v, 0);
                var p = grid.GetCellCenterWorld(cell);
                int kind = -1; Vector2 center = Vector2.zero;
                if (InRect(p, -14, -11, 14, 11)) { kind = 0; center = new Vector2(0, 0); }
                else if (InRect(p, 22, 28, 50, 50)) { kind = 1; center = new Vector2(36, 39); }
                else if (InRect(p, 22, 50, 50, 72)) { kind = 2; center = new Vector2(36, 61); }
                else if (InRect(p, 14, -4, 40, 4)) { kind = 3; center = new Vector2(27, 0); }
                else if (InRect(p, 32, 4, 40, 28)) { kind = 4; center = new Vector2(36, 16); }
                if (kind < 0) continue;
                int h = Hash(u, v) % 100;
                Tile tile = h < 92 ? clean : worn;
                float value = .80f + .10f * Mathf.PerlinNoise(p.x * .17f + 17, p.y * .17f + 29);
                Color color = new Color(value * .96f, value * .99f, value);
                Decorate(ref tile, ref color, p, center, kind, h, clean, worn, crack);
                map.SetTile(cell, tile); map.SetColor(cell, color);
                painted++;
            }
            if (painted < 8000) throw new Exception("Floor paint suspiciously small: " + painted);
        }

        // kind 0 start room, 1 chest room, 2 altar room, 3 horizontal corridor, 4 vertical corridor.
        static void Decorate(ref Tile tile, ref Color color, Vector3 p, Vector2 center, int kind, int h, Tile clean, Tile worn, Tile crack)
        {
            float lx = p.x - center.x, ly = p.y - center.y;
            if (kind <= 2 && (Mathf.Abs(lx) > 12.8f || ly > 10) && h > 93) tile = crack;
            if (kind == 3 && Mathf.Abs(ly) > 2.55f && h > 93) tile = crack;
            if (kind == 4 && Mathf.Abs(lx) > 2.55f && h > 93) tile = crack;
            bool route;
            if (kind == 0) route = Mathf.Abs(ly + 2) < 1.5f || (lx > 10.8f && Mathf.Abs(ly + 2) < 2.2f);
            else if (kind == 1) route = (Mathf.Abs(lx) < 1.5f && ly < 2) || (ly < -8.5f && Mathf.Abs(lx) < 2.2f);
            else if (kind == 2) route = (Mathf.Abs(lx) < 1.5f && ly < 3.5f) || (ly < -8.5f && Mathf.Abs(lx) < 2.2f);
            else if (kind == 3) route = Mathf.Abs(ly + 1) < 1.3f;
            else route = Mathf.Abs(lx) < 1.5f;
            if (route) { tile = clean; color = RouteColor; }
            if (kind == 0)
            {
                bool court = (Mathf.Abs(Mathf.Abs(lx) - 6) < .30f && Mathf.Abs(ly) < 4.5f) || (Mathf.Abs(Mathf.Abs(ly) - 4.5f) < .23f && Mathf.Abs(lx) < 6);
                if (court) { tile = worn; color = CourtColor; }
            }
            if (kind == 1 && Mathf.Abs(lx) < 1.7f && Mathf.Abs(ly) < 1.3f) { tile = clean; color = new Color(.83f, .79f, .67f); }
            if (kind == 2 && Mathf.Abs(lx) < 2.4f && Mathf.Abs(ly - 2.8f) < 1.7f) { tile = clean; color = new Color(.69f, .70f, .71f); }
        }

        // --- Wall vocabulary, all axis-aligned bands like the sample room ---

        static void Bays(string label, float x, float y, params float[] widths)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                var go = new GameObject(label + " " + i); go.transform.SetParent(architecture, false);
                go.transform.position = new Vector3(x, y, 0);
                go.transform.localScale = new Vector3(widths[i], 1.45f, 1);
                go.AddComponent<MeshFilter>().sharedMesh = masonryFace;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = continuousMasonry; mr.sortingOrder = BayOrder;
                x += widths[i];
            }
        }

        static void NorthWall(string label, float x0, float y, float w, float[] pierXs)
        {
            int count = Mathf.RoundToInt(w / 1.4f);
            var widths = new float[count];
            for (int i = 0; i < count; i++) widths[i] = w / count;
            Bays(label + " masonry bay", x0, y, widths);
            Band(label + " cornice", new Vector3(x0 - .15f, y + 1.45f, 0), new Vector2(w + .3f, .19f), CorniceColor, CorniceOrder);
            Band(label + " cornice shadow", new Vector3(x0, y + 1.35f, 0), new Vector2(w, .09f), JointColor, CorniceShadowOrder);
            Band(label + " foot", new Vector3(x0, y - .17f, 0), new Vector2(w, .18f), FootColor, FootOrder);
            if (pierXs == null) return;
            foreach (float px in pierXs)
            {
                Band(label + " pier", new Vector3(px - .15f, y, 0), new Vector2(.3f, 1.45f), PierColor, PierOrder);
                Band(label + " pier cap", new Vector3(px - .21f, y + 1.33f, 0), new Vector2(.42f, .13f), PierCapColor, PierCapOrder);
            }
        }

        static void SideWall(string label, float x, float y0, float h)
        {
            Band(label + " stone cutaway", new Vector3(x, y0, 0), new Vector2(.55f, h), CutawayColor, CutawayOrder);
            Band(label + " top highlight", new Vector3(x, y0, 0), new Vector2(.10f, h), HighlightColor, HighlightOrder);
            int joints = Mathf.FloorToInt(h);
            for (int j = 0; j < joints; j++)
                Band(label + " masonry joint", new Vector3(x, y0 + j, 0), new Vector2(.55f, .03f), JointColor, JointOrder);
        }

        static void PlinthLine(string label, float x0, float y, float w)
        {
            Band(label + " low plinth", new Vector3(x0, y - .45f, 0), new Vector2(w, .45f), PlinthColor, PlinthOrder);
            Band(label + " top edge", new Vector3(x0, y - .08f, 0), new Vector2(w, .10f), PlinthTopColor, PlinthTopOrder);
        }

        // A wall shared between the room below and the room above: the masonry face
        // pokes at most .6 above the boundary and stops flush with the upper room's
        // walkable edge, so neither room's actors ever hide behind it.
        static void SharedWall(string label, float x0, float yTop, float w, float doorX0, float doorW, float[] pierXs)
        {
            float y = yTop - .95f;
            float left = doorX0 - x0, right = x0 + w - (doorX0 + doorW);
            var leftWidths = new float[Mathf.Max(1, Mathf.RoundToInt(left / 1.4f))];
            float lw = left / leftWidths.Length;
            for (int i = 0; i < leftWidths.Length; i++) leftWidths[i] = lw;
            var rightWidths = new float[Mathf.Max(1, Mathf.RoundToInt(right / 1.4f))];
            float rw = right / rightWidths.Length;
            for (int i = 0; i < rightWidths.Length; i++) rightWidths[i] = rw;
            Bays(label + " masonry bay", x0, y, leftWidths);
            Bays(label + " masonry bay", doorX0 + doorW, y, rightWidths);
            Band(label + " foot", new Vector3(x0, y - .18f, 0), new Vector2(left, .18f), FootColor, FootOrder);
            Band(label + " foot", new Vector3(doorX0 + doorW, y - .18f, 0), new Vector2(right, .18f), FootColor, FootOrder);
            Band(label + " top edge", new Vector3(x0, yTop + .4f, 0), new Vector2(left, .10f), PlinthTopColor, PlinthTopOrder);
            Band(label + " top edge", new Vector3(doorX0 + doorW, yTop + .4f, 0), new Vector2(right, .10f), PlinthTopColor, PlinthTopOrder);
            Band(label + " jamb L", new Vector3(doorX0 - .35f, yTop - .5f, 0), new Vector2(.35f, 1.1f), JambColor, JambOrder);
            Band(label + " jamb R", new Vector3(doorX0 + doorW, yTop - .5f, 0), new Vector2(.35f, 1.1f), JambColor, JambOrder);
            Band(label + " threshold", new Vector3(doorX0, yTop - .08f, 0), new Vector2(doorW, .16f), ThresholdColor, ThresholdOrder);
            if (pierXs == null) return;
            foreach (float px in pierXs)
            {
                Band(label + " pier", new Vector3(px - .15f, y, 0), new Vector2(.3f, 1.45f), PierColor, PierOrder);
                Band(label + " pier cap", new Vector3(px - .21f, y + 1.33f, 0), new Vector2(.42f, .13f), PierCapColor, PierCapOrder);
            }
        }

        // Start room at the origin: full sample treatment, east wall carries the door.
        static void BuildNormalRoom()
        {
            NorthWall("Normal north", -14, 11, 28, new[] { -12f, -6f, 0f, 6f, 12f });
            SideWall("Normal west", -14.55f, -11, 23.45f);
            SideWall("Normal east lower", 14, -11, 7);
            SideWall("Normal east upper", 14, 0, 12.45f);
            PlinthLine("Normal south", -14, -11, 28);
            Band("Normal east jamb L", new Vector3(13.9f, -4.35f, 0), new Vector2(.75f, .35f), JambColor, JambOrder);
            Band("Normal east jamb R", new Vector3(13.9f, 0, 0), new Vector2(.75f, .35f), JambColor, JambOrder);
            Band("Normal east threshold", new Vector3(13.92f, -3.7f, 0), new Vector2(.16f, 3.4f), ThresholdColor, ThresholdOrder);
        }

        static void BuildCorridors()
        {
            // Horizontal corridor far wall stops at the vertical corridor's west edge.
            var widths = new float[13];
            for (int i = 0; i < 12; i++) widths[i] = 1.4f;
            widths[12] = 1.2f;
            Bays("Corridor north masonry bay", 14, 4, widths);
            Band("Corridor north cornice", new Vector3(13.9f, 5.45f, 0), new Vector2(18.3f, .19f), CorniceColor, CorniceOrder);
            Band("Corridor north cornice shadow", new Vector3(14, 5.35f, 0), new Vector2(18, .09f), JointColor, CorniceShadowOrder);
            Band("Corridor north foot", new Vector3(14, 3.83f, 0), new Vector2(18, .18f), FootColor, FootOrder);
            PlinthLine("Corridor south", 14, -4, 26);
            SideWall("Corridor east", 40, -4, 32);
            SideWall("Corridor west", 31.45f, 4, 24);
            // The corridor runs into the chest room's south wall: a short masonry
            // face fills the mouth on both sides of the closed arch.
            Bays("Chest mouth masonry bay", 32, 27.2f, 1.5f, 1.0f);
            Bays("Chest mouth masonry bay", 37.5f, 27.2f, 1.0f, 1.5f);
            Band("Chest mouth foot", new Vector3(32, 27.02f, 0), new Vector2(2.5f, .18f), FootColor, FootOrder);
            Band("Chest mouth foot", new Vector3(37.5f, 27.02f, 0), new Vector2(2.5f, .18f), FootColor, FootOrder);
            Band("Chest mouth top edge", new Vector3(32, 28.55f, 0), new Vector2(2.5f, .10f), PlinthTopColor, PlinthTopOrder);
            Band("Chest mouth top edge", new Vector3(37.5f, 28.55f, 0), new Vector2(2.5f, .10f), PlinthTopColor, PlinthTopOrder);
            Band("Chest mouth jamb L", new Vector3(34.15f, 27.9f, 0), new Vector2(.35f, 1.1f), JambColor, JambOrder);
            Band("Chest mouth jamb R", new Vector3(37.5f, 27.9f, 0), new Vector2(.35f, 1.1f), JambColor, JambOrder);
            Band("Chest mouth threshold", new Vector3(34.5f, 27.92f, 0), new Vector2(3, .16f), ThresholdColor, ThresholdOrder);
            PlinthLine("Chest south west", 22, 28, 10);
            PlinthLine("Chest south east", 40, 28, 10);
        }

        static void BuildChestRoom()
        {
            SideWall("Chest west", 21.45f, 28, 22);
            SideWall("Chest east", 50, 28, 22);
            SharedWall("Chest north", 22, 50, 28, 34.5f, 3, new[] { 24f, 30f, 42f, 48f });
        }

        static void BuildAltarRoom()
        {
            NorthWall("Altar north", 22, 72, 28, new[] { 24f, 30f, 36f, 42f, 48f });
            SideWall("Altar west", 21.45f, 50, 23.45f);
            SideWall("Altar east", 50, 50, 23.45f);
        }

        // --- Props, following the sample's focal vocabulary ---

        static GameObject Prop(string name, string path, Vector3 at, int order, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Load(path); sr.sortingOrder = order;
            return go;
        }

        static void Focus(string name, string path, Vector3 at, float sx, float sy, float alpha, float dy, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Load(path); sr.sortingOrder = PropOrder;
            Shadow(new Vector3(at.x, at.y + dy, 0), sx, sy, alpha, parent);
        }

        static void Brazier(Vector3 at, Transform parent)
        {
            Prop("Brazier", "Props/jh_iso_prop_brazier_00.png", at, PropOrder, parent);
            Shadow(at, .45f, .18f, .30f, parent);
            var flame = Prop("Living flame", "FX/jh_iso_fx_brazier_fire_00.png", at + Vector3.up * .55f, FlameOrder, parent);
            var animation = flame.AddComponent<IsoBrazierFire>();
            animation.Frames = new Sprite[6]; animation.FramesPerSecond = 8;
            for (int i = 0; i < 6; i++) animation.Frames[i] = Load("FX/jh_iso_fx_brazier_fire_0" + i + ".png");
            DiscObject("Soft warm ground wash", at, 2.1f, 1, new Color(.9f, .47f, .16f, .095f), -800, parent);
        }

        static void Rubble(int cluster, Vector2 at, Transform parent)
        {
            for (int j = 0; j < 3; j++)
            {
                Vector3 pos = new Vector3(at.x + j * .48f, at.y + (j % 2) * .32f, 0);
                var go = new GameObject("Rubble cluster " + cluster + "/" + j); go.transform.SetParent(parent, false); go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Load("Tiles/S1/Decal/jh_iso_decal_s1_rubble_0" + (j % 2) + ".png");
                sr.sortingOrder = RubbleOrder; sr.color = new Color(.63f, .68f, .72f, .85f);
            }
        }

        static void Gate(string name, Vector3 at, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Load(Folder + "/Refine/jh_sample_gate_south_closed.png");
            sr.sortingOrder = GateOrder;
        }

        static void Shadow(Vector3 at, float x, float y, float alpha, Transform parent, bool follow = false)
        {
            var go = DiscObject("Contact shadow", at, x, y, new Color(0, 0, 0, alpha), -750, parent);
            if (follow) go.transform.localPosition = Vector3.zero;
        }

        static GameObject DiscObject(string name, Vector3 at, float x, float y, Color tint, int order, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = at; go.transform.localScale = new Vector3(x, y, 1);
            go.AddComponent<MeshFilter>().sharedMesh = softDisc;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = wash; mr.sortingOrder = order;
            string matPath = Folder + "/Generated/" + (name.Contains("warm") ? "WarmWash" : "Shadow") + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(wash); AssetDatabase.CreateAsset(mat, matPath); }
            mat.color = tint; EditorUtility.SetDirty(mat); mr.sharedMaterial = mat; mr.SetPropertyBlock(null);
            return go;
        }

        static Mesh Disc()
        {
            string path = Folder + "/Generated/SoftDisc.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            mesh = new Mesh(); var vertices = new List<Vector3> { Vector3.zero }; var colors = new List<Color> { Color.white }; var triangles = new List<int>();
            for (int i = 0; i <= 32; i++)
            {
                float a = i * Mathf.PI * 2 / 32;
                vertices.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a)));
                colors.Add(new Color(1, 1, 1, 0));
                if (i > 0) { triangles.Add(0); triangles.Add(i); triangles.Add(i + 1); }
            }
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        static void Band(string name, Vector3 at, Vector2 size, Color color, int order)
        {
            string matPath = Folder + "/Generated/Stone_" + ColorUtility.ToHtmlStringRGBA(color) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Sprites/Default")); mat.color = color; AssetDatabase.CreateAsset(mat, matPath); }
            var go = new GameObject(name); go.transform.SetParent(architecture, false); go.transform.position = at; go.transform.localScale = new Vector3(size.x, size.y, 1);
            go.AddComponent<MeshFilter>().sharedMesh = unitQuad;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.sortingOrder = order;
        }
    }
}
