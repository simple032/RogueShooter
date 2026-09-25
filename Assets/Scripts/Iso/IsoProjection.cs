using UnityEngine;
using RogueShooter.Vision;

namespace RogueShooter.Iso
{
    /// <summary>
    /// Logic space stays orthographic: 1 cell = 1 unit. This type is the only
    /// logic ↔ presentation conversion. Nothing in gameplay calls it yet.
    ///
    /// Presentation is 2:1 isometric. A floor tile is 128×64 px at
    /// <see cref="PixelsPerUnit"/> 128, so one cell is 1u wide and 0.5u tall
    /// on screen. That is the same mapping as a Unity Grid with
    /// <c>cellLayout = Isometric</c>, <c>cellSize = (1, 0.5, 1)</c>,
    /// <c>cellGap = 0</c>, <c>cellSwizzle = XYZ</c>:
    /// <code>
    /// CellToLocal(x, y, z) = ((x - y) * 0.5, (x + y) * 0.25, z)
    /// </code>
    /// Checked points (cell origin, not <c>GetCellCenterLocal</c>):
    /// (0,0)→(0,0), (1,0)→(0.5, 0.25), (0,1)→(-0.5, 0.25), (1,1)→(0, 0.5).
    /// +logic X goes screen right-and-up; +logic Y goes screen left-and-up.
    /// This is not <c>IsometricZAsY</c>. Tile sprite pivot / <c>tileAnchor</c>
    /// is a later presentation offset and is not applied here.
    /// A non-zero Grid <c>cellGap</c> is not modeled.
    /// </summary>
    public static class IsoProjection
    {
        /// <summary>Logic width of one cell, in units. Also the Unity cellSize.x.</summary>
        public const float CellW = 1f;

        /// <summary>On-screen height of one floor cell, in units. Also the Unity cellSize.y.</summary>
        public const float CellH = 0.5f;

        /// <summary>
        /// Pixels per unit for the isometric floor tile (128×64 px).
        /// Distinct from the current orthographic character PPU (32).
        /// </summary>
        public const float PixelsPerUnit = 128f;

        /// <summary>
        /// Screen pixels of rise for 1 logic unit of vertical elevation.
        /// Spec: 1u vertical ≈ 78.4px. At <see cref="PixelsPerUnit"/> that is
        /// 78.4/128 = 0.6125 view units, taller than one floor step
        /// (CellH * 0.5 = 0.25u = 32px).
        /// </summary>
        public const float ElevationPixelsPerUnit = 78.4f;

        /// <summary>View-space Y added per 1 logic unit of <c>heightU</c>.</summary>
        public const float ElevationViewPerUnit = ElevationPixelsPerUnit / PixelsPerUnit;

        /// <summary>
        /// Ground-plane cell coordinate → isometric view position.
        /// ((x−y)*CellW*0.5, (x+y)*CellH*0.5) = ((x−y)*0.5, (x+y)*0.25).
        /// </summary>
        public static Vector2 LogicToScreen(Vector2 logic)
        {
            float sx = (logic.x - logic.y) * (CellW * 0.5f);
            float sy = (logic.x + logic.y) * (CellH * 0.5f);
            return new Vector2(sx, sy);
        }

        /// <summary>Exact inverse of <see cref="LogicToScreen"/>. Ground plane only (height 0).</summary>
        public static Vector2 ScreenToLogic(Vector2 screen)
        {
            float hx = CellW * 0.5f;
            float hy = CellH * 0.5f;
            float xMinusY = screen.x / hx;
            float xPlusY = screen.y / hy;
            return new Vector2((xPlusY + xMinusY) * 0.5f, (xPlusY - xMinusY) * 0.5f);
        }

        /// <summary>
        /// logic.xy is the play-plane cell position. logic.z is copied to view z
        /// and is not a height (Unity Isometric, not IsometricZAsY).
        /// <paramref name="heightU"/> is vertical elevation in logic units,
        /// added to view Y via <see cref="ElevationViewPerUnit"/>.
        /// </summary>
        public static Vector3 LogicToView(Vector3 logic, float heightU = 0f)
        {
            Vector2 screen = LogicToScreen(new Vector2(logic.x, logic.y));
            float y = screen.y + heightU * ElevationViewPerUnit;
            return new Vector3(screen.x, y, logic.z);
        }

