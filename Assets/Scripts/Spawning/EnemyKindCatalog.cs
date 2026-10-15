using RogueShooter.Ai;
using RogueShooter.Balance;

namespace RogueShooter.Spawning
{
    public struct EnemyKindProfile
    {
        public string KindId;
        public string DisplayName;
        public string Role;
        public float TtkAnchor0B;
        public bool Melee;
        public bool RangedOrb;
        public bool Shield;
        public float WalkSpeedPlayStub;
        public float AttackRange;
        public float WindupSeconds;
        public float AttackIntervalSeconds;
        public float AtkStub;
        /// <summary>Arrow-sweep lateral radius around the ground point. 0 = legacy locked feet radius.</summary>
        public float HitRadius;
    }

    /// <summary>
    /// Spec v0.5 kind table. HP/ATK come from EnemyPoolDraft CSVs (DRAFT, not lock).
    /// Stage id never changes these base attrs.
    /// </summary>
    public static class EnemyKindCatalog
    {
        public static readonly EnemyKindProfile Normal = MeleeProfile(
            EnemyKindIds.Normal, "普通小怪", "melee_normal", 3.0f,
            EnemyCombatRules.WalkNormalStub, EnemyCombatRules.MeleeHitRadius,
            EnemyDamageCatalog.E1,
            // Silhouette reach 0–1.5u band: idle ≤0.42, walk ≤0.59, atk ≤0.71, alert ≤0.78.
            0.82f);

        public static readonly EnemyKindProfile Dog = MeleeProfile(
            EnemyKindIds.Dog, "狗", "melee_dog", 1.5f,
            EnemyCombatRules.WalkDogStub, EnemyCombatRules.MeleeHitRadius,
            EnemyDamageCatalog.E2,
            // Long body: idle_e 0.75, lunge atk_ne 0.86, full extent incl. paws 0.89.
            0.90f);

        public static readonly EnemyKindProfile CultMage = MageProfile(
            EnemyKindIds.CultMage, "邪法师", "ranged_orb", 2.0f,
            EnemyCombatRules.WalkMageStub, EnemyDamageCatalog.E3);

        public static readonly EnemyKindProfile Shield = MeleeProfile(
            EnemyKindIds.Shield, "盾兵", "melee_shield", 4.0f,
            EnemyCombatRules.WalkShieldStub, 0.80f,
            EnemyDamageCatalog.E1,
            // Guarded poses ≤0.59; unshielded sword swing body ≤0.74 (blade tip excluded).
            0.78f);

        public static readonly EnemyKindProfile GrandMage = MageProfile(
            EnemyKindIds.GrandMage, "大邪术师", "ranged_grand", 3.0f,
            EnemyCombatRules.WalkGrandStub, EnemyDamageCatalog.E3);

        /// <summary>DRAFT 0B DPS placeholder from stats META (not a lock CSV).</summary>
        public static float StubDps0B => EnemyPoolDraft.DraftDps0B;

        public static EnemyKindProfile ForKind(string kindId)
        {
            if (string.IsNullOrEmpty(kindId))
                return Normal;
            switch (kindId.ToUpperInvariant())
            {
                case "E2": return Dog;
                case "E3": return CultMage;
                case "SHIELD": return Shield;
                case "GRAND": return GrandMage;
                default: return Normal;
            }
        }

        public static int StubHp(string kindId)
        {
            EnemyPoolDraft.EnsureLoaded();
            int mid = EnemyPoolDraft.Stat(kindId).HpMid;
            return mid < 1 ? 1 : mid;
        }

        public static float HitDamageStub(string kindId)
        {
            EnemyPoolDraft.EnsureLoaded();
            float atk = EnemyPoolDraft.Stat(kindId).Atk;
            if (atk > 0.01f)
                return atk;
            return ForKind(kindId).AtkStub;
        }

        public static float WalkSpeed(string kindId, bool shielded)
        {
            if (!shielded && kindId == EnemyKindIds.Dog)
                return EnemyCombatRules.WalkDogStub;
            EnemyPoolDraft.EnsureLoaded();
            float v = EnemyPoolDraft.MoveSpeed(kindId, shielded);
            if (v > 0.01f)
                return v;
            return ForKind(kindId).WalkSpeedPlayStub;
        }

        public static float LungeDamageStub()
        {
            return 0.5f * (EnemyPoolDraft.LungeDamageMinEasy + EnemyPoolDraft.LungeDamageMaxEasy);
        }

        static EnemyKindProfile MeleeProfile(
            string id, string name, string role, float ttk, float walk, float attackRange, EnemyDamageDef src,
            float hitRadius = 0f)
        {
            return new EnemyKindProfile
            {
                KindId = id,
                DisplayName = name,
                Role = role,
                TtkAnchor0B = ttk,
                Melee = true,
                RangedOrb = false,
                Shield = id == EnemyKindIds.Shield,
                WalkSpeedPlayStub = walk,
                AttackRange = EnemyCombatRules.MeleeStartRangeFor(id),
                WindupSeconds = EnemyCombatRules.MeleeWindupFor(id),
                AttackIntervalSeconds = src.AttackIntervalRecommend,
                AtkStub = src.AtkRecommend,
                HitRadius = hitRadius
            };
        }

        static EnemyKindProfile MageProfile(
            string id, string name, string role, float ttk, float walk, EnemyDamageDef src)
        {
            return new EnemyKindProfile
            {
                KindId = id,
                DisplayName = name,
                Role = role,
                TtkAnchor0B = ttk,
                Melee = false,
                RangedOrb = true,
                Shield = false,
                WalkSpeedPlayStub = walk,
                AttackRange = 4.50f,
                WindupSeconds = src.WindupRecommend,
                AttackIntervalSeconds = src.AttackIntervalRecommend,
                AtkStub = src.AtkRecommend,
                // Measured silhouette reach (pivot row ±, 0–1.5u band, all clips):
                // idle ≤0.40, walk ≤0.50, alert ≤0.81 (robe flare), cast ≤0.73 body.
                // 0.85 covers every body pose with margin; staff/glow FX stays outside.
                HitRadius = 0.85f
            };
        }

        /// <summary>Arrow sweep radius for a kind. Falls back to the legacy locked feet radius.</summary>
        public static float HitRadiusFor(string kindId)
        {
            EnemyKindProfile p = ForKind(kindId);
            return p.HitRadius > 0.01f ? p.HitRadius : RogueShooter.Demo.Stage1Coin.BodyRadius;
        }
    }
}
