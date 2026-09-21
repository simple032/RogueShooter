using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RogueShooter.Art
{
    /// <summary>
    /// Resolves jh_* sprites from Assets/Art/JianHai/ (Provide-sourced PNGs).
    /// Order: cache → Editor AssetDatabase → runtime PNG bytes (LoadImage) →
    /// Point-filter PPU=32 placeholder. Interact GameObject names stay HOOKS IDs.
    /// </summary>
    public static class JianHaiSprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> CenterCache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, bool> FileCache = new Dictionary<string, bool>();

        public static Sprite Get(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return Placeholder("jh_missing");
            Sprite sprite;
            if (Cache.TryGetValue(artId, out sprite) && sprite != null)
                return sprite;

            sprite = LoadImported(artId) ?? LoadFromPngBytes(artId) ?? Placeholder(artId);
            Cache[artId] = sprite;
            return sprite;
        }

        /// <summary>Center-pivot copy for SpriteDrawMode.Tiled volumes (AABB sits on transform.position).</summary>
        public static Sprite GetCentered(string artId)
        {
            Sprite sprite;
            if (CenterCache.TryGetValue(artId, out sprite) && sprite != null)
                return sprite;
            Sprite src = Get(artId);
            if (src == null || src.texture == null)
                return src;
            sprite = Sprite.Create(
                src.texture,
                src.rect,
                new Vector2(0.5f, 0.5f),
                src.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = artId + "_c";
            CenterCache[artId] = sprite;
            return sprite;
        }

        public static bool UsedPlaceholder(string artId)
        {
            return !HasSourceFile(artId);
        }

        /// <summary>
        /// True if a clip root, `_00`, or `_s_00` PNG exists. Missing walk/atk stay false
        /// so callers fall back to idle — do not invent frames.
        /// </summary>
        public static bool HasClip(string root)
        {
            if (string.IsNullOrEmpty(root))
                return false;
            return !string.IsNullOrEmpty(ResolveClipArt(root, "s", 0, null));
        }

        /// <summary>
        /// ACTION_SPEC naming: `root[_dir]_ff` then `root_ff` then `root`.
        /// Missing dir uses `s` then no-dir. `fallback` (idle) when nothing is on disk.
        /// </summary>
        public static string ResolveClipArt(string root, string dir, int frame, string fallback)
        {
            if (string.IsNullOrEmpty(root))
                return fallback;
            int f = frame < 0 ? 0 : frame;
            string ff = f < 10 ? "0" + f : f.ToString();
            if (!string.IsNullOrEmpty(dir))
            {
                string withDir = root + "_" + dir + "_" + ff;
                if (HasSourceFile(withDir))
                    return withDir;
            }

            string numbered = root + "_" + ff;
            if (HasSourceFile(numbered))
                return numbered;
            if (HasSourceFile(root))
                return root;
            if (HasSourceFile(root + "_s_" + ff))
                return root + "_s_" + ff;
            if (HasSourceFile(root + "_s_00"))
                return root + "_s_00";
            if (HasSourceFile(root + "_00"))
                return root + "_00";
            return fallback;
        }

        public static bool HasSourceFile(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return false;
            bool known;
            if (FileCache.TryGetValue(artId, out known))
                return known;
            string disk = DiskPath(artId);
            known = !string.IsNullOrEmpty(disk) && File.Exists(disk);
            FileCache[artId] = known;
            return known;
        }

        public static string DiskPath(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return "";
            string folder = JianHaiArtCatalog.FolderForArtId(artId);
            string file = artId + ".png";
            string[] ids = JianHaiArtCatalog.ArtSearchIds(artId);
            for (int i = 0; i < ids.Length; i++)
            {
                string found = FindDisk(ids[i]);
                if (!string.IsNullOrEmpty(found) && File.Exists(found))
                    return found;
            }

            return Path.Combine("Assets", "Art", "JianHai", folder, file);
        }

        static string FindDisk(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return "";
            string folder = JianHaiArtCatalog.FolderForArtId(artId);
            string file = artId + ".png";
            try
            {
                if (!string.IsNullOrEmpty(Application.dataPath))
                {
                    string underAssets = Path.Combine(Application.dataPath, "Art", "JianHai", folder, file);
                    if (File.Exists(underAssets))
                        return underAssets;
                }
            }
            catch
            {
                /* headless / tests fall through */
            }

            string relative = Path.Combine("Assets", "Art", "JianHai", folder, file);
            if (File.Exists(relative))
                return Path.GetFullPath(relative);
            try
            {
                string cwd = Path.Combine(Directory.GetCurrentDirectory(), relative);
                if (File.Exists(cwd))
                    return cwd;
            }
            catch
            {
            }

            return relative;
        }

        static Sprite LoadImported(string artId)
        {
#if UNITY_EDITOR
            string[] ids = JianHaiArtCatalog.ArtSearchIds(artId);
            for (int i = 0; i < ids.Length; i++)
            {
                string path = JianHaiArtCatalog.AssetPath(ids[i]);
                var imported = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (imported != null)
                    return imported;
            }
#endif
            return null;
        }

        static Sprite LoadFromPngBytes(string artId)
        {
            string disk = DiskPath(artId);
            if (string.IsNullOrEmpty(disk) || !File.Exists(disk))
                return null;
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(disk);
            }
            catch
            {
                return null;
            }

            if (bytes == null || bytes.Length < 24)
                return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = artId;
            if (!tex.LoadImage(bytes))
                return null;
            tex.filterMode = FilterMode.Point;
            bool tile = artId.StartsWith("jh_tile_", System.StringComparison.Ordinal)
                || artId.StartsWith("jh_wall_", System.StringComparison.Ordinal);
            tex.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.Apply(false, false);

            JianHaiArtCatalog.Vector2Like p = JianHaiArtCatalog.PivotForArtId(artId);
            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(p.x, p.y),
                JianHaiArtCatalog.Ppu,
                0,
                SpriteMeshType.FullRect);
            sprite.name = artId;
            return sprite;
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
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingLayerName = JianHaiArtCatalog.SortingLayer(artId);
            sr.sortingOrder = JianHaiArtCatalog.SortingOrderForArtId(artId);
            sr.color = Color.white;
        }

        public static void BindTiled(SpriteRenderer sr, string artId, Vector2 worldSize, int order, Color tint)
        {
            if (sr == null)
                return;
            sr.sprite = GetCentered(artId);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = worldSize;
            sr.color = tint.a <= 0.001f ? Color.white : tint;
            sr.sortingLayerName = JianHaiArtCatalog.SortingLayer(artId);
            sr.sortingOrder = order != 0 ? order : JianHaiArtCatalog.SortingOrderForArtId(artId);
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
            if (artId.Contains("proj_arrow") || artId.Contains("arrow_fly"))
                return Hex(0xd4c4a0);
            if (artId.Contains("orb_mage") || artId.Contains("mage_orb") || artId.Contains("proj_orb"))
                return Hex(0xc22bd4);
            if (artId.Contains("roll"))
                return Hex(0xb8b0a0);
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
