using System;
using System.Collections.Generic;
using System.Text;
using RogueShooter.Ai;
using RogueShooter.Player;
using RogueShooter.Vision;

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
                || !StageEnemyPool.HasKind(StageId.S2, EnemyKindIds.Shield)
                || !StageIdUtil.IncludesCultMage(StageId.S2))
                return "S2 pool is lunge-normal + paired dogs + shield + cult mage";
            string s2Mage = CheckS2CultMage();
            if (s2Mage != null)
                return s2Mage;
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

            if (EnemyCombatRules.CanLunge(EnemyKindIds.CultMage, StageId.S2)
                || EnemyCombatRules.CanLunge(EnemyKindIds.CultMage, StageId.S1))
                return "cult mage must not lunge (S1 or S2)";

            if (EnemyCombatRules.OrbCount(EnemyKindIds.CultMage) != 1)
                return "cult mage single orb";
            if (EnemyCombatRules.OrbCount(EnemyKindIds.GrandMage) != 3)
                return "grand mage 3 orbs";
            if (Math.Abs(EnemyCombatRules.GrandOrbSpreadDegrees - 15f) > 0.001f)
                return "grand spread ±15°";
            if (Math.Abs(EnemyCombatRules.OrbSpeedWalkMul - 2f) > 0.001f)
                return "orb mul is player_move×2";
            if (Math.Abs(EnemyCombatRules.OrbSpeedAbsStub - 12f) > 0.001f
                || Math.Abs(EnemyPoolDraft.DraftOrbSpeed - 12f) > 0.001f)
                return "orb speed abs 12 (player 6 × 2)";
            if (Math.Abs(EnemyCombatRules.OrbSpeedForKind(EnemyKindIds.CultMage, 3.6f) - 12f) > 0.001f)
                return "cult mage orb must be 12, not walk×2=7.2";
            if (Math.Abs(EnemyCombatRules.OrbSpeedForKind(EnemyKindIds.GrandMage, 3.3f) - 12f) > 0.001f)
                return "grand orb must be 12";
            if (Math.Abs(EnemyCombatRules.OrbMaxRange(12f) - L5Rules.OrbRangeU) > 0.001f
                || Math.Abs(L5Rules.DefaultOrbRangeU - 12f) > 0.001f)
                return "L5 orb range 12u / 1.0s (replaces camera width×0.7)";
            if (EnemyCombatRules.LungeDamageMinEasyStub != 30
                || EnemyCombatRules.LungeDamageMaxEasyStub != 40)
                return "lunge easy DRAFT [30,40]";
            if (EnemyPoolDraft.LungeDamageMinEasy != 30 || EnemyPoolDraft.LungeDamageMaxEasy != 40)
                return "draft lunge [30,40]";

            if (Math.Abs(EnemyCombatRules.ShieldRaiseDelaySeconds - 1f) > 0.001f)
                return "shield raise 1s";
            if (Math.Abs(EnemyCombatRules.ShieldMoveMul - 0.50f) > 0.001f
                || Math.Abs(EnemyPoolDraft.DraftShieldMoveMul - 0.50f) > 0.001f)
                return "shield move −50% (not −70%)";
            if (Math.Abs(EnemyCombatRules.WalkShieldedStub - 2.25f) > 0.001f
                || Math.Abs(EnemyPoolDraft.DraftShieldedMove - 2.25f) > 0.001f)
                return "shielded walk 2.25 (4.5×0.50)";
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
            if (!L5VisionChecks.OrthoMatchesMode(EnemyCombatRules.PlayOrthoSize))
                return "play ortho 6 ortho / 5.25 iso";

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

            string atkMoveErr = CheckDraftAtkMove();
            if (atkMoveErr != null)
                return atkMoveErr;

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
                if (c.Tier != "normal" || c.ExtraAdded != 0)
                    return "ordinary Chest must draw normal extra=0 got " + c.CompId + " " + c.Tier;
                if (c.CompId == null || c.CompId.IndexOf("-N", StringComparison.Ordinal) < 0)
                    return "ordinary Chest comp id " + c.CompId;
                DrawnComposition c2 = StageEnemyPool.DrawComposition(StageId.S2, CombatRoomKind.Chest, rng);
                if (c2.Tier != "normal" || c2.ExtraAdded != 0
                    || c2.CompId == null || c2.CompId.IndexOf("-N", StringComparison.Ordinal) < 0)
                    return "S2 ordinary Chest must draw normal got " + c2.CompId + " " + c2.Tier;
                if (CombatRoomKindUtil.PrefersEnhanced(CombatRoomKind.Chest)
                    || CombatRoomKindUtil.PrefersEnhanced(CombatRoomKind.Normal))
                    return "ordinary Chest/Normal must not PrefersEnhanced";
                if (!CombatRoomKindUtil.PrefersEnhanced(CombatRoomKind.Altar)
                    || !CombatRoomKindUtil.PrefersEnhanced(CombatRoomKind.LargeChest))
                    return "Altar/LargeChest must PrefersEnhanced";

                DrawnComposition b = StageEnemyPool.DrawComposition(StageId.S3, CombatRoomKind.LargeChest, rng);
                if (b.Tier != "enhanced" || b.ExtraAdded != 0)
                    return "LargeChest must draw enhanced extra=0 got " + b.CompId + " extra=" + b.ExtraAdded;
                if (b.CompId == null || b.CompId.IndexOf("-E", StringComparison.Ordinal) < 0)
                    return "LargeChest comp " + b.CompId;
                if (CombatRoomKindUtil.AddsBigChestExtra(CombatRoomKind.LargeChest))
                    return "LargeChest must not add extra units";
                DraftCompositionRow src = EnemyPoolDraft.FindComp(b.CompId);
                int baseN = EnemyPoolDraft.CountMembers(src.Members);
                if (b.UnitCount != baseN)
                    return "LargeChest total " + b.UnitCount + " != enhanced n " + baseN;
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

        /// <summary>
        /// Spec addendum: S2 pool includes 邪法师 with the same linear-orb rules as S1.
        /// </summary>
        static string CheckS2CultMage()
        {
            if (!StageEnemyPool.HasKind(StageId.S2, EnemyKindIds.CultMage))
                return "S2 pool must include cult mage (E3)";
            if (!CompsContainKind(StageEnemyPool.NormalComps(StageId.S2), EnemyKindIds.CultMage))
                return "S2 normal comps must include at least one 邪法师";
            if (!CompsContainKind(StageEnemyPool.EnhancedComps(StageId.S2), EnemyKindIds.CultMage))
                return "S2 enhanced comps must include at least one 邪法师";

            if (EnemyCombatRules.OrbCount(EnemyKindIds.CultMage) != 1)
                return "S2 cult mage same as S1: single linear orb";
            if (Math.Abs(EnemyCombatRules.OrbSpeedForKind(EnemyKindIds.CultMage, 3.6f) - 12f) > 0.001f
                || Math.Abs(EnemyCombatRules.OrbMaxRange(12f) - L5Rules.OrbRangeU) > 0.001f)
                return "S2 cult mage orb player×2=12 / L5 range 12u (same as S1)";
            if (EnemyCombatRules.CanLunge(EnemyKindIds.CultMage, StageId.S2))
                return "S2 cult mage must not lunge";

            DraftEnemyStat s1 = EnemyPoolDraft.Stat(EnemyKindIds.CultMage);
            if (Math.Abs(s1.Ttk0B - 2.0f) > 0.001f || s1.HpMid != 26 || Math.Abs(s1.Atk - 19f) > 0.001f)
                return "cult mage 0B TTK≈2s HP mid 26 atk 19 (stage-independent)";
            if (EnemyKindCatalog.StubHp(EnemyKindIds.CultMage) != 26)
                return "S2 cult mage HP must match S1 (no stage inflate)";
            if (EnemyKindCatalog.ForKind(EnemyKindIds.CultMage).RangedOrb != true)
                return "cult mage ranged orb profile";
            return null;
        }

        static bool CompsContainKind(DraftCompositionRow[] rows, string kind)
        {
            if (rows == null)
                return false;
            for (int i = 0; i < rows.Length; i++)
            {
                if (CountKind(rows[i].Members, kind) > 0)
                    return true;
            }

            return false;
        }

        public static string FormatPass()
        {
            var sb = new StringBuilder();
            sb.Append("ACCEPTANCE PASS stage-pools ");
            sb.Append("S1=").Append(StageEnemyPool.FormatKinds(StageId.S1));
            sb.Append(" S2=").Append(StageEnemyPool.FormatKinds(StageId.S2));
            sb.Append(" S3=").Append(StageEnemyPool.FormatKinds(StageId.S3));
            sb.Append(" comps=5n+5e/stage room=Normal/Chest→N Altar/LargeChest→E");
            sb.Append(" enhance=<4:+1 ==4:elite×HP1.25/atk1.15");
            sb.Append(" draftHp=E1:39,E2:20,E3:26,SHIELD:52,GRAND:39");
            sb.Append(" draftAtk=E1:25,E2:15,E3:19,SHIELD:30,GRAND:25");
            sb.Append(" draftMove=E1:4.5,E2:7.2,E3:3.6,SHIELD:4.5/2.25,GRAND:3.3");
            sb.Append(" playerMove=6 orb=12=player×2 dps0b=13 thrust=[30,40]");
            sb.Append(" S2cultMage=E3 sameS1orb ttk≈2s");
            sb.Append(" baseAttrStageIndependent DRAFT_NOT_LOCKED");
            return sb.ToString();
        }

        static string CheckDraftAtkMove()
        {
            if (Math.Abs(EnemyPoolDraft.DraftPlayerMove - 6f) > 0.001f
                || Math.Abs(EnemyCombatRules.WalkPlayerStub - 6f) > 0.001f)
                return "player move 6";

            if (!Near(EnemyPoolDraft.Atk(EnemyKindIds.Normal, false), 25f))
                return "DRAFT atk normal 25 (not 12)";
            if (!Near(EnemyPoolDraft.Atk(EnemyKindIds.Dog, false), 15f))
                return "DRAFT atk dog 15 (not 10)";
            if (!Near(EnemyPoolDraft.Atk(EnemyKindIds.CultMage, false), 19f))
                return "DRAFT atk mage 19 (not 14)";
            if (!Near(EnemyPoolDraft.Atk(EnemyKindIds.Shield, false), 30f))
                return "DRAFT atk shield 30 (not 15)";
            if (!Near(EnemyPoolDraft.Atk(EnemyKindIds.GrandMage, false), 25f))
                return "DRAFT atk grand 25 per orb (not 12)";

            if (!Near(EnemyKindCatalog.HitDamageStub(EnemyKindIds.Normal), 25f)
                || !Near(EnemyKindCatalog.HitDamageStub(EnemyKindIds.Dog), 15f)
                || !Near(EnemyKindCatalog.HitDamageStub(EnemyKindIds.CultMage), 19f)
                || !Near(EnemyKindCatalog.HitDamageStub(EnemyKindIds.Shield), 30f)
                || !Near(EnemyKindCatalog.HitDamageStub(EnemyKindIds.GrandMage), 25f))
                return "HitDamageStub must follow latest draft atk";

            if (!Near(EnemyPoolDraft.MoveSpeed(EnemyKindIds.Normal, false), 4.5f))
                return "DRAFT move normal 4.5";
            if (!Near(EnemyPoolDraft.MoveSpeed(EnemyKindIds.Dog, false), 7.2f))
                return "DRAFT move dog 7.2";
            if (!Near(EnemyPoolDraft.MoveSpeed(EnemyKindIds.CultMage, false), 3.6f))
                return "DRAFT move mage 3.6";
            if (!Near(EnemyPoolDraft.MoveSpeed(EnemyKindIds.Shield, false), 4.5f))
                return "DRAFT move shield unshielded 4.5";
            if (!Near(EnemyPoolDraft.MoveSpeed(EnemyKindIds.Shield, true), 2.25f))
                return "DRAFT move shield shielded 2.25";
            if (!Near(EnemyPoolDraft.MoveSpeed(EnemyKindIds.GrandMage, false), 3.3f))
                return "DRAFT move grand 3.3";

            if (!Near(EnemyKindCatalog.WalkSpeed(EnemyKindIds.Dog, false), 7.2f))
                return "WalkSpeed dog 7.2";
            if (!Near(EnemyKindCatalog.WalkSpeed(EnemyKindIds.Shield, true), 2.25f))
                return "WalkSpeed shield raised 2.25";

            if (!Near(EnemyPoolDraft.OrbSpeedFor(EnemyKindIds.CultMage), 12f)
                || !Near(EnemyPoolDraft.OrbSpeedFor(EnemyKindIds.GrandMage), 12f))
                return "orb_spd 12 from draft CSV";

            DraftEnemyStat dog = EnemyPoolDraft.Stat(EnemyKindIds.Dog);
            if (!Near(dog.Atk, 15f) || !Near(dog.MoveSpeed, 7.2f))
                return "dog CSV atk 15 move 7.2";
            DraftEnemyStat mage = EnemyPoolDraft.Stat(EnemyKindIds.CultMage);
            if (!Near(mage.Atk, 19f) || !Near(mage.MoveSpeed, 3.6f) || !Near(mage.OrbSpeed, 12f))
                return "mage CSV atk 19 move 3.6 orb 12";
            DraftEnemyStat shield = EnemyPoolDraft.Stat(EnemyKindIds.Shield);
            if (!Near(shield.Atk, 30f) || !Near(shield.MoveSpeed, 4.5f)
                || !Near(shield.ShieldedMoveSpeed, 2.25f))
                return "shield CSV atk 30 move 4.5 / 举盾 2.25";
            if (Near(shield.ShieldedMoveSpeed, 1.35f)
                || Near(EnemyCombatRules.ShieldMoveMul, 0.30f)
                || Near(EnemyKindCatalog.WalkSpeed(EnemyKindIds.Shield, true), 1.35f))
                return "stale shield −70% leftovers (0.30 / 1.35)";
            return null;
        }

        static bool Near(float a, float b)
        {
            return Math.Abs(a - b) <= 0.001f;
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
