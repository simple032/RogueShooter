using RogueShooter.Build;

namespace RogueShooter.Build
{
    public static class RewardScreenChecks
    {
        public static string Run()
        {
            var chest = new RewardScreenSession();
            chest.OpenChest();
            if (!chest.WorldPaused || !chest.ClockPaused)
                return "chest open must pause world and clock";
            if (chest.ChoicesVisible)
                return "chest choices must stay hidden during the clip";
            if (chest.ClipId != "jh_fx_chest_reward_")
                return "chest clip id";
            chest.Tick(0.4f);
            if (chest.ChoicesVisible || chest.AnimFrame < 4)
                return "chest still in clip at 0.4s frame=" + chest.AnimFrame;
            chest.Tick(0.2f);
            if (!chest.ChoicesVisible || !chest.WorldPaused)
                return "chest choices after 6 frames, still paused";

            chest.Close();
            if (chest.Open || chest.WorldPaused || chest.ClockPaused || chest.ChoicesVisible)
                return "chest close resumes";

            var altar = new RewardScreenSession();
            altar.OpenAltar();
            if (altar.ChoicesVisible || altar.ClipId != "jh_fx_altar_reward_")
                return "altar starts on altar clip, choices hidden";
            altar.Tick(RewardScreenSession.FrameCount / RewardScreenSession.FramesPerSecond);
            if (!altar.ChoicesVisible || !altar.ClockPaused)
                return "altar reveals after the clip";
            altar.Close();

            var shop = new RewardScreenSession();
            shop.OpenShop();
            if (!shop.ChoicesVisible || !shop.WorldPaused || !shop.ClockPaused)
                return "shop opens immediately and pauses";
            if (shop.Kind != RewardScreenKind.Shop)
                return "shop kind";
            shop.Close();
            if (shop.WorldPaused)
                return "shop close resumes";

            var shelves = ShopStock.RollShelves(new System.Random(7));
            if (shelves == null || shelves.Length != ShopStock.ShelfCount)
                return "shop shelf count";
            int low = 0, mid = 0, high = 0, heal = 0, elastic = 0;
            for (int i = 0; i < shelves.Length; i++)
            {
                if (shelves[i].IsElastic)
                {
                    elastic++;
                    if (shelves[i].IsHeal || shelves[i].ContentRole == ShopSlotRole.Heal)
                        return "elastic must not roll heal";
                }
                else if (shelves[i].ContentRole == ShopSlotRole.Low) low++;
                else if (shelves[i].ContentRole == ShopSlotRole.Mid) mid++;
                else if (shelves[i].ContentRole == ShopSlotRole.High) high++;
                else if (shelves[i].ContentRole == ShopSlotRole.Heal) heal++;
            }

            if (low != 1 || mid != 1 || high != 1 || heal != 1 || elastic != 2)
                return "shelf mix changed";

            return null;
        }

        public static string FormatPass()
        {
            return "ACCEPTANCE PASS RewardUI §9.1 chest/altar clip→3choice shop6 pause";
        }
    }
}
