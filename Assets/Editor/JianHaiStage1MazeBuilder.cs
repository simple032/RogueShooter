using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Art;
using RogueShooter.Iso.Render;

namespace RogueShooter.Tools
{
    /// <summary>
    /// Stage-1 tile asset gate. Missing PNG or an unloadable sprite aborts before any
    /// tile-asset write and does not SaveScene. After a successful rebind, SaveScene
    /// writes Assets/Scenes/Stage1Maze.scene (opened first when it is not loaded).
    /// Geometry paint uses those 26 tiles (floor by room/chest/altar/corridor,
    /// walls face/top/side/corner/opening, rubble decals) then SaveScene.
    /// </summary>
    public static class JianHaiStage1MazeBuilder
    {
        const string ScenePath = "Assets/Scenes/Stage1Maze.scene";
        const int W = 52;
        const int H = 40;

        /// <summary>
        /// Entity sprites a later geometry pass should request.
        /// Skel uses the on-disk first frame. Archer/dog/mage use framed _00 ids.
        /// </summary>
        public static readonly string[] EntitySpriteIds =
        {
            JianHaiArtCatalog.PlayerIdle,
            JianHaiArtCatalog.EnemyE1Idle,
            JianHaiArtCatalog.EnemyDogIdle,
            JianHaiArtCatalog.EnemyMageIdle,
        };

        [MenuItem("Tools/JianHai/Build Stage1 Maze (v0.5)")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Dictionary<string, Tile> tiles = EnsureTileAssets();
            if (tiles == null)
                return;

            if (!PaintStage1(tiles))
                return;
            if (!SaveStage1Scene())
                return;

            Debug.Log("[Stage1] painted " + tiles.Count
                + " tiles into " + ScenePath
                + " (floor RM/CH/AL/CO, walls face/top/side/corner/opening, rubble). SaveScene done.");
        }

        /// <summary>
        /// Opens Stage1Maze and paints Floor / Walls / Decor. Does not touch DungeonMap.
        /// </summary>
        static bool PaintStage1(Dictionary<string, Tile> tiles)
        {
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Stage1] could not open " + ScenePath + " — not painted");
                return false;
            }

