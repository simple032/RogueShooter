using System.Text;
using UnityEngine;

namespace RogueShooter.RoomCombat
{
    public static class RoomCombatChecks
    {
        /// <summary>Returns null on pass, else failure reason.</summary>
        public static string Run()
        {
            if (RoomCombatRules.WantsSecondWave(RoomCombatKind.Normal))
                return "Normal must be one wave";
            if (!RoomCombatRules.WantsSecondWave(RoomCombatKind.SmallChest))
                return "SmallChest must want second wave";
            if (!RoomCombatRules.WantsSecondWave(RoomCombatKind.Altar))
                return "Altar must want second wave";
            if (RoomCombatRules.WantsSecondWave(RoomCombatKind.Corridor)
                || RoomCombatRules.UsesRoomCombat(RoomCombatKind.Corridor))
                return "Corridor must not use room combat";
            if (RoomCombatRules.ExpectedWaveCount(RoomCombatKind.Normal) != 1
                || RoomCombatRules.ExpectedWaveCount(RoomCombatKind.SmallChest) != 2
                || RoomCombatRules.ExpectedWaveCount(RoomCombatKind.Altar) != 2)
                return "wave count table mismatch §4.5";

            float r = RoomCombatRules.DefaultFootRadius;
            var ok = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(r * 2f, 0f),
                new Vector2(0f, r * 2f)
            };
            if (!RoomCombatRules.FootCirclesClear(ok, r))
                return "spaced points should pass foot-circle check";

            var overlap = new[] { new Vector2(0f, 0f), new Vector2(r * 0.5f, 0f) };
            if (RoomCombatRules.FootCirclesClear(overlap, r))
                return "overlapping foot circles must fail";

            var coincident = new[] { new Vector2(1f, 1f), new Vector2(1f, 1f) };
            if (RoomCombatRules.FootCirclesClear(coincident, r))
                return "coincident points must fail";

            var bounds = new Rect(0f, 0f, 10f, 10f);
            if (!RoomCombatRules.TryPackPoints(bounds, 4, r, out Vector2[] packed)
                || packed.Length != 4
                || !RoomCombatRules.FootCirclesClear(packed, r))
                return "pack 4 points in 10x10 must succeed";

            // Normal: pre-spawn → enter lock → clear W1 → open
            var normal = new RoomCombatBrain();
            normal.Configure(RoomCombatKind.Normal);
            if (normal.InteriorVisible || normal.DoorLocked)
                return "start: hidden + unlocked";
            normal.MarkWave1PreSpawned(2);
            if (!normal.Wave1PreSpawned || normal.Phase != RoomCombatPhase.Outside)
                return "W1 pre-spawn while still Outside";
            normal.NotifyEnter();
            if (!normal.InteriorVisible || !normal.DoorLocked || normal.Phase != RoomCombatPhase.LockedWave1)
                return "enter → visible + locked W1";
            if (normal.CanLeave)
                return "must not leave during W1";
            normal.NotifyWave1Death();
            if (normal.Phase != RoomCombatPhase.LockedWave1)
                return "one kill still LockedWave1";
            normal.NotifyWave1Death();
            if (normal.Phase != RoomCombatPhase.Cleared || normal.DoorLocked || !normal.CanLeave)
                return "Normal clear W1 → Cleared unlock";

            // Chest: W1 clear → W2 spawn → clear → open
            var chest = new RoomCombatBrain();
            chest.Configure(RoomCombatKind.SmallChest);
            chest.MarkWave1PreSpawned(1);
            chest.NotifyEnter();
            chest.NotifyWave1Death();
            if (chest.Phase != RoomCombatPhase.LockedWave2 || !chest.DoorLocked)
                return "Chest W1 clear → LockedWave2 still locked";
            chest.MarkWave2Spawned(2);
            if (!chest.Wave2Spawned || chest.Phase != RoomCombatPhase.LockedWave2)
                return "W2 mark while LockedWave2";
            chest.NotifyWave2Death();
            chest.NotifyWave2Death();
            if (chest.Phase != RoomCombatPhase.Cleared || chest.DoorLocked)
                return "Chest clear W2 → Cleared";

            // Altar same second-wave path
            var altar = new RoomCombatBrain();
            altar.Configure(RoomCombatKind.Altar);
            altar.MarkWave1PreSpawned(1);
            altar.NotifyEnter();
            altar.NotifyWave1Death();
            if (altar.Phase != RoomCombatPhase.LockedWave2)
                return "Altar must enter LockedWave2";
            altar.MarkWave2Spawned(1);
            altar.NotifyWave2Death();
            if (altar.Phase != RoomCombatPhase.Cleared)
                return "Altar clear → Cleared";

            // Camera pitch lock (compile-time constant on CameraFollow2D)
            if (Mathf.Abs(Vision.CameraFollow2D.IsoPitchDegrees - 45f) > 0.01f)
                return "camera iso pitch must be 45°";

            return null;
        }

        public static string FormatPass()
        {
            var sb = new StringBuilder();
            sb.Append("ACCEPTANCE PASS RoomCombat §4.5 ");
            sb.Append("kinds=N1/CH2/AL2 ");
            sb.Append("preSpawn→lock→clear ");
            sb.Append("footCircle r=").Append(RoomCombatRules.DefaultFootRadius.ToString("0.00"));
            sb.Append(" cam=45°");
            return sb.ToString();
        }
    }
}
