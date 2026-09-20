using System;
using System.Collections.Generic;
using System.Text;
using RogueShooter.Balance;
using RogueShooter.Build;

namespace RogueShooter.Spawning
{
    public enum SpawnClassId
    {
        S1Pre,
        S1Post,
        S2Pre,
        S2Post,
        S3Pre,
        S3Post
    }

    public struct SpawnMember
    {
        public string KindId;
        public int Count;
    }

    public struct SpawnGroupDef
    {
        public string GroupId;
        public string Intent;
        public int SumP0;
        public SpawnMember[] Members;
    }

    /// <summary>
    /// N12/N13/N17/N19 total table B2: six classes × A/B.
    /// N19 cuts S1后B / S3后B last-segment pressure; Tm slope unchanged.
    /// HP = base × Tm × Bm × bagMul at spawn lock.
    /// M3: six_bag_hp_mul=0.40；elite_hp_mul=0.55（balance_enemy_hp_箭骸.csv）.
    /// </summary>
    public static class SpawnWaveCatalog
    {
        public const float HpPerP0 = 25f;
        public const float SixBagHpMul = 12.5f;
        public const float EliteHpMul = 18.0f;

        // M2 BaseHp lock (numeric): E1/E2/E3/E4 = 200/160/180/400
        static readonly Dictionary<string, int> KindBaseHp = new Dictionary<string, int>
        {
            { "E1", 200 },
            { "E2", 160 },
            { "E3", 180 },
            { "E4", 400 },
        };

        static readonly Dictionary<string, int> KindP0 = new Dictionary<string, int>
        {
            { "E1", 8 },
            { "E2", 7 },
            { "E3", 9 },
            { "E4", 16 },
        };

        static readonly SpawnGroupDef[] S1Pre =
        {
            G("A", "纯 E1 少量", 24, M("E1", 3)),
            G("B", "E1 小队 (N34砍 E1×4→×3)", 24, M("E1", 3)),
        };
        static readonly SpawnGroupDef[] S1Post =
        {
            G("A", "E1+E2 (N19砍1×E1)", 23, M("E1", 2), M("E2", 1)),
            G("B", "E1 小队 (N19重点砍)", 24, M("E1", 3)),
        };
        static readonly SpawnGroupDef[] S2Pre =
        {
            G("A", "E1+E3 (N13锁定)", 25, M("E1", 2), M("E3", 1)),
            G("B", "E2+E1 (N34砍1×E2)", 22, M("E2", 2), M("E1", 1)),
        };
        static readonly SpawnGroupDef[] S2Post =
        {
            G("A", "E1+E2+E3 (N19砍1×E1)", 24, M("E1", 1), M("E2", 1), M("E3", 1)),
            G("B", "E3+E1 (N19砍1×E3)", 25, M("E3", 1), M("E1", 2)),
        };
        static readonly SpawnGroupDef[] S3Pre =
        {
            G("A", "E1+E4 (N19砍1×E1)", 24, M("E1", 1), M("E4", 1)),
            G("B", "E2+E3 (M3加厚 E2×2+E3×2)", 32, M("E2", 2), M("E3", 2)),
        };
        static readonly SpawnGroupDef[] S3Post =
        {
            G("A", "E4+E1 收束 (N13锁定)", 24, M("E4", 1), M("E1", 1)),
            G("B", "E2+E3+E1 (N19重点砍)", 24, M("E2", 1), M("E3", 1), M("E1", 1)),
        };

        public static string ClassLabel(SpawnClassId id)
        {
            switch (id)
            {
                case SpawnClassId.S1Pre: return "S1前";
                case SpawnClassId.S1Post: return "S1后";
                case SpawnClassId.S2Pre: return "S2前";
                case SpawnClassId.S2Post: return "S2后";
                case SpawnClassId.S3Pre: return "S3前";
                default: return "S3后";
            }
        }

        public static SpawnGroupDef[] GroupsFor(SpawnClassId id)
        {
            switch (id)
            {
                case SpawnClassId.S1Pre: return S1Pre;
                case SpawnClassId.S1Post: return S1Post;
                case SpawnClassId.S2Pre: return S2Pre;
                case SpawnClassId.S2Post: return S2Post;
                case SpawnClassId.S3Pre: return S3Pre;
                default: return S3Post;
            }
        }

