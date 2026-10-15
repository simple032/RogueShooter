using System.Collections.Generic;
using UnityEngine;

namespace RogueShooter.Art
{
    /// <summary>
    /// Resolves jh_* sprites. Editor Play Mode loads imported PNGs when present;
    /// otherwise a Point-filter PPU=32 placeholder with the spec pivot is generated.
    /// Swap files in Assets/Art/JianHai/ — interact GameObject names stay HOOKS IDs.
    /// </summary>
    public static class JianHaiSprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return Placeholder("jh_missing");
            Sprite sprite;
            if (Cache.TryGetValue(artId, out sprite) && sprite != null)
                return sprite;

            sprite = LoadImported(artId) ?? Placeholder(artId);
            Cache[artId] = sprite;
            return sprite;
        }

        public static bool UsedPlaceholder(string artId)
        {
            return LoadImported(artId) == null;
        }

        static Sprite LoadImported(string artId)
        {
#if UNITY_EDITOR
            string path = JianHaiArtCatalog.AssetPath(artId);
            var imported = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (imported != null)
                return imported;
#endif
            return null;
        }

        public static Sprite Placeholder(string artId)
        {
            int w, h;
            JianHaiArtCatalog.CanvasForArtId(artId, out w, out h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = artId + "_placeholder";

            Color fill = FillColor(artId);
            Color ink = Darken(fill, 0.45f);
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++)
                px[i] = clear;

            int x0 = Mathf.Max(2, w / 8);
            int y0 = Mathf.Max(2, h / 10);
            int x1 = w - x0 - 1;
            int y1 = h - Mathf.Max(2, h / 12) - 1;
            FillRect(px, w, h, x0, y0, x1, y1, fill);
            OutlineRect(px, w, h, x0, y0, x1, y1, ink);
            tex.SetPixels(px);
            tex.Apply(false, false);

            JianHaiArtCatalog.Vector2Like p = JianHaiArtCatalog.PivotForArtId(artId);
            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, w, h),
                new Vector2(p.x, p.y),
                JianHaiArtCatalog.Ppu,
                0,
                SpriteMeshType.FullRect);
            sprite.name = artId;
            return sprite;
        }

        public static void Bind(SpriteRenderer sr, string artId)
        {
            if (sr == null)
                return;
            sr.sprite = Get(artId);
            sr.sortingLayerName = JianHaiArtCatalog.SortingLayer(artId);
            sr.sortingOrder = 0;
            sr.color = Color.white;
        }

        static Color FillColor(string artId)
        {
            if (artId.Contains("chest") && artId.Contains("open"))
                return Hex(0x8a7018);
            if (artId.Contains("chest"))
                return Hex(0xc9a227);
            if (artId.Contains("altar") && artId.Contains("active"))
                return Hex(0xe0b56a);
            if (artId.Contains("altar"))
                return Hex(0xd4a05a);
            if (artId.Contains("shop"))
                return Hex(0x5a8f7b);
            if (artId.Contains("boss"))
                return Hex(0x8a3030);
            if (artId.Contains("e1") || artId.Contains("enemy"))
                return Hex(0xc4453a);
            if (artId.Contains("char") || artId.Contains("archer"))
                return Hex(0xc8c0b0);
            if (artId.Contains("deadend"))
                return Hex(0x16181c);
            if (artId.Contains("hub"))
                return Hex(0x2a3038);
            if (artId.Contains("wall"))
                return Hex(0x3a424c);
            if (artId.Contains("tile") || artId.Contains("floor"))
                return Hex(0x1a1d24);
            if (artId.Contains("reticle") && artId.Contains("green"))
                return Hex(0x5a8f7b);
            if (artId.Contains("reticle"))
                return Hex(0xc8c0b0);
            if (artId.Contains("crit_flash"))
                return Hex(0xe0b56a);
            if (artId.Contains("glow_cold"))
                return Hex(0xb8c4d4);
            if (artId.Contains("string_glow") || artId.Contains("bow_edge") || artId.Contains("arrow_tip"))
                return Hex(0xd4a05a);
            if (artId.Contains("fx"))
                return Hex(0xd4a05a);
            if (artId.Contains("ui"))
                return Hex(0xc8c0b0);
            return Hex(0x6a6e74);
        }

        static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xff) / 255f,
                ((rgb >> 8) & 0xff) / 255f,
                (rgb & 0xff) / 255f,
                1f);
        }

        static Color Darken(Color c, float t)
        {
            return Color.Lerp(c, Color.black, t);
        }

        static void FillRect(Color[] px, int w, int h, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                px[y * w + x] = c;
        }

        static void OutlineRect(Color[] px, int w, int h, int x0, int y0, int x1, int y1, Color c)
        {
            for (int x = x0; x <= x1; x++)
            {
                px[y0 * w + x] = c;
                px[y1 * w + x] = c;
            }

            for (int y = y0; y <= y1; y++)
            {
                px[y * w + x0] = c;
                px[y * w + x1] = c;
            }
        }
    }
}
