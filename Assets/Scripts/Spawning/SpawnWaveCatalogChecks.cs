using System;
using System.Text;
using RogueShooter.Balance;

namespace RogueShooter.Spawning
{
    public static class SpawnWaveCatalogChecks
    {
        /// <summary>Returns null on pass.</summary>
        public static string Run()
        {
            // N13 locked anchors
            var s2a = Find(SpawnClassId.S2Pre, "A");
            if (s2a.SumP0 != 25 || !HasMember(s2a, "E1", 2) || !HasMember(s2a, "E3", 1))
                return "S2前A N13 E1×2+E3×1 Σ25";
            var s3a = Find(SpawnClassId.S3Post, "A");
            if (s3a.SumP0 != 24 || !HasMember(s3a, "E4", 1) || !HasMember(s3a, "E1", 1))
                return "S3后A N13 E4×1+E1×1 Σ24";

            // N34 group cuts
            var s1prb = Find(SpawnClassId.S1Pre, "B");
            if (s1prb.SumP0 != 24 || !HasMember(s1prb, "E1", 3))
                return "S1前B N34 E1×3 Σ24";
            var s2prb = Find(SpawnClassId.S2Pre, "B");
            if (s2prb.SumP0 != 22 || !HasMember(s2prb, "E2", 2) || !HasMember(s2prb, "E1", 1))
                return "S2前B N34 E2×2+E1×1 Σ22";
            var s3prb = Find(SpawnClassId.S3Pre, "B");
            if (s3prb.SumP0 != 32 || !HasMember(s3prb, "E2", 2) || !HasMember(s3prb, "E3", 2))
                return "S3前B M3 E2×2+E3×2 Σ32";

            if (Math.Abs(SpawnWaveCatalog.SixBagHpMul - 12.5f) > 0.001f)
                return "six_bag_hp_mul!=12.5";
            if (Math.Abs(SpawnWaveCatalog.EliteHpMul - 18.0f) > 0.001f)
                return "elite_hp_mul!=18";
            int sixHp = SpawnWaveCatalog.ScaledHp("E1", 1.00f, 0.70f, SpawnWaveCatalog.SixBagHpMul);
            if (sixHp != 1750)
                return "ScaledHp six E1 expect 1750 got " + sixHp;
            int eliteHp = SpawnWaveCatalog.ScaledHp("E4", 1.00f, 1.00f, SpawnWaveCatalog.EliteHpMul);
            if (eliteHp != 7200)
                return "ScaledHp elite E4 expect 7200 got " + eliteHp;

            // N19 key cuts
            var s1pb = Find(SpawnClassId.S1Post, "B");
            if (s1pb.SumP0 != 24 || !HasMember(s1pb, "E1", 3))
                return "S1后B N19 E1×3 Σ24";
            var s1pa = Find(SpawnClassId.S1Post, "A");
            if (s1pa.SumP0 != 23 || !HasMember(s1pa, "E1", 2) || !HasMember(s1pa, "E2", 1))
                return "S1后A N19 E1×2+E2×1 Σ23";
            var s3pb = Find(SpawnClassId.S3Post, "B");
            if (s3pb.SumP0 != 24 || !HasMember(s3pb, "E2", 1) || !HasMember(s3pb, "E3", 1) || !HasMember(s3pb, "E1", 1))
                return "S3后B N19 E2×1+E3×1+E1×1 Σ24";
            var s2pa = Find(SpawnClassId.S2Post, "A");
            if (s2pa.SumP0 != 24 || !HasMember(s2pa, "E1", 1) || !HasMember(s2pa, "E2", 1) || !HasMember(s2pa, "E3", 1))
                return "S2后A N19 E1×1+E2×1+E3×1 Σ24";
            var s3pra = Find(SpawnClassId.S3Pre, "A");
            if (s3pra.SumP0 != 24 || !HasMember(s3pra, "E1", 1) || !HasMember(s3pra, "E4", 1))
                return "S3前A N19 E1×1+E4×1 Σ24";

            SpawnClassId[] all =
            {
                SpawnClassId.S1Pre, SpawnClassId.S1Post,
                SpawnClassId.S2Pre, SpawnClassId.S2Post,
                SpawnClassId.S3Pre, SpawnClassId.S3Post
            };
            var rng = new Random(17);
            var saw = new System.Collections.Generic.HashSet<string>();
            for (int c = 0; c < all.Length; c++)
            {
                var pool = SpawnWaveCatalog.GroupsFor(all[c]);
                if (pool == null || pool.Length < 2)
                    return SpawnWaveCatalog.ClassLabel(all[c]) + " need A/B";
                bool a = false, b = false;
                for (int i = 0; i < 40; i++)
                {
                    var g = SpawnWaveCatalog.RollGroup(all[c], rng);
                    if (g.GroupId == "A") a = true;
                    if (g.GroupId == "B") b = true;
                    saw.Add(SpawnWaveCatalog.ClassLabel(all[c]) + g.GroupId);
                }

                if (!a || !b)
                    return SpawnWaveCatalog.ClassLabel(all[c]) + " must roll A and B";
            }

            if (saw.Count < 12)
                return "need 6×2 groups seen";

            // N31 continuous Tm (same as TimePressure PL)
            if (Math.Abs(SpawnWaveCatalog.TimeMul(0f) - 1.00f) > 0.001f) return "Tm@0";
            if (Math.Abs(SpawnWaveCatalog.TimeMul(4f) - 1.15f) > 0.001f) return "Tm@4";
            if (Math.Abs(SpawnWaveCatalog.TimeMul(5.5f) - 1.25f) > 0.001f) return "Tm@5.5";
            if (Math.Abs(SpawnWaveCatalog.TimeMul(7f) - 1.35f) > 0.001f) return "Tm@7";
            if (Math.Abs(SpawnWaveCatalog.TimeMul(10f) - 1.55f) > 0.001f) return "Tm@10";
            if (Math.Abs(SpawnWaveCatalog.TimeMul(12f) - 1.61f) > 0.001f) return "Tm@12";
            string cont = TimePressure.ContinuityCheck();
            if (cont != null)
                return "Tm " + cont;
            if (Math.Abs(SpawnWaveCatalog.BuildMul(10) - 1.00f) > 0.001f) return "Bm B10=1";
            if (Math.Abs(SpawnWaveCatalog.BuildMul(0) - 0.70f) > 0.001f) return "Bm B0=0.70";

            int hp = SpawnWaveCatalog.ScaledHp("E1", 1.00f, 0.70f);
            if (hp != 140)
                return "ScaledHp E1 Tm1 Bm0.7 expect 140 got " + hp;

            return null;
        }

        static SpawnGroupDef Find(SpawnClassId cls, string groupId)
        {
            var pool = SpawnWaveCatalog.GroupsFor(cls);
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].GroupId == groupId)
                    return pool[i];
            }

            return default(SpawnGroupDef);
        }

        static bool HasMember(SpawnGroupDef g, string kind, int count)
        {
            if (g.Members == null)
                return false;
            for (int i = 0; i < g.Members.Length; i++)
            {
                if (g.Members[i].KindId == kind && g.Members[i].Count == count)
                    return true;
            }

            return false;
        }

        public static string FormatPass()
        {
            var sb = new StringBuilder();
            sb.Append("ACCEPTANCE PASS N19 six-class A/B Tm×Bm ");
            sb.Append("S1后B=E1×3 Σ24 S3后B=E2+E3+E1 Σ24 ");
            sb.Append("S2前A=E1×2+E3×1 Σ25 S3后A=E4×1+E1×1 Σ24 ");
            sb.Append("Tm PL continuous Bm=0.70+0.03×B");
            return sb.ToString();
        }
    }
}
