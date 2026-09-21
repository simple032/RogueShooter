using System;
using System.Collections.Generic;
using RogueShooter.Balance;
using RogueShooter.Layout;

namespace RogueShooter.Build
{
    public static class BuildSliceChecks
    {
        public static string Run(BalanceLockData data)
        {
            if (data == null)
                return "lock data null";
            if (Math.Abs(data.pSpawn - 0.9f) > 0.001f)
                return "P_spawn expected 0.90 from LOCK";
            if (Math.Abs(data.powerBuildCoef - 0.45f) > 0.001f || Math.Abs(data.powerRarityCoef - 0.55f) > 0.001f)
                return "Power coefs expected 0.45 / 0.55";
            if (data.shopBuildFromShop != 1)
                return "shop Build-from-shop flag expected 1 (店购计 Build; 制作人 2026-09-20 改口)";
            if (ShopStock.BuildEquivFor(ShopSlotRole.Low) != 1
                || ShopStock.BuildEquivFor(ShopSlotRole.Mid) != 1
                || ShopStock.BuildEquivFor(ShopSlotRole.High) != 1
                || ShopStock.BuildEquivFor(ShopSlotRole.Heal) != 1)
                return "shop BuildEquiv expected +1 per buy (not tiered)";

            var st = new RunBuildState(data.powerBuildCoef, data.powerRarityCoef, 80);
            if (!st.TryShopBuy(20, ShopStock.BuildEquivFor(ShopSlotRole.Low), "R1L"))
                return "shop buy should succeed with start gold";
            if (st.BuildCount != 1)
                return "shop buy must add Build +1";
            if (st.OwnedRewardIds.Count < 1)
                return "shop buy must apply reward";
            if (!st.TryShopBuy(40, ShopStock.BuildEquivFor(ShopSlotRole.High), "R1H") || st.BuildCount != 2)
                return "second shop buy must add +1 (high is not +4)";

            if (ShopStock.ShelfCount != 6)
                return "shop must have 6 shelves";
            if (ShopStock.PriceBaseLow != 20
                || ShopStock.PriceBaseMid != 40
                || ShopStock.PriceBaseHigh != 80
                || ShopStock.PriceBaseHeal != 40)
                return "shop price bases expected 20/40/80/40";
            if (ShopStock.ElasticWLow != 56 || ShopStock.ElasticWMid != 32
                || ShopStock.ElasticWHigh != 12 || ShopStock.ElasticWHeal != 0)
                return "elastic weights expected 56/32/12/0";
            var shelves = ShopStock.RollShelves(new Random(11));
            if (shelves.Length != 6)
                return "shop roll must return 6";
            if (shelves[0].ContentRole != ShopSlotRole.Low || shelves[1].ContentRole != ShopSlotRole.Mid
                || shelves[2].ContentRole != ShopSlotRole.High || shelves[3].ContentRole != ShopSlotRole.Heal)
                return "fixed shelves must be 普/中/高/回血";
            if (!shelves[3].IsHeal)
                return "fixed heal shelf must be heal";
            int low = shelves[0].Price;
            if (low != ShopStock.PriceBaseLow)
                return "low price must be locked 20";
            if (shelves[1].Price != ShopStock.PriceBaseMid
                || shelves[2].Price != ShopStock.PriceBaseHigh
                || shelves[3].Price != ShopStock.PriceBaseHeal)
                return "locked prices must be 20/40/80/40";
            if (shelves[1].Price != low * 2 || shelves[2].Price != low * 4 || shelves[3].Price != low * 2)
                return "price ratio must be 1:2:4 and heal=mid";
            for (int i = 0; i < shelves.Length; i++)
            {
                if (shelves[i].Sold)
                    return "fresh shelves must not be sold";
                if (!ShopStock.PriceMatchesRatio(shelves[i].Price, shelves[i].ContentRole, shelves[i].LowAnchor))
                    return "shelf price must follow low-anchor ratio";
                if (shelves[i].IsElastic && shelves[i].ContentRole == ShopSlotRole.Heal)
                    return "elastic must not roll heal";
            }
            int elasticHeal = 0, elasticLow = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var role = ShopStock.RollElasticRole(new Random(seed));
                if (role == ShopSlotRole.Heal) elasticHeal++;
                if (role == ShopSlotRole.Low) elasticLow++;
            }
            if (elasticHeal != 0 || elasticLow < 80)
                return "elastic sample look wrong low=" + elasticLow + " heal=" + elasticHeal;
            if (RunBuildState.ChestBuildDelta(false) != 1 || RunBuildState.ChestBuildDelta(true) != 2)
                return "chest Build deltas expected small+1 large+2";

