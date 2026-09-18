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
            if (data.shopBuildFromShop != 0 || data.shopInPower != 0)
                return "shop must not enter Build/Power";

            var st = new RunBuildState(data.powerBuildCoef, data.powerRarityCoef, 80);
            if (!st.TryShopBuy(data.shopStubPrice > 0 ? data.shopStubPrice : 25))
                return "shop buy should succeed with start gold";
            if (st.BuildCount != 0 || st.RarityScore != 0 || Math.Abs(st.Power) > 1e-6f)
                return "shop buy mutated Build";
            st.GrantBuildPick("VIT_C", "C", data.ScoreForRarity("C"));
            int rs = data.ScoreForRarity("C");
            if (st.BuildCount != 1 || st.RarityScore != rs)
                return "chest/altar pick did not increment Build";
            float expect = data.powerBuildCoef * 1f + data.powerRarityCoef * rs;
            if (Math.Abs(st.Power - expect) > 1e-4f)
                return "Power formula mismatch";

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
