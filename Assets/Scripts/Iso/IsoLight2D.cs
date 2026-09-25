using UnityEngine;

namespace RogueShooter.Iso
{
    /// <summary>
    /// Screen-space 2D light for the sample room. Upper-left cool key, matching the fixed art light.
    /// </summary>
    [ExecuteAlways]
    public sealed class IsoLight2D : MonoBehaviour
    {
        public Vector3 Direction = new Vector3(-0.55f, 0.75f, 0.37f);
        public Color LightColor = new Color(0.78f, 0.86f, 1f, 1f);
        public Color Ambient = new Color(0.28f, 0.30f, 0.36f, 1f);

        public static IsoLight2D Current { get; private set; }

        void OnEnable()
        {
            Current = this;
        }

        void OnDisable()
        {
            if (Current == this)
                Current = null;
        }
    }
}