        public static SpawnGroupDef RollGroup(SpawnClassId id, Random rng)
        {
            SpawnGroupDef[] pool = GroupsFor(id);
            if (pool == null || pool.Length == 0)
                return default;
            if (rng == null)
                rng = new Random();
            return pool[rng.Next(pool.Length)];
        }

        /// <summary>Stable bag id for CSV / TTK: e.g. S1前_A.</summary>
        public static string BagId(SpawnClassId id, SpawnGroupDef group)
        {
            string g = string.IsNullOrEmpty(group.GroupId) ? "?" : group.GroupId;
            return ClassLabel(id) + "_" + g;
        }

        /// <summary>N13: Bm = 0.70 + 0.03×B (B10→1.00).</summary>
        public static float BuildMul(int buildCount)
        {
            int b = buildCount < 0 ? 0 : buildCount;
            return 0.70f + 0.03f * b;
        }

        /// <summary>Tm = TimePressure.AttrMul (N31 piecewise-linear continuous).</summary>
        public static float TimeMul(float wallMinutes)
        {
            return TimePressure.AttrMul(wallMinutes);
        }

        public static int BaseHp(string kindId)
        {
            int hp;
            if (KindBaseHp.TryGetValue(kindId, out hp))
                return hp;
            int p0;
            if (!KindP0.TryGetValue(kindId, out p0))
                p0 = 8;
            return MathfRound(p0 * HpPerP0);
        }

        public static int ScaledHp(string kindId, float tm, float bm)
        {
            return ScaledHp(kindId, tm, bm, 1f);
        }

        /// <summary>bagMul: SixBagHpMul for six-class bags; EliteHpMul for chest elite pool.</summary>
        public static int ScaledHp(string kindId, float tm, float bm, float bagMul)
        {
            if (bagMul < 0f)
                bagMul = 0f;
            float hp = BaseHp(kindId) * tm * bm * bagMul;
            int v = MathfRound(hp);
            return v < 1 ? 1 : v;
        }

        public static int TotalCount(SpawnGroupDef group)
        {
            if (group.Members == null)
                return 0;
            int n = 0;
            for (int i = 0; i < group.Members.Length; i++)
                n += group.Members[i].Count;
            return n;
        }

        public static string FormatGroup(SpawnGroupDef g)
        {
            var sb = new StringBuilder();
            sb.Append(g.GroupId).Append(' ').Append(g.Intent).Append(" ΣP0=").Append(g.SumP0).Append(" [");
            if (g.Members != null)
            {
                for (int i = 0; i < g.Members.Length; i++)
                {
                    if (i > 0)
                        sb.Append('+');
                    sb.Append(g.Members[i].KindId).Append('×').Append(g.Members[i].Count);
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Map band Z1/Z2/Z3/Pre + altar claims → six-class id.
        /// Small claimed → S1后; Mid → S2后; Large → S3后 (within stage).
        /// </summary>
        public static SpawnClassId ResolveClass(string bandId, RunBuildState build)
        {
            int stage = 1;
            if (bandId == "Z2")
                stage = 2;
            else if (bandId == "Z3" || bandId == "Pre")
                stage = 3;

            bool post = false;
            if (build != null)
            {
                if (stage == 1)
                    post = build.AltarSizeClaimed(AltarSize.Small);
                else if (stage == 2)
                    post = build.AltarSizeClaimed(AltarSize.Mid);
                else
                    post = build.AltarSizeClaimed(AltarSize.Large);
            }

            if (stage == 1)
                return post ? SpawnClassId.S1Post : SpawnClassId.S1Pre;
            if (stage == 2)
                return post ? SpawnClassId.S2Post : SpawnClassId.S2Pre;
            return post ? SpawnClassId.S3Post : SpawnClassId.S3Pre;
        }

        static SpawnGroupDef G(string id, string intent, int sumP0, params SpawnMember[] members)
        {
            return new SpawnGroupDef
            {
                GroupId = id,
                Intent = intent,
                SumP0 = sumP0,
                Members = members
            };
        }

        static SpawnMember M(string kind, int count)
        {
            return new SpawnMember { KindId = kind, Count = count };
        }

        static int MathfRound(float v)
        {
            return (int)(v + 0.5f);
        }
    }
}
