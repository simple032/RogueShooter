using System.Collections.Generic;
using UnityEngine;

namespace RogueShooter.Layout
{
    public enum SiteKind
    {
        Start,
        Chest,
        Altar,
        Hub,
        DeadEnd,
        Shop,
        Pre,
        Boss,
        Switch,
        Combat
    }

    public enum RouteId
    {
        Shared,
        Alpha,
        Beta,
        Gamma,
        Pre,
        Boss
    }

    public readonly struct SiteDef
    {
        public readonly string Id;
        public readonly Vector2 Position;
        public readonly SiteKind Kind;
        public readonly RouteId Route;
        public readonly string Note;
        public readonly float NoSpawnRadius;

        public SiteDef(string id, Vector2 position, SiteKind kind, RouteId route, string note = "", float noSpawnRadius = 0f)
        {
            Id = id;
            Position = position;
            Kind = kind;
            Route = route;
            Note = note;
            NoSpawnRadius = noSpawnRadius;
        }

        public bool IsNoSpawnCore => NoSpawnRadius > 0f;
    }

    public readonly struct CorridorDef
    {
        public readonly string FromId;
        public readonly string ToId;
        public readonly string BandId;

        public CorridorDef(string fromId, string toId, string bandId)
        {
            FromId = fromId;
            ToId = toId;
            BandId = bandId;
        }
    }

    public readonly struct RoomDef
    {
        public readonly string Id;
        public readonly Vector2 Min;
        public readonly Vector2 Size;

        public RoomDef(string id, Vector2 min, Vector2 size)
        {
            Id = id;
            Min = min;
            Size = size;
        }

        public Vector2 Center => Min + Size * 0.5f;
    }

    /// <summary>
    /// v0.4.1 HOOKS_v041_LOCKED interaction points. Coordinates are exact; no gameplay offset.
    /// Origin = START SW corner (0,0); +X east, +Y north toward BOSS.
    /// </summary>
    public static class LockSiteCatalog
    {
        public const float CorridorWidth = 5.5f;
        public const string HooksSource = "HOOKS_v041_LOCKED.md";

        public static readonly RoomDef[] Rooms =
        {
            new RoomDef("START", new Vector2(0f, 0f), new Vector2(8f, 5f)),
            new RoomDef("C1", new Vector2(0f, 7f), new Vector2(12f, 8f)),
            new RoomDef("HubA", new Vector2(1f, 17f), new Vector2(10f, 6f)),
            new RoomDef("A_Shared", new Vector2(-6f, 17f), new Vector2(6f, 5f)),
            new RoomDef("PreBoss", new Vector2(1f, 65.5f), new Vector2(10f, 5f)),
        };

