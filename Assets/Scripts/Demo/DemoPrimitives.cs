using UnityEngine;

namespace RogueShooter.Demo
{
    internal static class DemoPrimitives
    {
        static Sprite _white;

        public static Sprite WhiteSprite()
        {
            if (_white != null)
                return _white;
            Texture2D tex = Texture2D.whiteTexture;
            _white = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                tex.width);
            return _white;
        }

        public static GameObject Quad(string name, Vector3 position, Vector2 size, Color color, int sorting, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            AddSprite(go, color, sorting);
            return go;
        }

        public static SpriteRenderer AddSprite(GameObject go, Color color, int sorting)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = color;
            sr.sortingOrder = sorting;
            return sr;
        }

        public static GameObject Corridor(string name, Vector3 a, Vector3 b, float width, Color color, int sorting, Transform parent)
        {
            Vector3 delta = b - a;
            delta.z = 0f;
            float length = Mathf.Max(0.1f, delta.magnitude);
            Vector3 mid = (a + b) * 0.5f;
            mid.z = 1f;
            GameObject go = Quad(name, mid, new Vector2(length + 0.6f, width), color, sorting, parent);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            return go;
        }
    }
}
