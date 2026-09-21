using System;
using System.Text;

namespace RogueShooter.Spawning
{
    public struct DrawnUnit
    {
        public string KindId;
        public bool Elite;
        public int Hp;
        public float Atk;
    }

    public struct DrawnComposition
    {
        public StageId Stage;
        public CombatRoomKind RoomKind;
        public string CompId;
        public string Tier;
        public string CompLabel;
        public string CompositionText;
        public SpawnMember[] Members;
        public DrawnUnit[] Units;
        public int ExtraAdded;
        public int EliteCount;
        public bool FromDraftCsv;

        public int UnitCount
        {
            get { return Units != null ? Units.Length : EnemyPoolDraft.CountMembers(Members); }
        }

        public string KindsToken
        {
            get { return EnemyPoolDraft.FormatMembers(Members); }
        }

        /// <summary>Console contract: [StagePool] S? room=? comp=… kinds=…</summary>
        public string LogLine()
        {
            return "[StagePool] " + StageIdUtil.Label(Stage)
                + " room=" + CombatRoomKindUtil.Label(RoomKind)
                + " comp=" + (string.IsNullOrEmpty(CompId) ? "?" : CompId)
                + " kinds=" + KindsToken;
        }
    }

    /// <summary>
    /// Spec v0.5 §6 stage enemy pools. Draws one named composition from the
    /// current stage's normal or enhanced table. DRAFT numbers — not lock CSV.
    /// </summary>
    public static class StageEnemyPool
    {
        static readonly string[] S1Kinds = { EnemyKindIds.Normal, EnemyKindIds.Dog, EnemyKindIds.CultMage };
        static readonly string[] S2Kinds =
        {
            EnemyKindIds.Normal, EnemyKindIds.Dog, EnemyKindIds.CultMage, EnemyKindIds.Shield
        };
        static readonly string[] S3Kinds =
        {
            EnemyKindIds.Normal, EnemyKindIds.Dog, EnemyKindIds.CultMage,
            EnemyKindIds.Shield, EnemyKindIds.GrandMage
        };

        public static string[] KindsFor(StageId stage)
        {
            switch (stage)
            {
                case StageId.S2: return S2Kinds;
                case StageId.S3: return S3Kinds;
                default: return S1Kinds;
            }
        }

        public static string FormatKinds(StageId stage)
        {
            return string.Join(",", KindsFor(stage));
        }

        public static bool HasKind(StageId stage, string kindId)
        {
            if (string.IsNullOrEmpty(kindId))
                return false;
            string[] kinds = KindsFor(stage);
            for (int i = 0; i < kinds.Length; i++)
            {
                if (string.Equals(kinds[i], kindId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static DraftCompositionRow[] NormalComps(StageId stage)
        {
            return EnemyPoolDraft.CompsFor(stage, false);
        }

        public static DraftCompositionRow[] EnhancedComps(StageId stage)
        {
            return EnemyPoolDraft.CompsFor(stage, true);
        }

        /// <summary>
        /// Enter combat room: draw one composition from the current stage pool.
        /// Normal → tier=normal. Altar/chest → enhanced. LargeChest → enhanced +1～2.
        /// </summary>
        public static DrawnComposition DrawComposition(StageId stage, CombatRoomKind roomKind, Random rng)
        {
            EnemyPoolDraft.EnsureLoaded();
            if (rng == null)
                rng = new Random();

            bool enhanced = CombatRoomKindUtil.PrefersEnhanced(roomKind);
            DraftCompositionRow[] table = EnemyPoolDraft.CompsFor(stage, enhanced);
            if (table == null || table.Length == 0)
                table = EnemyPoolDraft.CompsFor(stage, false);
            DraftCompositionRow row = table != null && table.Length > 0
                ? table[rng.Next(table.Length)]
                : default(DraftCompositionRow);

            SpawnMember[] members = EnemyPoolDraft.CloneMembers(row.Members);
            int extra = 0;
            if (CombatRoomKindUtil.AddsBigChestExtra(roomKind))
            {
                extra = rng.Next(EnemyPoolDraft.BigChestExtraMin, EnemyPoolDraft.BigChestExtraMax + 1);
                members = EnemyPoolEnhance.AddSamePoolUnits(members, extra, stage);
            }

            bool eliteMark = row.EliteMark;
            string eliteKind = eliteMark ? EnemyPoolEnhance.PickEliteKind(members) : null;
            DrawnUnit[] units = ExpandUnits(members, eliteKind, rng);

            return new DrawnComposition
            {
                Stage = stage,
                RoomKind = roomKind,
                CompId = row.CompId,
                Tier = enhanced ? "enhanced" : "normal",
                CompLabel = row.CompLabel,
                CompositionText = row.CompositionText,
                Members = members,
                Units = units,
                ExtraAdded = extra,
                EliteCount = CountElite(units),
                FromDraftCsv = true
            };
        }

        public static string LogLine(StageId stage, CombatRoomKind roomKind, DrawnComposition drawn)
        {
            if (string.IsNullOrEmpty(drawn.CompId))
            {
                return "[StagePool] " + StageIdUtil.Label(stage)
                    + " room=" + CombatRoomKindUtil.Label(roomKind)
                    + " comp=? kinds=" + FormatKinds(stage);
            }

            return drawn.LogLine();
        }

        static DrawnUnit[] ExpandUnits(SpawnMember[] members, string eliteKind, Random rng)
        {
            int n = EnemyPoolDraft.CountMembers(members);
            var units = new DrawnUnit[n];
            int w = 0;
            bool marked = false;
            if (members != null)
            {
                for (int i = 0; i < members.Length; i++)
                {
                    for (int k = 0; k < members[i].Count; k++)
                    {
                        bool elite = false;
                        if (!marked && eliteKind != null && members[i].KindId == eliteKind)
                        {
                            elite = true;
                            marked = true;
                        }

                        units[w++] = new DrawnUnit
                        {
                            KindId = members[i].KindId,
                            Elite = elite,
                            Hp = EnemyPoolDraft.RollHp(members[i].KindId, rng, elite),
                            Atk = EnemyPoolDraft.Atk(members[i].KindId, elite)
                        };
                    }
                }
            }

            if (w != units.Length)
            {
                var trimmed = new DrawnUnit[w];
                Array.Copy(units, trimmed, w);
                return trimmed;
            }

            return units;
        }

        static int CountElite(DrawnUnit[] units)
        {
            if (units == null)
                return 0;
            int n = 0;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].Elite)
                    n++;
            }

            return n;
        }
    }

    /// <summary>Alias kept from the first sketch; prefer StageEnemyPool.</summary>
    public static class EnemyStagePool
    {
        public static DrawnComposition DrawComposition(StageId stage, CombatRoomKind roomKind, Random rng)
        {
            return StageEnemyPool.DrawComposition(stage, roomKind, rng);
        }

        public static string FormatKinds(StageId stage)
        {
            return StageEnemyPool.FormatKinds(stage);
        }

        public static string LogLine(StageId stage, CombatRoomKind roomKind, DrawnComposition drawn)
        {
            return StageEnemyPool.LogLine(stage, roomKind, drawn);
        }
    }
}
