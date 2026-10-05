namespace RogueShooter.Boss
{
    public enum FinalBossFightClock
    {
        Idle,
        DoorSealed,
        NoPlayer,
        Ticked
    }

    /// <summary>
    /// Scene and headless checks share this enter path.
    /// Begin rewrites the snapshot with the 1200 enter formula, then one combat executor
    /// follows <see cref="BossBrain"/>.
    /// </summary>
    public static class FinalBossLive
    {
        public static FinalBossCombat Begin(BossBrain brain, ref BossScaleSnapshot snap)
        {
            float pressure = snap.Tm > 0.01f ? snap.Tm : FinalBossRules.PressureRef;
            int build = snap.BuildCount;
            snap.Tm = pressure;
            snap.MaxHp = FinalBossRules.EnterMaxHp(build, pressure);
            snap.DmgMul = FinalBossRules.OutgoingMultiplier(build, pressure);
            float hpWeight;
            float dmgWeight;
            FinalBossRules.Weights(build, pressure, out hpWeight, out dmgWeight);
            snap.HpMul = hpWeight;
            if (brain != null)
            {
                brain.LockHpOnEnter(snap);
                brain.NotifyEnter();
            }

            return new FinalBossCombat(brain);
        }

        public static float ApplyDamage(FinalBossCombat combat, BossBrain brain, float amount)
        {
            return ApplyDamage(combat, brain, amount, 0f, 0f, false);
        }

        public static float ApplyDamage(FinalBossCombat combat, BossBrain brain, float amount, float dirX, float dirY, bool weak)
        {
            if (combat != null)
                return combat.ApplyPlayerShot(amount, dirX, dirY, weak);
            if (brain == null || amount <= 0f)
                return 0f;
            float before = brain.Hp;
            brain.ApplyDamage(amount);
            return before - brain.Hp;
        }

        public static FinalBossFightClock Step(
            BossBrain brain,
            FinalBossCombat combat,
            float dt,
            bool hasPlayer,
            float bossX,
            float bossY,
            float playerX,
            float playerY,
            bool bodyInView,
            bool crownInView,
            bool playerInvulnerable)
        {
            if (brain != null && brain.Phase == BossPhase.Entering)
            {
                brain.Tick(dt);
                return FinalBossFightClock.DoorSealed;
            }

            if (brain != null && (brain.Phase == BossPhase.IdleOutside || brain.Phase == BossPhase.Defeated))
                return FinalBossFightClock.Idle;

            if (combat == null || !hasPlayer)
                return FinalBossFightClock.NoPlayer;

            combat.SetBossPosition(bossX, bossY);
            combat.PlacePlayer(playerX, playerY, dt);
            combat.BodyAndCrownInView = bodyInView && crownInView;
            combat.PlayerInvulnerable = playerInvulnerable;
            if (brain.Phase == BossPhase.DoorSealed)
                brain.Tick(dt);
            if (brain.Transitioning)
                return FinalBossFightClock.Ticked;
            combat.Simulate(dt);
            return FinalBossFightClock.Ticked;
        }
    }
}
