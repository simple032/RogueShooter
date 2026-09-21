using System;
using RogueShooter.Layout;

namespace RogueShooter.Art
{
    /// <summary>
    /// STYLE_SPEC_v01 import contract. Hook IDs stay Chest_01 / A_Shared / Shop_01;
    /// sprites resolve through art roots so PNG drop-in does not rewrite interact IDs.
    /// </summary>
    public static class JianHaiArtCatalog
    {
        public const int Ppu = 32;
        public const string Filter = "Point";
        public const string RootFolder = "Assets/Art/JianHai/";
        public const string Naming = "jh_<cat>_<name>[_action][_dir][_frame]";

        /// <summary>
        /// Play Mode localScale vs imported PPU=32 canvas (1.0 = full canvas).
        /// Shrinks player/E1/BOSS occupancy at play ortho 4. Keep this shrink;
        /// do not raise ortho further to fake occupancy.
        /// </summary>
        public const float EntityStubWorldScale = 0.62f;

        /// <summary>Chest / altar / shop: slight shrink, secondary to entities.</summary>
        public const float PropStubWorldScale = 0.75f;

        public const string LayerGround = "Ground";
        public const string LayerDecal = "Decal";
        public const string LayerProp = "Prop";
        public const string LayerEntity = "Entity";
        public const string LayerFx = "FX";
        public const string LayerUi = "UI";

        public const string ChestRoot = "jh_prop_chest";
        public const string ChestLargeRoot = "jh_prop_chest_large";
        public const string AltarRoot = "jh_prop_altar";
        public const string ShopRoot = "jh_prop_shop_01";
        public const string PlayerIdle = "jh_char_archer_idle";
        public const string EnemyE1Idle = "jh_enemy_e1_skel_idle";
        public const string BossIdle = "jh_boss_lord_idle";

        public const string FxStringGlow = "jh_fx_charge_string_glow";
        public const string FxStringPulse = "jh_fx_charge_string_glow_pulse";
        public const string FxStringCold = "jh_fx_charge_string_glow_cold";
        public const string FxBowEdge = "jh_fx_charge_bow_edge";
        public const string FxTipWarm = "jh_fx_charge_arrow_tip";
        public const string FxTipIdle = "jh_fx_charge_arrow_tip_idle";
        public const string FxCritFlash = "jh_fx_crit_flash";
        public const string ReticleChargeIdle = "jh_ui_reticle_charge_idle";
        public const string ReticleChargeGreen = "jh_ui_reticle_charge_green";

        public static readonly Vector2Like PivotPlayer = new Vector2Like(0.5f, 0.15f);
        public static readonly Vector2Like PivotBoss = new Vector2Like(0.5f, 0.12f);
        public static readonly Vector2Like PivotProp = new Vector2Like(0.5f, 0.2f);
        public static readonly Vector2Like PivotTile = new Vector2Like(0.5f, 0.5f);
        public static readonly Vector2Like PivotWall = new Vector2Like(0.5f, 0.0f);

        public struct Vector2Like
        {
            public float x;
            public float y;
            public Vector2Like(float x, float y) { this.x = x; this.y = y; }
        }

        public static bool TryArtRoot(string hookId, out string artRoot)
        {
            artRoot = ArtRootForHook(hookId);
            return !string.IsNullOrEmpty(artRoot);
        }

        public static string ChestArtRoot(bool large)
        {
            return large ? ChestLargeRoot : ChestRoot;
        }

        public static string ArtRootForHook(string hookId)
        {
            if (string.IsNullOrEmpty(hookId))
                return "";
            if (hookId.StartsWith("Chest_", StringComparison.Ordinal))
                return ChestRoot;
            if (hookId == "A_Shared" || IsAltarId(hookId))
                return AltarRoot;
            if (hookId == "Shop_01")
                return ShopRoot;
            if (hookId == "BOSS")
                return "jh_boss_lord";
            if (hookId == "START")
                return "jh_char_archer";
            return "";
        }

        public static string SpriteName(string artRoot, string state)
        {
            if (string.IsNullOrEmpty(artRoot))
                return "";
            if (artRoot == ShopRoot)
                return ShopRoot;
            if (string.IsNullOrEmpty(state))
                return artRoot;
            return artRoot + "_" + state;
        }

        public static string SpriteNameForHook(string hookId, string state)
        {
            return SpriteName(ArtRootForHook(hookId), state);
        }

        public static string FolderForArtId(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return "Props";
            if (artId.StartsWith("jh_char_", StringComparison.Ordinal))
                return "Characters";
            if (artId.StartsWith("jh_enemy_", StringComparison.Ordinal))
                return "Enemies";
            if (artId.StartsWith("jh_boss_", StringComparison.Ordinal))
                return "Boss";
            if (artId.StartsWith("jh_prop_", StringComparison.Ordinal))
                return "Props";
            if (artId.StartsWith("jh_fx_", StringComparison.Ordinal))
                return "FX";
            if (artId.StartsWith("jh_ui_", StringComparison.Ordinal))
                return "UI";
            return "Tiles";
        }

