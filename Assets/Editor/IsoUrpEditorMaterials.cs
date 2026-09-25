using UnityEditor;
using UnityEngine;

namespace RogueShooter.Iso.Render
{
    /// <summary>
    /// Editor builders assign Sprite-Lit-Default on newly created renderers.
    /// Do not re-run the builders; existing scenes stay as serialized.
    /// </summary>
    public static class IsoUrpEditorMaterials
    {
        const string LitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        public static void AssignLit(Renderer renderer)
        {
            if (renderer == null)
                return;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LitPath);
            if (mat != null)
                renderer.sharedMaterial = mat;
        }
    }
}
