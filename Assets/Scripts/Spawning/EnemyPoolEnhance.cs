using System;
using System.Collections.Generic;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// DRAFT enhance rules: count&lt;4 → +1 same-pool unit; count==4 → mark 1 elite (HP×1.25 / atk×1.15).
    /// Large chest: after enhanced, +1～2 more same-pool units.
    /// Grand mage: do not add a second A unless the source already had two.
    /// </summary>
    public static class EnemyPoolEnhance
    {
        public static string[] AddPriority(StageId stage)
        {
            switch (stage)
            {
                case StageId.S2:
                    return new[] { EnemyKindIds.Normal, EnemyKindIds.CultMage, EnemyKindIds.Dog };
                case StageId.S3:
                    return new[] { EnemyKindIds.Dog, EnemyKindIds.Normal, EnemyKindIds.CultMage };
                default:
                    return new[] { EnemyKindIds.Dog, EnemyKindIds.Normal };
            }
        }

        public static SpawnMember[] AddSamePoolUnits(SpawnMember[] members, int addCount, StageId stage)
        {
            var list = new List<SpawnMember>(EnemyPoolDraft.CloneMembers(members));
            for (int n = 0; n < addCount; n++)
            {
                string kind = PickAddKind(list, stage);
                bool found = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].KindId == kind)
                    {
                        var m = list[i];
                        m.Count += 1;
                        list[i] = m;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    list.Add(new SpawnMember { KindId = kind, Count = 1 });
            }

            return list.ToArray();
        }

        public static string PickAddKind(List<SpawnMember> members, StageId stage)
        {
            string[] prio = AddPriority(stage);
            for (int p = 0; p < prio.Length; p++)
            {
                if (ContainsKind(members, prio[p]))
                    return prio[p];
            }

            string[] pool = StageEnemyPool.KindsFor(stage);
            for (int p = 0; p < prio.Length; p++)
            {
                if (ContainsId(pool, prio[p]))
                    return prio[p];
            }

            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != EnemyKindIds.GrandMage)
                    return pool[i];
            }

            return EnemyKindIds.Normal;
        }

        public static string PickEliteKind(SpawnMember[] members)
        {
            string[] threat =
            {
                EnemyKindIds.GrandMage,
                EnemyKindIds.Shield,
                EnemyKindIds.CultMage,
                EnemyKindIds.Normal,
                EnemyKindIds.Dog
            };
            for (int t = 0; t < threat.Length; t++)
            {
                if (ContainsKind(members, threat[t]))
                    return threat[t];
            }

            return members != null && members.Length > 0 ? members[0].KindId : EnemyKindIds.Normal;
        }

        static bool ContainsKind(List<SpawnMember> members, string kind)
        {
            if (members == null)
                return false;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].KindId == kind && members[i].Count > 0)
                    return true;
            }

            return false;
        }

        static bool ContainsKind(SpawnMember[] members, string kind)
        {
            if (members == null)
                return false;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].KindId == kind && members[i].Count > 0)
                    return true;
            }

            return false;
        }

        static bool ContainsId(string[] ids, string kind)
        {
            if (ids == null)
                return false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == kind)
                    return true;
            }

            return false;
        }
    }
}
