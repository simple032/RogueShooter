using System;
using System.Text;
using System.Collections.Generic;

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
    /// S2 pool: normals (no lunge) + paired dogs + shield + cult mage
    /// (邪法师, same S1 linear-orb rules).
    /// </summary>
    public static class StageEnemyPool
    {
        static readonly string[] S1Kinds = { EnemyKindIds.Normal, EnemyKindIds.Dog, EnemyKindIds.CultMage };
        // Spec addendum: S2 includes Cult Mage (E3) with S1 orb behavior.
        static readonly string[] S2Kinds =
        {
            EnemyKindIds.Normal, EnemyKindIds.Dog, EnemyKindIds.CultMage, EnemyKindIds.Shield
        };
        static readonly string[] S3Kinds =
        {
            EnemyKindIds.Normal, EnemyKindIds.Dog, EnemyKindIds.CultMage,
            EnemyKindIds.Shield
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
            return CurrentRows(stage, false);
        }

        public static DraftCompositionRow[] EnhancedComps(StageId stage)
        {
            return CurrentRows(stage, true);
        }

        /// <summary>
        /// Enter combat room: draw one composition from the current stage pool.
        /// Normal + ordinary Chest → tier=normal. Altar / LargeChest → enhanced (same, no extra count).
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

            SpawnMember[] members = NormalizeMembers(stage, row.Members);
            int extra = 0;

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

        public static SpawnMember[] NormalizeMembers(StageId stage, SpawnMember[] original)
        {
            var result=new List<SpawnMember>();
            if(original!=null)foreach(var item in original)
            {
                string kind=item.KindId==EnemyKindIds.GrandMage?EnemyKindIds.CultMage:item.KindId;
                if(!HasKind(stage,kind))kind=EnemyKindIds.Normal;
                int index=result.FindIndex(m=>m.KindId==kind);
                if(index<0)result.Add(new SpawnMember{KindId=kind,Count=item.Count});
                else {var merged=result[index];merged.Count+=item.Count;result[index]=merged;}
            }
            return result.ToArray();
        }
        static DraftCompositionRow[] CurrentRows(StageId stage,bool enhanced)
        {
            var rows=EnemyPoolDraft.CompsFor(stage,enhanced);
            for(int i=0;i<rows.Length;i++){var row=rows[i];row.Members=NormalizeMembers(stage,row.Members);rows[i]=row;}
            return rows;
        }
        public static DrawnComposition DrawRoom(StageId stage,string roomId,CombatRoomKind kind,int wave,Random rng)
        {
            if(wave!=1||stage==StageId.S1||roomId!="S"+(int)stage+"_A")return DrawComposition(stage,kind,rng);
            EnemyPoolDraft.EnsureLoaded();if(rng==null)rng=new Random();
            var members=new[]{new SpawnMember{KindId=EnemyKindIds.Normal,Count=2},new SpawnMember{KindId=stage==StageId.S2?EnemyKindIds.Shield:EnemyKindIds.CultMage,Count=1}};
            return new DrawnComposition{Stage=stage,RoomKind=kind,CompId="S"+(int)stage+"_OPEN",Tier="normal",CompLabel="首房教学",Members=members,Units=ExpandUnits(members,null,rng),ExtraAdded=0,EliteCount=0,FromDraftCsv=false};
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
