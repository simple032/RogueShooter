using UnityEngine;
using UnityEngine.Rendering;

namespace RogueShooter.Iso.Render
{
    /// <summary>
    /// Resources handle so the player build includes the lit sprite material and the
    /// global volume profile without editing scenes.
    /// </summary>
    public sealed class IsoUrpRenderRefs : ScriptableObject
    {
        public Material spriteLit;
        public VolumeProfile volumeProfile;
    }
}
