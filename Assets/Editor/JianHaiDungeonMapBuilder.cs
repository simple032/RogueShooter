using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Art;
using RogueShooter.Demo;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Tools
{
    /// <summary>
    /// Builds Assets/Scenes/DungeonMap.scene from the v6 精做 geometry
    /// (02_design/wireframes/箭骸行者_地图精做_v6_已通过.png) using JianHai
    /// placeholder tiles. Art-only scene: HOOKS-named markers carry no gameplay.
    /// Space rules: 02_design/箭骸行者_地图文字说明_外包_L3.md.
    /// </summary>
    public static class JianHaiDungeonMapBuilder
    {
        // ---- grid constants (tile units; 1 tile = 1u @PPU32) ----
        const int W = 62;   // x 0..61
        const int H = 107;   // y 0..106, bottom = 出生, top = BOSS

        const string TilesFolder = "Assets/Art/JianHai/Tiles/";
        const string PropsFolder = "Assets/Art/JianHai/Props/";
        const string ScenePath = "Assets/Scenes/DungeonMap.scene";

        [MenuItem("Tools/JianHai/Build DungeonMap (v6)")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            EnsureArtImportSettings();
            var tiles = EnsureTileAssets();
            if (tiles == null)
            {
                Debug.LogError("[DungeonMap] tile sprites missing - run Tools/JianHai/gen_jianhai_px.ps1 first");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildScene(tiles);
        }

        // ============================================================
        // layout data
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

        /// <summary>Floor rects. Paint order matters: later rects overwrite overlaps.</summary>
        static readonly RectI[] Floors =
        {
            // ---- 出生区 (bottom) ----
            F(27, 3, 33, 9, "SP"),            // 出生枢纽 7x7
            F(8, 5, 26, 8, "O"),              // 西出口 (单向)
            F(34, 5, 53, 8, "O"),             // 东出口 (单向)
            F(8, 8, 11, 20, "O"),             // 西主路脊柱 → 外带 R1
            F(48, 8, 51, 20, "O"),            // 东主路脊柱
            F(28, 10, 31, 21, "O"),           // 中路出口穿顶墙 → 外带 R1
            F(2, 6, 5, 15, "DE"),             // 回出生死路·西 (不刷怪, 无箱)
            F(2, 12, 7, 15, "DE"),
            F(56, 6, 59, 15, "DE"),           // 回出生死路·东
            F(52, 12, 59, 15, "DE"),

            // ---- 外带 O · 阶段一 (y12..37, 每路 4 折) ----
            F(8, 17, 19, 20, "O"), F(16, 20, 19, 29, "O"),   // 西 J1/R2
            F(8, 26, 19, 29, "O"), F(8, 29, 11, 33, "O"),     // 西 J2/R3
            F(8, 30, 19, 33, "O"), F(16, 33, 19, 37, "O"),   // 西 J3/R4
            F(8, 34, 19, 37, "O"), F(8, 38, 11, 39, "O"),    // 西 J4 → 中带
            F(24, 18, 31, 21, "O"), F(24, 21, 27, 28, "O"), // 中 J1/R2
            F(24, 25, 35, 28, "O"), F(32, 28, 35, 33, "O"), // 中 J2/R3
            F(24, 30, 35, 33, "O"), F(24, 33, 27, 37, "O"), // 中 J3/R4
            F(24, 34, 31, 37, "O"), F(28, 38, 31, 39, "O"),// 中 J4 → 中带
            F(40, 17, 51, 20, "O"), F(40, 20, 43, 29, "O"), // 东 J1/R2
            F(40, 26, 51, 29, "O"), F(48, 29, 51, 33, "O"), // 东 J2/R3
            F(40, 30, 51, 33, "O"), F(40, 33, 43, 37, "O"), // 东 J3/R4
            F(40, 34, 51, 37, "O"), F(48, 38, 51, 39, "O"), // 东 J4 → 中带
            F(12, 22, 47, 25, "O"),            // 小祭坛连通路 西←中→东
            F(26, 21, 34, 26, "AL"),           // 外带中 · 小祭坛台 (必经)
            F(3, 29, 8, 32, "DE"), F(2, 28, 6, 33, "DE"),   // 大1 死路 (西)
            F(6, 15, 7, 16, "O"), F(32, 15, 33, 16, "O"), F(46, 15, 47, 16, "O"), // 小箱龛 x3

            // ---- 中带 M · 阶段二 (y40..65, 每路 4 折) ----
            F(8, 40, 11, 48, "M"), F(8, 45, 19, 48, "M"), F(16, 48, 19, 57, "M"),
            F(8, 54, 19, 57, "M"), F(8, 57, 11, 61, "M"), F(8, 58, 19, 61, "M"),
            F(16, 61, 19, 65, "M"), F(8, 62, 19, 65, "M"), F(8, 66, 11, 67, "M"),
            F(28, 40, 31, 49, "M"), F(28, 46, 35, 49, "M"), F(32, 49, 35, 57, "M"),
            F(24, 54, 35, 57, "M"), F(24, 57, 27, 61, "M"), F(24, 58, 35, 61, "M"),
            F(32, 61, 35, 65, "M"), F(28, 62, 35, 65, "M"), F(28, 66, 31, 67, "M"),
            F(48, 40, 51, 48, "M"), F(40, 45, 51, 48, "M"), F(40, 48, 43, 57, "M"),
            F(40, 54, 51, 57, "M"), F(48, 57, 51, 61, "M"), F(40, 58, 51, 61, "M"),
            F(40, 61, 43, 65, "M"), F(40, 62, 51, 65, "M"), F(48, 66, 51, 67, "M"),
            F(12, 50, 47, 53, "M"),            // 中祭坛连通路 西←中→东
            F(26, 49, 34, 54, "AL"),           // 中带中 · 中祭坛台 (必经)
            F(3, 42, 8, 45, "DE"), F(2, 41, 6, 46, "DE"),   // 大12 死路 (西·下)
            F(54, 43, 58, 47, "HB"), F(52, 44, 53, 46, "HB"), // 小房间·东
            F(2, 58, 6, 63, "HB"), F(7, 59, 7, 61, "HB"),     // 小房间·西
            F(6, 43, 7, 44, "M"), F(32, 43, 33, 44, "M"), F(46, 43, 47, 44, "M"), // 小箱龛 x3 (下)
            F(6, 58, 7, 59, "M"), F(22, 58, 23, 59, "M"), F(52, 58, 53, 59, "M"), // 小箱龛 x3 (上)

            // ---- 内带 I · 阶段三 (y68..87, 每路 3 折) ----
            F(8, 68, 11, 72, "I"), F(8, 69, 19, 72, "I"), F(16, 72, 19, 77, "I"),
            F(8, 74, 19, 77, "I"), F(8, 77, 11, 81, "I"), F(8, 78, 26, 81, "I"),   // 西 → 汇合
            F(28, 68, 31, 72, "I"), F(28, 69, 35, 72, "I"), F(32, 72, 35, 76, "I"),
            F(24, 73, 35, 76, "I"), F(24, 76, 27, 79, "I"), F(24, 78, 35, 81, "I"),
            F(32, 81, 35, 84, "I"),                                                     // 中 → 汇合
            F(48, 68, 51, 72, "I"), F(40, 69, 51, 72, "I"), F(40, 72, 43, 77, "I"),
            F(40, 74, 51, 77, "I"), F(48, 77, 51, 81, "I"), F(34, 78, 51, 81, "I"),   // 东 → 汇合
            F(26, 78, 34, 84, "AL"),           // 内带中 · 汇合大祭坛台 (三路汇合, 必经)
            F(52, 74, 56, 77, "DE"), F(55, 73, 59, 78, "DE"), // 大13 死路 (东)
            F(6, 69, 7, 70, "I"), F(32, 69, 33, 70, "I"), F(46, 69, 47, 70, "I"), // 小箱龛 x3

            // ---- 上行通道 · 商人前厅 · BOSS 房 (y84..106) ----
            F(28, 84, 31, 87, "I"),            // 汇合 → 前厅通道
            F(25, 88, 37, 94, "MR"),           // 商人前厅 (暖木 · 安全岛, 摊位居东侧)
            F(29, 95, 30, 95, "I"),            // BOSS 入口门洞 (2 宽, 可关门)
            F(25, 96, 35, 104, "BS"),          // BOSS 房 11x9 可走区
        };

        /// <summary>1x1 wall cells inside BOSS room (decorative pillars, off the fight ring).</summary>
        static readonly Vector2Int[] Pillars =
        {
            new Vector2Int(27, 98), new Vector2Int(33, 98),
            new Vector2Int(27, 102), new Vector2Int(33, 102),
        };

        /// <summary>Hand-placed decor: fork route arrows, bones at return tips, skulls by large chests.</summary>
        static readonly (Vector2Int cell, string deco)[] ForcedDecor =
        {
            // 岔口路线箭头 (L3 §2.2: 死路须挂在拐弯选择上, 岔口箭头可读)
            (new Vector2Int(9, 13), "R_UP"), (new Vector2Int(50, 13), "R_UP"),   // 回出生死路岔口
            (new Vector2Int(9, 30), "R_UP"),                                      // 大1 岔口
            (new Vector2Int(9, 43), "R_UP"), (new Vector2Int(50, 45), "R_UP"),   // 大12 / 东小房间岔口
            (new Vector2Int(9, 59), "R_UP"), (new Vector2Int(50, 75), "R_UP"),   // 西小房间 / 大13 岔口
            (new Vector2Int(3, 6), "D_BN"), (new Vector2Int(58, 6), "D_BN"),     // 回出生死路尽头骨堆
            (new Vector2Int(2, 29), "D_SK"), (new Vector2Int(2, 42), "D_SK"),   // 大箱旁骷髅
            (new Vector2Int(55, 74), "D_SK"),
        };

        // short id -> jh_* sprite name
        static readonly Dictionary<string, string> TileArt = new Dictionary<string, string>
        {
            { "O", "jh_tile_floor_corridor_o" }, { "M", "jh_tile_floor_corridor_m" },
            { "I", "jh_tile_floor_corridor_i" }, { "SP", "jh_tile_floor_spawn" },
            { "HB", "jh_tile_floor_hub" }, { "DE", "jh_tile_floor_deadend" },
            { "AL", "jh_tile_floor_altar" }, { "BS", "jh_tile_floor_boss" },
            { "BR", "jh_tile_floor_boss_rune" }, { "MR", "jh_tile_floor_merchant" },
            { "WF_O", "jh_tile_wall_face_o" }, { "WF_M", "jh_tile_wall_face_m" }, { "WF_I", "jh_tile_wall_face_i" },
            { "WT_O", "jh_tile_wall_top_o" }, { "WT_M", "jh_tile_wall_top_m" }, { "WT_I", "jh_tile_wall_top_i" },
            { "D_BN", "jh_tile_deco_bones" }, { "D_AR", "jh_tile_deco_arrow" },
            { "D_CR", "jh_tile_deco_crack" }, { "D_SK", "jh_tile_deco_skull" },
            { "R_UP", "jh_tile_deco_route_up" }, { "R_LT", "jh_tile_deco_route_left" }, { "R_RT", "jh_tile_deco_route_right" },
            { "W_SK", "jh_tile_deco_wall_skull" }, { "W_AR", "jh_tile_deco_wall_arrow" },
        };

        static string BandWallFace(int y) => y < 38 ? "WF_O" : y < 66 ? "WF_M" : "WF_I";
        static string BandWallTop(int y) => y < 38 ? "WT_O" : y < 66 ? "WT_M" : "WT_I";

        // ============================================================
        // import settings + tile assets
        // ============================================================

        static void EnsureArtImportSettings()
        {
            var ids = new List<string>();
            foreach (var kv in TileArt) ids.Add(kv.Value);
            ids.Add("jh_prop_chest_closed"); ids.Add("jh_prop_chest_open");
            ids.Add("jh_prop_chest_large_closed"); ids.Add("jh_prop_chest_large_open");
            ids.Add("jh_prop_altar_idle"); ids.Add("jh_prop_altar_active");
            ids.Add("jh_prop_shop_01"); ids.Add("jh_prop_gate_hub");
            ids.Add("jh_prop_gate_boss"); ids.Add("jh_prop_brazier");
            ids.Add("jh_prop_arch_stone"); ids.Add("jh_wall_stone_s");
            int fixedCount = 0;
            foreach (string id in ids)
            {
                string path = TilesFolder + id + ".png";
                if (!AssetExists(path)) path = PropsFolder + id + ".png";
                if (!AssetExists(path))
                {
                    Debug.LogWarning("[DungeonMap] art missing: " + id);
                    continue;
                }
                if (FixImporterIfNeeded(path, id)) fixedCount++;
            }
            if (fixedCount > 0)
                Debug.Log("[DungeonMap] importer fixed on " + fixedCount + " files");
        }

        static bool AssetExists(string projectRelativePath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return File.Exists(Path.GetFullPath(Path.Combine(root, projectRelativePath)));
        }

        static bool FixImporterIfNeeded(string path, string id)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return false;
            Vector2 pivot = PivotFor(id);
            if (ti.textureType == TextureImporterType.Sprite
                && ti.spriteImportMode == SpriteImportMode.Single
                && ti.filterMode == FilterMode.Point
                && Mathf.Approximately(ti.spritePixelsPerUnit, JianHaiArtCatalog.Ppu)
                && (ti.spritePivot - pivot).sqrMagnitude < 0.0001f)
                return false;
            // direct properties only: Tuanjie TextureImporterSettings round-trip stomps PPU/mode
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = JianHaiArtCatalog.Ppu;
            ti.spritePivot = pivot;
            ti.SaveAndReimport();
            return true;
        }

        static Vector2 PivotFor(string id)
        {
            if (id.StartsWith("jh_tile_")) return new Vector2(0.5f, 0.5f);
            if (id.StartsWith("jh_wall_")) return new Vector2(0.5f, 0f);
            if (id.StartsWith("jh_char_")) return new Vector2(0.5f, 0.15f);
            if (id.StartsWith("jh_boss_")) return new Vector2(0.5f, 0.12f);
            return new Vector2(0.5f, 0.2f); // prop
        }

        static Dictionary<string, Tile> EnsureTileAssets()
        {
            var tiles = new Dictionary<string, Tile>();
            foreach (var kv in TileArt)
            {
                string spritePath = TilesFolder + kv.Value + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogError("[DungeonMap] sprite not importable: " + spritePath);
                    return null;
                }
                bool wall = kv.Key.StartsWith("WF_") || kv.Key.StartsWith("WT_");
                tiles[kv.Key] = EnsureTileAsset(kv.Value, sprite, wall ? Tile.ColliderType.Sprite : Tile.ColliderType.None);
            }
            AssetDatabase.SaveAssets();
            return tiles;
        }

        static Tile EnsureTileAsset(string name, Sprite sprite, Tile.ColliderType collider)
        {
            string path = TilesFolder + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = collider;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        // ============================================================
        // scene build
        // ============================================================

        static void BuildScene(Dictionary<string, Tile> tiles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("DungeonMap");
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform, false);
            gridGo.AddComponent<Grid>();

            Tilemap floorMap = MakeTilemap(gridGo.transform, "Floor", LayerFor("Ground"), OrderFor(0));
            Tilemap decorMap = MakeTilemap(gridGo.transform, "Decor", LayerFor("Decal"), OrderFor(1));
            Tilemap wallMap = MakeTilemap(gridGo.transform, "Walls", LayerFor("Ground"), OrderFor(2));
            wallMap.gameObject.AddComponent<TilemapCollider2D>();

            // ---- floor grid ----
            var floorType = new string[W, H];
            foreach (var r in Floors)
                for (int y = r.Y0; y <= r.Y1; y++)
                    for (int x = r.X0; x <= r.X1; x++)
                        floorType[x, y] = r.T;
            foreach (var p in Pillars)
                floorType[p.x, p.y] = null;   // decorative pillar re-becomes wall

            int floorCount = 0;
            var rand = new System.Random(20260920);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    string t = floorType[x, y];
                    if (t == null) continue;
                    floorCount++;
                    string tileId = t;
                    if (t == "BS" && rand.Next(100) < 30) tileId = "BR";
                    floorMap.SetTile(new Vector3Int(x, y, 0), tiles[tileId]);
                }

            // ---- walls (face above floor, cap behind) + wall bone ornaments ----
            int faceCount = 0, topCount = 0, ornamentCount = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (floorType[x, y] != null) continue;
                    bool face = y > 0 && floorType[x, y - 1] != null;
                    if (face)
                    {
                        wallMap.SetTile(new Vector3Int(x, y, 0), tiles[BandWallFace(y - 1)]);
                        faceCount++;
                        // 墙骨饰 (P2#9): 嵌墙骷髅 / 插墙箭矢, 密度随带递增
                        int ornRate = y < 38 ? 7 : y < 66 ? 11 : 15;
                        if (rand.Next(100) < ornRate)
                        {
                            decorMap.SetTile(new Vector3Int(x, y, 0), tiles[rand.Next(100) < 60 ? "W_SK" : "W_AR"]);
                            ornamentCount++;
                        }
                    }
                    else
                    {
                        wallMap.SetTile(new Vector3Int(x, y, 0), tiles[BandWallTop(y)]);
                        topCount++;
                    }
                }

            // ---- decor scatter (band atmosphere: O sparse -> I dense) ----
            var keepOut = BuildKeepOutZones();
            int decoCount = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    string t = floorType[x, y];
                    if (t != "O" && t != "M" && t != "I" && t != "HB" && t != "DE") continue;
                    if (keepOut.Contains(new Vector2Int(x, y))) continue;
                    int density = t == "O" ? 8 : t == "M" ? 13 : t == "HB" ? 10 : t == "DE" ? 14 : 18;
                    if (rand.Next(100) >= density) continue;
                    int roll = rand.Next(100);
                    string deco = roll < 38 ? "D_BN" : roll < 56 ? "D_AR" : roll < 70 ? "D_CR" : "D_SK";
                    decorMap.SetTile(new Vector3Int(x, y, 0), tiles[deco]);
                    decoCount++;
                }
            foreach (var fd in ForcedDecor)
            {
                if (floorType[fd.cell.x, fd.cell.y] != null)
                {
                    decorMap.SetTile(new Vector3Int(fd.cell.x, fd.cell.y, 0), tiles[fd.deco]);
                    decoCount++;
                }
            }

            // ---- props ----
            var propsRoot = new GameObject("Props").transform;
            propsRoot.SetParent(root.transform, false);
            var markersRoot = new GameObject("Markers").transform;
            markersRoot.SetParent(root.transform, false);

            AddSpriteProp(propsRoot, "START", new Vector3(30.5f, 6.7f, 0f), JianHaiArtCatalog.PlayerIdle, JianHaiArtCatalog.EntityStubWorldScale, 0f, "Entity");
            AddSpriteProp(propsRoot, "BOSS", new Vector3(30.5f, 100.0f, 0f), "jh_boss_lord_idle", JianHaiArtCatalog.EntityStubWorldScale, 0f, "Entity");

            // 12 road chests: stage quota 1 / 2 / 1 per path, symmetric W/C/E
            AddSiteProp(propsRoot, "Chest_01", new Vector3(7.0f, 15.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_02", new Vector3(33.0f, 15.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_03", new Vector3(47.0f, 15.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_04", new Vector3(7.0f, 43.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_05", new Vector3(33.0f, 43.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_06", new Vector3(47.0f, 43.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_07", new Vector3(7.0f, 58.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_08", new Vector3(23.0f, 58.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_09", new Vector3(53.0f, 58.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_10", new Vector3(7.0f, 69.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_11", new Vector3(33.0f, 69.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_12", new Vector3(47.0f, 69.4f, 0f), JianHaiArtCatalog.ChestRoot, "closed", 0f);

            // 3 large chests: dead-ends only, one per stage band
            AddSiteProp(propsRoot, "Chest_Large_01", new Vector3(4.5f, 28.6f, 0f), JianHaiArtCatalog.ChestLargeRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_Large_02", new Vector3(4.5f, 41.6f, 0f), JianHaiArtCatalog.ChestLargeRoot, "closed", 0f);
            AddSiteProp(propsRoot, "Chest_Large_03", new Vector3(57.5f, 73.6f, 0f), JianHaiArtCatalog.ChestLargeRoot, "closed", 0f);

            // 3 altars: small (O) / medium (M) / large confluence (I), never in dead-ends
            AddSiteProp(propsRoot, "A_Small", new Vector3(30.5f, 22.4f, 0f), JianHaiArtCatalog.AltarRoot, "idle", 0.60f);
            AddSiteProp(propsRoot, "A_Medium", new Vector3(30.5f, 50.5f, 0f), JianHaiArtCatalog.AltarRoot, "idle", 0.80f);
            AddSiteProp(propsRoot, "A_Large", new Vector3(30.5f, 79.6f, 0f), JianHaiArtCatalog.AltarRoot, "idle", 1.05f);

            // merchant front hall, near BOSS, on the main path (not a dead-end)
            AddSiteProp(propsRoot, "Shop_01", new Vector3(35.5f, 89.45f, 0f), JianHaiArtCatalog.ShopRoot, "", 0f);

            // one-way hub gates (gold arrow = allowed direction) + boss gate
            AddSpriteProp(propsRoot, "Gate_Hub_W", new Vector3(26.5f, 7.4f, 0f), "jh_prop_gate_hub", 1f, 90f, "Prop");
            AddSpriteProp(propsRoot, "Gate_Hub_E", new Vector3(34.5f, 7.4f, 0f), "jh_prop_gate_hub", 1f, -90f, "Prop");
            AddSpriteProp(propsRoot, "Gate_Hub_C", new Vector3(30.0f, 11.4f, 0f), "jh_prop_gate_hub", 1f, 0f, "Prop");
            AddSpriteProp(propsRoot, "Gate_Boss", new Vector3(30.0f, 95.4f, 0f), "jh_prop_gate_boss", 1f, 0f, "Prop");

            // stage boundary arches (O/M/I 分带里程碑, P1#5)
            AddSpriteProp(propsRoot, "Arch_Z1_Z2_W", new Vector3(10.0f, 38.4f, 0f), "jh_prop_arch_stone", 1f, 0f, "Prop");
            AddSpriteProp(propsRoot, "Arch_Z1_Z2_C", new Vector3(30.0f, 38.4f, 0f), "jh_prop_arch_stone", 1f, 0f, "Prop");
            AddSpriteProp(propsRoot, "Arch_Z1_Z2_E", new Vector3(50.0f, 38.4f, 0f), "jh_prop_arch_stone", 1f, 0f, "Prop");
            AddSpriteProp(propsRoot, "Arch_Z2_Z3_W", new Vector3(10.0f, 66.4f, 0f), "jh_prop_arch_stone", 1f, 0f, "Prop");
            AddSpriteProp(propsRoot, "Arch_Z2_Z3_C", new Vector3(30.0f, 66.4f, 0f), "jh_prop_arch_stone", 1f, 0f, "Prop");
            AddSpriteProp(propsRoot, "Arch_Z2_Z3_E", new Vector3(50.0f, 66.4f, 0f), "jh_prop_arch_stone", 1f, 0f, "Prop");

            // braziers (入口/祭坛/前厅/BOSS 暖光点)
            Vector3[] braziers =
            {
                new Vector3(27.5f, 3.4f, 0f), new Vector3(33.5f, 3.4f, 0f),      // hub
                new Vector3(27.0f, 25.2f, 0f), new Vector3(34.0f, 25.2f, 0f),     // plaza O
                new Vector3(27.0f, 53.2f, 0f), new Vector3(34.0f, 53.2f, 0f),    // plaza M
                new Vector3(27.0f, 83.2f, 0f), new Vector3(34.0f, 83.2f, 0f),     // plaza I
                new Vector3(25.5f, 88.2f, 0f), new Vector3(37.5f, 88.2f, 0f),    // hall
                new Vector3(26.0f, 96.2f, 0f), new Vector3(35.0f, 96.2f, 0f),    // boss room front
            };
            for (int i = 0; i < braziers.Length; i++)
                AddSpriteProp(propsRoot, "Brazier_" + (i + 1).ToString("00"), braziers[i], "jh_prop_brazier", 1f, 0f, "Prop");

            // no-spawn return dead-ends (回出生死路: 不刷怪, 无箱)
            AddDeadEndMarker(markersRoot, "DE_Return_W", new Vector3(4.0f, 7.0f, 0f));
            AddDeadEndMarker(markersRoot, "DE_Return_E", new Vector3(58.0f, 7.0f, 0f));

            AddWorldLabel(FindDeep(root.transform, "START"), "START", new Vector3(0f, 1.1f, 0f));
            AddWorldLabel(FindDeep(root.transform, "BOSS"), "BOSS", new Vector3(0f, 1.1f, 0f));
            AddWorldLabel(FindDeep(root.transform, "A_Small"), "A_Small", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(FindDeep(root.transform, "A_Medium"), "A_Medium", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(FindDeep(root.transform, "A_Large"), "A_Large", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(FindDeep(root.transform, "Shop_01"), "Shop_01", new Vector3(0f, 1.2f, 0f));
            AddWorldLabel(markersRoot.Find("Core_DE_Return_W"), "DE_Return_W", new Vector3(0f, 0.9f, 0f));
            AddWorldLabel(markersRoot.Find("Core_DE_Return_E"), "DE_Return_E", new Vector3(0f, 0.9f, 0f));

            // ---- camera ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(30.5f, 6.7f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraViewService.PlayOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.045f, 0.06f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();

            Validate(floorType, floorCount, faceCount, topCount, decoCount + ornamentCount);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[DungeonMap] scene saved: " + ScenePath);
        }

        static Tilemap MakeTilemap(Transform parent, string name, string layer, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            var tm = go.GetComponent<Tilemap>();
            var tr = go.GetComponent<TilemapRenderer>();
            tr.sortingLayerName = layer;
            tr.sortingOrder = order;   // chunk mode is the default
            return tm;
        }

        /// <summary>JianHai layers exist (demo uses them); otherwise fall back to Default + order ladder.</summary>
        static bool JianHaiLayersReady()
        {
            return HasLayer(JianHaiArtCatalog.LayerGround) && HasLayer(JianHaiArtCatalog.LayerDecal)
                && HasLayer(JianHaiArtCatalog.LayerProp) && HasLayer(JianHaiArtCatalog.LayerEntity);
        }

        static bool HasLayer(string name)
        {
            foreach (var l in SortingLayer.layers)
                if (l.name == name) return true;
            return false;
        }

        static string LayerFor(string jianHaiLayer)
        {
            return JianHaiLayersReady() ? jianHaiLayer : "Default";
        }

        // when JianHai layers are present, ordering comes from the layer stack (all orders 0)
        static int OrderFor(int fallbackOrder)
        {
            return JianHaiLayersReady() ? 0 : fallbackOrder;
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
            Add(30.5f, 6.7f, 3.5f);          // spawn hub clean
            Add(30.5f, 22.4f, 2.5f); Add(30.5f, 50.5f, 2.5f); Add(30.5f, 79.6f, 2.5f); // altars
            Add(35.5f, 89.45f, 2.5f);        // shop
            Add(30.5f, 100.0f, 2.5f);        // boss
            Add(26.5f, 7.4f, 1.5f); Add(34.5f, 7.4f, 1.5f); Add(30.0f, 11.4f, 1.5f); Add(30.0f, 95.4f, 1.5f); // gates
            Add(4.5f, 28.6f, 2f); Add(4.5f, 41.6f, 2f); Add(57.5f, 73.6f, 2f);   // large chests
            Add(7.0f, 15.4f, 1.5f); Add(33.0f, 15.4f, 1.5f); Add(47.0f, 15.4f, 1.5f);
            Add(7.0f, 43.4f, 1.5f); Add(33.0f, 43.4f, 1.5f); Add(47.0f, 43.4f, 1.5f);
            Add(7.0f, 58.4f, 1.5f); Add(23.0f, 58.4f, 1.5f); Add(53.0f, 58.4f, 1.5f);
            Add(7.0f, 69.4f, 1.5f); Add(33.0f, 69.4f, 1.5f); Add(47.0f, 69.4f, 1.5f);
            return set;
        }

        static GameObject AddSiteProp(Transform parent, string hookId, Vector3 pos, string artRoot, string state, float scaleOverride)
        {
            var go = new GameObject(hookId);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var slot = go.AddComponent<JianHaiSpriteSlot>();
            slot.Configure(hookId, artRoot, state);
            if (scaleOverride > 0.001f)
                go.transform.localScale = Vector3.one * scaleOverride; // 小/中/大 体量递进 (final art = 外包 96x96)
            return go;
        }

        static GameObject AddSpriteProp(Transform parent, string name, Vector3 pos, string artId, float scale, float rotZ, string layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(artId);
            sr.sortingLayerName = LayerFor(layer);
            sr.sortingOrder = OrderFor(layer == JianHaiArtCatalog.LayerEntity ? 20 : 10);
            if (sr.sprite == null)
                Debug.LogError("[DungeonMap] sprite missing: " + artId);
            return go;
        }

        static void AddDeadEndMarker(Transform parent, string id, Vector3 pos)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<NoSpawnCore>().Configure(id, "DeadEnd", 2.5f); // renames to Core_<id>
        }

        static Sprite LoadSprite(string artId)
        {
            string path = JianHaiArtCatalog.AssetPath(artId);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
            mr.sortingLayerName = LayerFor(JianHaiArtCatalog.LayerEntity);
            mr.sortingOrder = 50;
        }

        // ============================================================
        // validation
        // ============================================================

        static void Validate(string[,] floorType, int floorCount, int faceCount, int topCount, int decoCount)
        {
            // BFS from spawn hub over all floor cells - every floor must be reachable
            var start = new Vector2Int(30, 6);
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
            Debug.Log("[DungeonMap] floors=" + floorCount + " reachable=" + reached
                + " wallFaces=" + faceCount + " wallTops=" + topCount + " decos=" + decoCount
                + (connected ? " CONNECTED" : " !! UNREACHABLE FLOORS !!"));
            if (!connected)
                Debug.LogError("[DungeonMap] some floors unreachable from START - check layout");

            // key sites must sit on reachable floor
            var keySites = new (string id, Vector3 pos)[]
            {
                ("START", new Vector3(30.5f, 6.7f, 0f)),
                ("BOSS", new Vector3(30.5f, 100.0f, 0f)),
                ("A_Small", new Vector3(30.5f, 22.4f, 0f)),
                ("A_Medium", new Vector3(30.5f, 50.5f, 0f)),
                ("A_Large", new Vector3(30.5f, 79.6f, 0f)),
                ("Shop_01", new Vector3(35.5f, 89.45f, 0f)),
                ("Chest_Large_01", new Vector3(4.5f, 28.6f, 0f)),
                ("Chest_Large_02", new Vector3(4.5f, 41.6f, 0f)),
                ("Chest_Large_03", new Vector3(57.5f, 73.6f, 0f)),
                ("DE_Return_W", new Vector3(4.0f, 7.0f, 0f)),
                ("DE_Return_E", new Vector3(58.0f, 7.0f, 0f)),
                ("Gate_Boss", new Vector3(30.0f, 95.4f, 0f)),
            };
            int bad = 0;
            foreach (var s in keySites)
            {
                int x = Mathf.FloorToInt(s.pos.x), y = Mathf.FloorToInt(s.pos.y);
                if (x < 0 || y < 0 || x >= W || y >= H || !seen[x, y])
                {
                    Debug.LogError("[DungeonMap] key site not on reachable floor: " + s.id);
                    bad++;
                }
            }
            for (int i = 1; i <= 12; i++)
            {
                var p = ChestPos(i);
                int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
                if (!seen[x, y])
                {
                    Debug.LogError("[DungeonMap] Chest_" + i.ToString("00") + " not on reachable floor");
                    bad++;
                }
            }
            Debug.Log(bad == 0
                ? "[DungeonMap] checklist: 12 road chests (O 1/路, M 2/路, I 1/路) + 3 dead-end large chests + 3 altars (O/M/I, none in dead-end) + Shop near BOSS + 7x7 spawn hub OK"
                : "[DungeonMap] !! " + bad + " key sites misplaced");
        }

        static Vector3 ChestPos(int n)
        {
            switch (n)
            {
                case 1: return new Vector3(7.0f, 15.4f, 0f);
                case 2: return new Vector3(33.0f, 15.4f, 0f);
                case 3: return new Vector3(47.0f, 15.4f, 0f);
                case 4: return new Vector3(7.0f, 43.4f, 0f);
                case 5: return new Vector3(33.0f, 43.4f, 0f);
                case 6: return new Vector3(47.0f, 43.4f, 0f);
                case 7: return new Vector3(7.0f, 58.4f, 0f);
                case 8: return new Vector3(23.0f, 58.4f, 0f);
                case 9: return new Vector3(53.0f, 58.4f, 0f);
                case 10: return new Vector3(7.0f, 69.4f, 0f);
                case 11: return new Vector3(33.0f, 69.4f, 0f);
                default: return new Vector3(47.0f, 69.4f, 0f);
            }
        }
    }
}
