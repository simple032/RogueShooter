using UnityEngine;
using RogueShooter.Player;
using RogueShooter.Vision;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Stage1Maze painted tilemap play: camera follows the player, wall tilemap blocks.
    /// Does not rebuild rooms or touch floor/wall tiles.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class Stage1PaintedPlay : MonoBehaviour
    {
        public const string PortalAName = "FX_Portal_00";
        public const string PortalBName = "FX_Portal_01";

        void Awake()
        {
            var demo = GetComponent<Stage1MazeDemo>();
            if (demo != null)
                demo.enabled = false;

            Transform player = transform.Find("Player");
            if (player == null)
            {
                var existing = GameObject.Find("Player");
                if (existing != null)
                    player = existing.transform;
            }

            if (player != null)
            {
                var body = player.GetComponent<Rigidbody2D>();
                if (body == null)
                    body = player.gameObject.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                if (player.GetComponent<CircleCollider2D>() == null)
                {
                    var col = player.gameObject.AddComponent<CircleCollider2D>();
                    col.radius = 0.28f;
                }
                if (player.GetComponent<PlayerMotor2D>() == null)
                    player.gameObject.AddComponent<PlayerMotor2D>();
            }

            Camera cam = Camera.main;
            if (cam == null || player == null)
                return;
            CameraFollow2D follow = cam.GetComponent<CameraFollow2D>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<CameraFollow2D>();
            follow.SetTarget(player);
            Debug.Log("[Stage1Play] camera follow " + player.name
                      + " start=" + player.position
                      + " portals=" + PortalAName + "," + PortalBName);
        }
    }
}
