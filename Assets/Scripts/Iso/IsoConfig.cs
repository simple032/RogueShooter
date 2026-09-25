namespace RogueShooter.Iso
{
    /// <summary>
    /// Global isometric presentation switch. <see cref="Enabled"/> defaults off
    /// and nothing reads it, so the game stays orthographic. No scene, prefab,
    /// or ScriptableObject is required. A scripting define or a ScriptableObject
    /// may replace <see cref="Enabled"/> later.
    ///
    /// <see cref="IsoOrthoSize"/> is the camera orthographic size for the
    /// isometric presentation (5.25). The current orthographic size 6 stays
    /// owned by CameraViewService.PlayOrthoSize. The gameplay programmer will
    /// turn that const into a property that reads IsoConfig in their own PR.
    /// This type does not reference CameraViewService.
    ///
    /// <see cref="FacingMode"/> is the only 8-direction sector switch.
    /// <see cref="FacingHysteresisDeg"/> is the TBD stickiness placeholder.
    /// </summary>
    public static class IsoConfig
    {
        /// <summary>
        /// When false (the default), presentation stays orthographic.
        /// Assigning this has no effect until a later PR reads it.
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Orthographic size of the isometric camera at 16:9 and wider.
        /// The orthographic (current) size 6 remains CameraViewService.PlayOrthoSize.
        /// </summary>
        public const float IsoOrthoSize = 5.25f;

        /// <summary>Design aspect. Narrower windows keep this horizontal view width.</summary>
        public const float TargetAspect = 16f / 9f;

        /// <summary>
        /// Orthographic size for a window aspect. PR3 sets the camera's
        /// orthographicSize from this. At <see cref="TargetAspect"/> and wider
        /// (21:9) the size stays <see cref="IsoOrthoSize"/>. Narrower aspects
        /// (16:10, 4:3) return IsoOrthoSize * TargetAspect / aspect, which
        /// locks the horizontal view width to the 16:9 width.
        /// IsoProjection.ViewQuadInLogic must then read that camera's actual
        /// orthographicSize and aspect. It must not rebuild the quad from these constants.
        /// </summary>
        public static float OrthoSizeForAspect(float aspect)
        {
            if (aspect >= TargetAspect)
                return IsoOrthoSize;
            return IsoOrthoSize * TargetAspect / aspect;
        }

        /// <summary>
        /// The only sector-mode switch. Default is diamond-aligned
        /// (equal 45° in logic space). Gameplay does not read this yet.
        /// </summary>
        public static SectorMode FacingMode { get; set; } = SectorMode.DiamondAligned;

        /// <summary>
        /// TBD placeholder. Degrees past a sector boundary before
        /// <see cref="IsoFacingTracker"/> switches. Designer tunes this after
        /// the placeholder playtest. Not a locked value.
        /// <see cref="IsoFacing.FromLogic"/> ignores it.
        /// </summary>
        public const float DefaultFacingHysteresisDeg = 4f;

        /// <summary>Starts at <see cref="DefaultFacingHysteresisDeg"/> (TBD, 4°).</summary>
        public static float FacingHysteresisDeg { get; set; } = DefaultFacingHysteresisDeg;
    }
}
