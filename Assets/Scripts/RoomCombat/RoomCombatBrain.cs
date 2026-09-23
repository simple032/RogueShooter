namespace RogueShooter.RoomCombat
{
    /// <summary>
    /// Pure §4.5 state machine: Outside → LockedWave1 → (optional LockedWave2) → Cleared.
    /// Vision / door / spawn are side effects applied by the driver from these transitions.
    /// </summary>
    public sealed class RoomCombatBrain
    {
        public RoomCombatKind Kind { get; private set; }
        public RoomCombatPhase Phase { get; private set; }
        public bool InteriorVisible { get; private set; }
        public bool DoorLocked { get; private set; }
        public bool Wave1PreSpawned { get; private set; }
        public bool Wave2Spawned { get; private set; }
        public int Wave1Alive { get; private set; }
        public int Wave2Alive { get; private set; }
        public bool WantsSecondWave => RoomCombatRules.WantsSecondWave(Kind);

        public void Configure(RoomCombatKind kind)
        {
            Kind = kind;
            Phase = RoomCombatPhase.Outside;
            InteriorVisible = false;
            DoorLocked = false;
            Wave1PreSpawned = false;
            Wave2Spawned = false;
            Wave1Alive = 0;
            Wave2Alive = 0;
        }

        /// <summary>Call once before the player can enter (level start).</summary>
        public void MarkWave1PreSpawned(int aliveCount)
        {
            if (!RoomCombatRules.UsesRoomCombat(Kind))
                return;
            Wave1PreSpawned = true;
            Wave1Alive = aliveCount < 0 ? 0 : aliveCount;
        }

        public void NotifyEnter()
        {
            if (!RoomCombatRules.UsesRoomCombat(Kind))
                return;
            if (Phase != RoomCombatPhase.Outside)
                return;
            InteriorVisible = true;
            DoorLocked = true;
            Phase = RoomCombatPhase.LockedWave1;
        }

        public void NotifyWave1Death()
        {
            if (Phase != RoomCombatPhase.LockedWave1)
                return;
            if (Wave1Alive > 0)
                Wave1Alive--;
            if (Wave1Alive > 0)
                return;

            if (WantsSecondWave)
            {
                Phase = RoomCombatPhase.LockedWave2;
                return;
            }

            OpenCleared();
        }

        /// <summary>Driver calls after spawning wave 2 on LockedWave2 enter.</summary>
        public void MarkWave2Spawned(int aliveCount)
        {
            if (Phase != RoomCombatPhase.LockedWave2)
                return;
            Wave2Spawned = true;
            Wave2Alive = aliveCount < 0 ? 0 : aliveCount;
            if (Wave2Alive <= 0)
                OpenCleared();
        }

        public void NotifyWave2Death()
        {
            if (Phase != RoomCombatPhase.LockedWave2)
                return;
            if (Wave2Alive > 0)
                Wave2Alive--;
            if (Wave2Alive > 0)
                return;
            OpenCleared();
        }

        public bool CanLeave => Phase == RoomCombatPhase.Cleared || Phase == RoomCombatPhase.Outside;

        void OpenCleared()
        {
            Phase = RoomCombatPhase.Cleared;
            DoorLocked = false;
            InteriorVisible = true;
        }
    }
}