        public static readonly SiteDef[] Sites =
        {
            new SiteDef("START", new Vector2(4f, 2.5f), SiteKind.Start, RouteId.Shared, "SW origin (0,0); interact center", 4f),
            new SiteDef("Chest_01", new Vector2(6f, 11f), SiteKind.Chest, RouteId.Shared, "C01 必出", 2f),
            new SiteDef("HubA", new Vector2(6f, 20f), SiteKind.Hub, RouteId.Shared, "三分流", 3.5f),
            new SiteDef("Chest_02", new Vector2(6f, 20f), SiteKind.Chest, RouteId.Shared, "C02 HubA内", 2f),
            new SiteDef("A_Shared", new Vector2(-3f, 19.5f), SiteKind.Altar, RouteId.Shared, "HubA西邻 必出", 2.5f),
            new SiteDef("Anchor_S1_End", new Vector2(6f, 23.5f), SiteKind.Switch, RouteId.Shared, "HubA north S1→S2"),

            new SiteDef("A2", new Vector2(-8f, 28f), SiteKind.Altar, RouteId.Alpha, "必出", 2.5f),
            new SiteDef("Chest_04", new Vector2(-8f, 34f), SiteKind.Chest, RouteId.Alpha, "C04", 2f),
            new SiteDef("HubB", new Vector2(-8f, 48f), SiteKind.Hub, RouteId.Alpha, "≈6′", 3.5f),
            new SiteDef("Chest_06", new Vector2(-8f, 48f), SiteKind.Chest, RouteId.Alpha, "C06 HubB中心", 2f),
            new SiteDef("DE02", new Vector2(-14f, 48f), SiteKind.DeadEnd, RouteId.Alpha, "MobWave"),
            new SiteDef("Anchor_S2_End_α", new Vector2(-8f, 51.5f), SiteKind.Switch, RouteId.Alpha, "HubB north S2→S3"),
            new SiteDef("A3", new Vector2(-8f, 56f), SiteKind.Altar, RouteId.Alpha, "必出", 2.5f),
            new SiteDef("DE04", new Vector2(-14f, 56f), SiteKind.DeadEnd, RouteId.Alpha, "StaticRoom"),
            new SiteDef("Chest_05", new Vector2(-8f, 62f), SiteKind.Chest, RouteId.Alpha, "C05 段④入口", 2f),

            new SiteDef("A4", new Vector2(6f, 28f), SiteKind.Altar, RouteId.Gamma, "必出", 2.5f),
            new SiteDef("Chest_10", new Vector2(6f, 34f), SiteKind.Chest, RouteId.Gamma, "C10", 2f),
            new SiteDef("Room_γCombat", new Vector2(6f, 40f), SiteKind.Combat, RouteId.Gamma, "刷怪环"),
            new SiteDef("Chest_11", new Vector2(6f, 46f), SiteKind.Chest, RouteId.Gamma, "C11", 2f),
            new SiteDef("Anchor_S2_End_γ", new Vector2(6f, 50f), SiteKind.Switch, RouteId.Gamma, "A6南口前 S2→S3"),
            new SiteDef("A6", new Vector2(6f, 52f), SiteKind.Altar, RouteId.Gamma, "必出", 2.5f),
            new SiteDef("Chest_12", new Vector2(6f, 58f), SiteKind.Chest, RouteId.Gamma, "C12", 2f),

            new SiteDef("A1", new Vector2(20f, 28f), SiteKind.Altar, RouteId.Beta, "必出", 2.5f),
            new SiteDef("DE01", new Vector2(26f, 28f), SiteKind.DeadEnd, RouteId.Beta, "ChestReveal"),
            new SiteDef("Chest_07", new Vector2(20f, 34f), SiteKind.Chest, RouteId.Beta, "C07", 2f),
            new SiteDef("Chest_08", new Vector2(20f, 46f), SiteKind.Chest, RouteId.Beta, "C08", 2f),
            new SiteDef("Anchor_S2_End_β", new Vector2(20f, 50f), SiteKind.Switch, RouteId.Beta, "A5南口前 S2→S3"),
            new SiteDef("A5", new Vector2(20f, 52f), SiteKind.Altar, RouteId.Beta, "必出", 2.5f),
            new SiteDef("DE03", new Vector2(26f, 52f), SiteKind.DeadEnd, RouteId.Beta, "EmptySoft"),
            new SiteDef("Chest_09", new Vector2(20f, 58f), SiteKind.Chest, RouteId.Beta, "C09", 2f),

            new SiteDef("PreBoss", new Vector2(6f, 68f), SiteKind.Pre, RouteId.Pre, "整备 禁刷核大", 4f),
            new SiteDef("Chest_03", new Vector2(6f, 68f), SiteKind.Chest, RouteId.Pre, "C03 PreOnly", 2f),
            new SiteDef("Shop_01", new Vector2(-2f, 68f), SiteKind.Shop, RouteId.Pre, "不计 Build", 3f),
            new SiteDef("Anchor_S3_End", new Vector2(6f, 71f), SiteKind.Switch, RouteId.Pre, "Pre north S3→BOSS"),
            new SiteDef("BOSS", new Vector2(6f, 76f), SiteKind.Boss, RouteId.Boss, "不挂 SpawnBand; 全房禁刷 stub r=4", 4f),
        };

        public static readonly CorridorDef[] Corridors =
        {
            new CorridorDef("START", "Chest_01", "Z1"),
            new CorridorDef("Chest_01", "HubA", "Z1"),
            new CorridorDef("HubA", "A_Shared", "Z1"),

            new CorridorDef("HubA", "A2", "Z2"),
            new CorridorDef("A2", "Chest_04", "Z2"),
            new CorridorDef("Chest_04", "HubB", "Z2"),
            new CorridorDef("HubB", "DE02", "Z2"),
            new CorridorDef("HubB", "Anchor_S2_End_α", "Z2"),

            new CorridorDef("HubA", "A4", "Z2"),
            new CorridorDef("A4", "Chest_10", "Z2"),
            new CorridorDef("Chest_10", "Room_γCombat", "Z2"),
            new CorridorDef("Room_γCombat", "Chest_11", "Z2"),
            new CorridorDef("Chest_11", "Anchor_S2_End_γ", "Z2"),

            new CorridorDef("HubA", "A1", "Z2"),
            new CorridorDef("A1", "Chest_07", "Z2"),
            new CorridorDef("Chest_07", "Chest_08", "Z2"),
            new CorridorDef("Chest_08", "Anchor_S2_End_β", "Z2"),
            new CorridorDef("A1", "DE01", "Z2"),

            new CorridorDef("Anchor_S2_End_α", "A3", "Z3"),
            new CorridorDef("A3", "Chest_05", "Z3"),
            new CorridorDef("Chest_05", "PreBoss", "Z3"),
            new CorridorDef("A3", "DE04", "Z3"),

            new CorridorDef("Anchor_S2_End_γ", "A6", "Z3"),
            new CorridorDef("A6", "Chest_12", "Z3"),
            new CorridorDef("Chest_12", "PreBoss", "Z3"),

            new CorridorDef("Anchor_S2_End_β", "A5", "Z3"),
            new CorridorDef("A5", "Chest_09", "Z3"),
            new CorridorDef("Chest_09", "PreBoss", "Z3"),
            new CorridorDef("A5", "DE03", "Z3"),

            new CorridorDef("PreBoss", "Shop_01", "Pre"),
            new CorridorDef("PreBoss", "Anchor_S3_End", "Pre"),
            new CorridorDef("Anchor_S3_End", "BOSS", "Pre"),
        };