            st.Reset(80);
            st.GrantBuildPick("VIT_C", "C", data.ScoreForRarity("C"), RunBuildState.ChestBuildDelta(false));
            int rs = data.ScoreForRarity("C");
            if (st.BuildCount != 1 || st.RarityScore != rs)
                return "small chest confirm should add Build +1";
            st.GrantBuildPick("ATK_C", "C", data.ScoreForRarity("C"), RunBuildState.ChestBuildDelta(true));
            if (st.BuildCount != 3 || st.RarityScore != rs * 2)
                return "large chest confirm should add Build +2 (total 3)";
            float expect = data.powerBuildCoef * 3f + data.powerRarityCoef * (rs * 2);
            if (Math.Abs(st.Power - expect) > 1e-4f)
                return "Power formula mismatch after chest Build deltas";

            var chestIds = new List<string>();
            var altarIds = new List<string>();
            for (int i = 0; i < LockSiteCatalog.Sites.Length; i++)
            {
                SiteDef s = LockSiteCatalog.Sites[i];
                if (s.Kind == SiteKind.Chest)
                    chestIds.Add(s.Id);
                else if (s.Kind == SiteKind.Altar)
                    altarIds.Add(s.Id);
            }

            if (chestIds.Count < 6)
                return "expected Chest_* slots from HOOKS";
            if (altarIds.Count < 3)
                return "expected A_* altars from HOOKS";

            int first = -1;
            bool fluctuated = false;
            for (int seed = 0; seed < 80; seed++)
            {
                var roll = ChestPresenceRoller.RollChests(chestIds, data.pSpawn, new Random(seed));
                int c = ChestPresenceRoller.CountPresent(roll);
                if (first < 0)
                    first = c;
                else if (c != first)
                    fluctuated = true;
            }

            if (!fluctuated)
                return "chest counts did not fluctuate across seeds";

            var offerC = RewardOffer.RollUnique(data, "chest", new Random(3));
            var offerA = RewardOffer.RollUnique(data, "altar", new Random(3));
            if (offerC.Length != 3 || offerA.Length != 3)
                return "offer_count should be 3";
            if (!UniqueIds(offerC) || !UniqueIds(offerA))
                return "offer options must be unique";

            AltarRewardRoll.GetWeights(AltarSize.Small, out int sL, out int sM, out int sH);
            AltarRewardRoll.GetWeights(AltarSize.Mid, out int mL, out int mM, out int mH);
            AltarRewardRoll.GetWeights(AltarSize.Large, out int lL, out int lM, out int lH);
            if (sL != 72 || sM != 28 || sH != 0)
                return "small altar weights expected 72/28/0";
            if (mL != 53 || mM != 32 || mH != 15)
                return "mid altar weights expected 53/32/15";
            if (lL != 16 || lM != 54 || lH != 30)
                return "large altar weights expected 16/54/30";
            if (AltarRewardRoll.BuildDelta(AltarSize.Small) != 1
                || AltarRewardRoll.BuildDelta(AltarSize.Mid) != 2
                || AltarRewardRoll.BuildDelta(AltarSize.Large) != 3)
                return "altar Build deltas expected 1/2/3";

            var altarBuild = new RunBuildState(0.45f, 0.55f, 0);
            if (!altarBuild.ConfirmAltarSize(AltarSize.Small) || altarBuild.BuildCount != 1)
                return "small altar confirm should add Build +1";
            if (altarBuild.ConfirmAltarSize(AltarSize.Small) || altarBuild.BuildCount != 1)
                return "second small altar confirm must not add Build";
            if (!altarBuild.ConfirmAltarSize(AltarSize.Mid) || altarBuild.BuildCount != 3)
                return "mid altar confirm should add Build +2 (total 3)";
            if (!altarBuild.ConfirmAltarSize(AltarSize.Large) || altarBuild.BuildCount != 6)
                return "large altar confirm should add Build +3 (total 6)";

            if (AltarRewardRoll.SizeForHookId("A_Shared") != AltarSize.Small)
                return "A_Shared should be Small";
            if (AltarRewardRoll.SizeForHookId("A1") != AltarSize.Mid)
                return "A1 should be Mid";
            if (AltarRewardRoll.SizeForHookId("A3") != AltarSize.Large)
                return "A3 should be Large";

            var picks = AltarRewardRoll.RollThree(AltarSize.Small, new Random(7));
            if (picks.Length != 3)
                return "small altar should roll 3 picks";
            for (int i = 0; i < picks.Length; i++)
            {
                if (picks[i].Tier == RewardTier.High)
                    return "small altar high weight is 0 — no High tier";
            }

            return null;
        }

        static bool UniqueIds(RewardOption[] opts)
        {
            for (int i = 0; i < opts.Length; i++)
            for (int j = i + 1; j < opts.Length; j++)
            {
                if (opts[i].Id == opts[j].Id)
                    return false;
            }

            return true;
        }
    }
}
