using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Unity / Tuanjie 2022.3 removed Arial.ttf from GetBuiltinResource.
    /// Requesting it throws ArgumentException and aborts demo BuildWorld.
    /// </summary>
    public static class BuiltinUiFont
    {
        static Font _cached;
        static bool _tried;

        static readonly string[] Candidates =
        {
            "LegacyRuntime.ttf",
            "LegacyRuntime.otf",
        };

        public static Font Load()
        {
            if (_tried)
                return _cached;
            _tried = true;
            for (int i = 0; i < Candidates.Length; i++)
            {
                try
                {
                    _cached = Resources.GetBuiltinResource<Font>(Candidates[i]);
                }
                catch (System.Exception)
                {
                    _cached = null;
                }

                if (_cached != null)
                    return _cached;
            }

            return null;
        }

        public static void Apply(TextMesh tm)
        {
            if (tm == null)
                return;
            Font font = Load();
            if (font == null)
                return;
            tm.font = font;
            var renderer = tm.GetComponent<MeshRenderer>();
            if (renderer != null && font.material != null)
                renderer.sharedMaterial = font.material;
        }
    }
}
