using System;
using System.Collections.Generic;
using System.Text;
using RogueShooter.Ai;
using RogueShooter.Player;

namespace RogueShooter.Spawning
{
    /// <summary>Returns null on pass. DRAFT stage pools + enhance rules + play-feel guards.</summary>
    public static class StageEnemyPoolChecks
    {
        public static string Run()
        {
            if (!EnemyPoolDraft.EnsureLoaded())
                return "draft CSV load: " + EnemyPoolDraft.LoadError;

            string kinds1 = StageEnemyPool.FormatKinds(StageId.S1);
            if (kinds1 != "E1,E2,E3")
                return "S1 kinds expect E1,E2,E3 got " + kinds1;
            string kinds2 = StageEnemyPool.FormatKinds(StageId.S2);
            if (kinds2 != "E1,E2,E3,SHIELD")
                return "S2 kinds expect E1,E2,E3,SHIELD got " + kinds2;
            string kinds3 = StageEnemyPool.FormatKinds(StageId.S3);
            if (kinds3 != "E1,E2,E3,SHIELD,GRAND")
                return "S3 kinds expect E1,E2,E3,SHIELD,GRAND got " + kinds3;

            if (StageEnemyPool.HasKind(StageId.S1, EnemyKindIds.Shield)
                || StageEnemyPool.HasKind(StageId.S1, EnemyKindIds.GrandMage))
                return "S1 must not include SHIELD/GRAND";
            if (!StageEnemyPool.HasKind(StageId.S2, EnemyKindIds.CultMage)
                || !StageEnemyPool.HasKind(StageId.S2, EnemyKindIds.Shield))
                return "S2 pool is lunge-normal + paired dogs + shield + cult mage";
            if (!StageEnemyPool.HasKind(StageId.S3, EnemyKindIds.GrandMage)
                || !StageEnemyPool.HasKind(StageId.S3, EnemyKindIds.CultMage))
                return "S3 must include GRAND + earlier kinds";

            string nErr = CheckTier(StageId.S1, false, 5);
            if (nErr != null) return nErr;
            nErr = CheckTier(StageId.S1, true, 5);
            if (nErr != null) return nErr;
            nErr = CheckTier(StageId.S2, false, 5);
            if (nErr != null) return nErr;
            nErr = CheckTier(StageId.S2, true, 5);
            if (nErr != null) return nErr;
            nErr = CheckTier(StageId.S3, false, 5);
            if (nErr != null) return nErr;
            nErr = CheckTier(StageId.S3, true, 5);
            if (nErr != null) return nErr;

            string enhErr = CheckEnhancePairs();
            if (enhErr != null) return enhErr;

            if (EnemyCombatRules.CanLunge(EnemyKindIds.Normal, StageId.S1))
                return "S1 normal must not lunge";
            if (!EnemyCombatRules.CanLunge(EnemyKindIds.Normal, StageId.S2)
                || !EnemyCombatRules.CanLunge(EnemyKindIds.Normal, StageId.S3))
                return "S2/S3 normal must lunge";
            if (EnemyCombatRules.CanLunge(EnemyKindIds.Dog, StageId.S2))
                return "only normal melee lunges";

            if (EnemyCombatRules.OrbCount(EnemyKindIds.CultMage) != 1)
                return "cult mage single orb";
            if (EnemyCombatRules.OrbCount(EnemyKindIds.GrandMage) != 3)
                return "grand mage 3 orbs";
            if (Math.Abs(EnemyCombatRules.GrandOrbSpreadDegrees - 15f) > 0.001f)
                return "grand spread ±15°";
            if (Math.Abs(EnemyCombatRules.OrbSpeedWalkMul - 2f) > 0.001f)
                return "orb speed = walk×2";
            if (Math.Abs(EnemyCombatRules.OrbRangeCameraWidthFrac - 0.7f) > 0.001f)
                return "orb range ≤ camera width×0.7";
            if (EnemyCombatRules.LungeDamageMinEasyStub != 30
                || EnemyCombatRules.LungeDamageMaxEasyStub != 40)
                return "lunge easy DRAFT [30,40]";
            if (EnemyPoolDraft.LungeDamageMinEasy != 30 || EnemyPoolDraft.LungeDamageMaxEasy != 40)
                return "draft lunge [30,40]";

            if (Math.Abs(EnemyCombatRules.ShieldRaiseDelaySeconds - 1f) > 0.001f)
                return "shield raise 1s";
            if (Math.Abs(EnemyCombatRules.ShieldMoveMul - 0.30f) > 0.001f)
                return "shield move −70%";
            if (Math.Abs(EnemyCombatRules.IncomingDamageMul(true, true, false) - 0.50f) > 0.001f)
                return "shield front dmg −50%";
            if (Math.Abs(EnemyCombatRules.IncomingDamageMul(true, true, true) - 1f) > 0.001f)
                return "weak-spot ignores shield front mul";
            if (Math.Abs(EnemyCombatRules.WeakSpotStaggerSeconds(EnemyKindIds.Shield) - 1.00f) > 0.001f)
                return "shield weak-spot stagger 1s";
            if (Math.Abs(ChargeShotRules.WeakSpotStaggerSeconds - 0.50f) > 0.001f)
                return "weak-spot stagger 0.5s";
            if (Math.Abs(ChargeShotRules.GreenEnterSeconds - 0.68f) > 0.001f
                || Math.Abs(ChargeShotRules.GreenExitSeconds - 0.72f) > 0.001f)
                return "weak-spot window 0.68–0.72";
            if (Math.Abs(ChargeShotRules.RingFillSeconds - 0.70f) > 0.001f)
                return "ring full @0.70";
            if (Math.Abs(ChargeShotRules.RecoverSeconds - 0.20f) > 0.001f)
                return "shot recovery 0.2s";
            if (Math.Abs(EnemyCombatRules.PlayOrthoSize - 6f) > 0.001f)
                return "play ortho 6";

            if (EnemyKindCatalog.StubHp(EnemyKindIds.Normal) != 39)
                return "DRAFT HP normal mid 39";
            if (EnemyKindCatalog.StubHp(EnemyKindIds.Dog) != 20)
                return "DRAFT HP dog mid 20";
            if (EnemyKindCatalog.StubHp(EnemyKindIds.CultMage) != 26)
                return "DRAFT HP mage mid 26";
            if (EnemyKindCatalog.StubHp(EnemyKindIds.Shield) != 52)
                return "DRAFT HP shield mid 52";
            if (EnemyKindCatalog.StubHp(EnemyKindIds.GrandMage) != 39)
                return "DRAFT HP grand mid 39";
            if (Math.Abs(EnemyPoolDraft.DraftDps0B - 13f) > 0.001f)
                return "DRAFT 0B DPS 13";

            DraftEnemyStat n1 = EnemyPoolDraft.Stat(EnemyKindIds.Normal);
            if (n1.HpMid != 39 || n1.HpLo != 35 || n1.HpHi != 43)
                return "normal HP band 35-43 around 39";
            if (EnemyKindCatalog.StubHp(EnemyKindIds.Normal) != n1.HpMid)
                return "E1 HP must not inflate by stage";

            string drawErr = CheckDrawRouting();
            if (drawErr != null)
                return drawErr;

            var rng = new Random(7);
            DrawnComposition sample = StageEnemyPool.DrawComposition(StageId.S1, CombatRoomKind.Altar, rng);
            string log = sample.LogLine();
            if (log.IndexOf("[StagePool] S1 room=Altar comp=", StringComparison.Ordinal) != 0
                || log.IndexOf(" kinds=", StringComparison.Ordinal) < 0)
                return "log contract " + log;

            return null;
        }

