using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueShooter.Balance;

namespace RogueShooter.Spawning
{
    public struct DraftEnemyStat
    {
        public string KindId;
        public string DisplayName;
        public float Ttk0B;
        public int HpMid;
        public int HpLo;
        public int HpHi;
        public float Atk;
        public string AtkKind;
        public float MoveSpeed;
        public float ShieldedMoveSpeed;
        public float OrbSpeed;
    }

    public struct DraftCompositionRow
    {
        public int Phase;
        public string CompId;
        public string Tier;
        public string CompLabel;
        public int NTotal;
        public string CompositionText;
        public string UseRoom;
        public string EnhRule;
        public string Notes;
        public SpawnMember[] Members;
        public bool EliteMark;
        public bool CountPlusOne;
    }

    /// <summary>
    /// DRAFT (not locked) loader for StreamingAssets/EnemyPoolDraft.
    /// Numbers are placeholders for 数值; do not copy into BalanceLock CSVs.
    /// </summary>
    public static class EnemyPoolDraft
    {
        public const string FolderName = "EnemyPoolDraft";
        public const string CompsFile = "balance_enemy_comps_draft.csv";
        public const string StatsFile = "balance_enemy_stats_draft.csv";
        public const string MoveFile = "balance_enemy_move_draft.csv";
        public const string PoolFile = "balance_enemy_pool_s123_draft.csv";
        public const string LockNote = "DRAFT_NOT_LOCKED";

        public const float DraftDps0B = 13f;
        public const float DraftPlayerMove = 6f;
        public const float DraftOrbSpeed = 12f; // player_move × 2 (not mage walk × 2)
        public const float DraftShieldMoveMul = 0.50f; // 4.5 → 2.25 (−50%)
        public const float DraftShieldedMove = 2.25f;
        public const float EliteHpMul = 1.25f;
        public const float EliteAtkMul = 1.15f;
        public const int WaveCountMin = 3;
        public const int WaveCountMax = 4;
        public const int BigChestExtraMin = 1;
        public const int BigChestExtraMax = 2;
        public const int LungeDamageMinEasy = 30;
        public const int LungeDamageMaxEasy = 40;

        static readonly Dictionary<string, DraftEnemyStat> Stats =
            new Dictionary<string, DraftEnemyStat>(StringComparer.OrdinalIgnoreCase);
        static readonly List<DraftCompositionRow> Comps = new List<DraftCompositionRow>();
        static string _source = "unloaded";
        static string _error;
        static bool _loaded;

        public static string Source => _source;
        public static string LoadError => _error;
        public static bool Loaded => _loaded;
        public static IReadOnlyList<DraftCompositionRow> AllComps => Comps;

        public static bool EnsureLoaded()
        {
            if (_loaded)
                return _error == null;
            string dir = ResolveDirectory();
            return TryLoadFromDirectory(dir, out _error);
        }

        public static bool TryLoadFromDirectory(string dir, out string error)
        {
            error = null;
            _loaded = false;
            Stats.Clear();
            Comps.Clear();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                error = "missing EnemyPoolDraft dir " + dir;
                _error = error;
                _source = "missing";
                return false;
            }

            string compsPath = Path.Combine(dir, CompsFile);
            string statsPath = Path.Combine(dir, StatsFile);
            string movePath = Path.Combine(dir, MoveFile);
            if (!File.Exists(compsPath) || !File.Exists(statsPath) || !File.Exists(movePath))
            {
                error = "missing draft CSV under " + dir;
                _error = error;
                _source = dir;
                return false;
            }

            ParseStats(CsvTable.Parse(File.ReadAllText(statsPath, Encoding.UTF8)));
            ParseMove(CsvTable.Parse(File.ReadAllText(movePath, Encoding.UTF8)));
            ParseComps(CsvTable.Parse(File.ReadAllText(compsPath, Encoding.UTF8)));
            _loaded = true;
            _source = dir + " (" + LockNote + ")";
            _error = null;
            return true;
        }

        public static string ResolveDirectory()
        {
            string cwd = Directory.GetCurrentDirectory();
            string[] candidates =
            {
                Path.Combine(cwd, "Assets", "StreamingAssets", FolderName),
                Path.Combine(cwd, "StreamingAssets", FolderName),
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (Directory.Exists(candidates[i]))
                    return candidates[i];
            }

            return candidates[0];
        }

        public static DraftEnemyStat Stat(string kindId)
        {
            EnsureLoaded();
            DraftEnemyStat s;
            if (!string.IsNullOrEmpty(kindId) && Stats.TryGetValue(kindId, out s))
                return s;
            Stats.TryGetValue(EnemyKindIds.Normal, out s);
            return s;
        }

