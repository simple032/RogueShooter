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
