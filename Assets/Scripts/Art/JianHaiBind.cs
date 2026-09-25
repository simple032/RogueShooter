using UnityEngine;

namespace RogueShooter.Art
{
    public static class JianHaiBind
    {
        public static GameObject Spawn(string name, Vector3 position, string artId, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            ApplyScale(go.transform, artId);
            var sr = go.AddComponent<SpriteRenderer>();
            JianHaiSprites.Bind(sr, artId);
            return go;
        }

        public static void ApplyTo(GameObject go, string artId)
        {
            if (go == null)
                return;
            ApplyScale(go.transform, artId);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = go.AddComponent<SpriteRenderer>();
            JianHaiSprites.Bind(sr, artId);
        }

        public static void ApplyScale(Transform t, string artId)
        {
            if (t == null)
                return;
            float s = JianHaiArtCatalog.StubWorldScale(artId);
            t.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>
        /// Tiled JianHai PNG at localScale=1. Collision volumes must pass explicit hx/hy
        /// (do not derive AABB from sprite scale).
        /// </summary>
        public static GameObject SpawnTiled(
            string name,
            Vector3 position,
            Vector2 worldSize,
            string artId,
            Transform parent,
            int order,
            Quaternion rotation,
            Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = Vector3.one;
            var sr = go.AddComponent<SpriteRenderer>();
            JianHaiSprites.BindTiled(sr, artId, worldSize, order, tint);
            return go;
        }

        public static GameObject SpawnCorridorTiled(
            string name, Vector3 a, Vector3 b, float width, string artId, Transform parent, int order)
        {
            Vector3 delta = b - a;
            delta.z = 0f;
            float length = Mathf.Max(0.1f, delta.magnitude);
            Vector3 mid = (a + b) * 0.5f;
            mid.z = 1f;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            return SpawnTiled(
                name, mid, new Vector2(length + 0.6f, width), artId, parent, order,
                Quaternion.Euler(0f, 0f, angle), Color.white);
        }

        public static void SetLayer(GameObject go, string layer, int order)
        {
            if (go == null)
                return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                return;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
        }
    }
}