        public static int RollHp(string kindId, Random rng, bool elite)
        {
            DraftEnemyStat s = Stat(kindId);
            if (rng == null)
                rng = new Random();
            int lo = s.HpLo;
            int hi = s.HpHi;
            if (hi < lo)
                hi = lo;
            int hp = rng.Next(lo, hi + 1);
            if (elite)
                hp = RoundMul(hp, EliteHpMul);
            return hp < 1 ? 1 : hp;
        }

        public static float Atk(string kindId, bool elite)
        {
            float a = Stat(kindId).Atk;
            if (elite)
                a *= EliteAtkMul;
            return a;
        }

        public static float MoveSpeed(string kindId, bool shielded)
        {
            DraftEnemyStat s = Stat(kindId);
            float walk = s.MoveSpeed > 0.01f ? s.MoveSpeed : DraftFallbackMove(kindId);
            if (!shielded)
                return walk;
            if (s.ShieldedMoveSpeed > 0.01f)
                return s.ShieldedMoveSpeed;
            return walk * ShieldMoveMulFromTable();
        }

        public static float OrbSpeedFor(string kindId)
        {
            DraftEnemyStat s = Stat(kindId);
            if (s.OrbSpeed > 0.01f)
                return s.OrbSpeed;
            return DraftOrbSpeed;
        }

        static float ShieldMoveMulFromTable()
        {
            return DraftShieldMoveMul;
        }

        static float DraftFallbackMove(string kindId)
        {
            if (kindId == EnemyKindIds.Dog) return 7.2f;
            if (kindId == EnemyKindIds.CultMage) return 3.6f;
            if (kindId == EnemyKindIds.Shield) return 4.5f;
            if (kindId == EnemyKindIds.GrandMage) return 3.3f;
            return 4.5f;
        }

        public static int RollLungeDamageEasy(Random rng)
        {
            if (rng == null)
                rng = new Random();
            return rng.Next(LungeDamageMinEasy, LungeDamageMaxEasy + 1);
        }

        public static DraftCompositionRow[] CompsFor(StageId stage, bool enhanced)
        {
            EnsureLoaded();
            int phase = (int)stage;
            string tier = enhanced ? "enhanced" : "normal";
            var list = new List<DraftCompositionRow>();
            for (int i = 0; i < Comps.Count; i++)
            {
                DraftCompositionRow c = Comps[i];
                if (c.Phase == phase && string.Equals(c.Tier, tier, StringComparison.OrdinalIgnoreCase))
                    list.Add(c);
            }

            return list.ToArray();
        }

        public static DraftCompositionRow FindComp(string compId)
        {
            EnsureLoaded();
            for (int i = 0; i < Comps.Count; i++)
            {
                if (string.Equals(Comps[i].CompId, compId, StringComparison.OrdinalIgnoreCase))
                    return Comps[i];
            }

            return default(DraftCompositionRow);
        }

        public static string KindIdFromChinese(string name)
        {
            if (string.IsNullOrEmpty(name))
                return EnemyKindIds.Normal;
            if (name.IndexOf("大邪", StringComparison.Ordinal) >= 0)
                return EnemyKindIds.GrandMage;
            if (name.IndexOf("盾", StringComparison.Ordinal) >= 0)
                return EnemyKindIds.Shield;
            if (name.IndexOf("邪法", StringComparison.Ordinal) >= 0)
                return EnemyKindIds.CultMage;
            if (name.IndexOf("狗", StringComparison.Ordinal) >= 0)
                return EnemyKindIds.Dog;
            return EnemyKindIds.Normal;
        }

