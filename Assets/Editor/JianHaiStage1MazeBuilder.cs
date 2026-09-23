using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Art;
using RogueShooter.Demo;

namespace RogueShooter.Tools
{
    /// <summary>
    /// Builds Assets/Scenes/Stage1Maze.scene — 一阶段可玩迷宫（三阶段随机迷宫 v0.5 · 阶段1 · 玩法锁）。
    /// 玩法几何: 52x40 tile, 战斗房 20x16 净空 x4, 三段走廊净长约 30u, 门在走廊房口。
    /// 墙体四向可读: opening(门洞边) > face(北向砖面) > side(侧墙) > corner(角石) > top。
    /// 资产: 外包_一阶段迷宫美术_v01 + 补件_v011 + 外包_一阶段可玩_美术_v01（占位 P1，外包终稿同名替换）。
    /// </summary>
    public static class JianHaiStage1MazeBuilder
    {
        const int W = 52;   // x 0..51 (玩法锁)
        const int H = 40;   // y 0..39 (玩法锁)

        const string TilesFolder = "Assets/Art/JianHai/Tiles/";
        const string ScenePath = "Assets/Scenes/Stage1Maze.scene";

        // spec §0 分层: Ground 0 / Decal 5 / Prop 10 / Entity 20 / FX 30
        const int OrderGround = 0, OrderDecal = 5, OrderProp = 10, OrderEntity = 20, OrderFx = 30;

