using System.Collections.Generic;
using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Sprite table kept under Resources so a player build pulls in the
    /// floor, wall, character, and UI art Stage1IsoArt.Load asks for.
    /// </summary>
    public sealed class Stage1PackedSprites : ScriptableObject
    {
        public string[] paths = System.Array.Empty<string>();
        public Sprite[] sprites = System.Array.Empty<Sprite>();

        static Dictionary<string, Sprite> _map;

        public static Sprite Find(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;
            Ensure();
            Sprite sprite;
            if (_map != null && _map.TryGetValue(assetPath, out sprite))
                return sprite;
            return null;
        }

        static void Ensure()
        {
            if (_map != null)
                return;
            _map = new Dictionary<string, Sprite>();
            Stage1PackedSprites asset = Resources.Load<Stage1PackedSprites>("Stage1PackedSprites");
            if (asset == null || asset.paths == null || asset.sprites == null)
                return;
            int n = asset.paths.Length < asset.sprites.Length ? asset.paths.Length : asset.sprites.Length;
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(asset.paths[i]) || asset.sprites[i] == null)
                    continue;
                _map[asset.paths[i]] = asset.sprites[i];
            }
        }
    }
}
