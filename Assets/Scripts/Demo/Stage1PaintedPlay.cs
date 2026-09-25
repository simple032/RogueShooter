using UnityEngine;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Painted Stage1Maze adapter. Stage1MazeDemo stays the single runtime driver
    /// (door lock / portal → spawn / room clear / player charge + HP). When this
    /// component sits next to the Demo and the painted Grid exists, the Demo uses
    /// the painted room rects below instead of the procedural skeleton, skips its
    /// placeholder floor/wall quads, and gives its own spawned Player a
    /// Rigidbody2D + CircleCollider2D so the Walls TilemapCollider2D stops it.
    /// Rects mirror Editor/JianHaiStage1MazeBuilder.PaintStage1 (cell = 1 world unit).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class Stage1PaintedPlay : MonoBehaviour
    {
        public const string GridName = "Grid";
        public const float PlayerRadius = 0.28f;
        public const float CorridorWidth = 2f;

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
