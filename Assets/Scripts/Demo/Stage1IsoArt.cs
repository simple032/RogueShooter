using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Iso;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Dresses the generated stage-1 maze with the accepted sample-room look.
    /// Grid is Isometric with cell size (1, 0.5); one tile = one full 2:1
    /// diamond, so neighbouring cells share edges with no seams.
    /// Cell math (verified against IsoSampleRoom): cell (u,v) center =
    /// ((u-v)*0.5, (u+v)*0.25 + 0.25). p = u-v = 2*wx, q = u+v = 4*wy - 1.
    /// Rooms 28鑴?2 carry the sample vocabulary: continuous masonry faces with
    /// cornice, foot, piers and cutaway side strips; near rims get low plinths.
    /// Every door is the delivered DoorClose frame sheet: a double swinging door
    /// in two depth layers (back/front at actor order, iso axis sorted), locked =
    /// the close clip plays red, cleared = the open clip parks the leaves. Lintels,
    /// sills and cutaway patches seam the doorway into the wall. Corridors between
    /// rooms get their own masonry shell.
    /// </summary>
    public static class Stage1IsoArt
    {
        const string FloorRoot = "Assets/Art/JianHai/Iso/Tiles/S1/Floor/jh_iso_floor_s1_";
        const string WallRoot = "Assets/Art/JianHai/Iso/Tiles/S1/Wall/jh_iso_wall_s1_";
        const string DecalRoot = "Assets/Art/JianHai/Iso/Tiles/S1/Decal/jh_iso_decal_s1_";
        const string PropRoot = "Assets/Art/JianHai/Iso/Props/jh_iso_prop_";
        const string ActorRoot = "Assets/Art/JianHai/Iso/Characters/Archer/jh_archer_";
        const string EnemyRoot = "Assets/Art/JianHai/Iso/Enemies/";
        const string FxRoot = "Assets/Art/JianHai/Iso/FX/jh_iso_fx_";

        const float MarginX = 0.501f;
        const float MarginY = 0.251f;
        // Wall orders follow the sample room scheme shifted below the actors (order 0).
        // The door layers also sit at order 0: the camera's iso axis sort puts actors
        // between the door's back and front layers instead of clipping through them.
        const int FloorOrder = -1000;
        const int WashOrder = -800;
        const int ShadowOrder = -750;
        const int RubbleOrder = -700;
        const int CutawayOrder = -30;
        const int HighlightOrder = -29;
        const int JointOrder = -28;
        const int BayOrder = -16;
        const int CorniceOrder = -14;
        const int CorniceShadowOrder = -13;
        const int FootOrder = -12;
        const int PlinthOrder = -11;
        const int PlinthTopOrder = -10;
        const int PierOrder = -8;
        const int PierCapOrder = -7;
        const int PropOrder = -6;
        const int FlameOrder = -4;
        /// <summary>Door opening width, locked to the corridor width so the
        /// doorway and its corridor match exactly.</summary>
        public const float DoorGap = MazeRules.CorridorWidth;

        static readonly Color CutawayColor = new Color(.20f, .24f, .28f);
        static readonly Color HighlightColor = new Color(.30f, .34f, .37f);
        static readonly Color JointColor = new Color(.10f, .13f, .16f);
        static readonly Color CorniceColor = new Color(.27f, .31f, .35f);
        static readonly Color FootColor = new Color(.11f, .14f, .17f);
        static readonly Color PierColor = new Color(.24f, .28f, .32f);
        static readonly Color PierCapColor = new Color(.31f, .34f, .37f);
        static readonly Color PlinthColor = new Color(.16f, .20f, .24f);
        static readonly Color PlinthTopColor = new Color(.29f, .33f, .36f);
        static readonly Color RouteColor = new Color(.90f, .88f, .83f);
        static readonly Color CourtColor = new Color(.90f, .87f, .79f);
        static readonly Color ChestInlayColor = new Color(.83f, .79f, .67f);
        static readonly Color AltarInlayColor = new Color(.69f, .70f, .71f);
        static readonly Color RubbleTint = new Color(.63f, .68f, .72f, .85f);
        static readonly Color WashColor = new Color(.9f, .47f, .16f, .095f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, .30f);

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<Color, Material> BandMaterials = new Dictionary<Color, Material>();
        static readonly Dictionary<string, Sprite[]> FireFrames = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, int> FloorRolls = new Dictionary<string, int>();
        public static int FloorStyle(string id) { return FloorRolls.TryGetValue(id, out var roll) ? roll : -1; }
        public static int DecorStyle(string id) { return DecorRolls.TryGetValue(id, out var roll) ? roll : -1; }
        static readonly Dictionary<string, int> DecorRolls = new Dictionary<string, int>();
        static Mesh _quad, _masonryFace, _softDisc;

        public struct CellRect
        {
            public int PMin, PMax, QMin, QMax;
        }

        public struct DoorMark
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

        /// <summary>Floors the whole maze: quiet stone with sparse wear, edge cracks,
        /// light walking routes and local chest/altar/entry inlays 閳?the sample room
        /// treatment. Rooms paint first; corridors fill the door-to-door tubes.</summary>
        public static GameObject BuildFloors(Stage1Maze maze, Transform parent, Stage1Maze doorMaze = null, bool resetRolls = true)
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
            var colors = new List<Color>();

            Tile clean = MakeTile(FloorRoot + "room_00.png");
            Tile worn = MakeTile(FloorRoot + "room_01.png");
            Tile crack = MakeTile(FloorRoot + "room_02.png");
            int prevStyle = -1;
            int prevDecor = -1;
            if (resetRolls) { DecorRolls.Clear(); FloorRolls.Clear(); }
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                CellRect rc = RectCells(n);
                List<DoorMark> doors = CollectDoors(n, doorMaze ?? maze);
                Vector3 chestPad = n.Kind == MazeNodeKind.Chest || n.Kind == MazeNodeKind.LargeChest
                    ? NearestRoomCellCenter(n) : Vector3.zero;
                Vector3 altarPad = n.Kind == MazeNodeKind.Altar ? AltarPivot(n) : Vector3.zero;
                // Each room rolls ONE floor style for the run; a repeat of the
                // previous room's style steps aside so rooms read differently.
                int style = Hash(maze.Seed, n.Id.GetHashCode(), 101) % FloorStyleCount;
                if (style == prevStyle)
                    style = (style + 1) % FloorStyleCount;
                prevStyle = style;
                FloorRolls[n.Id] = style;
                // Decoration layouts are precomputed with the same step-aside rule
                // so consecutive rooms never share a layout; BuildRoomShell reads
                // them back when it dresses each room.
                int decor = DecorHash(maze.Seed, n.Id.GetHashCode(), 307) % DecorStyleCount;
                if (decor == prevDecor)
                    decor = (decor + 1) % DecorStyleCount;
                prevDecor = decor;
                DecorRolls[n.Id] = decor;
                // S1 altar rooms tile wall-to-wall like any room; S2/S3 altar rooms
                // share the combat rooms' corner-cut walk shape, so HasFloor alone
                // now decides where tiles stop (at the air-wall cut corners).
                for (int p = rc.PMin; p <= rc.PMax; p++)
                {
                    for (int q = rc.QMin; q <= rc.QMax; q++)
                    {
                        if (((p + q) & 1) != 0)
                            continue;
                        Vector3 sample = CellCenter((p + q) / 2, (q - p) / 2);
                        if (!n.HasFloor(sample.x,sample.y)) continue;
                        long id = Pack(p, q);
                        if (!owned.Add(id))
                            continue;
                        Vector3 world = CellCenter((p + q) / 2, (q - p) / 2);
                        Color color;
                        Tile tile = RoomFloor(n, world, style, doors, chestPad, altarPad, out color, clean, worn, crack);
                        positions.Add(new Vector3Int((p + q) / 2, (q - p) / 2, 0));
                        tiles.Add(tile);
                        colors.Add(color);
                    }
                }
            }

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
                        maze, owned, positions, tiles, colors, clean, worn, crack);
            }

            if (positions.Count > 0)
            {
                map.SetTiles(positions.ToArray(), tiles.ToArray());
                for (int i = 0; i < positions.Count; i++)
                    map.SetColor(positions[i], colors[i]);
                map.RefreshAllTiles();
            }
            FloorCount = positions.Count;
            return root;
        }

        public static int Hash(int a, int b, int salt)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ salt * 83492791;
                return h & 0x7fffffff;
            }
        }

        /// <summary>Independently mixed roll for the decoration layout. The plain
        /// XOR hash exposes its low bits as fixed XORs of the input low bits, so
        /// any two rolls over the same inputs share hard bit relations; the
        /// murmur-style avalanche breaks them so every style x decor pair can
        /// occur and all buckets stay reachable.</summary>
        static int DecorHash(int a, int b, int salt)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ salt * 83492791;
                h ^= h >> 16;
                h *= -2048144789;
                h ^= h >> 13;
                h *= -1028477387;
                h ^= h >> 16;
                return h & 0x7fffffff;
            }
        }

        /// <summary>Number of distinct floor-laying styles a room can roll.</summary>
        public const int FloorStyleCount = 10;

        /// <summary>One floor style per room, rolled from the run seed and stepped
        /// aside from the previous room's pick so neighbours read differently:
        /// quiet court, two-unit checker, framed border, diagonal stripes, large
        /// patches, worn rim, centre medallion, broad stone columns, concentric
        /// rings or a diagonal two-tone split. Routes through door axes and the
        /// chest/altar pads stay on top of every style for readability.</summary>
        static Tile RoomFloor(MazeNode n, Vector3 world, int style, List<DoorMark> doors,
            Vector3 chestPad, Vector3 altarPad, out Color color, Tile clean, Tile worn, Tile crack)
        {
            float lx = world.x - n.Center.X;
            float ly = world.y - n.Center.Y;
            int h = Hash(Mathf.RoundToInt(world.x * 2f), Mathf.RoundToInt(world.y * 4f), n.Id.GetHashCode()) % 100;
            float value = .80f + .10f * Mathf.PerlinNoise(world.x * .17f + 17f, world.y * .17f + 29f);
            color = new Color(value * .96f, value * .99f, value);
            Tile tile = h < 92 ? clean : worn;
            switch (style)
            {
                case 0: // Quiet court: sparse wear, cracks only along the wall feet.
                    if ((Mathf.Abs(lx) > 12.8f || ly > 10f) && h > 93)
                        tile = crack;
                    break;
                case 1: // Two-unit checker of light and worn stone.
                    {
                        int cellA = Mathf.FloorToInt(lx * 0.5f + 100f);
                        int cellB = Mathf.FloorToInt(ly * 0.5f + 100f);
                        tile = ((cellA + cellB) & 1) == 0 ? clean : worn;
                        float shift = ((cellA + cellB) & 1) == 0 ? .04f : -.02f;
                        color = new Color((value + shift) * .96f, (value + shift) * .99f, value + shift);
                        if ((Mathf.Abs(lx) > 12.8f || ly > 10f) && h > 93)
                            tile = crack;
                        break;
                    }
                case 2: // Worn court border framing a quiet centre.
                    {
                        bool border = (Mathf.Abs(Mathf.Abs(lx) - 6f) < .30f && Mathf.Abs(ly) < 4.5f)
                            || (Mathf.Abs(Mathf.Abs(ly) - 4.5f) < .23f && Mathf.Abs(lx) < 6f);
                        if (border)
                        {
                            tile = worn;
                            color = CourtColor;
                        }
                        else if (h > 94 && (Mathf.Abs(lx) > 12.8f || ly > 10f))
                            tile = crack;
                        break;
                    }
                case 3: // Diagonal worn stripes sweeping the room.
                    {
                        int band = Mathf.FloorToInt((lx + ly) * 0.5f + 100f);
                        if ((band % 4) == 0)
                        {
                            tile = worn;
                            color = new Color(value * .93f, value * .95f, value * .9f);
                        }
                        break;
                    }
                case 4: // Large five-unit patches of two tones.
                    {
                        int patch = Hash(Mathf.FloorToInt(lx / 5f) + 50, Mathf.FloorToInt(ly / 5f) + 50, n.Id.GetHashCode()) % 100;
                        if (patch < 45)
                            tile = worn;
                        float spread = patch < 45 ? -.04f : .05f;
                        color = new Color((value + spread) * .96f, (value + spread) * .99f, value + spread);
                        if ((Mathf.Abs(lx) > 12.8f || ly > 10f) && h > 95)
                            tile = crack;
                        break;
                    }
                case 5:
                default: // Heavy worn rim around a clean centre.
                    {
                        float rim = Mathf.Max(Mathf.Abs(lx) - 9.5f, Mathf.Abs(ly) - 7f);
                        if (rim > 0f)
                            tile = rim > 1.5f ? (h > 85 ? crack : worn) : worn;
                        break;
                    }
                case 6: // Central diamond medallion: cool court, worn frame ring.
                    {
                        // Sized past the route bands (|l|<3) so the cool core stays
                        // visible around the light route cross instead of under it.
                        float d = Mathf.Abs(lx) / 13f + Mathf.Abs(ly) / 10f;
                        if (d < .8f)
                        {
                            tile = clean;
                            color = AltarInlayColor;
                        }
                        else if (d < 1.05f)
                        {
                            tile = worn;
                            color = new Color(value * .90f, value * .92f, value * .87f);
                        }
                        else if (h > 94 && (Mathf.Abs(lx) > 12.8f || ly > 10f))
                            tile = crack;
                        break;
                    }
                case 7: // Broad two-unit stone columns, every third one worn.
                    {
                        int col = Mathf.FloorToInt(lx * 0.5f + 100f);
                        if ((col % 3) == 0)
                        {
                            tile = worn;
                            color = new Color(value * .93f, value * .95f, value * .90f);
                        }
                        if ((Mathf.Abs(lx) > 12.8f || ly > 10f) && h > 93)
                            tile = crack;
                        break;
                    }
                case 8: // Concentric elliptical wear rings around a clean centre.
                    {
                        float rr = Mathf.Sqrt(lx * lx / 121f + ly * ly / 72.25f);
                        if ((Mathf.FloorToInt(rr * 3f) & 1) == 1)
                        {
                            tile = worn;
                            color = new Color(value * .93f, value * .95f, value * .90f);
                        }
                        break;
                    }
                case 9: // Diagonal two-tone split with a cracked fault seam.
                    {
                        float s = lx + ly;
                        if (s > 0f)
                            color = new Color(value * 1.04f * .96f, value * 1.04f * .99f, value * 1.04f);
                        else
                        {
                            tile = worn;
                            color = new Color(value * .90f * .96f, value * .92f * .99f, value * .88f);
                        }
                        if (Mathf.Abs(s) < .35f && h > 80)
                            tile = crack;
                        break;
                    }
            }
            bool routed = false;
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i].Rim == 'E' || doors[i].Rim == 'W')
                {
                    if (Mathf.Abs(ly) < 3f)
                        routed = true;
                    if ((doors[i].Rim == 'E' ? lx > 10.8f : lx < -10.8f) && Mathf.Abs(ly) < 3f)
                        routed = true;
                }
                else
                {
                    if (Mathf.Abs(lx) < 3f)
                        routed = true;
                    if ((doors[i].Rim == 'N' ? ly > 7.8f : ly < -7.8f) && Mathf.Abs(lx) < 3f)
                        routed = true;
                }
            }
            if (routed)
            {
                tile = clean;
                color = RouteColor;
            }
            if (chestPad != Vector3.zero && Mathf.Abs(world.x - chestPad.x) < 1.7f && Mathf.Abs(world.y - chestPad.y) < 1.3f)
            {
                tile = clean;
                color = ChestInlayColor;
            }
            // The sanctum slab and causeway in StageAltarView carry the altar floor;
            // the tile pad stays a tight consecrated glow under the dais so its
            // rectangle never pokes past the mesh geometry.
            if (altarPad != Vector3.zero && Mathf.Abs(world.x - altarPad.x) < .85f && Mathf.Abs(world.y - altarPad.y) < .60f)
            {
                tile = clean;
                color = AltarInlayColor;
            }
            return tile;
        }

        /// <summary>Sample-room shell for one node: continuous masonry north wall with
        /// cornice, shadow, foot and piers; cutaway side strips with joints; low south
        /// plinth; every door a 3-unit opening with jambs, threshold and a closed-arch
        /// gate; braziers and grouped rubble along the walls.</summary>
        public static void BuildRoomShell(MazeNode node, Stage1Maze maze, Transform parent, List<GameObject> into, int seed, List<GameObject> doorArches, bool braziers = true, bool doorFurniture = true)
        {
            float x0 = node.Center.X - node.Width * 0.5f;
            float x1 = node.Center.X + node.Width * 0.5f;
            float y0 = node.Center.Y - node.Height * 0.5f;
            float y1 = node.Center.Y + node.Height * 0.5f;
            float cx = node.Center.X;
            float cy = node.Center.Y;
            var doors = CollectDoors(node, maze);
            bool nDoor = HasRim(doors, 'N');
            bool sDoor = HasRim(doors, 'S');
            bool eDoor = HasRim(doors, 'E');
            bool wDoor = HasRim(doors, 'W');
            float half = DoorGap * 0.5f;

            // North wall: masonry face above the floor edge, cornice on top, foot below.
            if (nDoor)
            {
                MasonryBays("Masonry north " + node.Id, x0, y1, cx - half - x0, parent, into);
                MasonryBays("Masonry north " + node.Id, cx + half, y1, x1 - cx - half, parent, into);
                Band("North foot " + node.Id, new Vector3(x0, y1 - .17f, 0), new Vector2(cx - half - x0, .18f), FootColor, FootOrder, parent, into);
                Band("North foot " + node.Id, new Vector3(cx + half, y1 - .17f, 0), new Vector2(x1 - cx - half, .18f), FootColor, FootOrder, parent, into);
            }
            else
            {
                MasonryBays("Masonry north " + node.Id, x0, y1, x1 - x0, parent, into);
                Band("North foot " + node.Id, new Vector3(x0, y1 - .17f, 0), new Vector2(x1 - x0, .18f), FootColor, FootOrder, parent, into);
            }
            if (nDoor)
            {
                Band("North cornice " + node.Id, new Vector3(x0 - .15f, y1 + 1.45f, 0), new Vector2(cx - half - .65f - x0 + .15f, .19f), CorniceColor, CorniceOrder, parent, into);
                Band("North cornice " + node.Id, new Vector3(cx + half + .65f, y1 + 1.45f, 0), new Vector2(x1 - cx - half - .65f + .15f, .19f), CorniceColor, CorniceOrder, parent, into);
                Band("North cornice shadow " + node.Id, new Vector3(x0, y1 + 1.35f, 0), new Vector2(cx - half - .65f - x0, .09f), JointColor, CorniceShadowOrder, parent, into);
                Band("North cornice shadow " + node.Id, new Vector3(cx + half + .65f, y1 + 1.35f, 0), new Vector2(x1 - cx - half - .65f, .09f), JointColor, CorniceShadowOrder, parent, into);
            }
            else
            {
                Band("North cornice " + node.Id, new Vector3(x0 - .15f, y1 + 1.45f, 0), new Vector2(node.Width + .3f, .19f), CorniceColor, CorniceOrder, parent, into);
                Band("North cornice shadow " + node.Id, new Vector3(x0, y1 + 1.35f, 0), new Vector2(node.Width, .09f), JointColor, CorniceShadowOrder, parent, into);
            }
            for (int i = 0; i < 5; i++)
            {
                float px = x0 + 2f + 6f * i;
                if (nDoor && px > cx - half - .9f && px < cx + half + .9f)
                    continue;
                Band("North pier " + node.Id, new Vector3(px - .15f, y1, 0), new Vector2(.3f, 1.45f), PierColor, PierOrder, parent, into);
                Band("North pier cap " + node.Id, new Vector3(px - .21f, y1 + 1.33f, 0), new Vector2(.42f, .13f), PierCapColor, PierCapOrder, parent, into);
            }

            // South edge: low plinth so the near silhouette stays readable.
            if (sDoor)
            {
                Band("South plinth " + node.Id, new Vector3(x0, y0 - .45f, 0), new Vector2(cx - half - x0, .45f), PlinthColor, PlinthOrder, parent, into);
                Band("South plinth " + node.Id, new Vector3(cx + half, y0 - .45f, 0), new Vector2(x1 - cx - half, .45f), PlinthColor, PlinthOrder, parent, into);
                Band("South plinth top " + node.Id, new Vector3(x0, y0 - .08f, 0), new Vector2(cx - half - x0, .10f), PlinthTopColor, PlinthTopOrder, parent, into);
                Band("South plinth top " + node.Id, new Vector3(cx + half, y0 - .08f, 0), new Vector2(x1 - cx - half, .10f), PlinthTopColor, PlinthTopOrder, parent, into);
            }
            else
            {
                Band("South plinth " + node.Id, new Vector3(x0, y0 - .45f, 0), new Vector2(node.Width, .45f), PlinthColor, PlinthOrder, parent, into);
                Band("South plinth top " + node.Id, new Vector3(x0, y0 - .08f, 0), new Vector2(node.Width, .10f), PlinthTopColor, PlinthTopOrder, parent, into);
            }

            SideWall(node, 'W', x0, y0, wDoor, parent, into);
            SideWall(node, 'E', x1, y0, eDoor, parent, into);

            if(doorFurniture) PlaceDoorFurniture(node, doors, parent, into, doorArches);
            // Each room rolls one decoration layout per run seed: fewer braziers,
            // more floor dressing, and no two rooms read the same.
            // Decoration layout: the precomputed step-aside roll when the maze
            // floors were built, otherwise a fresh avalanche roll here. The plain
            // XOR hash locked style/decor bit patterns together; DecorHash breaks
            // that coupling so all layout numbers stay reachable.
            int decor;
            if (!DecorRolls.TryGetValue(node.Id, out decor))
                decor = DecorHash(seed, node.Id.GetHashCode(), 307) % DecorStyleCount;
            if (braziers)
                if (node.Kind != MazeNodeKind.Altar) PlaceBraziers(node, decor, doors, parent, into);
            if (node.Kind != MazeNodeKind.Altar) PlaceRubble(node, decor, doors, parent, into);
        }

        /// <summary>Number of distinct decoration layouts a room can roll.</summary>
        public const int DecorStyleCount = 8;

        static bool HasRim(List<DoorMark> doors, char rim)
        {
            for (int i = 0; i < doors.Count; i++)
                if (doors[i].Rim == rim)
                    return true;
            return false;
        }

        /// <summary>True when the spot (with a small footprint pad) stands on kept
        /// floor: outside every cut-corner rect of the room's walk shape. Only
        /// S2/S3 combat rooms carry FloorVoids; everywhere else this is a no-op.</summary>
        public static bool OnKeptFloor(MazeNode node, Vector2 world, float pad)
        {
            if (node == null || node.FloorVoids == null)
                return true;
            for (int i = 0; i < node.FloorVoids.Length; i++)
            {
                Rect r = node.FloorVoids[i];
                if (world.x >= r.xMin - pad && world.x <= r.xMax + pad
                    && world.y >= r.yMin - pad && world.y <= r.yMax + pad)
                    return false;
            }
            return true;
        }

        /// <summary>Slides a decoration spot toward the room centre until it stands
        /// on kept floor. Rooms that delete their corner tiles must not park
        /// braziers, rubble or collapse piles on the missing floor.</summary>
        public static Vector3 KeepOnFloor(MazeNode node, Vector3 spot, float pad)
        {
            if (OnKeptFloor(node, spot, pad))
                return spot;
            Vector2 c = new Vector2(node.Center.X, node.Center.Y);
            Vector2 p = spot;
            for (int i = 0; i < 32 && !OnKeptFloor(node, p, pad); i++)
                p = Vector2.Lerp(p, c, .12f);
            return p;
        }

        /// <summary>Narrow stone cutaway just outside the floor edge, from the south
        /// rim up to the north cornice line; a door gap splits the strip in two.</summary>
        static void SideWall(MazeNode node, char rim, float xEdge, float y0, bool door, Transform parent, List<GameObject> into)
        {
            float cx = node.Center.X;
            float cy = node.Center.Y;
            float half = DoorGap * 0.5f;
            float top = node.Height + 1.45f;
            float bx = rim == 'W' ? xEdge - .55f : xEdge;
            float hx = rim == 'W' ? xEdge - .55f : xEdge;
            string id = "" + rim + " " + node.Id;
            if (door)
            {
                float lower = cy - half - y0;
                float upper = top - lower - DoorGap;
                Band(rim + " cutaway " + id, new Vector3(bx, y0, 0), new Vector2(.55f, lower), CutawayColor, CutawayOrder, parent, into);
                Band(rim + " cutaway " + id, new Vector3(bx, cy + half, 0), new Vector2(.55f, upper), CutawayColor, CutawayOrder, parent, into);
                Band(rim + " highlight " + id, new Vector3(hx, y0, 0), new Vector2(.10f, lower), HighlightColor, HighlightOrder, parent, into);
                Band(rim + " highlight " + id, new Vector3(hx, cy + half, 0), new Vector2(.10f, upper), HighlightColor, HighlightOrder, parent, into);
                Joints(rim + " joint " + id, bx, y0, Mathf.FloorToInt(lower), parent, into);
                Joints(rim + " joint " + id, bx, cy + half, Mathf.FloorToInt(upper), parent, into);
            }
            else
            {
                Band(rim + " cutaway " + id, new Vector3(bx, y0, 0), new Vector2(.55f, top), CutawayColor, CutawayOrder, parent, into);
                Band(rim + " highlight " + id, new Vector3(hx, y0, 0), new Vector2(.10f, top), HighlightColor, HighlightOrder, parent, into);
                Joints(rim + " joint " + id, bx, y0, Mathf.FloorToInt(top), parent, into);
            }
        }

        public static void Joints(string name, float x, float y0, int count, Transform parent, List<GameObject> into)
        {
            for (int j = 0; j < count; j++)
                Band(name, new Vector3(x, y0 + j, 0), new Vector2(.55f, .03f), JointColor, JointOrder, parent, into);
        }

        /// <summary>Stone-framed oak doors, with stationary ground-space hinges
        /// and independent projections for all four room rims.</summary>
        static void PlaceDoorFurniture(MazeNode node, List<DoorMark> doors, Transform parent, List<GameObject> into, List<GameObject> doorArches)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                DoorMark door = doors[i];
                float cx = node.Center.X;
                float cy = node.Center.Y;
                float x0 = node.Center.X - node.Width * 0.5f;
                float x1 = node.Center.X + node.Width * 0.5f;
                float y0 = node.Center.Y - node.Height * 0.5f;
                float y1 = node.Center.Y + node.Height * 0.5f;
                Vector3 gatePos;
                switch (door.Rim)
                {
                    case 'N': gatePos = new Vector3(cx, y1, 0); break;
                    case 'S': gatePos = new Vector3(cx, y0, 0); break;
                    case 'E': gatePos = new Vector3(x1, cy, 0); break;
                    default: gatePos = new Vector3(x0, cy, 0); break;
                }

                var gateGo = new GameObject("Door_" + node.Id + "_" + door.Rim);
                gateGo.transform.SetParent(parent, false);
                gateGo.transform.position = gatePos;
                var gate = gateGo.AddComponent<Stage1DoorGate>();
                bool side = door.Rim == 'E' || door.Rim == 'W';
                if (side || DoorGap <= 6f)
                {
                    var view = gateGo.AddComponent<Stage1SlidingDoorView>();
                    view.Initialize(door.Rim, DoorGap, !side && DoorGap < 3f,
                        Resources.Load<Texture2D>(Stage1HingedDoorView.TextureResource));
                    view.SetPoseImmediate(1f);
                    gate.SlidingView = view;
                }
                else
                {
                    var view = gateGo.AddComponent<Stage1HingedDoorView>();
                    view.Initialize(door.Rim, DoorGap);
                    gate.HingedView = view;
                }
                into.Add(gateGo);
                if (doorArches != null)
                    doorArches.Add(gateGo);
            }
        }

        /// <summary>Door sheet canvas width: 551 px at PPU 128 spans 4.30 world units.</summary>
        public const float SheetWidth = 4.30f;
        /// <summary>The sheet scales so the leaf band (leaves plus the outer pocket
        /// tabs) spans the corridor width exactly.</summary>
        public const float DoorScale = MazeRules.CorridorWidth / SheetWidth;
        /// <summary>Door anchor offset from the rim line: the back leaf rides 0.22
        /// units above its pivot at scale 1, so this keeps the closed band along the
        /// floor line and the parked leaves planted on it.</summary>
        public const float DoorPivotY = 0.22f * DoorScale;

        static readonly Dictionary<string, Sprite[]> DoorClips = new Dictionary<string, Sprite[]>();

        static Sprite[] DoorClip(string clip)
        {
            Sprite[] frames;
            if (DoorClips.TryGetValue(clip, out frames) && frames != null && frames.Length > 0)
                return frames;
            frames = new Sprite[4];
            for (int i = 0; i < 4; i++)
                frames[i] = Load("Assets/Art/JianHai/Iso/Props/DoorClose/jh_iso_prop_door_" + clip + "_" + i.ToString("00") + ".png");
            DoorClips[clip] = frames;
            return frames;
        }

        /// <summary>Chest / altar / gate props with feet on the tiles, each with its
        /// sample-style contact shadow parented to the prop. Returns the prop object.</summary>
        public static GameObject BuildProp(MazeNode node, Transform parent)
        {
            if (node.Kind == MazeNodeKind.Chest || node.Kind == MazeNodeKind.LargeChest)
            {
                Vector3 anchor = NearestRoomCellCenter(node);
                var chest = Place("Chest_" + node.Id, anchor, Load(PropRoot + (node.Kind==MazeNodeKind.LargeChest?"chest_large_closed_00.png":"chest_closed_00.png")), PropOrder, false, parent);
                ParentShadow(chest, anchor + Vector3.down * .1f, .9f, .3f, "Chest shadow " + node.Id);
                return chest;
            }
            if (node.Kind == MazeNodeKind.Altar)
            {
                Vector3 pivot = AltarPivot(node);
                var altar = Place("Altar_" + node.Id, pivot, Load(PropRoot + "altar_00.png"), PropOrder, false, parent);
                ParentShadow(altar, pivot + Vector3.down * .15f, 1.5f, .5f, "Altar shadow " + node.Id);
                return altar;
            }
            if (node.Kind == MazeNodeKind.Connector)
            {
                Vector3 center = new Vector3(node.Center.X, node.Center.Y, 0f);
                return Place("GateHub_" + node.Id, center, Load(PropRoot + "gate_hub_00.png"), PropOrder, false, parent);
            }
            return null;
        }

        /// <summary>A contact shadow that dies with its prop across run rebuilds.</summary>
        static void ParentShadow(GameObject prop, Vector3 worldPos, float sx, float sy, string name)
        {
            GameObject shadow = PlaceMesh(name, worldPos, new Vector3(sx, sy, 1), SoftDiscMesh(), BandMaterial(ShadowColor), ShadowOrder, prop.transform.parent);
            shadow.transform.SetParent(prop.transform, true);
        }

        public static void OpenChest(GameObject chest)
        {
            if (chest == null)
                return;
            var sr = chest.GetComponent<SpriteRenderer>();
            bool large=sr!=null && sr.sprite!=null && sr.sprite.name.Contains("chest_large");
            Sprite open = Load(PropRoot + (large?"chest_large_open_00.png":"chest_open_00.png"));
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

        static readonly Dictionary<string, bool> CompleteActorClips = new Dictionary<string, bool>();
        public static void InvalidateActorCache() { Cache.Clear(); CompleteActorClips.Clear(); Stage1PackedSprites.Invalidate(); }
        public static bool HasActorClip(string family, string action, string facing, int span)
        {
            bool flip; string source = facing;
            if (family != "archer" && !IsoFacing.TrySkeleton(facing, out source, out flip)) source = "s";
            string key = family+":"+action+":"+source+":"+span;
            if (CompleteActorClips.TryGetValue(key, out bool complete)) return complete;
            complete = true;
            for (int i = 0; i < span; i++)
                if (LoadActor(family, action, source, i) == null) { complete = false; break; }
            CompleteActorClips[key] = complete;
            return complete;
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
            if (sprite != null)
                return sprite;
            // A missing walk or attack frame must not flash idle in the middle of the clip.
            if (action == "walk" || action == "atk" || action == "cast" || action.StartsWith("unshielded_"))
                return null;
            return LoadActor(family, "idle", src, 0);
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
            return Load("Assets/Art/JianHai/Iso/Projectiles/jh_iso_proj_arrow_slim_" + index.ToString("00") + ".png");
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

        public static CellRect RectCells(MazeNode n)
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

        public static Vector3 CellCenter(int u, int v)
        {
            return new Vector3((u - v) * 0.5f, (u + v) * 0.25f + 0.25f, 0f);
        }

        static long Pack(int p, int q)
        {
            return ((long)p << 32) | (uint)q;
        }

        // ---- floors ----

        /// <summary>Corridor floor: same quiet stone, light route strip along the
        /// centre line, cracks only near the side walls.</summary>
        static void CoverCorridorSegment(Vector2 a, Vector2 b, Stage1Maze maze, HashSet<long> owned,
            List<Vector3Int> positions, List<TileBase> tiles, List<Color> colors, Tile clean, Tile worn, Tile crack)
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
                    float perp = DistToSegment(c, a, b);
                    if (perp > reach)
                        continue;
                    if (InsideAnyRoom(maze, c))
                        continue;
                    owned.Add(id);
                    int h = Hash(p, q, 3) % 100;
                    Tile tile = h < 92 ? clean : worn;
                    if (perp > 2.55f && h > 93)
                        tile = crack;
                    float value = .80f + .10f * Mathf.PerlinNoise(c.x * .17f + 17f, c.y * .17f + 29f);
                    Color color = new Color(value * .96f, value * .99f, value);
                    if (perp < 1.5f)
                    {
                        tile = clean;
                        color = RouteColor;
                    }
                    positions.Add(new Vector3Int((p + q) / 2, (q - p) / 2, 0));
                    tiles.Add(tile);
                    colors.Add(color);
                }
            }
        }

        public static bool InsideAnyRoom(Stage1Maze maze, Vector2 c)
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

        public static List<DoorMark> CollectDoors(MazeNode node, Stage1Maze maze)
        {
            var doors = new List<DoorMark>();
            for (int i = 0; i < maze.Edges.Length; i++)
            {
                MazeEdge e = maze.Edges[i];
                if (e.FromId != node.Id && e.ToId != node.Id)
                    continue;
                DoorMark mark = MakeDoor(node, e.FromId == node.Id ? e.From : e.To);
                // Fork arms can share one doorway on the same rim; the rim gets
                // one door, not a stack of identical gates drawn over each other.
                bool rimSeen = false;
                for (int d = 0; d < doors.Count; d++)
                {
                    if (doors[d].Rim == mark.Rim)
                    {
                        rimSeen = true;
                        break;
                    }
                }
                if (!rimSeen)
                    doors.Add(mark);
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

        // ---- sample vocabulary pieces ----

        /// <summary>Corridor shells between rooms: the far side of a horizontal run
        /// carries the masonry face with cornice and foot, the near side a low plinth;
        /// vertical runs get cutaway strips on both sides. Ends butt against the room
        /// walls. S1 templates only emit straight door-to-door runs; folded legs would
        /// still paint per-leg without corner clipping.</summary>
        public static void BuildCorridorShell(Stage1Maze maze, Transform parent, List<GameObject> into)
        {
            if (maze == null || maze.Edges == null)
                return;
            for (int e = 0; e < maze.Edges.Length; e++)
            {
                MazeEdge edge = maze.Edges[e];
                MazeVec2[] pts = edge.Points;
                if (pts == null || pts.Length < 2)
                    pts = new[] { edge.From, edge.To };
                for (int s = 0; s < pts.Length - 1; s++)
                {
                    Vector2 a = new Vector2(pts[s].X, pts[s].Y);
                    Vector2 b = new Vector2(pts[s + 1].X, pts[s + 1].Y);
                    string id = edge.FromId + "--" + edge.ToId;
                    if (Mathf.Abs(b.y - a.y) < 0.51f)
                    {
                        float y = a.y;
                        float xa = Mathf.Min(a.x, b.x);
                        float xb = Mathf.Max(a.x, b.x);
                        MasonryBays("Corridor north " + id, xa, y + 4f, xb - xa, parent, into);
                        Band("Corridor cornice " + id, new Vector3(xa - .15f, y + 5.45f, 0), new Vector2(xb - xa + .3f, .19f), CorniceColor, CorniceOrder, parent, into);
                        Band("Corridor cornice shadow " + id, new Vector3(xa, y + 5.35f, 0), new Vector2(xb - xa, .09f), JointColor, CorniceShadowOrder, parent, into);
                        Band("Corridor foot " + id, new Vector3(xa, y + 3.83f, 0), new Vector2(xb - xa, .18f), FootColor, FootOrder, parent, into);
                        Band("Corridor plinth " + id, new Vector3(xa, y - 4.45f, 0), new Vector2(xb - xa, .45f), PlinthColor, PlinthOrder, parent, into);
                        Band("Corridor plinth top " + id, new Vector3(xa, y - 4.08f, 0), new Vector2(xb - xa, .10f), PlinthTopColor, PlinthTopOrder, parent, into);
                    }
                    else
                    {
                        float x = a.x;
                        float ya = Mathf.Min(a.y, b.y);
                        float yb = Mathf.Max(a.y, b.y);
                        float len = yb - ya;
                        Band("Corridor west cutaway " + id, new Vector3(x - 4.55f, ya, 0), new Vector2(.55f, len), CutawayColor, CutawayOrder, parent, into);
                        Band("Corridor west highlight " + id, new Vector3(x - 4.55f, ya, 0), new Vector2(.10f, len), HighlightColor, HighlightOrder, parent, into);
                        Joints("Corridor west joint " + id, x - 4.55f, ya, Mathf.FloorToInt(len), parent, into);
                        Band("Corridor east cutaway " + id, new Vector3(x + 4f, ya, 0), new Vector2(.55f, len), CutawayColor, CutawayOrder, parent, into);
                        Band("Corridor east highlight " + id, new Vector3(x + 4f, ya, 0), new Vector2(.10f, len), HighlightColor, HighlightOrder, parent, into);
                        Joints("Corridor east joint " + id, x + 4f, ya, Mathf.FloorToInt(len), parent, into);
                    }
                }
            }
        }

        public static void MasonryBays(string name, float x, float y, float w, Transform parent, List<GameObject> into)
        {
            if (w < 0.05f)
                return;
            int count = Mathf.Max(1, Mathf.RoundToInt(w / 1.4f));
            float bw = w / count;
            for (int i = 0; i < count; i++)
            {
                GameObject go = PlaceMesh(name + " " + i, new Vector3(x + bw * i, y, 0), new Vector3(bw, 1.45f, 1), MasonryFaceMesh(), ContinuousMasonry(), BayOrder, parent);
                into.Add(go);
            }
        }

        public static void Band(string name, Vector3 at, Vector2 size, Color color, int order, Transform parent, List<GameObject> into)
        {
            if (size.x < 0.05f || size.y < 0.05f)
                return;
            GameObject go = PlaceMesh(name, at, new Vector3(size.x, size.y, 1), QuadMesh(), BandMaterial(color), order, parent);
            into.Add(go);
        }

        public static GameObject PlaceMesh(string name, Vector3 at, Vector3 scale, Mesh mesh, Material material, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingOrder = order;
            return go;
        }

        public static Mesh QuadMesh()
        {
            if (_quad != null)
                return _quad;
            _quad = new Mesh();
            _quad.vertices = new[] { Vector3.zero, Vector3.right, new Vector3(1, 1), Vector3.up };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.RecalculateBounds();
            return _quad;
        }

        /// <summary>Opaque painted face inside the source wall tile, the same UV cut
        /// the sample room uses, so the transparent silhouette never shows.</summary>
        static Mesh MasonryFaceMesh()
        {
            if (_masonryFace != null)
                return _masonryFace;
            _masonryFace = new Mesh();
            _masonryFace.vertices = new[] { Vector3.zero, Vector3.right, new Vector3(1, 1), Vector3.up };
            _masonryFace.uv = new[]
            {
                new Vector2(4f / 128, 1 - 180f / 256), new Vector2(60f / 128, 1 - 208f / 256),
                new Vector2(60f / 128, 1 - 56f / 256), new Vector2(4f / 128, 1 - 28f / 256)
            };
            var tint = new Color(.75f, .80f, .86f);
            _masonryFace.colors = new[] { tint, tint, tint, tint };
            _masonryFace.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _masonryFace.RecalculateBounds();
            return _masonryFace;
        }

        public static Material ContinuousMasonry()
        {
            Material mat;
            if (!BandMaterials.TryGetValue(Color.white, out mat))
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                mat.mainTexture = Load(WallRoot + "l_01.png").texture;
                BandMaterials[Color.white] = mat;
            }
            return mat;
        }

        public static Material BandMaterial(Color color)
        {
            Material mat;
            if (!BandMaterials.TryGetValue(color, out mat))
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = color;
                BandMaterials[color] = mat;
            }
            return mat;
        }

        public static Mesh SoftDiscMesh()
        {
            if (_softDisc != null)
                return _softDisc;
            _softDisc = new Mesh();
            var vertices = new List<Vector3> { Vector3.zero };
            var colors = new List<Color> { Color.white };
            var triangles = new List<int>();
            for (int i = 0; i <= 32; i++)
            {
                float a = i * Mathf.PI * 2 / 32;
                vertices.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a)));
                colors.Add(new Color(1, 1, 1, 0));
                if (i > 0)
                {
                    triangles.Add(0);
                    triangles.Add(i);
                    triangles.Add(i + 1);
                }
            }
            _softDisc.SetVertices(vertices);
            _softDisc.SetColors(colors);
            _softDisc.SetTriangles(triangles, 0);
            _softDisc.RecalculateBounds();
            return _softDisc;
        }

        public static void Disc(string name, Vector3 at, float x, float y, Color tint, int order, Transform parent, List<GameObject> into)
        {
            GameObject go = PlaceMesh(name, at, new Vector3(x, y, 1), SoftDiscMesh(), BandMaterial(tint), order, parent);
            into.Add(go);
        }

        /// <summary>Two braziers per room, placed from the room's decoration roll:
        /// gate torches on the north, south, east or west rim, diagonal court
        /// corners (NE+SW or NW+SE), a staggered side pair, or a south court
        /// pair. Every layout keeps clear of the door axes and the spawn court.</summary>
        static void PlaceBraziers(MazeNode node, int variant, List<DoorMark> doors, Transform parent, List<GameObject> into)
        {
            float cx = node.Center.X;
            float cy = node.Center.Y;
            // Mostly rolled as gate torches: the pair flanks one of the room's door
            // mouths at its jamb line, complementing the stone-framed doors. Rooms
            // without a door on the rolled rim fall back to the quiet court pair.
            float half = DoorGap * 0.5f;
            float cxEdge = node.Width * 0.5f, cyEdge = node.Height * 0.5f;
            Vector3[] spots = null;
            if (variant == 0 && HasRim(doors, 'N'))
                spots = new[] { new Vector3(cx - half - 1.4f, cy + cyEdge - 1.0f, 0), new Vector3(cx + half + 1.4f, cy + cyEdge - 1.0f, 0) };
            else if (variant == 1 && HasRim(doors, 'S'))
                spots = new[] { new Vector3(cx - half - 1.4f, cy - cyEdge + 1.0f, 0), new Vector3(cx + half + 1.4f, cy - cyEdge + 1.0f, 0) };
            else if ((variant == 2 || variant == 3) && (HasRim(doors, 'E') || HasRim(doors, 'W')))
            {
                // Variants 2 and 3 flank opposite side rims when both doors exist.
                bool isE = variant == 2 ? HasRim(doors, 'E') : !HasRim(doors, 'E');
                float x = isE ? cx + cxEdge - 1.0f : cx - cxEdge + 1.0f;
                spots = new[] { new Vector3(x, cy - half - 1.4f, 0), new Vector3(x, cy + half + 1.4f, 0) };
            }
            else if (variant == 4)
                spots = new[] { new Vector3(cx + 8.5f, cy + 5.5f, 0), new Vector3(cx - 8.5f, cy - 5.5f, 0) };
            else if (variant == 5)
                spots = new[] { new Vector3(cx - 8.5f, cy + 5.5f, 0), new Vector3(cx + 8.5f, cy - 5.5f, 0) };
            else if (variant == 6)
                spots = new[] { new Vector3(cx - 10.5f, cy - 3.2f, 0), new Vector3(cx + 10.5f, cy + 3.2f, 0) };
            else if (variant == 7)
                spots = new[] { new Vector3(cx - 3f, cy - 5.5f, 0), new Vector3(cx + 3f, cy - 5.5f, 0) };
            if (spots == null)
                spots = new[] { new Vector3(cx - 3f, cy + 7.1f, 0), new Vector3(cx + 3f, cy + 7.1f, 0) };
            // Corner-cut rooms (S2/S3) delete their corner tiles; a brazier that
            // rolled onto a cut rect slides inboard onto the kept floor.
            for (int i = 0; i < spots.Length; i++)
                spots[i] = KeepOnFloor(node, spots[i], .55f);
            for (int i = 0; i < spots.Length; i++)
                Brazier(spots[i], parent, into);
        }

        public static void Brazier(Vector3 at, Transform parent, List<GameObject> into)
        {
            GameObject bowl = Place("Brazier", at, Load(PropRoot + "brazier_00.png"), PropOrder, false, parent);
            into.Add(bowl);
            Disc("Brazier shadow", at, .45f, .18f, ShadowColor, ShadowOrder, parent, into);
            GameObject flame = Place("Living flame", at + Vector3.up * .55f, Load(FxRoot + "brazier_fire_00.png"), FlameOrder, false, parent);
            var anim = flame.AddComponent<IsoBrazierFire>();
            anim.Frames = FireClip("brazier_fire");
            anim.FramesPerSecond = 8;
            into.Add(flame);
            Disc("Warm wash", at, 2.1f, 1f, WashColor, WashOrder, parent, into);
        }

        static Sprite[] FireClip(string key)
        {
            Sprite[] frames;
            if (FireFrames.TryGetValue(key, out frames) && frames != null && frames.Length > 0)
                return frames;
            frames = new Sprite[6];
            for (int i = 0; i < 6; i++)
                frames[i] = Load(FxRoot + key + "_0" + i + ".png");
            FireFrames[key] = frames;
            return frames;
        }

        /// <summary>Per-layout dressing palettes: each decoration roll also draws
        /// from its own prop theme (indices into the dressing id table), so two
        /// layouts never share the same floor dressing vocabulary.</summary>
        static readonly int[][] DressPalettes =
        {
            new[] { 0, 3, 5 },          // wall ring: banners, plinths, cracks
            new[] { 4, 1, 2 },          // corners: bones, scrap, stubs
            new[] { 0, 5, 3 },          // north/south rows: banners, cracks, plinths
            new[] { 0, 1, 2, 3, 4, 5 }, // mid-field scatter: everything
            new[] { 2, 1, 5 },          // columns: stubs, scrap, cracks
            new[] { 4, 5, 1 },          // diagonal: bones, cracks, scrap
            new[] { 3, 0, 4 },          // annulus: plinths, banners, bones
            new[] { 5, 1, 3 },          // checker: cracks, scrap, plinths
        };

        /// <summary>Grouped rubble plus the delivered decal set (banner/scrap/stub/
        /// plinth/bones/crack). The room's decoration roll rotates the cluster set
        /// and picks one of eight dressing placements with its own prop theme, so
        /// rooms differ at a glance; every layout keeps the spawn court, door axes
        /// and walking routes clear.</summary>
        static void PlaceRubble(MazeNode node, int variant, List<DoorMark> doors, Transform parent, List<GameObject> into)
        {
            // Cluster spots stay inside the room under every Turn() transform:
            // |x| <= 10.5 and |y| <= 8 keep swapped coordinates inside the
            // half-extents (14, 11) so no rubble leaks past the walls.
            Vector2[] baseClusters =
            {
                new Vector2(-10.5f, 8f),
                new Vector2(10f, 8f),
                new Vector2(-10.5f, -7f),
                new Vector2(10f, -8f),
                new Vector2(-9.5f, 3f)
            };
            for (int c = 0; c < baseClusters.Length; c++)
            {
                Vector2 spot = Turn(baseClusters[c], variant);
                // Pull the cluster anchor inboard when it rolled onto a deleted
                // corner tile, so the whole group stays on kept floor.
                Vector3 anchor = KeepOnFloor(node, new Vector3(node.Center.X + spot.x, node.Center.Y + spot.y, 0f), 1f);
                for (int j = 0; j < 3; j++)
                {
                    Vector3 pos = new Vector3(anchor.x + j * .48f, anchor.y + (j % 2) * .32f, 0);
                    GameObject go = Place("Rubble cluster " + c + "/" + j, pos,
                        Load(DecalRoot + "rubble_0" + (j % 2) + ".png"), RubbleOrder, false, parent);
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                        sr.color = RubbleTint;
                    into.Add(go);
                }
            }

            string[] dressIds = { "banner_00", "scrap_00", "stub_00", "plinth_00", "bones_00", "crack_00" };
            float lxEdge = node.Width * 0.5f;
            float lyEdge = node.Height * 0.5f;
            float half = DoorGap * 0.5f;
            CellRect rc = RectCells(node);
            int placed = 0;
            const int budget = 14;
            int salt = node.Id.GetHashCode() + variant * 31;
            for (int p = rc.PMin + 2; p <= rc.PMax - 2 && placed < budget; p++)
            {
                for (int q = rc.QMin + 2; q <= rc.QMax - 2 && placed < budget; q++)
                {
                    if (((p + q) & 1) != 0)
                        continue;
                    int pick = Hash(p, q, salt) % 100;
                    if (pick >= 13)
                        continue;
                    Vector3 world = CellCenter((p + q) / 2, (q - p) / 2);
                    // Deleted corner tiles never receive floor dressing.
                    if (!OnKeptFloor(node, world, .6f))
                        continue;
                    float lx = world.x - node.Center.X;
                    float ly = world.y - node.Center.Y;
                    // Keep the spawn court, the door mouths with their swing/slide
                    // clearance, and the walking routes clear of floor dressing.
                    if (Mathf.Abs(lx) < 3f && Mathf.Abs(ly) < 3f)
                        continue;
                    if (Mathf.Abs(ly) < 2.2f || Mathf.Abs(lx) < 2.2f)
                        continue;
                    if (HasRim(doors, 'N') && Mathf.Abs(lx) < half + 1.4f && ly > lyEdge - 4.6f)
                        continue;
                    if (HasRim(doors, 'S') && Mathf.Abs(lx) < half + 1.4f && ly < -lyEdge + 4.6f)
                        continue;
                    if (HasRim(doors, 'E') && Mathf.Abs(ly) < half + 1.4f && lx > lxEdge - 4.6f)
                        continue;
                    if (HasRim(doors, 'W') && Mathf.Abs(ly) < half + 1.4f && lx < -lxEdge + 4.6f)
                        continue;
                    switch (variant)
                    {
                        case 0: // Ring near the walls.
                            if (Mathf.Abs(lx) < 9f && Mathf.Abs(ly) < 6.5f)
                                continue;
                            break;
                        case 1: // Corner-heavy.
                            if (Mathf.Abs(lx) < 7f || Mathf.Abs(ly) < 5f)
                                continue;
                            break;
                        case 2: // North and south rows.
                            if (Mathf.Abs(ly) < 6f)
                                continue;
                            break;
                        case 3: // Scattered mid-field.
                            if (Mathf.Abs(lx) > 11.5f || Mathf.Abs(ly) > 9f)
                                continue;
                            break;
                        case 4: // East and west columns.
                            if (Mathf.Abs(lx) < 6.5f)
                                continue;
                            break;
                        case 5: // Diagonal swath from south-west to north-east.
                            if (Mathf.Abs(lx - ly) > 5f)
                                continue;
                            break;
                        case 6: // Mid-radius annulus between court and walls.
                            {
                                float rr = Mathf.Max(Mathf.Abs(lx) / 12f, Mathf.Abs(ly) / 9.5f);
                                if (rr < .45f || rr > .8f)
                                    continue;
                                break;
                            }
                        default: // Coarse checkerboard scatter.
                            {
                                int cellA = Mathf.FloorToInt(lx * 0.34f + 100f);
                                int cellB = Mathf.FloorToInt(ly * 0.34f + 100f);
                                if (((cellA + cellB) & 1) != 0)
                                    continue;
                                break;
                            }
                    }
                    int[] picks = DressPalettes[variant % DressPalettes.Length];
                    int idx = picks[Hash(p, q, salt + 7) % picks.Length];
                    GameObject go = Place("Dressing " + dressIds[idx], world,
                        Load(DecalRoot + dressIds[idx] + ".png"), RubbleOrder, false, parent);
                    if (go.GetComponent<SpriteRenderer>().sprite != null)
                    {
                        into.Add(go);
                        placed++;
                    }
                    else
                        Object.Destroy(go);
                }
            }
        }

        /// <summary>Rotates or mirrors a room-local spot by the decoration layout so
        /// the same cluster set lands in different places from room to room; the
        /// eight cases cover every axis-aligned transform of the cluster set.</summary>
        static Vector2 Turn(Vector2 spot, int variant)
        {
            switch (variant)
            {
                case 1: return new Vector2(spot.y, -spot.x);    // quarter turn
                case 2: return new Vector2(-spot.x, -spot.y);   // half turn
                case 3: return new Vector2(-spot.y, spot.x);    // three-quarter turn
                case 4: return new Vector2(-spot.x, spot.y);    // mirror east-west
                case 5: return new Vector2(spot.x, -spot.y);    // mirror north-south
                case 6: return new Vector2(spot.y, spot.x);      // transpose
                case 7: return new Vector2(-spot.y, -spot.x);    // anti-transpose
                default: return spot;
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
            else if (family == "shield")
            {
                path = EnemyRoot + "Shield/jh_shield_" + action + "_" + facing + "_" + file + ".png";
            }
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
            // A runtime Tile defaults to TileFlags.LockColor, which makes
            // Tilemap.SetColor a silent no-op; the whole floor tint vocabulary
            // (noise base, routes, courts, inlays) depends on per-cell colors.
            tile.flags = TileFlags.None;
            return tile;
        }

        public static GameObject Place(string name, Vector3 pos, Sprite sprite, int order, bool flip, Transform parent)
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
