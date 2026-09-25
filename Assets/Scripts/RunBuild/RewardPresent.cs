using System.Collections.Generic;

namespace RogueShooter.Build
{
    /// <summary>
    /// §9.2 display copy: one icon and one number-free sentence per upgrade name.
    /// Does not change catalog values or shop prices.
    /// </summary>
    public static class RewardPresent
    {
        public struct Copy
        {
            public string CatalogName;
            public string DisplayName;
            public string Sentence;
            public string Icon;
        }

        public static readonly Copy[] All =
        {
            Line("锋矢", "锋矢", "射出的箭伤得更重", "jh_ui_icon_sharp_arrow"),
            Line("疾张", "疾张", "蓄满一箭更快", "jh_ui_icon_rapid_draw"),
            Line("鸿运", "鸿运", "命中弱点的时机更宽", "jh_ui_icon_luck"),
            Line("骨甲", "骨甲", "能承受更多伤害", "jh_ui_icon_bone_armor"),
            Line("残影", "残影", "走得更快", "jh_ui_icon_afterimage"),
            Line("盗墓者", "盗墓者", "击杀掉落更多金币", "jh_ui_icon_grave_robber"),
            Line("瞬击预感", "瞬击预感", "更容易打出暴击", "jh_ui_icon_instant_foresight"),
            Line("破甲猛击", "破甲猛击", "暴击打得更疼", "jh_ui_icon_armor_break"),
            Line("隙矢追猎", "隙矢追猎", "打中弱点时伤得更重", "jh_ui_icon_gap_hunter"),
            Line("止血", "止血", "立刻回复一部分生命", "jh_ui_icon_hemostasis"),
            Line("满血额外伤害", "满血额外伤害", "对满血的敌人伤得更重", "jh_ui_icon_full_health_strike"),
            Line("进房间5s内加攻", "进房加攻", "刚进房间时攻击更强", "jh_ui_icon_room_attack"),
            Line("攻击吸血", "攻击吸血", "造成伤害时回复生命", "jh_ui_icon_lifesteal"),
            Line("穿透后排", "穿透后排", "箭穿过前排后仍能伤到后面的敌人", "jh_ui_icon_pierce_backline"),
            Line("震矢", "震矢", "蓄满射出时把敌人推得更远", "jh_ui_icon_shock_arrow"),
        };

        /// <summary>Generic card icon for ids with no delivered icon (DODGE / FIRE / unknown).</summary>
        public const string GenericIcon = "jh_ui_icon_luck";

        /// <summary>
        /// BalanceLock demo_reward_pool ids (stem before _C/_R/_E). Same number-free copy style.
        /// Icon "" = no delivered icon, card uses <see cref="GenericIcon"/>.
        /// </summary>
        public static readonly KeyValuePair<string, Copy>[] DemoStems =
        {
            Demo("VIT", "骨甲", "能承受更多伤害", "jh_ui_icon_bone_armor"),
            Demo("ARM", "护甲", "受到的伤害更少", "jh_ui_icon_bone_armor"),
            Demo("DODGE", "闪身", "更容易躲开攻击", ""),
            Demo("GOLD", "盗墓者", "击杀掉落更多金币", "jh_ui_icon_grave_robber"),
            Demo("HASTE", "残影", "走得更快", "jh_ui_icon_afterimage"),
            Demo("DMG", "锋矢", "射出的箭伤得更重", "jh_ui_icon_sharp_arrow"),
            Demo("CRIT", "瞬击预感", "更容易打出暴击", "jh_ui_icon_instant_foresight"),
            Demo("FIRE", "火矢", "箭矢附带灼烧", ""),
        };

        /// <summary>demo_reward_pool.csv ids as shipped (R15_* resolve through RewardCatalog).</summary>
        public static readonly string[] DemoPoolIds =
        {
            "VIT_C", "ARM_C", "DODGE_C", "GOLD_C", "HASTE_C", "DMG_C", "CRIT_C", "FIRE_C",
            "VIT_R", "ARM_R", "GOLD_R", "HASTE_R", "DMG_R", "CRIT_R",
            "VIT_E", "GOLD_E", "DMG_E", "CRIT_E", "R15_C", "R15_R"
        };

        /// <summary>Demo pool id → copy. Icon falls back to <see cref="GenericIcon"/>.</summary>
        public static bool TryForDemoId(string id, out Copy copy)
        {
            copy = default(Copy);
            if (string.IsNullOrEmpty(id))
                return false;
            int cut = id.LastIndexOf('_');
            string stem = cut > 0 ? id.Substring(0, cut) : id;
            for (int i = 0; i < DemoStems.Length; i++)
            {
                if (DemoStems[i].Key == stem)
                {
                    copy = DemoStems[i].Value;
                    if (string.IsNullOrEmpty(copy.Icon))
                        copy.Icon = GenericIcon;
                    return true;
                }
            }

            return false;
        }

        public static bool TryForId(string id, out Copy copy)
        {
            copy = default(Copy);
            if (!RewardCatalog.TryGet(id, out RewardRow row))
                return false;
            return TryForCatalogName(row.Name, out copy);
        }

