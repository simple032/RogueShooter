using System;
using RogueShooter.Layout;

namespace RogueShooter.Art
{
    public static class JianHaiArtChecks
    {
        public static string Run()
        {
            if (JianHaiArtCatalog.Ppu != 32)
                return "PPU must be 32";
            if (JianHaiArtCatalog.EntityStubWorldScale != 0.5f)
                return "entity stub world scale must stay 0.5 (ortho stays 2.5)";
            if (JianHaiArtCatalog.StubWorldScale(JianHaiArtCatalog.PlayerIdle)
                != JianHaiArtCatalog.EntityStubWorldScale)
                return "player stub scale";
            if (JianHaiArtCatalog.StubWorldScale(JianHaiArtCatalog.EnemyE1Idle)
                != JianHaiArtCatalog.EntityStubWorldScale)
                return "enemy stub scale";
            if (JianHaiArtCatalog.StubWorldScale(JianHaiArtCatalog.BossIdle)
                != JianHaiArtCatalog.EntityStubWorldScale)
                return "boss stub scale";
            if (JianHaiArtCatalog.StubWorldScale(JianHaiArtCatalog.SpriteNameForHook("Chest_01", "closed"))
                != JianHaiArtCatalog.PropStubWorldScale)
                return "chest stub scale";
            if (JianHaiArtCatalog.SpriteNameForHook("Chest_01", "closed") != "jh_prop_chest_closed")
                return "Chest_* map";
            if (JianHaiArtCatalog.SpriteNameForHook("Chest_12", "open") != "jh_prop_chest_open")
                return "Chest_12 map";
            if (JianHaiArtCatalog.SpriteNameForHook("A_Shared", "idle") != "jh_prop_altar_idle")
                return "A_Shared map";
            if (JianHaiArtCatalog.SpriteNameForHook("A2", "active") != "jh_prop_altar_active")
                return "A2 map";
            if (JianHaiArtCatalog.SpriteNameForHook("Shop_01", "") != "jh_prop_shop_01")
                return "Shop_01 map";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.FxStringGlow) != "FX")
                return "fx folder";
            if (JianHaiArtCatalog.AssetPath(JianHaiArtCatalog.FxStringGlow)
                != "Assets/Art/JianHai/FX/jh_fx_charge_string_glow.png")
                return "fx path";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.ReticleChargeIdle) != "UI")
                return "reticle folder";
            if (JianHaiArtCatalog.SortingLayer(JianHaiArtCatalog.FxCritFlash) != JianHaiArtCatalog.LayerFx)
                return "fx layer";
            if (ChargeFxHooks.ChargeSeconds != 0.90f)
                return "Spec §7.5 charge 0.90s";
            if (ChargeFxHooks.GreenEnter != 0.72f || ChargeFxHooks.GreenExit != 0.84f)
                return "Spec §7.5 green 72–84%";

            for (int i = 0; i < LockSiteCatalog.Sites.Length; i++)
            {
                SiteDef s = LockSiteCatalog.Sites[i];
                if (s.Kind == SiteKind.Chest)
                {
                    if (JianHaiArtCatalog.ArtRootForHook(s.Id) != JianHaiArtCatalog.ChestRoot)
                        return "chest hook " + s.Id;
                    if (!s.Id.StartsWith("Chest_"))
                        return "interact id rewritten " + s.Id;
                }
                else if (s.Kind == SiteKind.Altar)
                {
                    if (JianHaiArtCatalog.ArtRootForHook(s.Id) != JianHaiArtCatalog.AltarRoot)
                        return "altar hook " + s.Id;
                }
                else if (s.Kind == SiteKind.Shop)
                {
                    if (JianHaiArtCatalog.ArtRootForHook(s.Id) != JianHaiArtCatalog.ShopRoot)
                        return "shop hook " + s.Id;
                }
            }

            return null;
        }
    }
}
