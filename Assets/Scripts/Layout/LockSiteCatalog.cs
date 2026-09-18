using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Spawning;

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
        Switch
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

        public SiteDef(string id, Vector2 position, SiteKind kind, RouteId route, string note = "")
        {
            Id = id;
            Position = position;
            Kind = kind;
            Route = route;
            Note = note;
        }
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

    /// <summary>
    /// Minimal α/β/γ → Pre → BOSS scaffold. IDs from v0.4.1 lock; positions are placeholders.
    /// Node spacing ≥ 10 u; corridor width 5.5.
    /// </summary>
    public static class LockSiteCatalog
    {
        public const float CorridorWidth = 5.5f;

        public static readonly SiteDef[] Sites =
        {
            new SiteDef("START", new Vector2(0f, 0f), SiteKind.Start, RouteId.Shared),
            new SiteDef("C01", new Vector2(12f, 0f), SiteKind.Chest, RouteId.Shared),
            new SiteDef("C02", new Vector2(24f, 0f), SiteKind.Chest, RouteId.Shared),
            new SiteDef("A_Shared", new Vector2(36f, 0f), SiteKind.Altar, RouteId.Shared),
            new SiteDef("HubA", new Vector2(48f, 0f), SiteKind.Hub, RouteId.Shared, "Z1_End"),
            new SiteDef("Z1_End", new Vector2(48f, 0f), SiteKind.Switch, RouteId.Shared, "Z1_End@HubA"),

            new SiteDef("A2", new Vector2(48f, 22f), SiteKind.Altar, RouteId.Alpha),
            new SiteDef("A3", new Vector2(62f, 22f), SiteKind.Altar, RouteId.Alpha),
            new SiteDef("C06", new Vector2(76f, 22f), SiteKind.Chest, RouteId.Alpha),
            new SiteDef("HubB", new Vector2(90f, 22f), SiteKind.Hub, RouteId.Alpha, "C04 / Z2_End"),
            new SiteDef("C04", new Vector2(90f, 22f), SiteKind.Chest, RouteId.Alpha, "Chest_04@HubB"),
            new SiteDef("Z2_End", new Vector2(90f, 22f), SiteKind.Switch, RouteId.Alpha, "Z2_End@HubB"),
            new SiteDef("C05", new Vector2(104f, 22f), SiteKind.Chest, RouteId.Alpha, "α→Pre"),
            new SiteDef("DE02", new Vector2(90f, 34f), SiteKind.DeadEnd, RouteId.Alpha, "MobWave"),
            new SiteDef("DE04", new Vector2(76f, 34f), SiteKind.DeadEnd, RouteId.Alpha, "StaticRoom"),

            new SiteDef("A1", new Vector2(62f, 0f), SiteKind.Altar, RouteId.Beta),
            new SiteDef("C07", new Vector2(76f, 0f), SiteKind.Chest, RouteId.Beta),
            new SiteDef("Z2_End_beta", new Vector2(90f, 0f), SiteKind.Switch, RouteId.Beta, "Z2_End@A5前"),
            new SiteDef("A5", new Vector2(104f, 0f), SiteKind.Altar, RouteId.Beta),
            new SiteDef("C08", new Vector2(118f, 0f), SiteKind.Chest, RouteId.Beta),
            new SiteDef("C09", new Vector2(132f, 0f), SiteKind.Chest, RouteId.Beta),
            new SiteDef("DE01", new Vector2(76f, -11f), SiteKind.DeadEnd, RouteId.Beta, "ChestReveal"),
            new SiteDef("DE03", new Vector2(118f, -11f), SiteKind.DeadEnd, RouteId.Beta, "EmptySoft"),

            new SiteDef("A4", new Vector2(48f, -22f), SiteKind.Altar, RouteId.Gamma),
            new SiteDef("C10", new Vector2(62f, -22f), SiteKind.Chest, RouteId.Gamma),
            new SiteDef("Z2_End_gamma", new Vector2(76f, -22f), SiteKind.Switch, RouteId.Gamma, "Z2_End@A6前"),
            new SiteDef("A6", new Vector2(90f, -22f), SiteKind.Altar, RouteId.Gamma),
            new SiteDef("C11", new Vector2(104f, -22f), SiteKind.Chest, RouteId.Gamma),
            new SiteDef("C12", new Vector2(118f, -22f), SiteKind.Chest, RouteId.Gamma),

            new SiteDef("Pre", new Vector2(148f, 0f), SiteKind.Pre, RouteId.Pre, "Z3_End"),
            new SiteDef("Z3_End", new Vector2(148f, 0f), SiteKind.Switch, RouteId.Pre, "Z3_End@Pre entrance"),
            new SiteDef("C03", new Vector2(160f, 0f), SiteKind.Chest, RouteId.Pre, "PreOnly"),
            new SiteDef("Shop_01", new Vector2(160f, 12f), SiteKind.Shop, RouteId.Pre, "not in Build"),
            new SiteDef("BOSS", new Vector2(176f, 0f), SiteKind.Boss, RouteId.Boss),
        };

        public static readonly CorridorDef[] Corridors =
        {
            new CorridorDef("START", "C01", "Z1"),
            new CorridorDef("C01", "C02", "Z1"),
            new CorridorDef("C02", "A_Shared", "Z1"),
            new CorridorDef("A_Shared", "HubA", "Z1"),

            new CorridorDef("HubA", "A2", "Z2"),
            new CorridorDef("A2", "A3", "Z2"),
            new CorridorDef("A3", "C06", "Z2"),
            new CorridorDef("C06", "HubB", "Z2"),
            new CorridorDef("HubB", "DE02", "Z2"),
            new CorridorDef("C06", "DE04", "Z2"),

            new CorridorDef("HubA", "A1", "Z2"),
            new CorridorDef("A1", "C07", "Z2"),
            new CorridorDef("C07", "Z2_End_beta", "Z2"),
            new CorridorDef("Z2_End_beta", "A5", "Z2"),
            new CorridorDef("C07", "DE01", "Z2"),
            new CorridorDef("C08", "DE03", "Z2"),

            new CorridorDef("HubA", "A4", "Z2"),
            new CorridorDef("A4", "C10", "Z2"),
            new CorridorDef("C10", "Z2_End_gamma", "Z2"),
            new CorridorDef("Z2_End_gamma", "A6", "Z2"),

            new CorridorDef("HubB", "C05", "Z3"),
            new CorridorDef("C05", "Pre", "Z3"),
            new CorridorDef("A5", "C08", "Z3"),
            new CorridorDef("C08", "C09", "Z3"),
            new CorridorDef("C09", "Pre", "Z3"),
            new CorridorDef("A6", "C11", "Z3"),
            new CorridorDef("C11", "C12", "Z3"),
            new CorridorDef("C12", "Pre", "Z3"),
            new CorridorDef("Pre", "C03", "Pre"),
            new CorridorDef("Pre", "Shop_01", "Pre"),
            new CorridorDef("C03", "BOSS", "Pre"),
        };

        public static readonly string[] RequiredShared = { "C01", "C02", "A_Shared", "C03" };
        public static readonly string[] RequiredAlpha = { "A2", "A3", "C04", "C06", "C05", "DE02", "DE04" };
        public static readonly string[] RequiredBeta = { "A1", "A5", "C07", "C08", "C09", "DE01", "DE03" };
        public static readonly string[] RequiredGamma = { "A4", "A6", "C10", "C11", "C12" };
        public static readonly string[] RequiredPre = { "Shop_01" };
        public static readonly string[] RequiredSwitches = { "Z1_End", "Z2_End", "Z2_End_beta", "Z2_End_gamma", "Z3_End" };

        public static bool TryGet(string id, out SiteDef site)
        {
            for (int i = 0; i < Sites.Length; i++)
            {
                if (Sites[i].Id == id)
                {
                    site = Sites[i];
                    return true;
                }
            }

            site = default;
            return false;
        }

        public static bool IsNoSpawnKind(SiteKind kind)
        {
            return kind == SiteKind.Hub
                || kind == SiteKind.Altar
                || kind == SiteKind.Chest
                || kind == SiteKind.Shop
                || kind == SiteKind.Pre;
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
                default: return new Color(0.85f, 0.85f, 0.55f);
            }
        }
    }
}