        public static readonly string[] RequiredShared = { "Chest_01", "Chest_02", "A_Shared", "Chest_03", "HubA", "START" };
        public static readonly string[] RequiredAlpha = { "A2", "A3", "Chest_04", "Chest_06", "Chest_05", "DE02", "DE04" };
        public static readonly string[] RequiredBeta = { "A1", "A5", "Chest_07", "Chest_08", "Chest_09", "DE01", "DE03" };
        public static readonly string[] RequiredGamma = { "A4", "A6", "Chest_10", "Chest_11", "Chest_12" };
        public static readonly string[] RequiredPre = { "Shop_01", "PreBoss", "BOSS" };
        public static readonly string[] RequiredSwitches =
        {
            "Anchor_S1_End", "Anchor_S2_End_α", "Anchor_S2_End_β", "Anchor_S2_End_γ", "Anchor_S3_End"
        };

        static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "C01", "Chest_01" }, { "C02", "Chest_02" }, { "C03", "Chest_03" },
            { "C04", "Chest_04" }, { "C05", "Chest_05" }, { "C06", "Chest_06" },
            { "C07", "Chest_07" }, { "C08", "Chest_08" }, { "C09", "Chest_09" },
            { "C10", "Chest_10" }, { "C11", "Chest_11" }, { "C12", "Chest_12" },
            { "Pre", "PreBoss" },
            { "Z1_End", "Anchor_S1_End" },
            { "Z2_End", "Anchor_S2_End_α" },
            { "Z2_End_beta", "Anchor_S2_End_β" },
            { "Z2_End_gamma", "Anchor_S2_End_γ" },
            { "Z3_End", "Anchor_S3_End" },
        };

        public static string CanonicalId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return id;
            string mapped;
            return Aliases.TryGetValue(id, out mapped) ? mapped : id;
        }

        public static bool TryGet(string id, out SiteDef site)
        {
            string canonical = CanonicalId(id);
            for (int i = 0; i < Sites.Length; i++)
            {
                if (Sites[i].Id == canonical || Sites[i].Id == id)
                {
                    site = Sites[i];
                    return true;
                }
            }

            site = default;
            return false;
        }

        public static IEnumerable<string> MissingRequiredIds()
        {
            string[][] groups =
            {
                RequiredShared, RequiredAlpha, RequiredBeta, RequiredGamma, RequiredPre, RequiredSwitches
            };
            for (int g = 0; g < groups.Length; g++)
            {
                for (int i = 0; i < groups[g].Length; i++)
                {
                    if (!TryGet(groups[g][i], out _))
                        yield return groups[g][i];
                }
            }
        }

        public static Color ColorFor(SiteKind kind)
        {
            switch (kind)
            {
                case SiteKind.Chest: return new Color(0.92f, 0.72f, 0.22f);
                case SiteKind.Altar: return new Color(0.62f, 0.38f, 0.92f);
                case SiteKind.Hub: return new Color(0.25f, 0.78f, 0.92f);
                case SiteKind.DeadEnd: return new Color(0.45f, 0.45f, 0.48f);
                case SiteKind.Shop: return new Color(0.28f, 0.82f, 0.48f);
                case SiteKind.Pre: return new Color(0.95f, 0.55f, 0.22f);
                case SiteKind.Boss: return new Color(0.86f, 0.22f, 0.22f);
                case SiteKind.Switch: return new Color(0.95f, 0.95f, 0.95f);
                case SiteKind.Combat: return new Color(0.55f, 0.22f, 0.22f);
                default: return new Color(0.85f, 0.85f, 0.55f);
            }
        }
    }
}
