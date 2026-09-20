using System;
using System.Text;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// M2 large-chest elite pool (balance_elite_chest_pool_箭骸.csv).
    /// Independent of six-class A/B bags; never "E4×1 only".
    /// </summary>
    public static class EliteChestPool
    {
        public struct EliteBag
        {
            public string BagId;
            public int Weight;
            public SpawnMember[] Members;
            public string Note;
        }

        static readonly EliteBag[] Bags =
        {
            Bag("EL_A", 34, "大箱精英池_重型+狗", M("E4", 1), M("E2", 1)),
            Bag("EL_B", 33, "大箱精英池_远程+垫", M("E3", 2), M("E1", 1)),
            Bag("EL_C", 33, "elite_chest_tank_ranged_E4+E3", M("E4", 1), M("E3", 1)),
        };

        public static EliteBag[] AllBags => Bags;

        public static EliteBag RollBag(Random rng)
        {
            if (rng == null)
                rng = new Random();
            int total = 0;
            for (int i = 0; i < Bags.Length; i++)
                total += Bags[i].Weight < 1 ? 1 : Bags[i].Weight;
            int roll = rng.Next(total);
            int acc = 0;
            for (int i = 0; i < Bags.Length; i++)
            {
                acc += Bags[i].Weight < 1 ? 1 : Bags[i].Weight;
                if (roll < acc)
                    return Bags[i];
            }

            return Bags[Bags.Length - 1];
        }

        public static EliteBag GetById(string bagId)
        {
            for (int i = 0; i < Bags.Length; i++)
            {
                if (string.Equals(Bags[i].BagId, bagId, StringComparison.OrdinalIgnoreCase))
                    return Bags[i];
            }

            return default(EliteBag);
        }

        /// <summary>Null if every bag has ≥2 members or ≥2 kinds (no lone E4×1).</summary>
        public static string VerifyNoLoneE4()
        {
            for (int i = 0; i < Bags.Length; i++)
            {
                var b = Bags[i];
                if (b.Members == null || b.Members.Length == 0)
                    return b.BagId + " empty";
                int kinds = 0;
                int count = 0;
                bool onlyE4 = true;
                for (int m = 0; m < b.Members.Length; m++)
                {
                    count += b.Members[m].Count;
                    kinds++;
                    if (!string.Equals(b.Members[m].KindId, "E4", StringComparison.OrdinalIgnoreCase))
                        onlyE4 = false;
                }

                if (onlyE4 && count <= 1)
                    return b.BagId + " lone E4×1 forbidden";
                if (count < 2 && kinds < 2)
                    return b.BagId + " need ≥2 count or ≥2 kinds";
            }

            return null;
        }

        public static string FormatBag(EliteBag b)
        {
            var sb = new StringBuilder();
            sb.Append(b.BagId).Append(" w=").Append(b.Weight).Append(" [");
            if (b.Members != null)
            {
                for (int i = 0; i < b.Members.Length; i++)
                {
                    if (i > 0)
                        sb.Append('+');
                    sb.Append(b.Members[i].KindId).Append('×').Append(b.Members[i].Count);
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        static EliteBag Bag(string id, int weight, string note, params SpawnMember[] members)
        {
            return new EliteBag { BagId = id, Weight = weight, Note = note, Members = members };
        }

        static SpawnMember M(string kind, int count)
        {
            return new SpawnMember { KindId = kind, Count = count };
        }
    }
}
