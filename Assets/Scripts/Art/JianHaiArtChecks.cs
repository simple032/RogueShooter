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
            if (!L5VisionChecks.OrthoMatchesMode(CameraViewService.PlayOrthoSize))
                return "play ortho must be 6 ortho / 5.25 iso (CameraViewService.PlayOrthoSize)";
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
            if (JianHaiArtCatalog.PlayerIdle != "jh_char_archer_idle_00")
                return "player idle must be framed _00";
            if (JianHaiArtCatalog.SpriteNameForHook("START", "idle") != JianHaiArtCatalog.PlayerIdle)
                return "START idle frame";
            if (JianHaiArtCatalog.AssetPath(JianHaiArtCatalog.PlayerIdle)
                != "Assets/Art/JianHai/Characters/jh_char_archer_idle_00.png")
                return "player idle path";
            if (JianHaiArtCatalog.EnemyE1Idle != "jh_enemy_e1_skel_idle_00")
                return "skel idle must be framed _00 (bare idle is a solid placeholder)";
            if (JianHaiArtCatalog.EnemyDogIdle != "jh_enemy_dog_idle_00")
                return "dog idle";
            if (JianHaiArtCatalog.EnemyMageIdle != "jh_enemy_mage_idle_00")
                return "mage idle";
            if (JianHaiArtCatalog.SpriteName("jh_enemy_dog", "idle") != JianHaiArtCatalog.EnemyDogIdle)
                return "dog idle frame";
            if (JianHaiArtCatalog.SpriteName("jh_enemy_mage", "idle") != JianHaiArtCatalog.EnemyMageIdle)
                return "mage idle frame";
            if (JianHaiArtCatalog.SpriteName("jh_enemy_e1_skel", "idle") != JianHaiArtCatalog.EnemyE1Idle)
                return "skel idle frame";
            if (LegacyS1Enemy(JianHaiArtCatalog.EnemyE1Idle)
                || LegacyS1Enemy(JianHaiArtCatalog.EnemyDogIdle)
                || LegacyS1Enemy(JianHaiArtCatalog.EnemyMageIdle))
                return "legacy s1 enemy id";
            string tileIds = JianHaiStage1Art.ValidateIds();
            if (tileIds != null)
                return tileIds;
            if (JianHaiStage1Art.RejectReason(_ => false, _ => true) == null)
                return "missing png must abort";
            if (JianHaiStage1Art.RejectReason(_ => true, _ => false) == null)
                return "unloaded sprite must abort";
            if (JianHaiStage1Art.RejectReason(_ => true, _ => true) != null)
                return "complete tiles must pass";
            string partial = JianHaiStage1Art.RejectReason(
                id => id != "jh_decal_s1_rubble_01",
                _ => true);
            if (partial == null || partial.IndexOf("jh_decal_s1_rubble_01", StringComparison.Ordinal) < 0)
                return "single missing tile must name the png";
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
            if (Math.Abs(ChargeFxHooks.ChargeSeconds - ChargeShotRules.RingFillSeconds) > 0.001f)
                return "ring fill must follow ChargeShotRules.RingFillSeconds";
            if (Math.Abs(ChargeFxHooks.GreenEnter - ChargeShotRules.RingFillSeconds * ChargeShotRules.WeakSpotEnterPct) > 0.001f
                || Math.Abs(ChargeFxHooks.GreenExit - ChargeShotRules.RingFillSeconds * ChargeShotRules.WeakSpotExitPct) > 0.001f)
                return "weak-spot window must be WeakSpotEnterPct–ExitPct × full charge";
            if (Math.Abs(ChargeShotRules.RecoverSeconds - 0.20f) > 0.001f)
                return "shoot recovery must be 0.20s";
            if (Math.Abs(ChargeShotRules.WeakSpotStaggerSeconds - 0.50f) > 0.001f)
                return "weak-spot stagger must be 0.50s";
            if (JianHaiArtCatalog.ArrowFlight != "jh_proj_arrow_fly")
                return "arrow flight PHASE1 jh_proj_arrow_fly";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.ArrowFlight) != "Projectiles")
                return "arrow projectile folder";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.OrbFlight) != "Projectiles")
                return "mage orb projectile folder";
            if (JianHaiArtCatalog.SortingLayer(JianHaiArtCatalog.OrbFlight) != JianHaiArtCatalog.LayerFx)
                return "mage orb fx layer";
            if (JianHaiArtCatalog.AssetPath(JianHaiArtCatalog.OrbFlight)
                != "Assets/Art/JianHai/Projectiles/jh_proj_orb_mage_fly.png")
                return "mage orb path";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.TileFloorCorridor) != "Tiles")
                return "floor tile folder";
            if (JianHaiArtCatalog.FolderForArtId(JianHaiArtCatalog.WallStone) != "Tiles")
                return "wall folder";
            if (JianHaiArtCatalog.SortingLayer(JianHaiArtCatalog.TileFloorCorridor) != JianHaiArtCatalog.LayerGround)
                return "floor sorting";
            // Arrow / orb projectile PNGs are not on main (placeholder until art lands).
            if (!JianHaiSprites.HasSourceFile(JianHaiArtCatalog.PlayerIdle)
                || !JianHaiSprites.HasSourceFile(JianHaiArtCatalog.TileFloorCorridor)
                || !JianHaiSprites.HasSourceFile(JianHaiArtCatalog.WallStone))
                return "Provide JianHai PNGs must exist on disk";
            if (Math.Abs(JianHaiArtCatalog.PivotS1.y - 0.15f) > 0.001f
                || JianHaiArtCatalog.Ppu != 32)
                return "PHASE1 PPU32 / S1 pivot 0.15";

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

        static bool LegacyS1Enemy(string artId)
        {
            return !string.IsNullOrEmpty(artId)
                && artId.StartsWith("jh_enemy_", StringComparison.Ordinal)
                && artId.IndexOf("_s1_", StringComparison.Ordinal) >= 0;
        }
    }
}
