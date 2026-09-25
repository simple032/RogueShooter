using System;

namespace RogueShooter.Build
{
    public static class RewardCatalogChecks
    {
        public static string Run()
        {
            RewardCatalog.BindPoolOwned(null);
            RewardStatHooks.ResetRoomEnter();
            if (RewardCatalog.All.Length < 22)
                return "catalog rows";

            string[] deleted = RewardCatalog.DeletedHighIds;
            for (int i = 0; i < deleted.Length; i++)
            {
                if (RewardCatalog.TryGet(deleted[i], out _))
                    return "deleted H still in catalog " + deleted[i];
            }

            if (!RewardCatalog.TryGet("R11L", out var r11l) || Math.Abs(r11l.Value - 0.20f) > 0.001f
                || r11l.Stat != "dmg_vs_fullhp" || r11l.Tier != RewardTier.Low)
                return "R11L dmg_vs_fullhp";
            if (!RewardCatalog.TryGet("R11M", out var r11m) || Math.Abs(r11m.Value - 0.40f) > 0.001f
                || r11m.Tier != RewardTier.Mid)
                return "R11M dmg_vs_fullhp";
            if (!RewardCatalog.TryGet("R12M", out var r12m) || Math.Abs(r12m.Value - 0.30f) > 0.001f
                || r12m.Stat != "atk_enter_room_5s" || r12m.Tier != RewardTier.Mid)
                return "R12M atk_enter_room_5s";
            if (!RewardCatalog.TryGet("R12H", out var r12h) || Math.Abs(r12h.Value - 0.45f) > 0.001f
                || r12h.Tier != RewardTier.High)
                return "R12H atk_enter_room_5s";
            if (!RewardCatalog.TryGet("R13H", out var r13h) || Math.Abs(r13h.Value - 0.15f) > 0.001f
                || r13h.Stat != "lifesteal" || r13h.Tier != RewardTier.High)
                return "R13H lifesteal";
            if (!RewardCatalog.TryGet("R14H", out var r14h) || Math.Abs(r14h.Value - 0.40f) > 0.001f
                || r14h.Stat != "pierce_back" || r14h.ValueType != "percent_add"
                || r14h.Tier != RewardTier.High)
                return "R14H pierce_back";

            if (!RewardCatalog.TryGet("R1L", out var aL) || Math.Abs(aL.Value - 0.10f) > 0.001f)
                return "attack low 10%";
            if (!RewardCatalog.TryGet("R1M", out var aM) || Math.Abs(aM.Value - 0.20f) > 0.001f)
                return "attack mid 20%";
            if (aL.Tier != RewardTier.Low || aM.Tier != RewardTier.Mid)
                return "attack tier map";

            if (!RewardCatalog.TryGet("R2L", out var r2l) || Math.Abs(r2l.Value - -0.0909f) > 0.001f)
                return "R2L charge_time";
            if (!RewardCatalog.TryGet("R2M", out var r2m) || Math.Abs(r2m.Value - -0.1667f) > 0.001f)
                return "R2M charge_time";

            if (!RewardCatalog.TryGet("R3", out var r3) || r3.Tier != RewardTier.High)
                return "R3 鸿运 must high";
            if (r3.Stat != "crit_window" || Math.Abs(r3.Value - 0.06f) > 0.001f)
                return "R3 crit_window +0.06s";
            if (r3.ShopPrice != 60)
                return "R3 shop mid of 54-66";
            if (!RewardCatalog.TryGet("R10", out var r10) || r10.Tier != RewardTier.Mid)
                return "R10 止血 must mid";
            if (Math.Abs(r10.Value - 0.40f) > 0.001f)
                return "R10 heal 40%";
            if (!RewardCatalog.TryGet("R7M", out var r7m) || Math.Abs(r7m.Value - 0.26f) > 0.001f)
                return "R7M crit_rate";
            if (!RewardCatalog.TryGet("R7H", out var r7h) || Math.Abs(r7h.Value - 0.39f) > 0.001f)
                return "R7H crit_rate";
            if (!RewardCatalog.TryGet("R8H", out var r8) || Math.Abs(r8.Value - 0.65f) > 0.001f)
                return "R8H crit_damage";
            if (!RewardCatalog.TryGet("R9H", out var r9) || Math.Abs(r9.Value - 0.65f) > 0.001f)
                return "R9H weak_damage";
            if (!RewardCatalog.TryGet("R4L", out var r4l) || Math.Abs(r4l.Value - 0.10f) > 0.001f
                || !RewardCatalog.TryGet("R4M", out var r4m) || Math.Abs(r4m.Value - 0.20f) > 0.001f)
                return "R4 骨甲 L/M 0.10/0.20";
            if (!RewardCatalog.TryGet("R5L", out var r5l) || Math.Abs(r5l.Value - 0.10f) > 0.001f
                || !RewardCatalog.TryGet("R5M", out var r5m) || Math.Abs(r5m.Value - 0.20f) > 0.001f)
                return "R5 残影 L/M 0.10/0.20";
            if (!RewardCatalog.TryGet("R6L", out var r6l) || Math.Abs(r6l.Value - 0.10f) > 0.001f
                || !RewardCatalog.TryGet("R6M", out var r6m) || Math.Abs(r6m.Value - 0.20f) > 0.001f)
                return "R6 盗墓者 L/M 0.10/0.20";

            if (RewardCatalog.CountInTier(RewardTier.High) != RewardCatalog.HighPoolIds.Length)
                return "high pool size";

            var allowedHigh = new System.Collections.Generic.HashSet<string>(RewardCatalog.HighPoolIds);
            RewardCatalog.BindPoolOwned(null);
            var highRng = new Random(123);
            bool sawR12H = false, sawR13H = false, sawR14H = false;
            for (int t = 0; t < 500; t++)
            {
                if (!RewardCatalog.TryPickEqual(RewardTier.High, null, highRng, out var hi))
                    return "high pick failed";
                if (!allowedHigh.Contains(hi.Id) || RewardCatalog.IsDeletedHighId(hi.Id))
                    return "high pick outside lock " + hi.Id;
                if (hi.Id == "R12H") sawR12H = true;
                if (hi.Id == "R13H") sawR13H = true;
                if (hi.Id == "R14H") sawR14H = true;
            }

            if (!sawR12H || !sawR13H || !sawR14H)
                return "high sample missing R12H/R13H/R14H";

            var ids = new System.Collections.Generic.List<string> { "R11L" };
            if (Math.Abs(RewardStatHooks.DmgVsFullHpMul(ids, true) - 1.20f) > 0.001f
                || Math.Abs(RewardStatHooks.DmgVsFullHpMul(ids, false) - 1f) > 0.001f)
                return "R11L full-hp mul";
            ids[0] = "R11M";
            if (Math.Abs(RewardStatHooks.DmgVsFullHpMul(ids, true) - 1.40f) > 0.001f)
                return "R11M full-hp mul";

            RewardStatHooks.ResetRoomEnter();
            ids[0] = "R12H";
            if (Math.Abs(RewardStatHooks.EnterRoomAtkMul(ids, 0f) - 1f) > 0.001f)
                return "R12H atk idle";
            RewardStatHooks.NotifyRoomEntered(0f);
            if (Math.Abs(RewardStatHooks.EnterRoomAtkMul(ids, 1f) - 1.45f) > 0.001f)
                return "R12H atk in 5s window";
            if (Math.Abs(RewardStatHooks.EnterRoomAtkMul(ids, 5.01f) - 1f) > 0.001f)
                return "R12H atk after 5s";
            ids[0] = "R12M";
            RewardStatHooks.NotifyRoomEntered(10f);
            if (Math.Abs(RewardStatHooks.EnterRoomAtkMul(ids, 12f) - 1.30f) > 0.001f)
                return "R12M atk in 5s window";

            ids[0] = "R13H";
            if (Math.Abs(RewardStatHooks.LifestealFraction(ids) - 0.15f) > 0.001f
                || Math.Abs(RewardStatHooks.LifestealHeal(ids, 100f) - 15f) > 0.001f)
                return "R13H lifesteal 15%";

            ids.Clear();
            ids.Add("R14H");
            ids.Add("R14H");
            if (Math.Abs(RewardStatHooks.PierceBackAdd(ids) - 0.80f) > 0.001f
                || RewardStatHooks.PierceBackSaturated(ids))
                return "R14H two stacks still in pool";
            RewardCatalog.BindPoolOwned(ids);
            bool stillSawR14 = false;
            for (int t = 0; t < 120; t++)
            {
                if (!RewardCatalog.TryPickEqual(RewardTier.High, null, new Random(t + 3), out var hi))
                    return "high pick at 0.80 pierce failed";
                if (hi.Id == "R14H")
                    stillSawR14 = true;
            }

            if (!stillSawR14)
                return "R14H should remain in pool at Σ=0.80";
            ids.Add("R14H");
            if (!RewardStatHooks.PierceBackSaturated(ids)
                || Math.Abs(RewardStatHooks.PierceBackAdd(ids) - 1.20f) > 0.001f)
                return "R14H three stacks cap";
            RewardCatalog.BindPoolOwned(ids);
            for (int t = 0; t < 200; t++)
            {
                if (!RewardCatalog.TryPickEqual(RewardTier.High, null, new Random(t + 11), out var hi))
                    return "high pick at pierce cap failed";
                if (hi.Id == "R14H" || RewardCatalog.IsDeletedHighId(hi.Id)
                    || !allowedHigh.Contains(hi.Id))
                    return "R14H must leave pool at Σ≥100% got " + hi.Id;
            }

            RewardCatalog.BindPoolOwned(null);
            RewardStatHooks.ResetRoomEnter();

            if (!RewardCatalog.TryGet("R15_C", out var r15c)
                || Math.Abs(r15c.Value - 0.20f) > 0.001f
                || r15c.Tier != RewardTier.Low
                || r15c.BuildEquiv != 1
                || r15c.Stat != "kb_dist_pct")
                return "R15_C 震矢 +20% Build+1";
            if (!RewardCatalog.TryGet("R15_R", out var r15r)
                || Math.Abs(r15r.Value - 0.40f) > 0.001f
                || r15r.Tier != RewardTier.Mid
                || r15r.BuildEquiv != 2
                || r15r.Stat != "kb_dist_pct")
                return "R15_R 震矢 +40% Build+2";
            if (RewardCatalog.TryGet("R15_H", out _))
                return "震矢 must have no high tier";

            float dps0 = Crit2Dps.Dps0();
            if (dps0 < 160f || dps0 > 180f)
                return "DPS0 expect~169 got " + dps0.ToString("0.#");

            var owned = new System.Collections.Generic.List<string> { "R1L" };
            float dps1 = Crit2Dps.FromIds(owned, true).Dps;
            if (dps1 <= dps0)
                return "DPS must rise with 锋矢";

            AltarRewardRoll.GetWeights(AltarSize.Small, out int sL, out int sM, out int sH);
            if (sL != 72 || sM != 28 || sH != 0)
                return "small weights";

            var rng = new Random(42);
            var picks = AltarRewardRoll.RollThree(AltarSize.Mid, rng);
            if (picks.Length != 3)
                return "roll three";
            for (int i = 0; i < picks.Length; i++)
            {
                if (string.IsNullOrEmpty(picks[i].Id) || string.IsNullOrEmpty(picks[i].Effect))
                    return "pick id/effect";
                if (!RewardCatalog.TryGet(picks[i].Id, out var row) || row.Tier != picks[i].Tier)
                    return "pick tier mismatch " + picks[i].Id;
            }

            bool sawHigh = false;
            for (int t = 0; t < 80; t++)
            {
                if (AltarRewardRoll.RollTier(AltarSize.Small, rng) == RewardTier.High)
                    sawHigh = true;
            }

            if (sawHigh)
                return "small must not roll high";

            var used = new System.Collections.Generic.HashSet<string>();
            RewardTier tier = AltarRewardRoll.RollTier(AltarSize.Large, new Random(7));
            if (!RewardCatalog.TryPickEqual(tier, used, new Random(7), out _))
                return "tier→equal pick";

            if (ShopStock.ShelfCount != 6)
                return "shop shelves!=6";
            if (ShopStock.BuildEquivFor(ShopSlotRole.Low) != 1
                || ShopStock.BuildEquivFor(ShopSlotRole.Mid) != 1
                || ShopStock.BuildEquivFor(ShopSlotRole.High) != 1
                || ShopStock.BuildEquivFor(ShopSlotRole.Heal) != 1)
                return "shop equiv must be +1 each buy";

            var st = new RunBuildState(0.45f, 0.55f, 100);
            if (!st.TryShopBuy(20, 1, "R1L") || st.BuildCount != 1 || st.OwnedRewardIds.Count != 1)
                return "shop buy must add Build +1";
            if (!st.TryShopBuy(60, 3, "R7H") || st.BuildCount != 2)
                return "shop buy stays +1 even if caller passes old high equiv";
            if (ShopStock.PriceBaseHigh != 60
                || ShopStock.HighPriceMin != 54
                || ShopStock.HighPriceMax != 66
                || ShopStock.PriceFromLow(ShopSlotRole.High, 20) != 60)
                return "shop high must be 54-66 (mid 60, ratio 1:2:3)";
            float dpsShop = Crit2Dps.FromState(st, true).Dps;
            if (dpsShop <= dps0)
                return "shop reward must affect DPS";

            return null;
        }

        public static string FormatPass(AltarPick[] picks)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("ACCEPTANCE PASS crit2 ")
                .Append(RewardCatalog.FormatAttackTiers())
                .Append(' ')
                .Append(RewardCatalog.FormatSpecialTiers())
                .Append(" DPS0=")
                .Append(Crit2Dps.Dps0().ToString("0.#"))
                .Append(" flow=掷档→加权抽行 weights=72/28/0|53/32/15|16/54/30 picks=");
            for (int i = 0; i < picks.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(picks[i].Id)
                    .Append('/')
                    .Append(AltarRewardRoll.TierLabel(picks[i].Tier))
                    .Append('/')
                    .Append(picks[i].Effect);
            }

            return sb.ToString();
        }
    }
}