        public static bool TryForCatalogName(string catalogName, out Copy copy)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].CatalogName == catalogName)
                {
                    copy = All[i];
                    return true;
                }
            }

            copy = default(Copy);
            return false;
        }

        public static RewardCardData ToCard(string id, RewardTier tier, string price, string mark, int index, bool sold)
        {
            // Never show a raw id: catalog copy → demo pool copy → generic card.
            string name = "强化";
            string sentence = "获得一项强化";
            string icon = GenericIcon;
            if (RewardCatalog.TryGet(id, out RewardRow row))
            {
                tier = row.Tier;
                name = row.Name;
                if (TryForCatalogName(row.Name, out Copy copy))
                {
                    name = copy.DisplayName;
                    sentence = copy.Sentence;
                    icon = copy.Icon;
                }
            }
            else if (TryForDemoId(id, out Copy demo))
            {
                name = demo.DisplayName;
                sentence = demo.Sentence;
                icon = demo.Icon;
            }

            if (sold)
                name += " 已售";
            return new RewardCardData
            {
                Tier = tier,
                Name = name,
                Desc = sentence,
                Price = price ?? "",
                Mark = mark ?? "",
                Index = index,
                Icon = icon
            };
        }

        public static string Check()
        {
            if (All.Length != 15)
                return "copy count";
            var seen = new Dictionary<string, Copy>();
            for (int i = 0; i < RewardCatalog.All.Length; i++)
            {
                RewardRow row = RewardCatalog.All[i];
                if (!TryForCatalogName(row.Name, out Copy copy))
                    return "missing copy " + row.Name;
                if (HasDigit(copy.Sentence) || copy.Sentence.IndexOf('%') >= 0)
                    return "sentence has a number " + copy.DisplayName;
                if (string.IsNullOrEmpty(copy.Icon) || string.IsNullOrEmpty(copy.Sentence))
                    return "empty present " + row.Name;
                if (seen.TryGetValue(row.Name, out Copy prev))
                {
                    if (prev.Icon != copy.Icon || prev.Sentence != copy.Sentence || prev.DisplayName != copy.DisplayName)
                        return "same name diverged " + row.Name;
                }
                else
                    seen[row.Name] = copy;

                RewardCardData low = ToCard(row.Id, row.Tier, "", "低", 0, false);
                if (low.Desc != copy.Sentence || low.Icon != copy.Icon || low.Name != copy.DisplayName)
                    return "card mismatch " + row.Id;
                if (HasDigit(low.Desc))
                    return "card desc has a number " + row.Id;
            }

            if (seen.Count != 15)
                return "family count " + seen.Count;
            if (!RewardCatalog.TryGet("R1L", out RewardRow sharp) || sharp.Desc.IndexOf("10") < 0)
                return "catalog number changed";
            if (!RewardCatalog.TryGet("R1M", out RewardRow sharpMid) || sharpMid.Desc.IndexOf("20") < 0 || sharpMid.ShopPrice != 40)
                return "catalog price changed";
            if (!TryForId("R12H", out Copy room) || room.DisplayName != "进房加攻" || room.Sentence != "刚进房间时攻击更强")
                return "room attack copy";
            if (!TryForId("R1L", out Copy a) || !TryForId("R1M", out Copy b) || a.Icon != b.Icon || a.Sentence != b.Sentence)
                return "sharp tiers must share icon and sentence";

            for (int i = 0; i < DemoPoolIds.Length; i++)
            {
                string id = DemoPoolIds[i];
                RewardCardData card = ToCard(id, RewardTier.Low, "", "低", i, false);
                if (card.Name == id || card.Name.IndexOf('_') >= 0 || string.IsNullOrEmpty(card.Name))
                    return "demo raw id shown " + id;
                if (string.IsNullOrEmpty(card.Icon) || string.IsNullOrEmpty(card.Desc) || HasDigit(card.Desc))
                    return "demo present " + id;
                if (!RewardCatalog.TryGet(id, out _) && !TryForDemoId(id, out _))
                    return "demo id unmapped " + id;
            }

            ShopShelf[] shelves = ShopStock.RollShelves(new System.Random(7));
            if (shelves == null || shelves.Length != 6)
                return "shop shelves";
            for (int i = 0; i < shelves.Length; i++)
            {
                ShopShelf shelf = shelves[i];
                RewardCardData card = ToCard(shelf.Id, shelf.Tier, shelf.Price + "金", "", i, shelf.Sold);
                if (card.Price != shelf.Price + "金" || !HasDigit(card.Price))
                    return "shop price hidden " + shelf.Id;
                if (HasDigit(card.Desc) || string.IsNullOrEmpty(card.Icon))
                    return "shop line " + shelf.Id;
                if (RewardCatalog.TryGet(shelf.Id, out RewardRow row) && shelf.Effect != row.Desc)
                    return "shelf effect changed " + shelf.Id;
            }

            return null;
        }

        static bool HasDigit(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '0' && text[i] <= '9')
                    return true;
            }

            return false;
        }

        static KeyValuePair<string, Copy> Demo(string stem, string displayName, string sentence, string icon)
        {
            return new KeyValuePair<string, Copy>(stem, Line(stem, displayName, sentence, icon));
        }

        static Copy Line(string catalogName, string displayName, string sentence, string icon)
        {
            return new Copy
            {
                CatalogName = catalogName,
                DisplayName = displayName,
                Sentence = sentence,
                Icon = icon
            };
        }
    }
}