        [MenuItem("Tools/JianHai/Build Stage1 Maze (v0.5)")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            EnsureArtImportSettings();
            var tiles = EnsureTileAssets();
            if (tiles == null)
            {
                Debug.LogError("[Stage1Maze] tile sprites missing - run Tools/JianHai/gen_jianhai_px.ps1 first");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildScene(tiles);
        }

        // ============================================================
        // layout (tile coords, inclusive; 2-wide corridors, 20x16 rooms)
        // ============================================================

        readonly struct RectI
        {
            public readonly int X0, Y0, X1, Y1;
            public readonly string T;
            public RectI(int x0, int y0, int x1, int y1, string t)
            {
                X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; T = t;
            }
        }

        static RectI F(int x0, int y0, int x1, int y1, string t) => new RectI(x0, y0, x1, y1, t);

        /// <summary>RM=普通房 CH=小宝箱房 AL=祭坛房 CO=走廊(不刷怪)。走廊树: A-B, B-D, D-C。</summary>
        static readonly RectI[] Floors =
        {
            F(3, 2, 22, 17, "RM"),     // Room A 普通房1 20x16 (START)
            F(29, 2, 48, 17, "CH"),    // Room B 小宝箱房 20x16
            F(29, 22, 48, 37, "RM"),   // Room D 普通房2 20x16
            F(3, 22, 22, 37, "AL"),    // Room C 祭坛房 20x16
            F(23, 9, 28, 10, "CO"),    // 走廊 A-B (净长 6u)
            F(38, 18, 39, 21, "CO"),   // 走廊 B-D (净长 4u)
            F(23, 29, 28, 30, "CO"),   // 走廊 D-C (净长 6u)
        };

        /// <summary>门 2x2 站位（走廊贴房口）。state: 0=关 1=开。</summary>
        static readonly (Vector2Int min, string name, bool open)[] Doors =
        {
            (new Vector2Int(27, 9),  "Door_B_Closed", false),  // B 西口
            (new Vector2Int(38, 20), "Door_D_Open",   true),   // D 北口
            (new Vector2Int(23, 29), "Door_C_Closed", false),  // C 东口
        };

        static readonly Dictionary<string, string[]> TileArt = new Dictionary<string, string[]>
        {
            { "RM", new[] { "jh_tile_floor_s1_room_00", "jh_tile_floor_s1_room_01", "jh_tile_floor_s1_room_02" } },
            { "CH", new[] { "jh_tile_floor_s1_chest_00", "jh_tile_floor_s1_chest_01", "jh_tile_floor_s1_chest_02" } },
            { "AL", new[] { "jh_tile_floor_s1_altar_00", "jh_tile_floor_s1_altar_01", "jh_tile_floor_s1_altar_02" } },
            { "CO", new[] { "jh_tile_floor_s1_corridor_00", "jh_tile_floor_s1_corridor_01", "jh_tile_floor_s1_corridor_02" } },
        };

        // wall tile ids
        const string WallFace = "jh_wall_s1_stone_face";
        const string WallTop = "jh_wall_s1_stone_top";
        const string WallSideE = "jh_wall_s1_stone_side_e";
        const string WallSideW = "jh_wall_s1_stone_side_w";
        static readonly string[] WallCorner = { "jh_wall_s1_stone_corner_ne", "jh_wall_s1_stone_corner_nw", "jh_wall_s1_stone_corner_se", "jh_wall_s1_stone_corner_sw" };
        static readonly string[] WallOpening = { "jh_wall_s1_stone_opening_n", "jh_wall_s1_stone_opening_s", "jh_wall_s1_stone_opening_e", "jh_wall_s1_stone_opening_w" };
        static readonly string[] RubbleArt = { "jh_decal_s1_rubble_00", "jh_decal_s1_rubble_01" };

        // ============================================================
        // import + tile assets
        // ============================================================

        static void EnsureArtImportSettings()
        {
            var ids = new List<string>();
            foreach (var kv in TileArt) ids.AddRange(kv.Value);
            ids.Add(WallFace); ids.Add(WallTop); ids.Add(WallSideE); ids.Add(WallSideW);
            ids.AddRange(WallCorner); ids.AddRange(WallOpening); ids.AddRange(RubbleArt);
            ids.Add("jh_decal_s1_altarglow");
            ids.Add("jh_prop_door_s1_closed"); ids.Add("jh_prop_door_s1_open");
            ids.Add("jh_prop_chest_closed"); ids.Add("jh_prop_altar_lit");
            ids.Add("jh_fx_portal_spawn_00"); ids.Add("jh_fx_portal_spawn_01");
            ids.Add("jh_fx_warn_bang");
            ids.Add("jh_wall_s1_stone_n"); ids.Add("jh_wall_s1_stone_e"); ids.Add("jh_wall_s1_stone_s"); ids.Add("jh_wall_s1_stone_w");
            ids.Add("jh_char_archer_idle");
            ids.Add("jh_enemy_s1_melee_idle"); ids.Add("jh_enemy_s1_dog_idle"); ids.Add("jh_enemy_s1_mage_idle");
            ids.Add("jh_enemy_s1_melee_elite_idle");
            ids.Add("jh_proj_arrow"); ids.Add("jh_proj_mage_orb");
            int fixedCount = 0;
            foreach (string id in ids)
            {
                string path = FindAssetPath(id);
                if (path == null)
                {
                    Debug.LogWarning("[Stage1Maze] art missing: " + id);
                    continue;
                }
                if (FixImporterIfNeeded(path, id)) fixedCount++;
            }
            if (fixedCount > 0)
                Debug.Log("[Stage1Maze] importer fixed on " + fixedCount + " files");
        }

        static string FindAssetPath(string id)
        {
            string p = JianHaiArtCatalog.AssetPath(id);
            string root = System.IO.Directory.GetParent(Application.dataPath).FullName;
            if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(System.IO.Path.GetFullPath(System.IO.Path.Combine(root, p))))
                return p;
            // 非 catalog 前缀 (jh_proj_/jh_decal_) 落 FX/Tiles 的兜底
            foreach (string dir in new[] { "Assets/Art/JianHai/FX/", "Assets/Art/JianHai/Tiles/", "Assets/Art/JianHai/Enemies/" })
            {
                string q = dir + id + ".png";
                if (System.IO.File.Exists(System.IO.Path.GetFullPath(System.IO.Path.Combine(root, q))))
                    return q;
            }
            return null;
        }

