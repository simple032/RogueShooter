using System.IO;
using UnityEngine;
using UnityEditor;

namespace RogueShooter.EditorTools
{
    public static class ShieldAnimationImport
    {
        [MenuItem("JianHai/Art/Import Latest Shield Animations")]
        public static void Import()
        {
            int count = 0;
            foreach (string file in Directory.GetFiles("Assets/Art/JianHai/Iso/Enemies/Shield", "*.png"))
            {
                string path=file.Replace('\\','/');
                string name = Path.GetFileNameWithoutExtension(path);
                // Auxiliary normal maps and artist temporary files keep their own settings.
                if (name.EndsWith("_n") || name.Contains(".tmp")) continue;
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) throw new UnityEditor.Build.BuildFailedException("Shield animation has no valid importer: "+path);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 128f;
                var settings = new TextureImporterSettings(); imp.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(.5f,.125f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(settings);
                imp.mipmapEnabled = false; imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.filterMode = FilterMode.Bilinear;
                imp.SaveAndReimport(); count++;
            }
            Stage1PackedSpritesBuilder.BuildCatalog();
            Debug.Log("[ShieldMotion] imported="+count+" ppu=128 foot=(.5,.125)");
        }
    }
}
