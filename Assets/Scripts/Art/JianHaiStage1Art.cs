using System;
using System.Collections.Generic;

namespace RogueShooter.Art
{
    /// <summary>
    /// Stage-1 tile/wall/decal ids (26) and the missing-art gate.
    /// A non-null <see cref="RejectReason"/> means abort: do not SaveScene and do not write tile assets.
    /// Once each PNG exists at Assets/Art/JianHai/Tiles/&lt;id&gt;.png and imports as a sprite,
    /// the editor builder may create or update Tiles/&lt;id&gt;.asset with m_Sprite rebound by filename.
    /// </summary>
    public static class JianHaiStage1Art
    {
        public const int ExpectedTileCount = 26;
        public const string TilesFolder = "Assets/Art/JianHai/Tiles/";

        public static readonly string[] TileIds =
        {
            "jh_tile_floor_s1_room_00",
            "jh_tile_floor_s1_room_01",
            "jh_tile_floor_s1_room_02",
            "jh_tile_floor_s1_chest_00",
            "jh_tile_floor_s1_chest_01",
            "jh_tile_floor_s1_chest_02",
            "jh_tile_floor_s1_altar_00",
            "jh_tile_floor_s1_altar_01",
            "jh_tile_floor_s1_altar_02",
            "jh_tile_floor_s1_corridor_00",
            "jh_tile_floor_s1_corridor_01",
            "jh_tile_floor_s1_corridor_02",
            "jh_wall_s1_stone_face",
            "jh_wall_s1_stone_top",
            "jh_wall_s1_stone_side_e",
            "jh_wall_s1_stone_side_w",
            "jh_wall_s1_stone_corner_ne",
            "jh_wall_s1_stone_corner_nw",
            "jh_wall_s1_stone_corner_se",
            "jh_wall_s1_stone_corner_sw",
            "jh_wall_s1_stone_opening_n",
            "jh_wall_s1_stone_opening_s",
            "jh_wall_s1_stone_opening_e",
            "jh_wall_s1_stone_opening_w",
            "jh_decal_s1_rubble_00",
            "jh_decal_s1_rubble_01",
        };

        public static string PngPath(string id)
        {
            return TilesFolder + id + ".png";
        }

        public static string ValidateIds()
        {
            if (TileIds == null || TileIds.Length != ExpectedTileCount)
                return "stage1 tile id count != " + ExpectedTileCount;
            var seen = new HashSet<string>();
            for (int i = 0; i < TileIds.Length; i++)
            {
                string id = TileIds[i];
                if (string.IsNullOrEmpty(id))
                    return "stage1 tile id empty";
                if (!seen.Add(id))
                    return "duplicate stage1 tile id " + id;
            }
            return null;
        }

        /// <summary>
        /// Null only when every id has a PNG on disk and a loadable sprite.
        /// Otherwise a reason. Callers must abort and must not SaveScene.
        /// </summary>
        public static string RejectReason(Func<string, bool> pngExists, Func<string, bool> spriteLoaded)
        {
            string ids = ValidateIds();
            if (ids != null)
                return ids;
            if (pngExists == null || spriteLoaded == null)
                return "stage1 tile probe missing";
            for (int i = 0; i < TileIds.Length; i++)
            {
                string id = TileIds[i];
                if (!pngExists(id))
                    return "missing png " + PngPath(id);
                if (!spriteLoaded(id))
                    return "sprite not loadable " + PngPath(id);
            }
            return null;
        }
    }
}
