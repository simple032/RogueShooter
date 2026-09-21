using System;
using RogueShooter.Layout;
using RogueShooter.Player;
using RogueShooter.Vision;

namespace RogueShooter.Art
{
    public static class JianHaiArtChecks
    {
        public static string Run()
        {
            if (JianHaiArtCatalog.Ppu != 32)
                return "PPU must be 32";
            if (JianHaiArtCatalog.EntityStubWorldScale != 0.62f)
                return "entity stub world scale must stay 0.62 (play ortho 6)";
            if (Math.Abs(CameraViewService.PlayOrthoSize - 6f) > 0.001f)
                return "play ortho must stay 6 (producer retune)";
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
            if (JianHaiArtCatalog.SpriteName(JianHaiArtCatalog.ChestArtRoot(true), "closed")
                != "jh_prop_chest_large_closed")
                return "large chest map";
            if (JianHaiArtCatalog.SpriteNameForHook("A_Shared", "idle") != "jh_prop_altar_idle")
                return "A_Shared map";
            if (JianHaiArtCatalog.SpriteNameForHook("A2", "active") != "jh_prop_altar_active")
                return "A2 map";
            if (JianHaiArtCatalog.SpriteNameForHook("Shop_01", "") != "jh_prop_shop_01")
                return "Shop_01 map";

            int aw, ah, sw, sh, lw, lh;
            JianHaiArtCatalog.CanvasForArtId("jh_prop_altar_idle", out aw, out ah);
            JianHaiArtCatalog.CanvasForArtId("jh_prop_chest_closed", out sw, out sh);
            JianHaiArtCatalog.CanvasForArtId("jh_prop_chest_large_closed", out lw, out lh);
            if (aw != 96 || ah != 96)
                return "altar canvas 96";
            if (sw != 64 || sh != 64)
                return "small chest canvas 64";
            if (lw != 96 || lh != 96)
                return "large chest canvas 96";
            if (!(lw > sw && lh > sh))
                return "large chest must read bigger than small";
            if (!(aw > sw))
                return "altar 96 must read bigger than small chest 64";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.FxStringGlow) != "FX")
                return "fx folder";
            if (JianHaiArtCatalog.AssetPath(JianHaiArtCatalog.FxStringGlow)
                != "Assets/Art/JianHai/FX/jh_fx_charge_string_glow.png")
                return "fx path";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.ReticleChargeIdle) != "UI")
                return "reticle folder";
            if (ChargeReticle.OuterRadius > 0.36f || ChargeReticle.OuterRadius * 2f > 0.72f + 0.001f)
                return "charge reticle must stay ≤ ~0.72u (fig2 / half-head)";
            if (JianHaiArtCatalog.SortingLayer(JianHaiArtCatalog.FxCritFlash) != JianHaiArtCatalog.LayerFx)
                return "fx layer";
            if (Math.Abs(ChargeFxHooks.ChargeSeconds - 0.70f) > 0.001f)
                return "ring fill must be 0.70s";
            if (Math.Abs(ChargeFxHooks.GreenEnter - 0.68f) > 0.001f
                || Math.Abs(ChargeFxHooks.GreenExit - 0.72f) > 0.001f)
                return "weak-spot window 0.68–0.72s";
            if (Math.Abs(ChargeShotRules.RecoverSeconds - 0.20f) > 0.001f)
                return "shoot recovery must be 0.20s";
            if (Math.Abs(ChargeShotRules.WeakSpotStaggerSeconds - 0.50f) > 0.001f)
                return "weak-spot stagger must be 0.50s";

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