        /// <summary>
        /// Inverse of <see cref="LogicToView"/> on xy. Subtracts
        /// <paramref name="heightU"/> before <see cref="ScreenToLogic"/>.
        /// view.z is ignored. Pass the same height that was projected.
        /// </summary>
        public static Vector2 ViewToLogic(Vector3 view, float heightU = 0f)
        {
            float y = view.y - heightU * ElevationViewPerUnit;
            return ScreenToLogic(new Vector2(view.x, y));
        }

        /// <summary>
        /// Direction through the same linear map as <see cref="LogicToScreen"/>.
        /// Not normalized. Zero stays zero. Length is not preserved.
        /// </summary>
        public static Vector2 LogicDirToScreenDir(Vector2 logicDir)
        {
            return LogicToScreen(logicDir);
        }

        /// <summary>
        /// Inverse of <see cref="LogicDirToScreenDir"/>. Not normalized.
        /// </summary>
        public static Vector2 ScreenDirToLogicDir(Vector2 screenDir)
        {
            return ScreenToLogic(screenDir);
        }

        /// <summary>
        /// Pixel under an orthographic camera that frames the isometric view
        /// plane → ground-plane logic cell. Composes
        /// <see cref="CameraViewMath.ScreenToWorldOnPlayPlane"/> then
        /// <see cref="ScreenToLogic"/>. A null camera returns logic (0,0).
        /// Elevation is not recovered; this is the height-0 floor hit.
        /// </summary>
        public static Vector2 ScreenPixelToLogic(Camera cam, Vector3 screenPixel)
        {
            Vector3 view = CameraViewMath.ScreenToWorldOnPlayPlane(cam, screenPixel);
            return ScreenToLogic(new Vector2(view.x, view.y));
        }

        /// <summary>
        /// Orthographic view rectangle → 4 logic-space corners (a quad / diamond).
        /// Uses the same axis-aligned rect as <see cref="CameraViewMath"/>
        /// (unrotated camera). Index 0 bottom-left, 1 bottom-right, 2 top-right,
        /// 3 top-left, in view space, then <see cref="ScreenToLogic"/>.
        /// Null, non-orthographic, or a short/null buffer: null/short writes
        /// nothing; a present buffer of length ≥ 4 is cleared to zero when the
        /// camera cannot be used.
        /// </summary>
        public static void ViewQuadInLogic(Camera cam, Vector2[] out4)
        {
            if (out4 == null || out4.Length < 4)
                return;
            if (cam == null || !cam.orthographic)
            {
                ClearQuad(out4);
                return;
            }

            Rect view = CameraViewMath.GetOrthographicWorldRect(
                cam.transform.position,
                cam.orthographicSize,
                CameraViewMath.ResolveAspect(cam));
            ViewQuadInLogic(view, out4);
        }

        /// <summary>
        /// Same corner order as <see cref="ViewQuadInLogic(Camera, Vector2[])"/>
        /// for a view-space rect the caller already has. Short/null buffer: no write.
        /// </summary>
        public static void ViewQuadInLogic(Rect viewRect, Vector2[] out4)
        {
            if (out4 == null || out4.Length < 4)
                return;
            out4[0] = ScreenToLogic(new Vector2(viewRect.xMin, viewRect.yMin));
            out4[1] = ScreenToLogic(new Vector2(viewRect.xMax, viewRect.yMin));
            out4[2] = ScreenToLogic(new Vector2(viewRect.xMax, viewRect.yMax));
            out4[3] = ScreenToLogic(new Vector2(viewRect.xMin, viewRect.yMax));
        }

        /// <summary>
        /// Painter key. Equals logic.x + logic.y, monotonic with view Y
        /// (viewY = SortKey * CellH * 0.5). Larger = higher on screen = farther
        /// from the camera. Draw back-to-front by descending key.
        /// </summary>
        public static float SortKey(Vector2 logic)
        {
            return logic.x + logic.y;
        }

        static void ClearQuad(Vector2[] out4)
        {
            Vector2 z = Vector2.zero;
            out4[0] = z;
            out4[1] = z;
            out4[2] = z;
            out4[3] = z;
        }
    }
}
