namespace RogueShooter.Iso
{
    /// <summary>
    /// Global isometric presentation switch. Nothing reads this yet, so leaving
    /// it off changes no behavior. No scene, prefab, or ScriptableObject is required.
    /// A scripting define or a ScriptableObject may replace <see cref="Enabled"/>
    /// later; until that lands, the in-memory default stays off.
    ///
    /// <see cref="IsoOrthoSize"/> is the camera orthographic size for the
    /// isometric presentation (5.25). The current orthographic size 6 stays
    /// owned by CameraViewService.PlayOrthoSize. The gameplay programmer will
    /// turn that const into a property that reads IsoConfig in their own PR.
    /// This type does not reference CameraViewService.
    /// </summary>
    public static class IsoConfig
    {
        /// <summary>
        /// When false (the default), presentation stays orthographic.
        /// Assigning this has no effect until a later PR reads it.
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Orthographic size of the isometric camera.
        /// The orthographic (current) size 6 remains CameraViewService.PlayOrthoSize.
        /// </summary>
        public const float IsoOrthoSize = 5.25f;
    }
}