            GameObject root = null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "Stage1Maze")
                {
                    root = roots[i];
                    break;
                }
            }
            if (root == null)
            {
                root = new GameObject("Stage1Maze");
                EditorSceneManager.MoveGameObjectToScene(root, scene);
            }

            Transform oldGrid = root.transform.Find("Grid");
            if (oldGrid != null)
                Object.DestroyImmediate(oldGrid.gameObject);

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform, false);
            gridGo.AddComponent<Grid>();

            Tilemap floorMap = MakeTilemap(gridGo.transform, "Floor", "Ground", 0);
            Tilemap wallMap = MakeTilemap(gridGo.transform, "Walls", "Ground", 1);
            Tilemap decorMap = MakeTilemap(gridGo.transform, "Decor", "Decal", 5);
            wallMap.gameObject.AddComponent<TilemapCollider2D>();

            var floorType = new string[W, H];
            PaintRect(floorType, 3, 2, 22, 17, "room");
            PaintRect(floorType, 29, 2, 48, 17, "chest");
            PaintRect(floorType, 29, 22, 48, 37, "room");
            PaintRect(floorType, 3, 22, 22, 37, "altar");
            PaintRect(floorType, 23, 9, 28, 10, "corridor");
            PaintRect(floorType, 38, 18, 39, 21, "corridor");
            PaintRect(floorType, 23, 29, 28, 30, "corridor");

            var mouth = new HashSet<Vector2Int>();
            MarkMouth(mouth, 27, 9);
            MarkMouth(mouth, 38, 20);
            MarkMouth(mouth, 23, 29);

            var rand = new System.Random(20260921);
            int floorCount = 0;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    string kind = floorType[x, y];
                    if (kind == null)
                        continue;
                    floorCount++;
                    int v = rand.Next(100);
                    string suffix = v < 55 ? "_00" : v < 85 ? "_01" : "_02";
                    string id = "jh_tile_floor_s1_" + kind + suffix;
                    if (!tiles.ContainsKey(id))
                    {
                        Debug.LogError("[Stage1] missing floor tile " + id);
                        return false;
                    }
                    floorMap.SetTile(new Vector3Int(x, y, 0), tiles[id]);
                }
            }

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (floorType[x, y] != null)
                        continue;
                    string wallId = PickWall(floorType, mouth, x, y);
                    if (!tiles.ContainsKey(wallId))
                    {
                        Debug.LogError("[Stage1] missing wall tile " + wallId);
                        return false;
                    }
                    wallMap.SetTile(new Vector3Int(x, y, 0), tiles[wallId]);
                }
            }

            var keepOut = BuildKeepOut();
            int decoCount = 0;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    string kind = floorType[x, y];
                    if (kind == null || keepOut.Contains(new Vector2Int(x, y)))
                        continue;
                    int chance = kind == "corridor" ? 3 : 8;
                    if (rand.Next(100) >= chance)
                        continue;
                    string id = rand.Next(100) < 50 ? "jh_decal_s1_rubble_00" : "jh_decal_s1_rubble_01";
                    decorMap.SetTile(new Vector3Int(x, y, 0), tiles[id]);
                    decoCount++;
                }
            }

            if (floorCount < 100 || decoCount < 1)
            {
                Debug.LogError("[Stage1] paint too sparse floor=" + floorCount + " deco=" + decoCount);
                return false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Stage1] paint floor=" + floorCount + " rubble=" + decoCount);
            return true;
        }

        static void PaintRect(string[,] floorType, int x0, int y0, int x1, int y1, string kind)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    floorType[x, y] = kind;
        }

        static void MarkMouth(HashSet<Vector2Int> mouth, int x, int y)
        {
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                    mouth.Add(new Vector2Int(x + dx, y + dy));
        }

        static bool IsFloor(string[,] floorType, int x, int y)
        {
            return x >= 0 && y >= 0 && x < W && y < H && floorType[x, y] != null;
        }

        static string PickWall(string[,] floorType, HashSet<Vector2Int> mouth, int x, int y)
        {
            if (mouth.Contains(new Vector2Int(x, y - 1))) return "jh_wall_s1_stone_opening_s";
            if (mouth.Contains(new Vector2Int(x, y + 1))) return "jh_wall_s1_stone_opening_n";
            if (mouth.Contains(new Vector2Int(x - 1, y))) return "jh_wall_s1_stone_opening_w";
            if (mouth.Contains(new Vector2Int(x + 1, y))) return "jh_wall_s1_stone_opening_e";
            if (IsFloor(floorType, x, y - 1)) return "jh_wall_s1_stone_face";
            if (IsFloor(floorType, x + 1, y)) return "jh_wall_s1_stone_side_e";
            if (IsFloor(floorType, x - 1, y)) return "jh_wall_s1_stone_side_w";
            if (IsFloor(floorType, x + 1, y - 1)) return "jh_wall_s1_stone_corner_se";
            if (IsFloor(floorType, x - 1, y - 1)) return "jh_wall_s1_stone_corner_sw";
            if (IsFloor(floorType, x + 1, y + 1)) return "jh_wall_s1_stone_corner_ne";
            if (IsFloor(floorType, x - 1, y + 1)) return "jh_wall_s1_stone_corner_nw";
            return "jh_wall_s1_stone_top";
        }

        static HashSet<Vector2Int> BuildKeepOut()
        {
            var set = new HashSet<Vector2Int>();
            void Add(int cx, int cy, int r)
            {
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        set.Add(new Vector2Int(cx + dx, cy + dy));
            }
            Add(13, 10, 3);
            Add(17, 13, 2);
            Add(42, 12, 2);
            Add(34, 30, 2);
            Add(7, 34, 2);
            Add(36, 9, 2);
            Add(13, 30, 3);
            Add(28, 10, 2);
            Add(39, 21, 2);
            Add(24, 30, 2);
            return set;
        }

        static Tilemap MakeTilemap(Transform parent, string name, string layer, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            var tr = go.GetComponent<TilemapRenderer>();
            tr.sortingLayerName = layer;
            tr.sortingOrder = order;
            IsoUrpEditorMaterials.AssignLit(tr);
            return go.GetComponent<Tilemap>();
        }

        /// <summary>
        /// Producer order after paint: open Stage1Maze if needed, then SaveScene.
        /// </summary>
        static bool SaveStage1Scene()
        {
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Stage1] could not open " + ScenePath + " — scene not saved");
                return false;
            }

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[Stage1] SaveScene failed: " + ScenePath);
                return false;
            }

            Debug.Log("[Stage1] SaveScene " + ScenePath);
            return true;
        }

        /// <summary>
        /// Returns the 26 tiles when every PNG exists and imports as a sprite.
        /// Returns null when any PNG is missing or any sprite fails to load.
        /// Null means the caller must not SaveScene and must not keep partial tile writes.
        /// </summary>
        public static Dictionary<string, Tile> EnsureTileAssets()
        {
            var missing = new List<string>();
            for (int i = 0; i < JianHaiStage1Art.TileIds.Length; i++)
            {
                string id = JianHaiStage1Art.TileIds[i];
                if (!PngExists(id))
                    missing.Add(JianHaiStage1Art.PngPath(id));
            }
            if (missing.Count > 0)
            {
                Debug.LogError("[Stage1] missing png (" + missing.Count
                    + ") — abort rebuild, scene not saved. First: " + missing[0]
                    + ". Drop files at Assets/Art/JianHai/Tiles/<id>.png");
                return null;
            }

            for (int i = 0; i < JianHaiStage1Art.TileIds.Length; i++)
            {
                string id = JianHaiStage1Art.TileIds[i];
                FixImporterIfNeeded(JianHaiStage1Art.PngPath(id), id);
            }

            string reason = JianHaiStage1Art.RejectReason(PngExists, SpriteLoads);
            if (reason != null)
            {
                Debug.LogError("[Stage1] " + reason + " — abort rebuild, scene not saved");
                return null;
            }

            var loaded = new List<KeyValuePair<string, Sprite>>();
            for (int i = 0; i < JianHaiStage1Art.TileIds.Length; i++)
            {
                string id = JianHaiStage1Art.TileIds[i];
                string path = JianHaiStage1Art.PngPath(id);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogError("[Stage1] sprite not importable: " + path + " — abort rebuild, scene not saved");
                    return null;
                }
                loaded.Add(new KeyValuePair<string, Sprite>(id, sprite));
            }

            if (loaded.Count != JianHaiStage1Art.ExpectedTileCount)
            {
                Debug.LogError("[Stage1] loaded " + loaded.Count + " sprites — abort rebuild, scene not saved");
                return null;
            }

            var tiles = new Dictionary<string, Tile>();
            for (int i = 0; i < loaded.Count; i++)
            {
                string id = loaded[i].Key;
                Tile.ColliderType collider = id.StartsWith("jh_wall_")
                    ? Tile.ColliderType.Sprite
                    : Tile.ColliderType.None;
                tiles[id] = EnsureTileAsset(id, loaded[i].Value, collider);
            }
            AssetDatabase.SaveAssets();
            return tiles;
        }

        static bool PngExists(string id)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string rel = JianHaiStage1Art.PngPath(id);
            return File.Exists(Path.GetFullPath(Path.Combine(root, rel)));
        }

        static bool SpriteLoads(string id)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(JianHaiStage1Art.PngPath(id)) != null;
        }

        static bool FixImporterIfNeeded(string path, string id)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
                return false;
            Vector2 pivot = PivotFor(id);
            if (ti.textureType == TextureImporterType.Sprite
                && ti.spriteImportMode == SpriteImportMode.Single
                && ti.filterMode == FilterMode.Point
                && Mathf.Approximately(ti.spritePixelsPerUnit, JianHaiArtCatalog.Ppu)
                && (ti.spritePivot - pivot).sqrMagnitude < 0.0001f)
                return false;
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
            if (id.StartsWith("jh_wall_"))
                return new Vector2(0.5f, 0f);
            return new Vector2(0.5f, 0.5f);
        }

        static Tile EnsureTileAsset(string name, Sprite sprite, Tile.ColliderType collider)
        {
            string path = JianHaiStage1Art.TilesFolder + name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
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
    }
}
