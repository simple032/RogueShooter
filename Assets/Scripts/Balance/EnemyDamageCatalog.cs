using System.Text;

namespace RogueShooter.Balance
{
    public struct EnemyDamageDef
    {
        public string KindId;
        public string Name;
        public string Role;
        public float HitPctMin;
        public float HitPctMax;
        public float AtkRecommend;
        public float AttackIntervalRecommend;
        public float WindupRecommend;
        public float PlayerMaxHpRef;
    }

    /// <summary>
    /// N20: embedded copy of 02_design/balance_enemy_damage_箭骸.csv (recommend mid).
    /// Hit% = ATK / PlayerMaxHpRef; do not alter producer hit% bands.
    /// </summary>
    public static class EnemyDamageCatalog
    {
        public const float PlayerMaxHpRef = 100f;

        public static readonly EnemyDamageDef E1 = Def(
            "E1", "近战普通", "melee_normal", 25f, 35f, 30f, 1.10f, 0.42f);
        public static readonly EnemyDamageDef E2 = Def(
            "E2", "近战狗", "melee_dog", 15f, 25f, 20f, 0.80f, 0.42f);
        public static readonly EnemyDamageDef E3 = Def(
            "E3", "远程", "ranged", 15f, 25f, 20f, 1.10f, 0.22f);
        public static readonly EnemyDamageDef E4 = Def(
            "E4", "近战重型", "melee_heavy", 35f, 45f, 40f, 1.80f, 0.22f);

        public static EnemyDamageDef ForKind(string kindId)
        {
            if (string.IsNullOrEmpty(kindId))
                return E1;
            switch (kindId.ToUpperInvariant())
            {
                case "E2": return E2;
                case "E3": return E3;
                case "E4": return E4;
                default: return E1;
            }
        }

        /// <summary>Flat ATK at recommend mid (player MaxHP ref 100 → hit%).</summary>
        public static float HitDamage(string kindId)
        {
            return ForKind(kindId).AtkRecommend;
        }

        public static float HitPct(string kindId)
        {
            var d = ForKind(kindId);
            float max = d.PlayerMaxHpRef > 1f ? d.PlayerMaxHpRef : PlayerMaxHpRef;
            return d.AtkRecommend / max * 100f;
        }

        public static string FormatAll()
        {
            var sb = new StringBuilder();
            Append(sb, E1);
            sb.Append(" | ");
            Append(sb, E2);
            sb.Append(" | ");
            Append(sb, E3);
            sb.Append(" | ");
            Append(sb, E4);
            return sb.ToString();
        }

        static void Append(StringBuilder sb, EnemyDamageDef d)
        {
            sb.Append(d.KindId)
                .Append(" atk=").Append(d.AtkRecommend.ToString("0"))
                .Append(" iv=").Append(d.AttackIntervalRecommend.ToString("0.00"))
                .Append(" wu=").Append(d.WindupRecommend.ToString("0.00"))
                .Append(" hit%=").Append(HitPct(d.KindId).ToString("0"));
        }

        static EnemyDamageDef Def(
            string id, string name, string role,
            float hitMin, float hitMax, float atk, float interval, float windup)
        {
            return new EnemyDamageDef
            {
                KindId = id,
                Name = name,
                Role = role,
                HitPctMin = hitMin,
                HitPctMax = hitMax,
                AtkRecommend = atk,
                AttackIntervalRecommend = interval,
                WindupRecommend = windup,
                PlayerMaxHpRef = PlayerMaxHpRef
            };
        }
    }
}
