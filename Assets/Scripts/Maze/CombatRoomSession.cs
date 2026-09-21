using System.Collections.Generic;

namespace RogueShooter.Maze
{
    public enum CombatRoomPhase
    {
        Vacant = 0,
        Sealed = 1,
        Fighting = 2,
        Cleared = 3
    }

    public struct CombatStep
    {
        public string Kind;
        public string Line;
        public int Wave;
        public bool ShouldSpawn;
        public bool ShouldOpen;
        public bool Portal;
    }

    /// <summary>
    /// Spec v0.5 §2: enter → lock doors → (portal FX) → wave → clear →
    /// [chest/altar: portal + wave 2] → open. Corridors never construct a session.
    /// </summary>
    public sealed class CombatRoomSession
    {
        readonly MazeNode _node;

        public CombatRoomPhase Phase { get; private set; }
        public int CurrentWave { get; private set; }
        public int LiveCount { get; private set; }
        public int WavesTotal { get { return _node != null ? _node.WaveCount : 0; } }
        public string RoomId { get { return _node != null ? _node.Id : ""; } }
        public MazeNodeKind Kind { get { return _node != null ? _node.Kind : MazeNodeKind.Start; } }
        public bool DoorsLocked
        {
            get { return Phase == CombatRoomPhase.Sealed || Phase == CombatRoomPhase.Fighting; }
        }

        public bool IsCombat { get { return WavesTotal > 0; } }

        public CombatRoomSession(MazeNode node)
        {
            _node = node;
            Phase = CombatRoomPhase.Vacant;
        }

        public CombatStep[] Enter()
        {
            var steps = new List<CombatStep>();
            if (_node == null || !IsCombat)
                return steps.ToArray();
            if (Phase == CombatRoomPhase.Cleared || Phase == CombatRoomPhase.Fighting || Phase == CombatRoomPhase.Sealed)
                return steps.ToArray();

            Phase = CombatRoomPhase.Sealed;
            CurrentWave = 1;
            steps.Add(LockStep());
            steps.Add(PortalStep(1));
            steps.Add(SpawnStep(1));
            return steps.ToArray();
        }

        public void MarkSpawned(int live)
        {
            LiveCount = live < 0 ? 0 : live;
            Phase = CombatRoomPhase.Fighting;
        }

        public CombatStep[] NotifyKilled()
        {
            var steps = new List<CombatStep>();
            if (Phase != CombatRoomPhase.Fighting)
                return steps.ToArray();
            if (LiveCount > 0)
                LiveCount--;
            if (LiveCount > 0)
                return steps.ToArray();

            steps.Add(ClearStep(CurrentWave));
            if (CurrentWave < WavesTotal)
            {
                CurrentWave++;
                Phase = CombatRoomPhase.Sealed;
                steps.Add(PortalStep(CurrentWave));
                steps.Add(SpawnStep(CurrentWave));
                return steps.ToArray();
            }

            Phase = CombatRoomPhase.Cleared;
            steps.Add(OpenStep());
            return steps.ToArray();
        }

        /// <summary>Headless lock → draw-ready → clear → open for one room.</summary>
        public CombatStep[] DryRun()
        {
            var all = new List<CombatStep>();
            CombatStep[] enter = Enter();
            for (int i = 0; i < enter.Length; i++)
                all.Add(enter[i]);
            while (Phase != CombatRoomPhase.Cleared && Phase != CombatRoomPhase.Vacant)
            {
                if (Phase == CombatRoomPhase.Sealed)
                    MarkSpawned(1);
                CombatStep[] next = NotifyKilled();
                if (next.Length == 0)
                    break;
                for (int i = 0; i < next.Length; i++)
                    all.Add(next[i]);
            }

            return all.ToArray();
        }

        CombatStep LockStep()
        {
            return new CombatStep
            {
                Kind = "lock",
                Wave = CurrentWave,
                Line = "[S1Maze] lock room=" + RoomId + " kind=" + MazeRules.Label(Kind)
            };
        }

        CombatStep PortalStep(int wave)
        {
            return new CombatStep
            {
                Kind = "portal",
                Wave = wave,
                Portal = true,
                Line = PortalFxHook.Format(RoomId, wave)
            };
        }

        CombatStep SpawnStep(int wave)
        {
            return new CombatStep
            {
                Kind = "spawn",
                Wave = wave,
                ShouldSpawn = true,
                Line = PortalFxHook.FormatSpawn(RoomId, wave)
            };
        }

        CombatStep ClearStep(int wave)
        {
            return new CombatStep
            {
                Kind = "clear",
                Wave = wave,
                Line = "[S1Maze] clear room=" + RoomId + " wave=" + wave + "/" + WavesTotal
            };
        }

        CombatStep OpenStep()
        {
            return new CombatStep
            {
                Kind = "open",
                Wave = CurrentWave,
                ShouldOpen = true,
                Line = "[S1Maze] open room=" + RoomId
            };
        }
    }
}
