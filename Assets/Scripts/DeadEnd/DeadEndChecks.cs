using System;
using RogueShooter.Build;
using RogueShooter.Layout;

namespace RogueShooter.DeadEnd
{
    public static class DeadEndChecks
    {
        /// <summary>Catalog + resolver + once-each tracker. Returns null on pass.</summary>
        public static string Run()
        {
            if (EconomyGold.EmptySoftMin != 0 || EconomyGold.EmptySoftMax != 0)
                return "EmptySoft gold must stay 0–0";
            if (DeadEndEventTypes.MobWaveCountMin != 1 || DeadEndEventTypes.MobWaveCountMax != 3)
                return "MobWave count must stay 1–3";
            if (DeadEndEventTypes.AcceptanceLine.IndexOf("[DeadEnd]", StringComparison.Ordinal) < 0)
                return "acceptance line must keep [DeadEnd] prefix";

            var seen = new System.Collections.Generic.HashSet<DeadEndEventType>();
            for (int i = 0; i < DeadEndEventTypes.RequiredIds.Length; i++)
            {
                string id = DeadEndEventTypes.RequiredIds[i];
                if (!LockSiteCatalog.TryGet(id, out SiteDef site))
                    return "missing " + id;
                if (site.Kind != SiteKind.DeadEnd)
                    return id + " must stay SiteKind.DeadEnd";
                if (!DeadEndEventTypes.TryResolve(site, out DeadEndEventType type))
                    return id + " note must resolve a playable type (got '" + site.Note + "')";
                if (type != DeadEndEventTypes.ExpectedForId(id))
                    return id + " expected " + DeadEndEventTypes.ExpectedForId(id) + " got " + type;
                if (!seen.Add(type))
                    return id + " duplicate type " + type;
            }

            if (seen.Count != 4)
                return "need four distinct DeadEnd types";
            // 点位预配置：类型只来自 Note，禁止局内 roll 出第五型或换型。
            if (DeadEndEventTypes.FromNote("ChestReveal") != DeadEndEventType.ChestReveal
                || DeadEndEventTypes.FromNote("MobWave") != DeadEndEventType.MobWave
                || DeadEndEventTypes.FromNote("StaticRoom") != DeadEndEventType.StaticRoom
                || DeadEndEventTypes.FromNote("EmptySoft") != DeadEndEventType.EmptySoft)
                return "note tokens must map 1:1 (no type roll)";

            // Coordinates stay HOOKS-locked (do not rewrite).
            if (!PosOk("DE01", 26f, 28f) || !PosOk("DE02", -14f, 48f)
                || !PosOk("DE03", 26f, 52f) || !PosOk("DE04", -14f, 56f))
                return "HOOKS DeadEnd coordinates changed";

            var once = new DeadEndOnceTracker();
            if (once.TryMark("DE01", DeadEndEventType.Unknown))
                return "unknown type must not mark";
            if (!once.TryMark("DE01", DeadEndEventType.ChestReveal))
                return "first DE01 mark";
            if (once.TryMark("DE01", DeadEndEventType.ChestReveal))
                return "DE01 must fire once only";
            if (once.ConsumeAcceptanceIfReady() != null)
                return "acceptance must wait for all four types";
            if (!once.TryMark("DE02", DeadEndEventType.MobWave)
                || !once.TryMark("DE03", DeadEndEventType.EmptySoft)
                || !once.TryMark("DE04", DeadEndEventType.StaticRoom))
                return "remaining three marks";
            if (!once.AllFourTypesOnce)
                return "tracker should report four types";
            string pass = once.ConsumeAcceptanceIfReady();
            if (pass != DeadEndEventTypes.AcceptanceLine)
                return "acceptance line mismatch";
            if (once.ConsumeAcceptanceIfReady() != null)
                return "acceptance must log once";

            return null;
        }

        static bool PosOk(string id, float x, float y)
        {
            if (!LockSiteCatalog.TryGet(id, out SiteDef site))
                return false;
            return Math.Abs(site.Position.x - x) < 0.001f && Math.Abs(site.Position.y - y) < 0.001f;
        }
    }
}
