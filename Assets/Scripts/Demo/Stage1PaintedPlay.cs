using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Painted Stage1Maze adapter. Stage1MazeDemo stays the single runtime driver
    /// (door lock / portal → spawn / room clear / player charge + HP). When this
    /// component sits next to the Demo and the painted Grid exists, the Demo uses
    /// the painted room rects below instead of the procedural skeleton, skips its
    /// placeholder floor/wall quads, and gives its own spawned Player a
    /// Rigidbody2D + foot-based CircleCollider2D. Wall collision comes from
    /// <see cref="BuildWallFootprints"/>: one box per wall cell footprint (the
    /// tall 32x64 wall art overhang is not solid), replacing the sprite-shaped
    /// TilemapCollider2D. Rects mirror Editor/JianHaiStage1MazeBuilder.PaintStage1
    /// (cell = 1 world unit).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class Stage1PaintedPlay : MonoBehaviour
    {
        public const string GridName = "Grid";
        public const float PlayerRadius = 0.28f;
        public const float CorridorWidth = 2f;
        /// <summary>Circle centre height above the feet (transform pivot), world units.</summary>
        public const float PlayerFootOffset = 0.08f;
        public const string WallsName = "Walls";
        public const string FootprintName = "WallFootprints";

        public bool HasPaintedGrid
        {
            get { return isActiveAndEnabled && transform.Find(GridName) != null; }
        }

        /// <summary>Painted layout: START(BL) → CHEST(BR) → N1(TR) → ALTAR(TL).</summary>
        public Stage1Maze BuildMaze(int seed)
        {
            MazeNode start = Room("START", MazeNodeKind.Start, 3, 2, 22, 17);
            MazeNode chest = Room("CHEST", MazeNodeKind.Chest, 29, 2, 48, 17);
            MazeNode n1 = Room("N1", MazeNodeKind.Normal, 29, 22, 48, 37);
            MazeNode altar = Room("ALTAR", MazeNodeKind.Altar, 3, 22, 22, 37);
            start.NeighborIds = new[] { chest.Id };
            chest.NeighborIds = new[] { start.Id, n1.Id };
            n1.NeighborIds = new[] { chest.Id, altar.Id };
            altar.NeighborIds = new[] { n1.Id };
            var maze = new Stage1Maze
            {
                Seed = seed,
                TemplateId = "S1_PAINTED",
                LargeChestUpgraded = false,
                Nodes = new[] { start, chest, n1, altar },
                Edges = new[] { Edge(start, chest), Edge(chest, n1), Edge(n1, altar) },
                Signature = "painted:START-CHEST-N1-ALTAR"
            };
            return maze;
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
            // AddComponent auto-fits the circle to the sprite bounds (centre ~0.43u above the
            // feet with pivot y=0.15). Keep it on the feet so corridor clearance matches the floor.
            col.offset = new Vector2(0f, PlayerFootOffset / s);
            // Zero friction so the circle slides along walls at steep angles instead of sticking.
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

        /// <summary>
        /// Replaces the Walls TilemapCollider2D (sprite rect = footprint + 1.5 cells of
        /// overhang) with one box per wall cell footprint, merged into a CompositeCollider2D.
        /// Level geometry and tiles are untouched. Returns the number of wall cells.
        /// </summary>
        public int BuildWallFootprints()
        {
            Transform grid = transform.Find(GridName);
            Transform wallsTr = grid != null ? grid.Find(WallsName) : null;
            Tilemap walls = wallsTr != null ? wallsTr.GetComponent<Tilemap>() : null;
            if (walls == null)
                return 0;
            var tileCol = walls.GetComponent<TilemapCollider2D>();
            if (tileCol != null)
                tileCol.enabled = false;

            Transform old = wallsTr.Find(FootprintName);
            if (old != null)
                Destroy(old.gameObject);

            var go = new GameObject(FootprintName);
            go.layer = wallsTr.gameObject.layer;
            go.transform.SetParent(wallsTr, false);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;

            walls.CompressBounds();
            BoundsInt b = walls.cellBounds;
            Vector3 cell = walls.layoutGrid != null ? walls.layoutGrid.cellSize : Vector3.one;
            int count = 0;
            for (int y = b.yMin; y < b.yMax; y++)
            {
                int x = b.xMin;
                while (x < b.xMax)
                {
                    if (!walls.HasTile(new Vector3Int(x, y, 0)))
                    {
                        x++;
                        continue;
                    }
                    int x0 = x;
                    while (x < b.xMax && walls.HasTile(new Vector3Int(x, y, 0)))
                        x++;
                    int len = x - x0;
                    count += len;
                    Vector3 corner = walls.CellToLocal(new Vector3Int(x0, y, 0));
                    var box = go.AddComponent<BoxCollider2D>();
                    box.usedByComposite = true;
                    box.size = new Vector2(len * cell.x, cell.y);
                    box.offset = new Vector2(corner.x + len * cell.x * 0.5f, corner.y + cell.y * 0.5f);
                }
            }
            composite.GenerateGeometry();
            return count;
        }

        static MazeNode Room(string id, MazeNodeKind kind, int x0, int y0, int x1, int y1)
        {
            float w = x1 - x0 + 1;
            float h = y1 - y0 + 1;
            return new MazeNode
            {
                Id = id,
                Kind = kind,
                Center = new MazeVec2(x0 + w * 0.5f, y0 + h * 0.5f),
                Width = w,
                Height = h,
                NeighborIds = new string[0]
            };
        }

        static MazeEdge Edge(MazeNode a, MazeNode b)
        {
            float len = a.Center.Dist(b.Center);
            return new MazeEdge
            {
                FromId = a.Id,
                ToId = b.Id,
                From = a.Center,
                To = b.Center,
                Width = CorridorWidth,
                Length = len,
                MaxSegment = len,
                FoldCount = 0,
                Points = new[] { a.Center, b.Center }
            };
        }
    }
}
