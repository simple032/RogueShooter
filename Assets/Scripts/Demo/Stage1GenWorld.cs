using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Art;
using RogueShooter.Combat;
using RogueShooter.Maze;
using Debug = UnityEngine.Debug;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Stage-1 generator geometry at runtime (replaces the hand-painted 52×40 Grid). Stage1MazeDemo stays the
    /// single driver; after <see cref="Stage1MazeGen.Generate"/> it calls <see cref="Build"/>, which rasterizes
    /// the maze (<see cref="Stage1MazeRaster"/>) and from that one grid:
    /// 1. paints Grid/Floor, Walls, Decor tilemaps (floor by room kind, ring walls with face/side/corner/opening
    ///    frames, rubble) — tiles are serialized refs written by Editor/JianHaiStage1MazeBuilder;
    /// 2. builds the Rigidbody wall footprints (one box per merged wall rect → CompositeCollider2D; the tall wall
    ///    art overhang is not solid);
    /// 3. registers the same rects as CollisionWorld Wall volumes, so corridor walls stop mobs (TryMove) and
    ///    arrows / mage orbs (Trace) as well as room walls.
    /// Walls are only the ring around the floor (8-neighbourhood), never the whole bounding box.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class Stage1GenWorld : MonoBehaviour
    {
        public const string GridName = "Grid";
        public const string FloorName = "Floor";
        public const string WallsName = "Walls";
        public const string DecorName = "Decor";
        public const string FootprintName = "WallFootprints";
        public const string VolumesName = "WallVolumes";
        public const float PlayerRadius = 0.28f;
        /// <summary>Circle centre height above the feet (transform pivot), world units.</summary>
        public const float PlayerFootOffset = 0.08f;

        [SerializeField] string[] tileIds = new string[0];
        [SerializeField] TileBase[] tileAssets = new TileBase[0];

        Dictionary<string, TileBase> _tiles;
        Tilemap _floor;
        Tilemap _walls;
        Tilemap _decor;
        GameObject _footprints;
        GameObject _volumes;

        public Stage1MazeRaster Raster { get; private set; }
        public float LastRasterMs { get; private set; }
        public float LastTilemapMs { get; private set; }
        public float LastColliderMs { get; private set; }
        public float LastVolumesMs { get; private set; }
        public int LastFootprintBoxes { get; private set; }
        public int LastCompositePaths { get; private set; }
        public int MissingTiles { get; private set; }
        public Tilemap WallsMap => _walls;
        public Tilemap FloorMap => _floor;
        public CompositeCollider2D Composite => _footprints != null ? _footprints.GetComponent<CompositeCollider2D>() : null;

        /// <summary>Editor builder: tile refs in <see cref="JianHaiStage1Art.TileIds"/> order.</summary>
        public void SetTiles(string[] ids, TileBase[] assets)
        {
            tileIds = ids ?? new string[0];
            tileAssets = assets ?? new TileBase[0];
            _tiles = null;
        }

        public int TileCount => tileAssets != null ? tileAssets.Length : 0;

        TileBase Tile(string id)
        {
            if (_tiles == null)
            {
                _tiles = new Dictionary<string, TileBase>();
                for (int i = 0; tileIds != null && i < tileIds.Length && i < tileAssets.Length; i++)
                {
                    if (!string.IsNullOrEmpty(tileIds[i]) && tileAssets[i] != null)
                        _tiles[tileIds[i]] = tileAssets[i];
                }
            }

            TileBase t;
            if (id != null && _tiles.TryGetValue(id, out t))
                return t;
            MissingTiles++;
            return null;
        }

        /// <summary>Rasterize + paint + collision for <paramref name="maze"/>. Returns the raster.</summary>
        public Stage1MazeRaster Build(Stage1Maze maze, int seed)
        {
            MissingTiles = 0;
            var sw = Stopwatch.StartNew();
            Raster = Stage1MazeRaster.Build(maze);
            LastRasterMs = (float)sw.Elapsed.TotalMilliseconds;

            EnsureTilemaps();
            sw.Restart();
            Paint(Raster, seed);
            LastTilemapMs = (float)sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            BuildFootprints(Raster);
            LastColliderMs = (float)sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            BuildVolumes(Raster);
            LastVolumesMs = (float)sw.Elapsed.TotalMilliseconds;
            if (MissingTiles > 0)
                Debug.LogError("[S1Gen] missing tile refs x" + MissingTiles + " — run Tools/JianHai/Build Stage1 Maze (v0.5)");
            return Raster;
        }

        public string PerfLine()
        {
            Stage1MazeRaster r = Raster;
            return "[S1Gen] " + (r != null ? r.Describe() : "-")
                   + " rasterMs=" + LastRasterMs.ToString("0.0")
                   + " tilemapMs=" + LastTilemapMs.ToString("0.0")
                   + " compositeMs=" + LastColliderMs.ToString("0.0") + " (boxes=" + LastFootprintBoxes + " paths=" + LastCompositePaths + ")"
                   + " worldVolumesMs=" + LastVolumesMs.ToString("0.0");
        }

        void EnsureTilemaps()
        {
            Transform grid = transform.Find(GridName);
            if (grid == null)
            {
                var gridGo = new GameObject(GridName);
                gridGo.transform.SetParent(transform, false);
                gridGo.AddComponent<Grid>();
                grid = gridGo.transform;
            }

            _floor = EnsureMap(grid, FloorName, JianHaiArtCatalog.LayerGround, 0);
            _walls = EnsureMap(grid, WallsName, JianHaiArtCatalog.LayerGround, 1);
            _decor = EnsureMap(grid, DecorName, JianHaiArtCatalog.LayerDecal, 5);
            var tileCol = _walls.GetComponent<TilemapCollider2D>();
            if (tileCol != null)
                tileCol.enabled = false;
        }

        static Tilemap EnsureMap(Transform grid, string name, string layer, int order)
        {
            Transform t = grid.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            if (t == null)
                go.transform.SetParent(grid, false);
            var map = go.GetComponent<Tilemap>();
            if (map == null)
                map = go.AddComponent<Tilemap>();
            var tr = go.GetComponent<TilemapRenderer>();
            if (tr == null)
                tr = go.AddComponent<TilemapRenderer>();
            tr.sortingLayerName = layer;
            tr.sortingOrder = order;
            return map;
        }

        void Paint(Stage1MazeRaster r, int seed)
        {
            _floor.ClearAllTiles();
            _walls.ClearAllTiles();
            _decor.ClearAllTiles();
            var rand = new System.Random(seed * 7919 + 20260921);
            var fPos = new List<Vector3Int>(r.FloorCount);
            var fTile = new List<TileBase>(r.FloorCount);
            var wPos = new List<Vector3Int>(r.WallCount);
            var wTile = new List<TileBase>(r.WallCount);
            var dPos = new List<Vector3Int>();
            var dTile = new List<TileBase>();
            for (int y = r.MinY; y < r.MaxY; y++)
            {
                for (int x = r.MinX; x < r.MaxX; x++)
                {
                    MazeCell c = r.Get(x, y);
                    if (c == MazeCell.Wall)
                    {
                        wPos.Add(new Vector3Int(x, y, 0));
                        wTile.Add(Tile(r.WallTileId(x, y)));
                        continue;
                    }

                    string kind = r.FloorKind(x, y);
                    if (kind == null)
                        continue;
                    int v = rand.Next(100);
                    string suffix = v < 55 ? "_00" : v < 85 ? "_01" : "_02";
                    fPos.Add(new Vector3Int(x, y, 0));
                    fTile.Add(Tile("jh_tile_floor_s1_" + kind + suffix));
                    if (c == MazeCell.Mouth || NearKeepOut(r, x, y))
                        continue;
                    int chance = c == MazeCell.Room ? 8 : 3;
                    if (rand.Next(100) >= chance)
                        continue;
                    dPos.Add(new Vector3Int(x, y, 0));
                    dTile.Add(Tile(rand.Next(100) < 50 ? "jh_decal_s1_rubble_00" : "jh_decal_s1_rubble_01"));
                }
            }

            _floor.SetTiles(fPos.ToArray(), fTile.ToArray());
            _walls.SetTiles(wPos.ToArray(), wTile.ToArray());
            _decor.SetTiles(dPos.ToArray(), dTile.ToArray());
        }

        /// <summary>No rubble within 2 cells of a door line or 3 cells of a room centre (props / sites).</summary>
        static bool NearKeepOut(Stage1MazeRaster r, int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (r.IsMouth(x + dx, y + dy))
                        return true;
            int ri = r.RoomAt(x, y);
            if (ri >= 0)
            {
                MazeNode n = r.Maze.Nodes[ri];
                if (Mathf.Abs(x + 0.5f - n.Center.X) <= 3.5f && Mathf.Abs(y + 0.5f - n.Center.Y) <= 3.5f)
                    return true;
            }

            return false;
        }

        void BuildFootprints(Stage1MazeRaster r)
        {
            Kill(_footprints);
            Transform old = _walls.transform.Find(FootprintName);
            if (old != null)
                Kill(old.gameObject);
            var go = new GameObject(FootprintName);
            go.layer = CollisionRules.UnityLayerWall;
            go.transform.SetParent(_walls.transform, false);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            for (int i = 0; i < r.WallRects.Count; i++)
            {
                CellRect rc = r.WallRects[i];
                var box = go.AddComponent<BoxCollider2D>();
                box.usedByComposite = true;
                box.size = new Vector2(rc.W, rc.H);
                box.offset = new Vector2(rc.CenterX, rc.CenterY);
            }

            composite.GenerateGeometry();
            LastFootprintBoxes = r.WallRects.Count;
            LastCompositePaths = composite.pathCount;
            _footprints = go;
        }

        void BuildVolumes(Stage1MazeRaster r)
        {
            Kill(_volumes);
            _volumes = new GameObject(VolumesName);
            _volumes.transform.SetParent(transform, false);
            for (int i = 0; i < r.WallRects.Count; i++)
            {
                CellRect rc = r.WallRects[i];
                var go = new GameObject("GenWall_" + i);
                go.transform.SetParent(_volumes.transform, false);
                go.transform.position = new Vector3(rc.CenterX, rc.CenterY, 1f);
                CollisionVolume.Add(go, CollisionLayer.Wall, true, rc.W * 0.5f, rc.H * 0.5f);
            }
        }

        static void Kill(GameObject go)
        {
            if (go == null)
                return;
            if (Application.isPlaying)
            {
                go.SetActive(false);
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        /// <summary>Adds physics to the Demo-spawned player. Call before PlayerMotor2D is added.</summary>
        public static void AttachPhysics(GameObject player)
        {
            if (player == null)
                return;
            var body = player.GetComponent<Rigidbody2D>();
            if (body == null)
                body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = player.GetComponent<CircleCollider2D>();
            if (col == null)
                col = player.AddComponent<CircleCollider2D>();
            // Player root is scaled by EntityStubWorldScale; keep the world radius fixed.
            float s = Mathf.Max(0.01f, Mathf.Abs(player.transform.localScale.x));
            col.radius = PlayerRadius / s;
            // Keep the circle on the feet (not the auto-fitted sprite centre) so corridor clearance matches the floor.
            col.offset = new Vector2(0f, PlayerFootOffset / s);
            col.sharedMaterial = NoFriction;
            body.sharedMaterial = NoFriction;
        }

        static PhysicsMaterial2D _noFriction;

        /// <summary>Runtime zero-friction / zero-bounce material (no asset).</summary>
        public static PhysicsMaterial2D NoFriction
        {
            get
            {
                if (_noFriction == null)
                    _noFriction = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
                return _noFriction;
            }
        }
    }
}