        public static SpawnMember[] ParseCompositionText(string text)
        {
            var members = new List<SpawnMember>();
            if (string.IsNullOrEmpty(text))
                return members.ToArray();
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0)
                    continue;
                int sep = p.LastIndexOf('×');
                if (sep < 0)
                    sep = p.LastIndexOf('x');
                if (sep < 0)
                    sep = p.LastIndexOf('X');
                string name = sep > 0 ? p.Substring(0, sep).Trim() : p;
                int count = 1;
                if (sep > 0 && sep < p.Length - 1)
                    count = CsvTable.ToInt(p.Substring(sep + 1), 1);
                if (count < 1)
                    count = 1;
                members.Add(new SpawnMember
                {
                    KindId = KindIdFromChinese(name),
                    Count = count
                });
            }

            return members.ToArray();
        }

        public static int CountMembers(SpawnMember[] members)
        {
            if (members == null)
                return 0;
            int n = 0;
            for (int i = 0; i < members.Length; i++)
                n += members[i].Count;
            return n;
        }

        public static string FormatMembers(SpawnMember[] members)
        {
            if (members == null || members.Length == 0)
                return "";
            var sb = new StringBuilder();
            for (int i = 0; i < members.Length; i++)
            {
                if (i > 0)
                    sb.Append('+');
                sb.Append(members[i].KindId).Append('×').Append(members[i].Count);
            }

            return sb.ToString();
        }

        public static SpawnMember[] CloneMembers(SpawnMember[] src)
        {
            if (src == null)
                return Array.Empty<SpawnMember>();
            var dst = new SpawnMember[src.Length];
            Array.Copy(src, dst, src.Length);
            return dst;
        }

        static void ParseStats(CsvTable table)
        {
            foreach (string[] row in table.DataRows())
            {
                string enemy = table.Get(row, "enemy");
                if (string.IsNullOrEmpty(enemy) || enemy.IndexOf("突刺", StringComparison.Ordinal) >= 0)
                    continue;
                if (enemy.IndexOf("普通/狗", StringComparison.Ordinal) >= 0)
                    continue;
                string kind = KindIdFromChinese(enemy);
                float move = CsvTable.ToFloat(table.Get(row, "move_spd"), 0f);
                float orb = CsvTable.ToFloat(table.Get(row, "orb_spd"), 0f);
                if (enemy.IndexOf("举盾", StringComparison.Ordinal) >= 0)
                {
                    ApplyMove(kind, 0f, move, true, 0f);
                    continue;
                }

                int mid = CsvTable.ToInt(table.Get(row, "hp_mid"), 0);
                if (mid <= 0)
                    continue;
                var stat = new DraftEnemyStat
                {
                    KindId = kind,
                    DisplayName = enemy,
                    Ttk0B = CsvTable.ToFloat(table.Get(row, "ttk_s_0B"), 0f),
                    HpMid = mid,
                    HpLo = CsvTable.ToInt(table.Get(row, "hp_lo"), mid),
                    HpHi = CsvTable.ToInt(table.Get(row, "hp_hi"), mid),
                    Atk = CsvTable.ToFloat(table.Get(row, "atk"), 0f),
                    AtkKind = table.Get(row, "atk_kind"),
                    MoveSpeed = move,
                    OrbSpeed = orb
                };
                if (!Stats.ContainsKey(kind))
                    Stats[kind] = stat;
                else
                    ApplyMove(kind, move, 0f, false, orb);
            }
        }

        static void ParseMove(CsvTable table)
        {
            foreach (string[] row in table.DataRows())
            {
                string enemy = table.Get(row, "enemy");
                if (string.IsNullOrEmpty(enemy))
                    continue;
                string kind = KindIdFromChinese(enemy);
                string state = (table.Get(row, "state") ?? "").ToLowerInvariant();
                float move = CsvTable.ToFloat(table.Get(row, "move_spd"), 0f);
                float orb = CsvTable.ToFloat(table.Get(row, "orb_spd"), 0f);
                bool shielded = state.IndexOf("shield", StringComparison.Ordinal) >= 0
                    && state.IndexOf("unshield", StringComparison.Ordinal) < 0;
                ApplyMove(kind, shielded ? 0f : move, shielded ? move : 0f, shielded, orb);
            }
        }

        static void ApplyMove(string kind, float move, float shieldedMove, bool shielded, float orb)
        {
            DraftEnemyStat s;
            if (!Stats.TryGetValue(kind, out s))
            {
                s = new DraftEnemyStat { KindId = kind };
            }

            if (!shielded && move > 0.01f)
                s.MoveSpeed = move;
            if (shieldedMove > 0.01f)
                s.ShieldedMoveSpeed = shieldedMove;
            if (orb > 0.01f)
                s.OrbSpeed = orb;
            Stats[kind] = s;
        }

        static void ParseComps(CsvTable table)
        {
            foreach (string[] row in table.DataRows())
            {
                string id = table.Get(row, "comp_id");
                if (string.IsNullOrEmpty(id))
                    continue;
                string notes = table.Get(row, "notes");
                string enh = table.Get(row, "enh_rule");
                var c = new DraftCompositionRow
                {
                    Phase = CsvTable.ToInt(table.Get(row, "phase"), 1),
                    CompId = id,
                    Tier = (table.Get(row, "tier") ?? "").ToLowerInvariant(),
                    CompLabel = table.Get(row, "comp_label"),
                    NTotal = CsvTable.ToInt(table.Get(row, "n_total"), 0),
                    CompositionText = table.Get(row, "composition"),
                    UseRoom = table.Get(row, "use_room"),
                    EnhRule = enh,
                    Notes = notes,
                    EliteMark = string.Equals(notes, "elite", StringComparison.OrdinalIgnoreCase)
                        || (enh != null && enh.IndexOf("精英", StringComparison.Ordinal) >= 0
                            && notes != null && notes.IndexOf("elite", StringComparison.OrdinalIgnoreCase) >= 0),
                    CountPlusOne = string.Equals(notes, "count", StringComparison.OrdinalIgnoreCase)
                };
                c.Members = ParseCompositionText(c.CompositionText);
                if (c.NTotal <= 0)
                    c.NTotal = CountMembers(c.Members);
                Comps.Add(c);
            }
        }

        static int RoundMul(int value, float mul)
        {
            int v = (int)(value * mul + 0.5f);
            return v < 1 ? 1 : v;
        }
    }
}
