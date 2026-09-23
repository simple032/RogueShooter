using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Art;

namespace RogueShooter.Tools
{
    /// <summary>
    /// Stage-1 tile asset gate. Missing PNG or an unloadable sprite aborts before any
    /// tile-asset write and does not SaveScene. After a successful rebind, SaveScene
    /// writes Assets/Scenes/Stage1Maze.scene (opened first when it is not loaded).
    /// Geometry paint of that scene is still not performed here.
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

            if (!SaveStage1Scene())
                return;

            Debug.Log("[Stage1] rebound " + tiles.Count
                + " tile assets by filename. PNGs have landed. Entity contract: "
                + string.Join(", ", EntitySpriteIds)
                + ". SaveScene " + ScenePath
                + ". Geometry paint is still not in this builder (deferred painter was never restored); scene remains the runtime skeleton.");
        }

        /// <summary>
        /// Producer order after the tile gate: open Stage1Maze if needed, then SaveScene.
        /// Does not paint maze geometry.
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
