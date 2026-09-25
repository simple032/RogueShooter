using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Art;
using RogueShooter.Demo;

namespace RogueShooter.Tools
{
    /// <summary>
    /// Stage-1 tile asset gate + scene setup. Missing PNG or an unloadable sprite aborts before any
    /// tile-asset write and does not SaveScene. After a successful rebind, the Stage1Maze root gets
    /// Stage1MazeDemo + Stage1GenWorld (the 26 tile refs) and an empty Grid (Floor / Walls / Decor);
    /// SaveScene writes Assets/Scenes/Stage1Maze.scene. No geometry is baked: the generator
    /// (Stage1MazeGen, rooms 52×40, maps up to ~380×238) is painted at runtime by Stage1GenWorld
    /// (floor, ring walls with face/top/side/corner/opening frames, rubble).
    /// </summary>
    public static class JianHaiStage1MazeBuilder
    {
        const string ScenePath = "Assets/Scenes/Stage1Maze.scene";

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

            if (!SetupStage1(tiles))
                return;
            if (!SaveStage1Scene())
                return;

            Debug.Log("[Stage1] bound " + tiles.Count
                + " tiles to Stage1GenWorld in " + ScenePath
                + " (runtime generator paint: floor RM/CH/AL/CO, ring walls face/top/side/corner/opening, rubble). SaveScene done.");
        }

        /// <summary>Batchmode entry: Build() then exit 0 / 1.</summary>
        public static void BuildBatch()
        {
            bool ok = false;
            try
            {
                AssetDatabase.Refresh();
                Dictionary<string, Tile> tiles = EnsureTileAssets();
                ok = tiles != null && SetupStage1(tiles) && SaveStage1Scene();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Opens Stage1Maze; root = Stage1MazeDemo + Stage1GenWorld (tile refs), empty Grid tilemaps.
        /// Drops the old hand-painted Grid and any missing-script component (old Stage1PaintedPlay).
        /// </summary>
        static bool SetupStage1(Dictionary<string, Tile> tiles)
        {
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Stage1] could not open " + ScenePath + " — not built");
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

            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            if (root.GetComponent<Stage1MazeDemo>() == null)
                root.AddComponent<Stage1MazeDemo>();
            var gen = root.GetComponent<Stage1GenWorld>();
            if (gen == null)
                gen = root.AddComponent<Stage1GenWorld>();

            var ids = new string[JianHaiStage1Art.TileIds.Length];
            var assets = new TileBase[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = JianHaiStage1Art.TileIds[i];
                Tile t;
                if (!tiles.TryGetValue(ids[i], out t) || t == null)
                {
                    Debug.LogError("[Stage1] missing tile " + ids[i] + " — not built");
                    return false;
                }
                assets[i] = t;
            }
            gen.SetTiles(ids, assets);
            EditorUtility.SetDirty(gen);

            Transform oldGrid = root.transform.Find("Grid");
            if (oldGrid != null)
                Object.DestroyImmediate(oldGrid.gameObject);

            var gridGo = new GameObject(Stage1GenWorld.GridName);
            gridGo.transform.SetParent(root.transform, false);
            gridGo.AddComponent<Grid>();
            MakeTilemap(gridGo.transform, Stage1GenWorld.FloorName, JianHaiArtCatalog.LayerGround, 0);
            MakeTilemap(gridGo.transform, Stage1GenWorld.WallsName, JianHaiArtCatalog.LayerGround, 1);
            MakeTilemap(gridGo.transform, Stage1GenWorld.DecorName, JianHaiArtCatalog.LayerDecal, 5);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Stage1] scene setup: Stage1MazeDemo + Stage1GenWorld tiles=" + assets.Length
                + " emptyGrid=Floor/Walls/Decor removedMissingScripts=" + removed);
            return true;
        }

        static Tilemap MakeTilemap(Transform parent, string name, string layer, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            var tr = go.GetComponent<TilemapRenderer>();
            tr.sortingLayerName = layer;
            tr.sortingOrder = order;
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
