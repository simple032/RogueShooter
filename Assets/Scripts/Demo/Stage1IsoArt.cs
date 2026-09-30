using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Iso;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Dresses the generated stage-1 maze with the accepted iso sprites.
    /// Grid is Isometric with cell size (1, 0.5); one tile = one full 2:1
    /// diamond, so neighbouring cells share edges with no seams.
    /// Cell math (verified against IsoSampleRoom): cell (u,v) center =
    /// ((u-v)*0.5, (u+v)*0.25 + 0.25). p = u-v = 2*wx, q = u+v = 4*wy - 1.
    /// Walls chain along exposed diamond edges: far rims (S, E) get tall
    /// wall_l/wall_r, near rims (N, W) get low walls plus platform edges.
    /// </summary>
    public static class Stage1IsoArt
    {
        const string FloorRoot = "Assets/Art/JianHai/Iso/Tiles/S1/Floor/jh_iso_floor_s1_";
        const string WallRoot = "Assets/Art/JianHai/Iso/Tiles/S1/Wall/jh_iso_wall_s1_";
        const string EdgeRoot = "Assets/Art/JianHai/Iso/Tiles/S1/Edge/jh_iso_edge_s1_";
        const string DecalRoot = "Assets/Art/JianHai/Iso/Tiles/S1/Decal/jh_iso_decal_s1_";
        const string PropRoot = "Assets/Art/JianHai/Iso/Props/jh_iso_prop_";
        const string ActorRoot = "Assets/Art/JianHai/Iso/Characters/Archer/jh_archer_";
        const string EnemyRoot = "Assets/Art/JianHai/Iso/Enemies/";

        const float MarginX = 0.501f;
        const float MarginY = 0.251f;
        const int WallOrder = 0;
        const int DecalOrder = -5;
        const int FloorOrder = -10;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        struct CellRect
        {
            public int PMin, PMax, QMin, QMax;
        }

        struct DoorMark
        {
            public Vector2 Point;
            public char Rim;
        }

        // ---- public API ----

        public static int FloorCount;

        public static void HideHandPlaced(GameObject demo)
        {
            if (demo == null)
                return;
            Transform root = demo.transform;
            for (int i = 0; i < root.childCount; i++)
                root.GetChild(i).gameObject.SetActive(false);

            var scene = demo.scene;
            if (!scene.IsValid())
                return;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == demo)
                    continue;
                if (IsHandPlaced(roots[i].name))
                    roots[i].SetActive(false);
            }

            Transform[] all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && IsHandPlaced(all[i].name))
                    all[i].gameObject.SetActive(false);
            }
        }

        static bool IsHandPlaced(string name)
        {
            return name == "Grid" || name == "Floor" || name == "Walls" || name == "Decor"
                || name == "Player" || name.StartsWith("FX_Portal");
        }

        /// <summary>Floors the whole maze: room tiles by room kind, corridor tiles elsewhere.</summary>
        public static GameObject BuildFloors(Stage1Maze maze, Transform parent)
        {
            var root = new GameObject("IsoFloor");
            root.transform.SetParent(parent, false);
            var grid = root.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            var mapGo = new GameObject("FloorTiles");
            mapGo.transform.SetParent(root.transform, false);
            var map = mapGo.AddComponent<Tilemap>();
            var rend = mapGo.AddComponent<TilemapRenderer>();
            rend.sortingOrder = FloorOrder;

            var owned = new HashSet<long>();
            var positions = new List<Vector3Int>();
            var tiles = new List<TileBase>();

            Tile[] roomTiles = FloorTiles(MazeNodeKind.Connector);
            Tile[] chestTiles = FloorTiles(MazeNodeKind.Chest);
            Tile[] altarTiles = FloorTiles(MazeNodeKind.Altar);
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                CellRect rc = RectCells(n);
                for (int p = rc.PMin; p <= rc.PMax; p++)
                {
                    for (int q = rc.QMin; q <= rc.QMax; q++)
                    {
                        if (((p + q) & 1) != 0)
                            continue;
                        long id = Pack(p, q);
                        if (!owned.Add(id))
                            continue;
                        positions.Add(new Vector3Int((p + q) / 2, (q - p) / 2, 0));
                        tiles.Add(FloorForCell(n, p, q, rc, roomTiles, chestTiles, altarTiles));
                    }
                }
            }

            Tile[] corridorTiles = new[]
            {
                MakeTile(FloorRoot + "corridor_00.png"),
                MakeTile(FloorRoot + "corridor_01.png"),
                MakeTile(FloorRoot + "corridor_02.png"),
            };
            for (int e = 0; e < maze.Edges.Length; e++)
            {
                MazeEdge edge = maze.Edges[e];
                MazeVec2[] pts = edge.Points;
                if (pts == null || pts.Length < 2)
                    pts = new[] { edge.From, edge.To };
                for (int s = 0; s < pts.Length - 1; s++)
                    CoverCorridorSegment(
                        new Vector2(pts[s].X, pts[s].Y),
                        new Vector2(pts[s + 1].X, pts[s + 1].Y),
                        maze, owned, positions, tiles, corridorTiles);
            }

            if (positions.Count > 0)
            {
                map.SetTiles(positions.ToArray(), tiles.ToArray());
                map.RefreshAllTiles();
            }
            FloorCount = positions.Count;
            return root;
        }

        /// <summary>Walls, low walls, platform edges, doors and corners for one room.</summary>
        public static void BuildRoomShell(MazeNode node, Stage1Maze maze, Transform parent, List<GameObject> into, int seed, List<GameObject> doorArches)
        {
            CellRect rc = RectCells(node);
            var doors = CollectDoors(node, maze);
            var holes = new HashSet<long>();
            var caps = new Dictionary<long, int>();
            foreach (DoorMark door in doors)
                MarkHole(rc, door, holes, caps);

            // The corner pieces replace the rim pieces on their own cells.
            var cornerCells = new HashSet<long>();
            if (((rc.PMax - 1 + rc.QMax) & 1) == 0)
                cornerCells.Add(Pack(rc.PMax - 1, rc.QMax));   // back corner_out
            if (((rc.PMin + 1 + rc.QMin) & 1) == 0)
                cornerCells.Add(Pack(rc.PMin + 1, rc.QMin));   // front low_corner

            for (int p = rc.PMin; p <= rc.PMax; p++)
            {
                for (int q = rc.QMin; q <= rc.QMax; q++)
                {
                    if (((p + q) & 1) != 0)
                        continue;
                    int u = (p + q) / 2;
                    int v = (q - p) / 2;
                    int capMask;
                    caps.TryGetValue(Pack(p, q), out capMask);
                    bool hole = holes.Contains(Pack(p, q)) || cornerCells.Contains(Pack(p, q));
                    PlaceRimEdges(rc, u, v, p, q, hole, capMask, parent, into);
                }
            }

            PlaceDoorArches(node, rc, doors, parent, into, doorArches);
            PlaceCorners(rc, parent, into);
            PlaceEdgeRow(rc, 'S', parent, into);
            PlaceEdgeRow(rc, 'W', parent, into);
            PlaceRubble(node, rc, seed, parent, into);
        }

        /// <summary>Chest / altar / gate props with feet on the tiles. Returns the prop object.</summary>
        public static GameObject BuildProp(MazeNode node, Transform parent)
        {
            if (node.Kind == MazeNodeKind.Chest || node.Kind == MazeNodeKind.LargeChest)
            {
                Vector3 anchor = NearestRoomCellCenter(node);
                return Place("Chest_" + node.Id, anchor, Load(PropRoot + "chest_closed_00.png"), WallOrder, false, parent);
            }
            if (node.Kind == MazeNodeKind.Altar)
            {
                Vector3 pivot = AltarPivot(node);
                return Place("Altar_" + node.Id, pivot, Load(PropRoot + "altar_00.png"), WallOrder, false, parent);
            }
            if (node.Kind == MazeNodeKind.Connector)
            {
                Vector3 center = new Vector3(node.Center.X, node.Center.Y, 0f);
                return Place("GateHub_" + node.Id, center, Load(PropRoot + "gate_hub_00.png"), WallOrder, false, parent);
            }
            return null;
        }

        public static void OpenChest(GameObject chest)
        {
            if (chest == null)
                return;
            var sr = chest.GetComponent<SpriteRenderer>();
            Sprite open = Load(PropRoot + "chest_open_00.png");
            if (sr != null && open != null)
                sr.sprite = open;
        }

        public static Sprite ActorSprite(string family, bool moving, string facing, int frame, out bool flip)
        {
            string action = "idle";
            int span = 4;
            if (moving)
            {
                action = "walk";
                span = 6;
            }
            return ActorAction(family, action, facing, frame, span, out flip);
        }

        public static Sprite ActorAction(string family, string action, string facing, int frame, int span, out bool flip)
        {
            flip = false;
            string src = facing;
            if (family != "archer" && !IsoFacing.TrySkeleton(facing, out src, out flip))
                src = "s";
            if (string.IsNullOrEmpty(action))
                action = "idle";
            if (span < 1)
                span = 1;
            int f = frame < 0 ? 0 : frame;
            Sprite sprite = LoadActor(family, action, src, f % span);
            if (sprite == null && action != "idle" && action != "walk" && action != "atk" && action != "cast")
                sprite = LoadActor(family, "idle", src, f % 3);
            if (sprite == null)
                sprite = LoadActor(family, "idle", src, 0);
            return sprite;
        }

        public static Sprite RollSprite(string facing, int frame)
        {
            return Load(ActorRoot + "roll_" + facing + "_" + Mathf.Clamp(frame, 0, 7).ToString("00") + ".png");
        }

        public static Sprite AttackSprite(string facing, int frame)
        {
            return Load(ActorRoot + "atk_" + facing + "_" + Mathf.Clamp(frame, 0, 4).ToString("00") + ".png");
        }

        public static Sprite ArrowFlySprite(int frame)
        {
            int index = frame < 0 ? 0 : frame % 2;
            return Load("Assets/Art/JianHai/Iso/Projectiles/jh_iso_proj_arrow_fly_" + index.ToString("00") + ".png");
        }

        public static Sprite MageOrbSprite(int frame)
        {
            int index = frame < 0 ? 0 : frame % 4;
            return Load("Assets/Art/JianHai/Iso/Projectiles/jh_iso_proj_orb_mage_fly_" + index.ToString("00") + ".png");
        }

        public static Sprite PickupCoin()
        {
            return Load("Assets/Art/JianHai/Iso/Props/jh_iso_pickup_coin_00.png");
        }

        public static Sprite LoadSprite(string path)
        {
            return Load(path);
        }

        // ---- cell math ----

        static CellRect RectCells(MazeNode n)
        {
            float x0 = n.Center.X - n.Width * 0.5f - MarginX;
            float x1 = n.Center.X + n.Width * 0.5f + MarginX;
            float y0 = n.Center.Y - n.Height * 0.5f - MarginY;
            float y1 = n.Center.Y + n.Height * 0.5f + MarginY;
            return new CellRect
            {
                PMin = Mathf.CeilToInt(2f * x0),
                PMax = Mathf.FloorToInt(2f * x1),
                QMin = Mathf.CeilToInt(4f * y0 - 1f),
                QMax = Mathf.FloorToInt(4f * y1 - 1f),
            };
        }

        static Vector3 CellCenter(int u, int v)
        {
            return new Vector3((u - v) * 0.5f, (u + v) * 0.25f + 0.25f, 0f);
        }

        static long Pack(int p, int q)
        {
            return ((long)p << 32) | (uint)q;
        }

        static int WrapIndex(int value, int mod)
        {
            int r = value % mod;
            return r < 0 ? r + mod : r;
        }

        // ---- floors ----

        /// <summary>
        /// Field is room stone. Chest and altar keep a pad of their own tiles
        /// so the room reads. Not one repeating set across every cell.
        /// </summary>
        static Tile FloorForCell(MazeNode n, int p, int q, CellRect rc, Tile[] room, Tile[] chest, Tile[] altar)
        {
            Vector3 world = CellCenter((p + q) / 2, (q - p) / 2);
            if ((n.Kind == MazeNodeKind.Chest || n.Kind == MazeNodeKind.LargeChest) && chest != null && chest.Length > 0)
            {
                Vector3 anchor = NearestRoomCellCenter(n);
                if ((world - anchor).sqrMagnitude <= 2.6f * 2.6f)
                    return chest[CellHash(p, q, 1) % chest.Length];
            }
            if (n.Kind == MazeNodeKind.Altar && altar != null && altar.Length > 0)
            {
                Vector3 pivot = AltarPivot(n);
                if ((world - pivot).sqrMagnitude <= 2.8f * 2.8f)
                    return altar[CellHash(p, q, 2) % altar.Length];
            }

            if (room == null || room.Length == 0)
                return null;
            bool rim = p - rc.PMin <= 2 || rc.PMax - p <= 2 || q - rc.QMin <= 2 || rc.QMax - q <= 2;
            int patch = CellHash(p / 5, q / 5, n.Id.GetHashCode());
            if (rim)
                return room[patch % 2];
            int speck = CellHash(p, q, n.Id.GetHashCode());
            if (room.Length >= 4 && (speck % 5) == 0)
                return room[2 + (speck % 2)];
            return room[patch % room.Length];
        }

        static int CellHash(int a, int b, int salt)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ salt * 83492791;
                return h & 0x7fffffff;
            }
        }

        static Tile[] FloorTiles(MazeNodeKind kind)
        {
            switch (kind)
            {
                case MazeNodeKind.Chest:
                case MazeNodeKind.LargeChest:
                    return new[]
                    {
                        MakeTile(FloorRoot + "chest_00.png"),
                        MakeTile(FloorRoot + "chest_01.png"),
                        MakeTile(FloorRoot + "chest_02.png"),
                    };
                case MazeNodeKind.Altar:
                    return new[]
                    {
                        MakeTile(FloorRoot + "altar_00.png"),
                        MakeTile(FloorRoot + "altar_01.png"),
                        MakeTile(FloorRoot + "altar_02.png"),
                    };
                default:
                    return new[]
                    {
                        MakeTile(FloorRoot + "room_00.png"),
                        MakeTile(FloorRoot + "room_01.png"),
                        MakeTile(FloorRoot + "room_02.png"),
                        MakeTile(FloorRoot + "room_03.png"),
                    };
            }
        }

        static void CoverCorridorSegment(Vector2 a, Vector2 b, Stage1Maze maze, HashSet<long> owned,
            List<Vector3Int> positions, List<TileBase> tiles, Tile[] corridorTiles)
        {
            float reach = 4.15f + 0.56f;
            Vector2 min = Vector2.Min(a, b);
            Vector2 max = Vector2.Max(a, b);
            int pMin = Mathf.FloorToInt(2f * (min.x - reach));
            int pMax = Mathf.CeilToInt(2f * (max.x + reach));
            int qMin = Mathf.FloorToInt(4f * (min.y - reach) - 1f);
            int qMax = Mathf.CeilToInt(4f * (max.y + reach) - 1f);
            for (int p = pMin; p <= pMax; p++)
            {
                for (int q = qMin; q <= qMax; q++)
                {
                    if (((p + q) & 1) != 0)
                        continue;
                    long id = Pack(p, q);
                    if (owned.Contains(id))
                        continue;
                    Vector2 c = new Vector2(p * 0.5f, (q + 1) * 0.25f);
                    if (DistToSegment(c, a, b) > reach)
                        continue;
                    if (InsideAnyRoom(maze, c))
                        continue;
                    owned.Add(id);
                    positions.Add(new Vector3Int((p + q) / 2, (q - p) / 2, 0));
                    tiles.Add(corridorTiles[CellHash(p, q, 3) % corridorTiles.Length]);
                }
            }
        }

        static bool InsideAnyRoom(Stage1Maze maze, Vector2 c)
        {
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                if (Mathf.Abs(c.x - n.Center.X) <= n.Width * 0.5f + MarginX
                    && Mathf.Abs(c.y - n.Center.Y) <= n.Height * 0.5f + MarginY)
                    return true;
            }
            return false;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float d = ab.sqrMagnitude;
            if (d < 0.0001f)
                return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / d);
            return Vector2.Distance(p, a + ab * t);
        }

        // ---- walls ----

        static List<DoorMark> CollectDoors(MazeNode node, Stage1Maze maze)
        {
            var doors = new List<DoorMark>();
            for (int i = 0; i < maze.Edges.Length; i++)
            {
                MazeEdge e = maze.Edges[i];
                if (e.FromId == node.Id)
                    doors.Add(MakeDoor(node, e.From));
                else if (e.ToId == node.Id)
                    doors.Add(MakeDoor(node, e.To));
            }
            return doors;
        }

        static DoorMark MakeDoor(MazeNode node, MazeVec2 point)
        {
            float dx = point.X - node.Center.X;
            float dy = point.Y - node.Center.Y;
            char rim = Mathf.Abs(dx) > Mathf.Abs(dy) + 0.01f ? (dx > 0 ? 'E' : 'W') : (dy > 0 ? 'N' : 'S');
            return new DoorMark { Point = new Vector2(point.X, point.Y), Rim = rim };
        }

        // Bit flags for the cap edge on a rim cell.
        const int CapNE = 1;
        const int CapSE = 2;
        const int CapSW = 4;
        const int CapNW = 8;

        /// <summary>The door hole spans two rim cells; the rim edge next to the
        /// hole on each side is replaced by an end cap. Bit flags name the capped edge.</summary>
        static void MarkHole(CellRect rc, DoorMark door, HashSet<long> holes, Dictionary<long, int> caps)
        {
            if (door.Rim == 'E' || door.Rim == 'W')
            {
                int p = door.Rim == 'E' ? rc.PMax : rc.PMin;
                int qDoor = Mathf.RoundToInt(4f * door.Point.y - 1f);
                int q0 = SnapToParity(qDoor, p);
                int first, second, capBefore, capAfter;
                if (door.Rim == 'E')
                {
                    first = q0; second = q0 + 2; capBefore = q0 - 2; capAfter = q0 + 4;
                    AddHole(holes, p, first, rc);
                    AddHole(holes, p, second, rc);
                    AddCap(caps, p, capBefore, rc, CapNE);
                    AddCap(caps, p, capAfter, rc, CapSE);
                }
                else
                {
                    first = q0 - 2; second = q0; capBefore = q0 - 4; capAfter = q0 + 2;
                    AddHole(holes, p, first, rc);
                    AddHole(holes, p, second, rc);
                    AddCap(caps, p, capBefore, rc, CapNW);
                    AddCap(caps, p, capAfter, rc, CapSW);
                }
            }
            else
            {
                int q = door.Rim == 'S' ? rc.QMin : rc.QMax;
                int pDoor = Mathf.RoundToInt(2f * door.Point.x);
                int p0 = SnapToParity(pDoor, q);
                int first, second, capBefore, capAfter;
                if (door.Rim == 'S')
                {
                    first = p0; second = p0 + 2; capBefore = p0 - 2; capAfter = p0 + 4;
                    AddHole(holes, first, q, rc);
                    AddHole(holes, second, q, rc);
                    AddCap(caps, capBefore, q, rc, CapSE);
                    AddCap(caps, capAfter, q, rc, CapSW);
                }
                else
                {
                    first = p0 - 2; second = p0; capBefore = p0 - 4; capAfter = p0 + 2;
                    AddHole(holes, first, q, rc);
                    AddHole(holes, second, q, rc);
                    AddCap(caps, capBefore, q, rc, CapNE);
                    AddCap(caps, capAfter, q, rc, CapNW);
                }
            }
        }

        static void AddHole(HashSet<long> set, int p, int q, CellRect rc)
        {
            if (p < rc.PMin || p > rc.PMax || q < rc.QMin || q > rc.QMax)
                return;
            if (((p + q) & 1) != 0)
                return;
            set.Add(Pack(p, q));
        }

        static void AddCap(Dictionary<long, int> caps, int p, int q, CellRect rc, int flag)
        {
            if (p < rc.PMin || p > rc.PMax || q < rc.QMin || q > rc.QMax)
                return;
            if (((p + q) & 1) != 0)
                return;
            long id = Pack(p, q);
            int mask;
            caps.TryGetValue(id, out mask);
            caps[id] = mask | flag;
        }

        /// <summary>Snaps a coordinate to the parity used by the given rim so it names a real cell.</summary>
        static int SnapToParity(int value, int rimCoord)
        {
            return Mathf.Abs(value - rimCoord) % 2 == 0 ? value : value - 1;
        }

        // Piece anchors follow the accepted art: tall wall_l / end_l pivots sit on
        // the lower-right end of their "\" edge (E or S vertex); wall_r / end_r on
        // the left end of their "/" edge (S or W vertex); low pieces pivot on the
        // cell centre whose own SW / SE edge is the boundary.
        // Face-on camera: back rims (N, E) are tall, front rims (S, W) are low —
        // matching the accepted sample-room orientation.
        static void PlaceRimEdges(CellRect rc, int u, int v, int p, int q,
            bool hole, int capMask, Transform parent, List<GameObject> into)
        {
            // NE edge: neighbour (u+1, v). Back rims (E and N): tall wall_l on the E vertex.
            if (q + 1 > rc.QMax || p + 1 > rc.PMax)
            {
                if (!hole)
                {
                    if ((capMask & CapNE) != 0)
                        PlaceTall(EndL, u, v, 0.5f, 0f, parent, into);
                    else
                        PlaceTall(WallL, u, v, 0.5f, 0f, parent, into);
                }
            }

            // SE edge: neighbour (u, v-1). Front (S) rim: low_r on the cell centre;
            // back (E) rim: tall wall_r on the S vertex.
            if (q - 1 < rc.QMin || p + 1 > rc.PMax)
            {
                if (!hole)
                {
                    if (q - 1 < rc.QMin)
                    {
                        if ((capMask & CapSE) != 0)
                            PlaceLow(LowEndR, u, v, parent, into);
                        else
                            PlaceLow(LowR, u, v, parent, into);
                    }
                    else
                    {
                        if ((capMask & CapSE) != 0)
                            PlaceTall(EndR, u, v, 0f, -0.25f, parent, into);
                        else
                            PlaceTall(WallR, u, v, 0f, -0.25f, parent, into);
                    }
                }
            }

            // SW edge: neighbour (u-1, v). Front rims (W and S): low_l on the cell centre.
            if (p - 1 < rc.PMin || q - 1 < rc.QMin)
            {
                if (!hole)
                {
                    if ((capMask & CapSW) != 0)
                        PlaceLow(LowEndL, u, v, parent, into);
                    else
                        PlaceLow(LowL, u, v, parent, into);
                }
            }

            // NW edge: neighbour (u, v+1). Back (N) rim: tall wall_r on the W vertex;
            // front (W) rim: low_r on the outside cell whose SE edge is the boundary.
            if (q + 1 > rc.QMax || p - 1 < rc.PMin)
            {
                if (!hole)
                {
                    if (q + 1 > rc.QMax)
                    {
                        if ((capMask & CapNW) != 0)
                            PlaceTall(EndR, u, v, -0.5f, 0f, parent, into);
                        else
                            PlaceTall(WallR, u, v, -0.5f, 0f, parent, into);
                    }
                    else
                    {
                        if ((capMask & CapNW) != 0)
                            PlaceLow(LowEndR, u, v + 1, parent, into);
                        else
                            PlaceLow(LowR, u, v + 1, parent, into);
                    }
                }
            }
        }

        static void PlaceTall(string[] variants, int u, int v, float ox, float oy, Transform parent, List<GameObject> into)
        {
            int idx = WrapIndex(u + v, variants.Length);
            Vector3 pos = CellCenter(u, v) + new Vector3(ox, oy, 0f);
            into.Add(Place("Wall", pos, Load(WallRoot + variants[idx] + ".png"), WallOrder, false, parent));
        }

        static void PlaceLow(string[] variants, int u, int v, Transform parent, List<GameObject> into)
        {
            int idx = WrapIndex(u + v, variants.Length);
            Vector3 pos = CellCenter(u, v);
            into.Add(Place("LowWall", pos, Load(WallRoot + variants[idx] + ".png"), WallOrder, false, parent));
        }

        static readonly string[] WallL = { "l_00", "l_01", "l_02" };
        static readonly string[] WallR = { "r_00", "r_01", "r_02" };
        static readonly string[] LowL = { "low_l_00" };
        static readonly string[] LowR = { "low_r_00" };
        static readonly string[] EndL = { "end_l_00" };
        static readonly string[] EndR = { "end_r_00" };
        static readonly string[] LowEndL = { "low_end_l_00" };
        static readonly string[] LowEndR = { "low_end_r_00" };

        /// <summary>Door arches at every doorway, anchored at the centre of the
        /// 2-cell hole span (the same chain vertex the wall pieces share).
        /// Back rims use the tall-wall pairing (door_r on N, door_l on E);
        /// front rims mirror it (door_r on S, door_l on W). The demo toggles
        /// these with the room lock: visible tinted while locked.</summary>
        static void PlaceDoorArches(MazeNode node, CellRect rc, List<DoorMark> doors, Transform parent, List<GameObject> into, List<GameObject> doorArches)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                DoorMark door = doors[i];
                Vector3 pos;
                string file;
                switch (door.Rim)
                {
                    case 'N':
                        int pn = SnapToParity(Mathf.RoundToInt(2f * door.Point.x), rc.QMax);
                        pos = new Vector3(pn * 0.5f - 0.5f, (rc.QMax + 1) * 0.25f, 0f);
                        file = "door_r_00.png";
                        break;
                    case 'S':
                        int ps = SnapToParity(Mathf.RoundToInt(2f * door.Point.x), rc.QMin);
                        pos = new Vector3(ps * 0.5f + 0.5f, (rc.QMin + 1) * 0.25f, 0f);
                        file = "door_r_00.png";
                        break;
                    case 'E':
                        int qe = SnapToParity(Mathf.RoundToInt(4f * door.Point.y - 1f), rc.PMax);
                        pos = new Vector3(rc.PMax * 0.5f, (qe + 2) * 0.25f, 0f);
                        file = "door_l_00.png";
                        break;
                    default:
                        int pw = SnapToParity(Mathf.RoundToInt(4f * door.Point.y - 1f), rc.PMin);
                        pos = new Vector3(rc.PMin * 0.5f, pw * 0.25f, 0f);
                        file = "door_l_00.png";
                        break;
                }

                GameObject go = Place("Door_" + node.Id + "_" + door.Rim, pos, Load(WallRoot + file), WallOrder, false, parent);
                into.Add(go);
                if (doorArches != null)
                    doorArches.Add(go);
            }
        }

        /// <summary>Convex corner at the back (N-E) corner: replaces the two wall
        /// pieces on the corner cell's NW + NE edges. The apex sits one art-step
        /// above the pivot.</summary>
        static void PlaceCorners(CellRect rc, Transform parent, List<GameObject> into)
        {
            int pOut = rc.PMax - 1;
            int qOut = rc.QMax;
            if (((pOut + qOut) & 1) == 0)
            {
                Vector3 pos = CellCenter((pOut + qOut) / 2, (qOut - pOut) / 2) + new Vector3(0f, 0.5f - 0.1f, 0f);
                into.Add(Place("CornerOut", pos, Load(WallRoot + "corner_out_00.png"), WallOrder, false, parent));
            }

            // Front corner (S-W): the low walls meet at a concave foot.
            int pLow = rc.PMin + 1;
            int qLow = rc.QMin;
            if (((pLow + qLow) & 1) == 0)
            {
                Vector3 pos = CellCenter((pLow + qLow) / 2, (qLow - pLow) / 2);
                into.Add(Place("LowCorner", pos, Load(WallRoot + "low_corner_00.png"), WallOrder, false, parent));
            }
        }

        // Platform edges on the near rims (N and W): hang below the floor rim.
        static void PlaceEdgeRow(CellRect rc, char rim, Transform parent, List<GameObject> into)
        {
            if (rim == 'N')
            {
                int q = rc.QMax + 1;
                for (int p = rc.PMin + 1; p <= rc.PMax - 1; p++)
                {
                    if (((p + q) & 1) != 0)
                        continue;
                    Vector3 pos = CellCenter((p + q) / 2, (q - p) / 2);
                    into.Add(Place("Edge", pos, Load(EdgeRoot + "r_00.png"), WallOrder, false, parent));
                }
            }
            else
            {
                int p = rc.PMin - 1;
                for (int q = rc.QMin + 1; q <= rc.QMax - 1; q++)
                {
                    if (((p + q) & 1) != 0)
                        continue;
                    Vector3 pos = CellCenter((p + q) / 2, (q - p) / 2);
                    into.Add(Place("Edge", pos, Load(EdgeRoot + "l_00.png"), WallOrder, false, parent));
                }
            }
        }

        static void PlaceRubble(MazeNode node, CellRect rc, int seed, Transform parent, List<GameObject> into)
        {
            var rng = new System.Random(seed * 31 + node.Id.GetHashCode());
            string[] decals = { "rubble_00", "rubble_01" };
            int placed = 0;
            const int budget = 6;
            for (int p = rc.PMin + 4; p <= rc.PMax - 4 && placed < budget; p += 2)
            {
                for (int q = rc.QMin + 4; q <= rc.QMax - 4 && placed < budget; q += 2)
                {
                    if (((p + q) & 1) != 0)
                        continue;
                    if (rng.Next(100) >= 7)
                        continue;
                    Vector3 pos = CellCenter((p + q) / 2, (q - p) / 2);
                    into.Add(Place("Rubble", pos, Load(DecalRoot + decals[rng.Next(2)] + ".png"), DecalOrder, false, parent));
                    placed++;
                }
            }
        }

        // ---- props ----

        static Vector3 NearestRoomCellCenter(MazeNode node)
        {
            CellRect rc = RectCells(node);
            float best = float.MaxValue;
            int pBest = rc.PMin, qBest = rc.QMin;
            for (int p = rc.PMin; p <= rc.PMax; p++)
            {
                for (int q = rc.QMin; q <= rc.QMax; q++)
                {
                    if (((p + q) & 1) != 0)
                        continue;
                    Vector2 c = new Vector2(p * 0.5f, (q + 1) * 0.25f);
                    float d = (c - new Vector2(node.Center.X, node.Center.Y)).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        pBest = p;
                        qBest = q;
                    }
                }
            }
            return CellCenter((pBest + qBest) / 2, (qBest - pBest) / 2);
        }

        /// <summary>Altar base is a 2x2-cell diamond centred on a cell corner point.</summary>
        static Vector3 AltarPivot(MazeNode node)
        {
            float targetU = node.Center.X + 2f * node.Center.Y - 0.5f;
            float targetV = 2f * node.Center.Y - node.Center.X - 0.5f;
            int cu = Mathf.RoundToInt(targetU - 0.5f);
            int cv = Mathf.RoundToInt(targetV - 0.5f);
            CellRect rc = RectCells(node);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int uu = cu + (attempt & 1);
                int vv = cv + (attempt >> 1);
                int p = uu - vv;
                int q = uu + vv;
                if (p < rc.PMin || p > rc.PMax || q < rc.QMin || q > rc.QMax)
                    continue;
                if (((p + q) & 1) != 0)
                    continue;
                Vector2 corner = new Vector2((uu - 0.5f - (vv - 0.5f)) * 0.5f, (uu - 0.5f + vv - 0.5f) * 0.25f + 0.25f);
                return new Vector3(corner.x, corner.y + 0.125f, 0f);
            }
            return new Vector3(node.Center.X, node.Center.Y + 0.125f, 0f);
        }

        // ---- actors ----

        static Sprite LoadActor(string family, string action, string facing, int frame)
        {
            string file = frame.ToString("00");
            string path;
            if (family == "archer")
                path = ActorRoot + action + "_" + facing + "_" + file + ".png";
            else if (family == "dog")
                path = EnemyRoot + "Dog/jh_dog_" + action + "_" + facing + "_" + file + ".png";
            else if (family == "mage")
                path = EnemyRoot + "Mage/jh_mage_" + action + "_" + facing + "_" + file + ".png";
            else
                path = EnemyRoot + "Skel/jh_skel_" + action + "_" + facing + "_" + file + ".png";
            return Load(path);
        }

        static Tile MakeTile(string path)
        {
            Sprite sprite = Load(path);
            if (sprite == null)
                return null;
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            return tile;
        }

        static GameObject Place(string name, Vector3 pos, Sprite sprite, int order, bool flip, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.flipX = flip;
            return go;
        }

        public static string ProportionLine()
        {
            Sprite floor = Load("Assets/Art/JianHai/Iso/Tiles/S1/Floor/jh_iso_floor_s1_room_00.png");
            Sprite actor = Load("Assets/Art/JianHai/Iso/Characters/Archer/jh_archer_idle_s_00.png");
            Sprite wall = Load("Assets/Art/JianHai/Iso/Tiles/S1/Wall/jh_iso_wall_s1_l_00.png");
            return "[Stage1] proportion floor=" + SizeText(floor)
                   + " actor=" + SizeText(actor)
                   + " wall=" + SizeText(wall)
                   + " ppu=128";
        }

        static string SizeText(Sprite sprite)
        {
            if (sprite == null)
                return "missing";
            Vector3 s = sprite.bounds.size;
            return s.x.ToString("0.00") + "x" + s.y.ToString("0.00");
        }

        public static string PackLine()
        {
            int floor = Load("Assets/Art/JianHai/Iso/Tiles/S1/Floor/jh_iso_floor_s1_room_00.png") != null ? 1 : 0;
            int wall = Load("Assets/Art/JianHai/Iso/Tiles/S1/Wall/jh_iso_wall_s1_l_00.png") != null ? 1 : 0;
            int actor = Load("Assets/Art/JianHai/Iso/Characters/Archer/jh_archer_idle_e_00.png") != null ? 1 : 0;
            int anim = Load("Assets/Art/JianHai/Iso/Characters/Archer/jh_archer_walk_e_00.png") != null ? 1 : 0;
            int ui = Load("Assets/Art/JianHai/UI/jh_ui_bar_hp_back.png") != null ? 1 : 0;
            return "[Stage1Pack] floor=" + floor + " wall=" + wall + " actor=" + actor + " anim=" + anim + " ui=" + ui;
        }

        static Sprite Load(string path)
        {
            Sprite sprite;
            if (Cache.TryGetValue(path, out sprite) && sprite != null)
                return sprite;
            sprite = Stage1PackedSprites.Find(path);
#if UNITY_EDITOR
            if (sprite == null)
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Object[] all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                for (int i = 0; i < all.Length; i++)
                {
                    sprite = all[i] as Sprite;
                    if (sprite != null)
                        break;
                }
            }
#endif
            if (sprite != null)
                Cache[path] = sprite;
            return sprite;
        }
    }
}
