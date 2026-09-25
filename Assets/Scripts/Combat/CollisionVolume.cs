using UnityEngine;

namespace RogueShooter.Combat
{
    /// <summary>
    /// Readable collider volume. Adds a trigger BoxCollider2D and a Unity layer
    /// so player / mob / wall / door / projectile are inspectable. Gameplay queries
    /// go through CollisionWorld (AABB), not Physics2D simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public class CollisionVolume : MonoBehaviour
    {
        [SerializeField] CollisionLayer layer = CollisionLayer.Wall;
        [SerializeField] bool solid = true;
        [SerializeField] float halfX;
        [SerializeField] float halfY;

        BoxCollider2D _box;

        public CollisionLayer Layer => layer;
        public bool Solid => solid && isActiveAndEnabled;

        public CollisionAabb WorldAabb
        {
            get
            {
                Vector3 p = transform.position;
                if (halfX > 0.001f && halfY > 0.001f)
                    return new CollisionAabb(p.x, p.y, halfX, halfY);

                Vector3 s = transform.lossyScale;
                float rad = transform.eulerAngles.z * Mathf.Deg2Rad;
                float c = Mathf.Abs(Mathf.Cos(rad));
                float sn = Mathf.Abs(Mathf.Sin(rad));
                float hx = 0.5f * (c * Mathf.Abs(s.x) + sn * Mathf.Abs(s.y));
                float hy = 0.5f * (sn * Mathf.Abs(s.x) + c * Mathf.Abs(s.y));
                return new CollisionAabb(p.x, p.y, hx, hy);
            }
        }

        public void Configure(CollisionLayer collisionLayer, bool isSolid, float hx = 0f, float hy = 0f)
        {
            layer = collisionLayer;
            solid = isSolid;
            halfX = hx;
            halfY = hy;
            ApplyUnity();
        }

        public void SetSolid(bool isSolid)
        {
            solid = isSolid;
        }

        void OnEnable()
        {
            ApplyUnity();
            CollisionWorld.Register(this);
        }

        void OnDisable()
        {
            CollisionWorld.Unregister(this);
        }

        void ApplyUnity()
        {
            _box = GetComponent<BoxCollider2D>();
            if (_box == null)
                _box = gameObject.AddComponent<BoxCollider2D>();
            _box.isTrigger = true;
            if (halfX > 0.001f && halfY > 0.001f)
            {
                Vector3 s = transform.lossyScale;
                float sx = Mathf.Abs(s.x) < 0.0001f ? 1f : Mathf.Abs(s.x);
                float sy = Mathf.Abs(s.y) < 0.0001f ? 1f : Mathf.Abs(s.y);
                _box.size = new Vector2(halfX * 2f / sx, halfY * 2f / sy);
            }
            else
            {
                _box.size = Vector2.one;
            }

            gameObject.layer = CollisionRules.UnityLayer(layer);
        }

        void OnDrawGizmos()
        {
            CollisionAabb box = WorldAabb;
            Gizmos.color = GizmoColor();
            Gizmos.DrawWireCube(new Vector3(box.X, box.Y, 0f), new Vector3(box.Hx * 2f, box.Hy * 2f, 0.1f));
        }

        Color GizmoColor()
        {
            if ((layer & CollisionLayer.Door) != 0)
                return new Color(0.95f, 0.25f, 0.2f, 0.9f);
            if ((layer & CollisionLayer.Wall) != 0)
                return new Color(0.55f, 0.6f, 0.7f, 0.8f);
            if ((layer & CollisionLayer.Player) != 0)
                return new Color(0.3f, 0.85f, 0.4f, 0.9f);
            if ((layer & CollisionLayer.Mob) != 0)
                return new Color(0.95f, 0.45f, 0.2f, 0.9f);
            return new Color(0.9f, 0.85f, 0.2f, 0.9f);
        }

        public static CollisionVolume Add(
            GameObject go, CollisionLayer collisionLayer, bool isSolid, float hx = 0f, float hy = 0f)
        {
            if (go == null)
                return null;
            var vol = go.GetComponent<CollisionVolume>();
            if (vol == null)
                vol = go.AddComponent<CollisionVolume>();
            vol.Configure(collisionLayer, isSolid, hx, hy);
            return vol;
        }
    }
}
