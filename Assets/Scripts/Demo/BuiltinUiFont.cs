using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Unity / Tuanjie 2022.3 removed Arial.ttf from GetBuiltinResource.
    /// Requesting it throws ArgumentException and aborts demo BuildWorld.
    /// LoadUi() adds the bundled CJK UI font with OS / LegacyRuntime fallback.
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

        /// <summary>Project-bundled CJK UI font (Noto Sans SC subset, OFL) under Resources.</summary>
        public const string ProjectUiFontPath = "Fonts/JianHaiUI-SC";

        /// <summary>OS CJK fallbacks, only used when the bundled font is missing.</summary>
        static readonly string[] OsCjkFallbacks =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "PingFang SC",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "SimHei",
        };

        static Font _ui;
        static bool _triedUi;

        /// <summary>
        /// Chinese UI text font. Order: bundled Resources font → installed OS CJK font →
        /// LegacyRuntime.ttf (never Arial: Tuanjie 2022.3 throws on Arial.ttf).
        /// </summary>
        public static Font LoadUi()
        {
            if (_triedUi)
                return _ui;
            _triedUi = true;
            _ui = Resources.Load<Font>(ProjectUiFontPath);
            if (_ui != null)
            {
                Debug.Log("[UiFont] bundled " + ProjectUiFontPath);
                return _ui;
            }

            string os = FirstInstalled(OsCjkFallbacks);
            if (os != null)
            {
                _ui = Font.CreateDynamicFontFromOSFont(OsCjkFallbacks, 32);
                Debug.LogWarning("[UiFont] bundled font missing; OS fallback " + os);
                return _ui;
            }

            _ui = Load();
            Debug.LogWarning("[UiFont] bundled + OS CJK fonts missing; LegacyRuntime (CJK may render as boxes)");
            return _ui;
        }

        static string FirstInstalled(string[] names)
        {
            string[] installed;
            try
            {
                installed = Font.GetOSInstalledFontNames();
            }
            catch (System.Exception)
            {
                return null;
            }

            if (installed == null)
                return null;
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (string.Equals(installed[j], names[i], System.StringComparison.OrdinalIgnoreCase))
                        return names[i];
                }
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