        static bool FixImporterIfNeeded(string path, string id)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return false;
            Vector2 pivot = PivotFor(id);
            // Tuanjie: spritePivot 只在 m_Alignment == Custom(9) 时生效
            var so = new SerializedObject(ti);
            var alignProp = so.FindProperty("m_Alignment");
            bool alignOk = alignProp != null && alignProp.intValue == (int)SpriteAlignment.Custom;
            if (ti.textureType == TextureImporterType.Sprite
                && ti.spriteImportMode == SpriteImportMode.Single
                && ti.filterMode == FilterMode.Point
                && Mathf.Approximately(ti.spritePixelsPerUnit, JianHaiArtCatalog.Ppu)
                && alignOk
                && (ti.spritePivot - pivot).sqrMagnitude < 0.0001f)
                return false;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = JianHaiArtCatalog.Ppu;
            ti.spritePivot = pivot;
            if (alignProp == null)
            {
                so = new SerializedObject(ti);
                alignProp = so.FindProperty("m_Alignment");
            }
            if (alignProp != null)
            {
                alignProp.intValue = (int)SpriteAlignment.Custom;
                so.ApplyModifiedProperties();
            }
            ti.SaveAndReimport();
            return true;
        }

        static Vector2 PivotFor(string id)
        {
            if (id.StartsWith("jh_tile_") || id.StartsWith("jh_decal_")) return new Vector2(0.5f, 0.5f);
            if (id.StartsWith("jh_wall_")) return new Vector2(0.5f, 0f);
            if (id.StartsWith("jh_fx_warn")) return new Vector2(0.5f, 0f);
            if (id.StartsWith("jh_fx_") || id.StartsWith("jh_proj_")) return new Vector2(0.5f, 0.5f);
            if (id.StartsWith("jh_prop_door_")) return new Vector2(0.5f, 0.5f); // 门洞 2x2 中心
            if (id.StartsWith("jh_char_") || id.StartsWith("jh_enemy_")) return new Vector2(0.5f, 0.15f);
            return new Vector2(0.5f, 0.2f);
        }

        static Dictionary<string, Tile> EnsureTileAssets()
        {
            var tiles = new Dictionary<string, Tile>();
            foreach (var kv in TileArt)
                for (int i = 0; i < kv.Value.Length; i++)
                    tiles[kv.Key + "_" + i] = EnsureTileAsset(kv.Value[i], false);
            tiles["WALL_FACE"] = EnsureTileAsset(WallFace, true);
            tiles["WALL_TOP"] = EnsureTileAsset(WallTop, false);
            tiles["WALL_SIDE_E"] = EnsureTileAsset(WallSideE, false);
            tiles["WALL_SIDE_W"] = EnsureTileAsset(WallSideW, false);
            string[] cornerKeys = { "CORNER_NE", "CORNER_NW", "CORNER_SE", "CORNER_SW" };
            for (int i = 0; i < WallCorner.Length; i++)
                tiles[cornerKeys[i]] = EnsureTileAsset(WallCorner[i], false);
            string[] openingKeys = { "OPEN_N", "OPEN_S", "OPEN_E", "OPEN_W" };
            for (int i = 0; i < WallOpening.Length; i++)
                tiles[openingKeys[i]] = EnsureTileAsset(WallOpening[i], false);
            for (int i = 0; i < RubbleArt.Length; i++)
                tiles["RU_" + i] = EnsureTileAsset(RubbleArt[i], false);
            AssetDatabase.SaveAssets();
            return tiles;
        }

        static Tile EnsureTileAsset(string name, bool collider)
        {
            string path = TilesFolder + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TilesFolder + name + ".png");
            tile.color = Color.white;
            tile.colliderType = collider ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        // ============================================================
        // scene build
        // ============================================================

        static void BuildScene(Dictionary<string, Tile> tiles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Stage1Maze");
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform, false);
            gridGo.AddComponent<Grid>();

