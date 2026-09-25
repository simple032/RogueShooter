using System.Collections.Generic;
using RogueShooter.Maze;

namespace RogueShooter.Combat
{
    public struct MazeSolid
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public CollisionLayer Layer;
        public string Name;
        public bool Door;
        public string RoomId;
        public string OtherId;
    }

    /// <summary>
    /// Punches door openings in room walls and places door volumes on those openings.
    /// 走廊宽度_建议_v02: opening = corridor − 2 × 1u stub (5u corridor → 3u door), centred on
    /// <see cref="MazeRules.DoorAxis"/> so door edges and stubs land on whole cells.
    /// Geometry matches Stage-1 maze wall thickness.
    /// </summary>
    public static class MazeCollisionBuilder
    {
        public const int SideN = 0;
        public const int SideS = 1;
        public const int SideW = 2;
        public const int SideE = 3;

        public static List<MazeSolid> Build(Stage1Maze maze)
        {
            return Build(maze, -1f);
        }

        /// <summary>corridorWidth &lt;= 0 uses CollisionRules.DoorOpening (3u).
        /// Otherwise the opening is <see cref="MazeRules.DoorOpeningFor"/>(corridorWidth):
        /// painted Stage1 passes its 5u corridor width → 3u door + 1u stub each side.</summary>
        public static List<MazeSolid> Build(Stage1Maze maze, float corridorWidth)
        {
            float doorOpening = corridorWidth > 0.01f ? MazeRules.DoorOpeningFor(corridorWidth) : -1f;
            var list = new List<MazeSolid>();
            if (maze == null || maze.Nodes == null)
                return list;
            for (int i = 0; i < maze.Nodes.Length; i++)
                BuildRoom(maze, maze.Nodes[i], list, doorOpening);
            return list;
        }

        public static void BuildRoom(Stage1Maze maze, MazeNode room, List<MazeSolid> into, float doorOpening = -1f)
        {
            if (room == null || into == null)
                return;
            float t = CollisionRules.WallThickness;
            float opening = doorOpening > 0.01f ? doorOpening : CollisionRules.DoorOpening;
            var openings = new List<float>[4];
            var others = new List<string>[4];
            for (int s = 0; s < 4; s++)
            {
                openings[s] = new List<float>();
                others[s] = new List<string>();
            }

            if (room.NeighborIds != null && maze != null)
            {
                for (int n = 0; n < room.NeighborIds.Length; n++)
                {
                    MazeNode other = maze.Find(room.NeighborIds[n]);
                    if (other == null)
                        continue;
                    int side;
                    float along;
                    DoorOnWall(room, other, out side, out along);
                    openings[side].Add(along);
                    others[side].Add(other.Id);
                }
            }

            EmitSide(into, room, SideN, openings[SideN], others[SideN], t, opening);
            EmitSide(into, room, SideS, openings[SideS], others[SideS], t, opening);
            EmitSide(into, room, SideW, openings[SideW], others[SideW], t, opening);
            EmitSide(into, room, SideE, openings[SideE], others[SideE], t, opening);
        }

        public static void DoorOnWall(MazeNode room, MazeNode other, out int side, out float along)
        {
            float dx = other.Center.X - room.Center.X;
            float dy = other.Center.Y - room.Center.Y;
            if (Abs(dx) > Abs(dy))
            {
                side = dx > 0f ? SideE : SideW;
                along = MazeRules.DoorAxis(room.Center.Y);
            }
            else
            {
                side = dy > 0f ? SideN : SideS;
                along = MazeRules.DoorAxis(room.Center.X);
            }
        }

        static void EmitSide(
            List<MazeSolid> into, MazeNode room, int side, List<float> holes, List<string> holeOthers,
            float thickness, float opening)
        {
            bool horiz = side == SideN || side == SideS;
            float cx = room.Center.X;
            float cy = room.Center.Y;
            float hw = room.Width * 0.5f;
            float hh = room.Height * 0.5f;
            float wallX = cx;
            float wallY = cy;
            float wallW = horiz ? room.Width + thickness : thickness;
            float wallH = horiz ? thickness : room.Height;
            if (side == SideN) wallY = cy + hh;
            if (side == SideS) wallY = cy - hh;
            if (side == SideW) wallX = cx - hw;
            if (side == SideE) wallX = cx + hw;

            float axisMin = horiz ? cx - hw : cy - hh;
            float axisMax = horiz ? cx + hw : cy + hh;

            var cuts = new List<float>();
            if (holes != null)
            {
                for (int i = 0; i < holes.Count; i++)
                    cuts.Add(holes[i]);
            }

            cuts.Sort();
            if (cuts.Count == 0)
            {
                into.Add(MakeFullWall(room.Id, side, wallX, wallY, wallW, wallH));
                return;
            }

            float cursor = axisMin;
            for (int i = 0; i < cuts.Count; i++)
            {
                float mid = cuts[i];
                float a = mid - opening * 0.5f;
                float b = mid + opening * 0.5f;
                if (a < axisMin) a = axisMin;
                if (b > axisMax) b = axisMax;
                if (a - cursor > 0.05f)
                    into.Add(MakeWall(room.Id, side, horiz, wallX, wallY, wallW, wallH, cursor, a));
                string other = holeOthers != null && i < holeOthers.Count ? holeOthers[i] : "";
                into.Add(MakeDoor(room.Id, other, horiz, wallX, wallY, thickness, opening, mid));
                cursor = b;
            }

            if (axisMax - cursor > 0.05f)
                into.Add(MakeWall(room.Id, side, horiz, wallX, wallY, wallW, wallH, cursor, axisMax));
        }

        static MazeSolid MakeFullWall(string roomId, int side, float x, float y, float w, float h)
        {
            return new MazeSolid
            {
                X = x,
                Y = y,
                Width = w,
                Height = h,
                Layer = CollisionLayer.Wall,
                Name = WallName(roomId, side),
                Door = false,
                RoomId = roomId
            };
        }

        static MazeSolid MakeWall(
            string roomId, int side, bool horiz, float wallX, float wallY, float wallW, float wallH,
            float from, float to)
        {
            float mid = (from + to) * 0.5f;
            float span = to - from;
            float x = horiz ? mid : wallX;
            float y = horiz ? wallY : mid;
            float w = horiz ? span : wallW;
            float h = horiz ? wallH : span;
            return new MazeSolid
            {
                X = x,
                Y = y,
                Width = w,
                Height = h,
                Layer = CollisionLayer.Wall,
                Name = WallName(roomId, side),
                Door = false,
                RoomId = roomId
            };
        }

        static MazeSolid MakeDoor(
            string roomId, string otherId, bool horiz, float wallX, float wallY, float thickness,
            float opening, float along)
        {
            float x = horiz ? along : wallX;
            float y = horiz ? wallY : along;
            float w = horiz ? opening : thickness;
            float h = horiz ? thickness : opening;
            return new MazeSolid
            {
                X = x,
                Y = y,
                Width = w,
                Height = h,
                Layer = CollisionLayer.Door,
                Name = "Door_" + roomId + "_" + otherId,
                Door = true,
                RoomId = roomId,
                OtherId = otherId
            };
        }

        static string WallName(string roomId, int side)
        {
            string s = side == SideN ? "N" : side == SideS ? "S" : side == SideW ? "W" : "E";
            return "Wall" + s + "_" + roomId;
        }

        static float Abs(float v)
        {
            return v < 0f ? -v : v;
        }
    }
}