        static string CheckTier(StageId stage, bool enhanced, int minCount)
        {
            DraftCompositionRow[] rows = EnemyPoolDraft.CompsFor(stage, enhanced);
            if (rows == null || rows.Length < minCount)
                return StageIdUtil.Label(stage) + (enhanced ? " enhanced" : " normal")
                    + " need ≥" + minCount + " comps got " + (rows == null ? 0 : rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                DraftCompositionRow r = rows[i];
                int n = EnemyPoolDraft.CountMembers(r.Members);
                if (n != r.NTotal)
                    return r.CompId + " n_total " + r.NTotal + " != parsed " + n;
                if (!enhanced && (n < EnemyPoolDraft.WaveCountMin || n > EnemyPoolDraft.WaveCountMax))
                    return r.CompId + " wave size " + n;
                if (r.Members == null)
                    return r.CompId + " no members";
                for (int m = 0; m < r.Members.Length; m++)
                {
                    if (!StageEnemyPool.HasKind(stage, r.Members[m].KindId))
                        return r.CompId + " kind " + r.Members[m].KindId + " not in pool";
                }
            }

            return null;
        }

        static string CheckEnhancePairs()
        {
            StageId[] stages = { StageId.S1, StageId.S2, StageId.S3 };
            for (int s = 0; s < stages.Length; s++)
            {
                DraftCompositionRow[] normals = StageEnemyPool.NormalComps(stages[s]);
                for (int i = 0; i < normals.Length; i++)
                {
                    DraftCompositionRow n = normals[i];
                    string eid = n.CompId.Replace("-N", "-E");
                    DraftCompositionRow e = EnemyPoolDraft.FindComp(eid);
                    if (string.IsNullOrEmpty(e.CompId))
                        return n.CompId + " missing enhanced pair " + eid;
                    int nn = EnemyPoolDraft.CountMembers(n.Members);
                    int en = EnemyPoolDraft.CountMembers(e.Members);
                    if (nn < 4)
                    {
                        if (en != nn + 1)
                            return e.CompId + " count<4 should +1 (n=" + nn + " e=" + en + ")";
                        if (e.EliteMark)
                            return e.CompId + " count+1 must not also elite-mark";
                    }
                    else
                    {
                        if (en != nn)
                            return e.CompId + " count==4 should keep n (elite mark)";
                        if (!e.EliteMark)
                            return e.CompId + " count==4 must elite-mark";
                    }

                    if (ContainsKind(e.Members, EnemyKindIds.GrandMage)
                        && CountKind(n.Members, EnemyKindIds.GrandMage) == 1
                        && CountKind(e.Members, EnemyKindIds.GrandMage) > 1)
                        return e.CompId + " must not add a second grand mage";
                }
            }

            return null;
        }