        public static string AssetPath(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return "";
            return RootFolder + FolderForArtId(artId) + "/" + artId + ".png";
        }

        public static float StubWorldScale(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return PropStubWorldScale;
            if (artId.StartsWith("jh_char_", StringComparison.Ordinal)
                || artId.StartsWith("jh_enemy_", StringComparison.Ordinal)
                || artId.StartsWith("jh_boss_", StringComparison.Ordinal))
                return EntityStubWorldScale;
            if (artId.StartsWith("jh_prop_", StringComparison.Ordinal))
                return PropStubWorldScale;
            return 1f;
        }

        public static string SortingLayer(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return LayerProp;
            if (artId.StartsWith("jh_char_", StringComparison.Ordinal)
                || artId.StartsWith("jh_enemy_", StringComparison.Ordinal)
                || artId.StartsWith("jh_boss_", StringComparison.Ordinal))
                return LayerEntity;
            if (artId.StartsWith("jh_prop_", StringComparison.Ordinal))
                return LayerProp;
            if (artId.StartsWith("jh_fx_", StringComparison.Ordinal))
                return LayerFx;
            if (artId.StartsWith("jh_ui_", StringComparison.Ordinal))
                return LayerUi;
            if (artId.StartsWith("jh_tile_", StringComparison.Ordinal))
                return LayerGround;
            if (artId.StartsWith("jh_wall_", StringComparison.Ordinal))
                return LayerProp;
            return LayerDecal;
        }

        public static Vector2Like PivotForArtId(string artId)
        {
            if (string.IsNullOrEmpty(artId))
                return PivotProp;
            if (artId.StartsWith("jh_boss_", StringComparison.Ordinal))
                return PivotBoss;
            if (artId.StartsWith("jh_fx_", StringComparison.Ordinal))
                return PivotTile;
            if (artId.StartsWith("jh_char_", StringComparison.Ordinal)
                || artId.StartsWith("jh_enemy_", StringComparison.Ordinal))
                return PivotPlayer;
            if (artId.StartsWith("jh_wall_", StringComparison.Ordinal))
                return PivotWall;
            if (artId.StartsWith("jh_tile_", StringComparison.Ordinal)
                || artId.StartsWith("jh_ui_", StringComparison.Ordinal))
                return PivotTile;
            return PivotProp;
        }

        public static void CanvasForArtId(string artId, out int width, out int height)
        {
            width = 64;
            height = 64;
            if (string.IsNullOrEmpty(artId))
                return;
            if (artId.StartsWith("jh_boss_", StringComparison.Ordinal))
            {
                width = 128;
                height = 128;
            }
            else if (artId.StartsWith(AltarRoot, StringComparison.Ordinal)
                     || artId.StartsWith(ChestLargeRoot, StringComparison.Ordinal)
                     || artId == ShopRoot || artId.StartsWith(ShopRoot + "_", StringComparison.Ordinal))
            {
                width = 96;
                height = 96;
            }
            else if (artId.StartsWith(ChestRoot, StringComparison.Ordinal))
            {
                width = 64;
                height = 64;
            }
            else if (artId.StartsWith("jh_tile_", StringComparison.Ordinal))
            {
                width = 32;
                height = 32;
            }
            else if (artId.StartsWith("jh_wall_", StringComparison.Ordinal))
            {
                width = 32;
                height = 64;
            }
            else if (artId.StartsWith("jh_fx_charge_bow_edge", StringComparison.Ordinal))
            {
                width = 32;
                height = 8;
            }
            else if (artId.StartsWith("jh_fx_charge_string_glow_cold", StringComparison.Ordinal))
            {
                width = 12;
                height = 12;
            }
            else if (artId.StartsWith("jh_fx_crit_flash", StringComparison.Ordinal))
            {
                width = 32;
                height = 32;
            }
            else if (artId.StartsWith("jh_fx_", StringComparison.Ordinal))
            {
                width = 16;
                height = 16;
            }
            else if (artId.StartsWith("jh_ui_bar_", StringComparison.Ordinal))
            {
                width = 128;
                height = 16;
            }
            else if (artId.StartsWith("jh_ui_reticle_charge", StringComparison.Ordinal)
                     || artId.StartsWith("jh_ui_badge_", StringComparison.Ordinal))
            {
                width = 48;
                height = 48;
            }
            else if (artId.StartsWith("jh_ui_reticle_", StringComparison.Ordinal))
            {
                width = 12;
                height = 12;
            }
            else if (artId.StartsWith("jh_ui_", StringComparison.Ordinal))
            {
                width = 32;
                height = 32;
            }
        }

        public static string DefaultState(SiteKind kind)
        {
            switch (kind)
            {
                case SiteKind.Chest: return "closed";
                case SiteKind.Altar: return "idle";
                default: return "";
            }
        }

        static bool IsAltarId(string hookId)
        {
            if (hookId.Length < 2 || hookId[0] != 'A')
                return false;
            if (hookId[1] == '_')
                return true;
            return hookId.Length <= 3 && char.IsDigit(hookId[1]);
        }
    }
}