            Tilemap floorMap = MakeTilemap(gridGo.transform, "Floor", "Ground", OrderGround);
            Tilemap decorMap = MakeTilemap(gridGo.transform, "Decor", "Decal", OrderDecal);
            Tilemap wallMap = MakeTilemap(gridGo.transform, "Walls", "Ground", OrderGround + 1);
            wallMap.gameObject.AddComponent<TilemapCollider2D>();

            // ---- floors (variants per cell) ----
            var floorType = new string[W, H];
            foreach (var r in Floors)
                for (int y = r.Y0; y <= r.Y1; y++)
                    for (int x = r.X0; x <= r.X1; x++)
                        floorType[x, y] = r.T;

            // door mouth cells (floor, for opening-trim classification)
            var mouth = new HashSet<Vector2Int>();
            foreach (var d in Doors)
                for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 2; x++)
                        mouth.Add(new Vector2Int(d.min.x + x, d.min.y + y));

            var rand = new System.Random(20260921);
            int floorCount = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    string t = floorType[x, y];
                    if (t == null) continue;
                    floorCount++;
                    int v = rand.Next(100);
                    floorMap.SetTile(new Vector3Int(x, y, 0), tiles[t + (v < 55 ? "_0" : v < 85 ? "_1" : "_2")]);
                }

            // ---- walls: opening > face > side > corner > top (四向可读) ----
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (floorType[x, y] != null) continue;
                    Tile pick = tiles["WALL_TOP"];
                    if (mouth.Contains(new Vector2Int(x, y - 1))) pick = tiles["OPEN_S"];    // 门洞在南
                    else if (mouth.Contains(new Vector2Int(x, y + 1))) pick = tiles["OPEN_N"]; // 门洞在北
                    else if (mouth.Contains(new Vector2Int(x - 1, y))) pick = tiles["OPEN_W"]; // 门洞在西
                    else if (mouth.Contains(new Vector2Int(x + 1, y))) pick = tiles["OPEN_E"]; // 门洞在东
                    else if (IsFloor(x, y - 1)) pick = tiles["WALL_FACE"];                    // 北向砖面 (地板在南)
                    else if (IsFloor(x + 1, y)) pick = tiles["WALL_SIDE_E"];                  // 房东界墙
                    else if (IsFloor(x - 1, y)) pick = tiles["WALL_SIDE_W"];                  // 房西界墙
                    else if (IsFloor(x + 1, y - 1)) pick = tiles["CORNER_SE"];
                    else if (IsFloor(x - 1, y - 1)) pick = tiles["CORNER_SW"];
                    else if (IsFloor(x + 1, y + 1)) pick = tiles["CORNER_NE"];
                    else if (IsFloor(x - 1, y + 1)) pick = tiles["CORNER_NW"];
                    wallMap.SetTile(new Vector3Int(x, y, 0), pick);
                }
            bool IsFloor(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && floorType[x, y] != null;

            // ---- rubble scatter (rooms 8%, corridors 3%) ----
            var keepOut = BuildKeepOutZones();
            int decoCount = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    string t = floorType[x, y];
                    if (t == null || keepOut.Contains(new Vector2Int(x, y))) continue;
                    if (rand.Next(100) >= (t == "CO" ? 3 : 8)) continue;
                    decorMap.SetTile(new Vector3Int(x, y, 0), tiles["RU_" + (rand.Next(100) < 50 ? 0 : 1)]);
                    decoCount++;
                }

            // ---- props ----
            var propsRoot = new GameObject("Props").transform;
            propsRoot.SetParent(root.transform, false);

            // 主角 + 敌我陈列 (Entity, 剪影同框可辨)
            AddSpriteProp(propsRoot, "START", new Vector3(13.0f, 10.0f, 0f), "jh_char_archer_idle",
                JianHaiArtCatalog.EntityStubWorldScale, "Entity", OrderEntity);
            AddSpriteProp(propsRoot, "E_Melee", new Vector3(17.0f, 13.0f, 0f), "jh_enemy_s1_melee_idle",
                JianHaiArtCatalog.EntityStubWorldScale, "Entity", OrderEntity);
            AddSpriteProp(propsRoot, "E_Mage", new Vector3(42.0f, 12.0f, 0f), "jh_enemy_s1_mage_idle",
                JianHaiArtCatalog.EntityStubWorldScale, "Entity", OrderEntity);
            AddSpriteProp(propsRoot, "E_Dog", new Vector3(34.0f, 30.0f, 0f), "jh_enemy_s1_dog_idle",
                JianHaiArtCatalog.EntityStubWorldScale, "Entity", OrderEntity);
            AddSpriteProp(propsRoot, "E_Melee_Elite", new Vector3(7.0f, 34.0f, 0f), "jh_enemy_s1_melee_elite_idle",
                JianHaiArtCatalog.EntityStubWorldScale, "Entity", OrderEntity);

            // 交互物
            AddSpriteProp(propsRoot, "Chest_01", new Vector3(36.0f, 9.4f, 0f), "jh_prop_chest_closed",
                JianHaiArtCatalog.PropStubWorldScale, "Prop", OrderProp);
            AddSpriteProp(propsRoot, "AltarGlow_01", new Vector3(13.0f, 30.5f, 0f), "jh_decal_s1_altarglow", 1f, "Decal", OrderDecal);
            AddSpriteProp(propsRoot, "A_01", new Vector3(13.0f, 29.6f, 0f), "jh_prop_altar_lit",
                JianHaiArtCatalog.PropStubWorldScale, "Prop", OrderProp);

            // 门 (B 关 / D 开 / C 关, 站走廊贴房口)
            AddSpriteProp(propsRoot, "Door_B_Closed", new Vector3(28.0f, 10.0f, 0f), "jh_prop_door_s1_closed", 1f, "Prop", OrderProp);
            AddSpriteProp(propsRoot, "Door_D_Open", new Vector3(39.0f, 21.0f, 0f), "jh_prop_door_s1_open", 1f, "Prop", OrderProp);
            AddSpriteProp(propsRoot, "Door_C_Closed", new Vector3(24.0f, 30.0f, 0f), "jh_prop_door_s1_closed", 1f, "Prop", OrderProp);

            // FX + 投射物陈列
            AddSpriteProp(propsRoot, "FX_Portal_00", new Vector3(7.0f, 6.0f, 0f), "jh_fx_portal_spawn_00", 1f, "FX", OrderFx);
            AddSpriteProp(propsRoot, "FX_Portal_01", new Vector3(43.0f, 33.0f, 0f), "jh_fx_portal_spawn_01", 1f, "FX", OrderFx);
            AddSpriteProp(propsRoot, "FX_WarnBang", new Vector3(17.0f, 13.9f, 0f), "jh_fx_warn_bang", 1f, "FX", OrderFx);
            AddSpriteProp(propsRoot, "Proj_Arrow", new Vector3(20.0f, 7.0f, 0f), "jh_proj_arrow", 1f, "FX", OrderFx);
            AddSpriteProp(propsRoot, "Proj_Mage_Orb", new Vector3(45.5f, 12.0f, 0f), "jh_proj_mage_orb", 1f, "FX", OrderFx);

            // ---- labels ----
            AddWorldLabel(FindDeep(root.transform, "START"), "START", new Vector3(0f, 1.1f, 0f));
            AddWorldLabel(FindDeep(root.transform, "Chest_01"), "Chest_01", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(FindDeep(root.transform, "A_01"), "Altar_lit", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(FindDeep(root.transform, "Door_B_Closed"), "Door_Closed", new Vector3(0f, 1.3f, 0f));
            AddWorldLabel(FindDeep(root.transform, "Door_D_Open"), "Door_Open", new Vector3(0f, -1.4f, 0f));
            AddWorldLabel(FindDeep(root.transform, "Door_C_Closed"), "Door_Closed", new Vector3(0f, 1.3f, 0f));
            AddWorldLabel(FindDeep(root.transform, "FX_Portal_00"), "FX_portal", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(FindDeep(root.transform, "E_Melee_Elite"), "Elite", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(FindDeep(root.transform, "E_Mage"), "Mage", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(FindDeep(root.transform, "E_Dog"), "Dog", new Vector3(0f, 1.2f, 0f));
            AddRoomLabel(propsRoot, "Room_A_Normal_20x16", new Vector3(13.0f, 16.4f, 0f));
            AddRoomLabel(propsRoot, "Room_B_Chest_20x16", new Vector3(39.0f, 16.4f, 0f));
            AddRoomLabel(propsRoot, "Room_C_Altar_20x16", new Vector3(13.0f, 36.4f, 0f));
            AddRoomLabel(propsRoot, "Room_D_Normal_20x16", new Vector3(39.0f, 36.4f, 0f));

            // ---- camera: 手感锁 ortho 6 · GDD 斜约 45° 俯视 ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(13.0f, 2.0f, -8f);
            camGo.transform.rotation = Quaternion.Euler(RogueShooter.Vision.CameraFollow2D.IsoPitchDegrees, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.045f, 0.06f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<RogueShooter.Vision.CameraFollow2D>();
            var startTf = FindDeep(root.transform, "START");
            if (startTf != null)
                follow.SetTarget(startTf);
            follow.ConfigureIso(RogueShooter.Vision.CameraFollow2D.IsoPitchDegrees, new Vector3(0f, -8f, -8f));

            Validate(floorType, floorCount, decoCount);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Stage1Maze] scene saved: " + ScenePath);
        }

        static Tilemap MakeTilemap(Transform parent, string name, string layer, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            var tm = go.GetComponent<Tilemap>();
            var tr = go.GetComponent<TilemapRenderer>();
            tr.sortingLayerName = SafeLayer(layer);
            tr.sortingOrder = order;
            return tm;
        }

        static HashSet<Vector2Int> BuildKeepOutZones()
        {
            var set = new HashSet<Vector2Int>();
            void Add(float wx, float wy, float radius)
            {
                int cx = Mathf.FloorToInt(wx), cy = Mathf.FloorToInt(wy);
                int r = Mathf.CeilToInt(radius);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        set.Add(new Vector2Int(cx + dx, cy + dy));
            }
            Add(13.0f, 10.0f, 3f);       // START 区净空
            Add(17.0f, 13.0f, 1.5f); Add(42.0f, 12.0f, 1.5f); Add(34.0f, 30.0f, 1.5f); Add(7.0f, 34.0f, 1.5f); // 敌陈列
            Add(36.0f, 9.4f, 2f);        // 宝箱
            Add(13.0f, 29.6f, 2.5f);     // 祭坛
            Add(28.0f, 10.0f, 1.5f); Add(39.0f, 21.0f, 1.5f); Add(24.0f, 30.0f, 1.5f); // 门
            Add(7.0f, 6.0f, 1.5f); Add(43.0f, 33.0f, 1.5f); Add(17.0f, 13.9f, 1f);      // FX
            Add(20.0f, 7.0f, 1f); Add(45.5f, 12.0f, 1f);                                  // 投射物
            return set;
        }

        static GameObject AddSpriteProp(Transform parent, string name, Vector3 pos, string artId, float scale, string layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            string path = FindAssetPath(artId);
            sr.sprite = path != null ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null;
            sr.sortingLayerName = SafeLayer(layer);
            sr.sortingOrder = order;
            if (sr.sprite == null)
                Debug.LogError("[Stage1Maze] sprite missing: " + artId);
            return go;
        }

        static void AddRoomLabel(Transform parent, string text, Vector3 worldPos)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.2f;
            tm.fontSize = 26;
            tm.color = new Color(0.85f, 0.85f, 0.9f, 0.9f);
            BuiltinUiFont.Apply(tm);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = SafeLayer("Entity");
            mr.sortingOrder = OrderEntity + 30;
        }

        static void AddWorldLabel(Transform target, string text, Vector3 localOffset)
        {
            if (target == null) return;
            var label = new GameObject("Label_" + text);
            label.transform.SetParent(target, false);
            label.transform.localPosition = localOffset;
            var tm = label.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.18f;
            tm.fontSize = 24;
            tm.color = Color.white;
            BuiltinUiFont.Apply(tm);
            var mr = label.GetComponent<MeshRenderer>();
            mr.sortingLayerName = SafeLayer("Entity");
            mr.sortingOrder = OrderEntity + 30;
        }

        static Transform FindDeep(Transform root, string name)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                if (t.name == name) return t;
                for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
            }
            return null;
        }

        static string SafeLayer(string want)
        {
            foreach (var l in SortingLayer.layers)
                if (l.name == want) return want;
            return "Default";
        }

        // ============================================================
        // validation
        // ============================================================

        static void Validate(string[,] floorType, int floorCount, int decoCount)
        {
            var start = new Vector2Int(13, 10);
            var seen = new bool[W, H];
            var queue = new Queue<Vector2Int>();
            if (floorType[start.x, start.y] != null)
            {
                seen[start.x, start.y] = true;
                queue.Enqueue(start);
            }
            int reached = 0;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                reached++;
                TryVisit(c.x + 1, c.y); TryVisit(c.x - 1, c.y);
                TryVisit(c.x, c.y + 1); TryVisit(c.x, c.y - 1);
            }
            void TryVisit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || seen[x, y] || floorType[x, y] == null) return;
                seen[x, y] = true;
                queue.Enqueue(new Vector2Int(x, y));
            }

            bool connected = reached == floorCount;
            if (!connected)
                Debug.LogError("[Stage1Maze] unreachable floors: " + (floorCount - reached));

            var sites = new (string id, Vector2Int cell)[]
            {
                ("Chest_01", new Vector2Int(36, 9)),
                ("A_01", new Vector2Int(13, 29)),
                ("Door_B", new Vector2Int(27, 9)),
                ("Door_D", new Vector2Int(38, 20)),
                ("Door_C", new Vector2Int(23, 29)),
                ("FX_Portal_A", new Vector2Int(7, 6)),
                ("FX_Portal_D", new Vector2Int(43, 33)),
                ("E_Melee", new Vector2Int(17, 13)),
                ("E_Mage", new Vector2Int(42, 12)),
                ("E_Dog", new Vector2Int(34, 30)),
                ("E_Elite", new Vector2Int(7, 34)),
            };
            int bad = 0;
            foreach (var s in sites)
                if (!seen[s.cell.x, s.cell.y])
                {
                    Debug.LogError("[Stage1Maze] site not reachable: " + s.id);
                    bad++;
                }

            int rooms = 0, chestRooms = 0, altarRooms = 0, corridors = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    string t = floorType[x, y];
                    if (t == "RM") rooms++;
                    else if (t == "CH") chestRooms++;
                    else if (t == "AL") altarRooms++;
                    else if (t == "CO") corridors++;
                }

            Debug.Log("[Stage1Maze] floors=" + floorCount + " reachable=" + reached + " decos=" + decoCount
                + (connected ? " CONNECTED" : " !!BROKEN!!"));
            Debug.Log("[Stage1Maze] checklist: 52x40 玩法锁 | 普通房=" + rooms + "t 小宝箱房=" + chestRooms + "t 祭坛房=" + altarRooms
                + "t(各20x16) 走廊=" + corridors + "t(约30u) | 门×3(关/开/关) 宝箱 祭坛(lit) 传送门×2 感叹号 箭矢 法球 敌陈列×4"
                + (bad == 0 ? " | key sites OK" : " | !! " + bad + " sites bad"));
        }
    }
}