        static string CheckDrawRouting()
        {
            var rng = new Random(11);
            for (int i = 0; i < 24; i++)
            {
                DrawnComposition n = StageEnemyPool.DrawComposition(StageId.S2, CombatRoomKind.Normal, rng);
                if (n.Tier != "normal" || n.ExtraAdded != 0)
                    return "Normal room must draw normal tier extra=0 got " + n.CompId + " " + n.Tier;
                if (n.CompId == null || n.CompId.IndexOf("-N", StringComparison.Ordinal) < 0)
                    return "Normal room comp id " + n.CompId;

                DrawnComposition a = StageEnemyPool.DrawComposition(StageId.S2, CombatRoomKind.Altar, rng);
                if (a.Tier != "enhanced" || a.ExtraAdded != 0)
                    return "Altar must draw enhanced extra=0 got " + a.CompId;
                if (a.CompId.IndexOf("-E", StringComparison.Ordinal) < 0)
                    return "Altar comp " + a.CompId;

                DrawnComposition c = StageEnemyPool.DrawComposition(StageId.S1, CombatRoomKind.Chest, rng);
                if (c.Tier != "enhanced" || c.ExtraAdded != 0)
                    return "Chest waves prefer enhanced extra=0 got " + c.CompId;

                DrawnComposition b = StageEnemyPool.DrawComposition(StageId.S3, CombatRoomKind.LargeChest, rng);
                if (b.Tier != "enhanced")
                    return "LargeChest must start from enhanced got " + b.CompId;
                if (b.ExtraAdded < 1 || b.ExtraAdded > 2)
                    return "LargeChest extra +1～2 got " + b.ExtraAdded;
                DraftCompositionRow src = EnemyPoolDraft.FindComp(b.CompId);
                int baseN = EnemyPoolDraft.CountMembers(src.Members);
                if (b.UnitCount != baseN + b.ExtraAdded)
                    return "LargeChest total " + b.UnitCount + " != " + baseN + "+" + b.ExtraAdded;
            }

            return null;
        }

        static bool ContainsKind(SpawnMember[] members, string kind)
        {
            return CountKind(members, kind) > 0;
        }

        static int CountKind(SpawnMember[] members, string kind)
        {
            if (members == null)
                return 0;
            int n = 0;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].KindId == kind)
                    n += members[i].Count;
            }

            return n;
        }

        public static string FormatPass()
        {
            var sb = new StringBuilder();
            sb.Append("ACCEPTANCE PASS stage-pools ");
            sb.Append("S1=").Append(StageEnemyPool.FormatKinds(StageId.S1));
            sb.Append(" S2=").Append(StageEnemyPool.FormatKinds(StageId.S2));
            sb.Append(" S3=").Append(StageEnemyPool.FormatKinds(StageId.S3));
            sb.Append(" comps=5n+5e/stage room=Normal→N Altar/Chest→E LargeChest→E+1..2");
            sb.Append(" enhance=<4:+1 ==4:elite×HP1.25/atk1.15");
            sb.Append(" draftHp=E1:39,E2:20,E3:26,SHIELD:52,GRAND:39 dps0b=13 thrust=[30,40]");
            sb.Append(" baseAttrStageIndependent DRAFT_NOT_LOCKED");
            return sb.ToString();
        }

        public static List<string> SampleKindLines()
        {
            var rng = new Random(3);
            var lines = new List<string>();
            StageId[] stages = { StageId.S1, StageId.S2, StageId.S3 };
            CombatRoomKind[] rooms =
            {
                CombatRoomKind.Normal, CombatRoomKind.Altar, CombatRoomKind.Chest, CombatRoomKind.LargeChest
            };
            for (int s = 0; s < stages.Length; s++)
            {
                for (int r = 0; r < rooms.Length; r++)
                    lines.Add(StageEnemyPool.DrawComposition(stages[s], rooms[r], rng).LogLine());
            }

            return lines;
        }
    }

    /// <summary>Alias so older call sites compile.</summary>
    public static class EnemyStagePoolChecks
    {
        public static string Run()
        {
            return StageEnemyPoolChecks.Run();
        }

        public static string FormatPass()
        {
            return StageEnemyPoolChecks.FormatPass();
        }
    }
}
