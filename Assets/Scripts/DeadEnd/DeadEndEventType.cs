using RogueShooter.Layout;

namespace RogueShooter.DeadEnd
{
    /// <summary>死路四类型 — 点位预配置写死在 SiteDef.Note，不做局内 roll，不扩第五种。</summary>
    public enum DeadEndEventType
    {
        Unknown = 0,
        ChestReveal,
        MobWave,
        StaticRoom,
        EmptySoft
    }

    public static class DeadEndEventTypes
    {
        public const string AcceptanceLine = "[DeadEnd] ACCEPTANCE PASS DeadEnd four-types once each";

        /// <summary>MobWave 只数区间（设计定稿）；不改数值表，只用现有怪。</summary>
        public const int MobWaveCountMin = 1;
        public const int MobWaveCountMax = 3;

        public static readonly string[] RequiredIds = { "DE01", "DE02", "DE03", "DE04" };

        public static DeadEndEventType FromNote(string note)
        {
            if (string.IsNullOrEmpty(note))
                return DeadEndEventType.Unknown;
            if (ContainsToken(note, "ChestReveal"))
                return DeadEndEventType.ChestReveal;
            if (ContainsToken(note, "MobWave"))
                return DeadEndEventType.MobWave;
            if (ContainsToken(note, "StaticRoom"))
                return DeadEndEventType.StaticRoom;
            if (ContainsToken(note, "EmptySoft"))
                return DeadEndEventType.EmptySoft;
            if (ContainsToken(note, "Static"))
                return DeadEndEventType.StaticRoom;
            if (ContainsToken(note, "Reveal"))
                return DeadEndEventType.ChestReveal;
            return DeadEndEventType.Unknown;
        }

        public static DeadEndEventType FromSite(SiteDef site)
        {
            return FromNote(site.Note);
        }

        public static bool TryResolve(SiteDef site, out DeadEndEventType type)
        {
            type = FromSite(site);
            return site.Kind == SiteKind.DeadEnd && IsPlayable(type);
        }

        public static bool IsPlayable(DeadEndEventType type)
        {
            return type == DeadEndEventType.ChestReveal
                || type == DeadEndEventType.MobWave
                || type == DeadEndEventType.StaticRoom
                || type == DeadEndEventType.EmptySoft;
        }

        public static DeadEndEventType ExpectedForId(string id)
        {
            switch (id)
            {
                case "DE01": return DeadEndEventType.ChestReveal;
                case "DE02": return DeadEndEventType.MobWave;
                case "DE03": return DeadEndEventType.EmptySoft;
                case "DE04": return DeadEndEventType.StaticRoom;
                default: return DeadEndEventType.Unknown;
            }
        }

        static bool ContainsToken(string note, string token)
        {
            return note.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
