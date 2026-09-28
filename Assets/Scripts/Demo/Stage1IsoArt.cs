using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Iso;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Dresses the generated stage-1 maze with the accepted iso sprites.
    /// </summary>
    public static class Stage1IsoArt
    {
        const string FloorPath = "Assets/Art/JianHai/Iso/Tiles/S1/Floor/jh_iso_floor_s1_room_00.png";
        const string WallLPath = "Assets/Art/JianHai/Iso/Tiles/S1/Wall/jh_iso_wall_s1_l_00.png";
        const string WallRPath = "Assets/Art/JianHai/Iso/Tiles/S1/Wall/jh_iso_wall_s1_r_00.png";
        const string DoorLPath = "Assets/Art/JianHai/Iso/Tiles/S1/Wall/jh_iso_wall_s1_door_l_00.png";
        const string CornerPath = "Assets/Art/JianHai/Iso/Tiles/S1/Wall/jh_iso_wall_s1_corner_in_00.png";
        /// <summary>
        /// door_l painted foot is 24px lower than the wall foot (pivot 48 vs opaque 22, wall pivot 32 vs opaque 30).
        /// Raising the door pivot by this puts that foot on the same diamond corner as the wall.
        /// </summary>
        const float DoorFootLift = 24f / 128f;
        /// <summary>
        /// Play camera is pitched, so the sample-room south corner still leaves a gap under the wall.
        /// Drop the stage-1 foot onto the tile. Sample room placement is unchanged.
        /// </summary>
        const float PlayFootDrop = 0.45f;
        const string AltarPath = "Assets/Art/JianHai/Iso/Props/jh_iso_prop_altar_00.png";
        const string ChestPath = "Assets/Art/JianHai/Iso/Props/jh_iso_prop_chest_closed_00.png";
        const string HubPath = "Assets/Art/JianHai/Iso/Props/jh_iso_prop_gate_hub_00.png";

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

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

        public static GameObject BuildFloors(Stage1Maze maze, Transform parent)
        {
            var root = new GameObject("IsoFloor");
            root.transform.SetParent(parent, false);
            var grid = root.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            var mapGo = new GameObject("FloorTiles");
            mapGo.transform.SetParent(root.transform, false);
            var map = mapGo.AddComponent<Tilemap>();
            var rend = mapGo.AddComponent<TilemapRenderer>();
            rend.sortingOrder = 0;
            Sprite floor = Load(FloorPath);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = floor;

            var cells = new HashSet<long>();
            if (maze != null)
            {
                for (int i = 0; i < maze.Nodes.Length; i++)
                {
                    MazeNode n = maze.Nodes[i];
                    CoverRect(cells, n.Center.X, n.Center.Y, n.Width, n.Height);
                }

                for (int i = 0; i < maze.Edges.Length; i++)
                {
                    MazeEdge e = maze.Edges[i];
                    MazeVec2[] pts = e.Points;
                    if (pts == null || pts.Length < 2)
                        pts = new[] { e.From, e.To };
                    for (int s = 0; s < pts.Length - 1; s++)
                    {
                        CoverSegment(cells,
                            new Vector2(pts[s].X, pts[s].Y),
                            new Vector2(pts[s + 1].X, pts[s + 1].Y),
                            e.Width);
                    }
                }
            }

            var positions = new Vector3Int[cells.Count];
            var tiles = new TileBase[cells.Count];
            int k = 0;
            foreach (long packed in cells)
            {
                int x = (int)(packed >> 32);
                int y = (int)packed;
                positions[k] = new Vector3Int(x, y, 0);
                tiles[k] = tile;
                k++;
            }

            if (k > 0)
                map.SetTiles(positions, tiles);
            FloorCount = k;
            return root;
        }

        public static void RingRoom(MazeNode room, List<Vector3> gaps, Transform parent, List<GameObject> into)
        {
            float x0 = room.Center.X - room.Width * 0.5f;
            float x1 = room.Center.X + room.Width * 0.5f;
            float y0 = room.Center.Y - room.Height * 0.5f;
            float y1 = room.Center.Y + room.Height * 0.5f;
            int ix0 = Mathf.FloorToInt(x0);
            int ix1 = Mathf.CeilToInt(x1);
            int iy0 = Mathf.FloorToInt(y0 / 0.5f);
            int iy1 = Mathf.CeilToInt(y1 / 0.5f);
            var used = new bool[gaps.Count];
            Sprite wallL = Load(WallLPath);
            Sprite wallR = Load(WallRPath);
            Sprite corner = Load(CornerPath);
            Sprite door = Load(DoorLPath);
            for (int ix = ix0; ix <= ix1; ix++)
            {
                for (int iy = iy0; iy <= iy1; iy++)
                {
                    if (!CellIn(ix, iy, x0, x1, y0, y1))
                        continue;
                    bool south = !CellIn(ix, iy - 1, x0, x1, y0, y1);
                    bool north = !CellIn(ix, iy + 1, x0, x1, y0, y1);
                    bool west = !CellIn(ix - 1, iy, x0, x1, y0, y1);
                    bool east = !CellIn(ix + 1, iy, x0, x1, y0, y1);
                    if (!south && !north && !west && !east)
                        continue;
                    Vector3 foot = DiamondSouth(ix, iy) + new Vector3(0f, -PlayFootDrop, 0f);
                    int gap = Near(foot, gaps, 1.6f);
                    if (gap >= 0)
                    {
                        if (!used[gap])
                        {
                            used[gap] = true;
                            into.Add(Place("Door_" + room.Id + "_" + gap, foot + new Vector3(0f, DoorFootLift, 0f), door, 6, false, parent));
                        }

                        continue;
                    }

                    bool turn = (south || north) && (west || east);
                    Sprite wall = turn ? corner : (south || west ? wallL : wallR);
                    into.Add(Place("Wall_" + room.Id, foot, wall, 4, false, parent));
                }
            }
        }

        /// <summary>Same seat as the sample room: tall-wall foot on the cell's south diamond corner.</summary>
        public static Vector3 DiamondSouth(int ix, int iy)
        {
            return new Vector3(ix + 0.5f, iy * 0.5f, 0f);
        }

        public static GameObject Prop(string name, Vector3 pos, MazeNodeKind kind, Transform parent)
        {
            string path = kind == MazeNodeKind.Altar ? AltarPath
                : kind == MazeNodeKind.Connector ? HubPath
                : ChestPath;
            return Place(name, pos, Load(path), 8, false, parent);
        }

        public static Sprite ActorSprite(string family, bool moving, string facing, int frame, out bool flip)
        {
            flip = false;
            string src = facing;
            if (family != "archer" && !IsoFacing.TrySkeleton(facing, out src, out flip))
                src = "s";
            string action = moving && family == "archer" ? "walk" : "idle";
            int f = frame < 0 ? 0 : frame;
            Sprite sprite = LoadActor(family, action, src, f % 3);
            if (sprite == null && action != "idle")
                sprite = LoadActor(family, "idle", src, f % 3);
            if (sprite == null)
                sprite = LoadActor(family, "idle", src, 0);
            return sprite;
        }

        static bool CellIn(int ix, int iy, float x0, float x1, float y0, float y1)
        {
            float px = ix + 0.5f;
            float py = iy * 0.5f + 0.25f;
            return px >= x0 && px <= x1 && py >= y0 && py <= y1;
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

        static Sprite LoadActor(string family, string action, string facing, int frame)
        {
            string file = frame.ToString("00");
            string path;
            if (family == "archer")
                path = "Assets/Art/JianHai/Iso/Characters/Archer/jh_archer_" + action + "_" + facing + "_" + file + ".png";
            else if (family == "dog")
                path = "Assets/Art/JianHai/Iso/Enemies/Dog/jh_dog_idle_" + facing + "_" + file + ".png";
            else if (family == "mage")
                path = "Assets/Art/JianHai/Iso/Enemies/Mage/jh_mage_idle_" + facing + "_" + file + ".png";
            else
                path = "Assets/Art/JianHai/Iso/Enemies/Skel/jh_skel_idle_" + facing + "_" + file + ".png";
            return Load(path);
        }

        static Sprite Load(string path)
        {
            Sprite sprite;
            if (Cache.TryGetValue(path, out sprite) && sprite != null)
                return sprite;
#if UNITY_EDITOR
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

        static void CoverRect(HashSet<long> cells, float cx, float cy, float w, float h)
        {
            float x0 = cx - w * 0.5f;
            float x1 = cx + w * 0.5f;
            float y0 = cy - h * 0.5f;
            float y1 = cy + h * 0.5f;
            int ix0 = Mathf.FloorToInt(x0);
            int ix1 = Mathf.CeilToInt(x1);
            int iy0 = Mathf.FloorToInt(y0 / 0.5f);
            int iy1 = Mathf.CeilToInt(y1 / 0.5f);
            for (int ix = ix0; ix <= ix1; ix++)
            {
                for (int iy = iy0; iy <= iy1; iy++)
                {
                    float px = ix + 0.5f;
                    float py = iy * 0.5f + 0.25f;
                    if (px >= x0 && px <= x1 && py >= y0 && py <= y1)
                        cells.Add(Pack(ix, iy));
                }
            }
        }

        static void CoverSegment(HashSet<long> cells, Vector2 a, Vector2 b, float width)
        {
            float half = width * 0.5f + 0.2f;
            Vector2 min = Vector2.Min(a, b) - new Vector2(half, half);
            Vector2 max = Vector2.Max(a, b) + new Vector2(half, half);
            int ix0 = Mathf.FloorToInt(min.x);
            int ix1 = Mathf.CeilToInt(max.x);
            int iy0 = Mathf.FloorToInt(min.y / 0.5f);
            int iy1 = Mathf.CeilToInt(max.y / 0.5f);
            for (int ix = ix0; ix <= ix1; ix++)
            {
                for (int iy = iy0; iy <= iy1; iy++)
                {
                    var c = new Vector2(ix + 0.5f, iy * 0.5f + 0.25f);
                    if (Dist(c, a, b) <= half)
                        cells.Add(Pack(ix, iy));
                }
            }
        }

        static float Dist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float d = ab.sqrMagnitude;
            if (d < 0.0001f)
                return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / d);
            return Vector2.Distance(p, a + ab * t);
        }

        static int Near(Vector3 p, List<Vector3> gaps, float r)
        {
            float r2 = r * r;
            for (int i = 0; i < gaps.Count; i++)
            {
                if ((gaps[i] - p).sqrMagnitude <= r2)
                    return i;
            }

            return -1;
        }

        static long Pack(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }
    }
}
