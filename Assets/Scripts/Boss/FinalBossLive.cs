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
    /// 场景终局战和自检共用的入口。BossFightDriver.BeginEnter / Update 只走这里。
    /// 开战把这场最大生命写成终局 1200；之后每一拍用距离切换，不再推进 BossBrain 的招式。
    /// </summary>
    public static class FinalBossLive
    {
        /// <summary>
        /// 进门。缩放快照里的最大生命改成这场的 1200 再锁上，伤害倍率仍留在快照上。
        /// </summary>
        public static FinalBossCombat Begin(BossBrain brain, ref BossScaleSnapshot snap)
        {
            snap.MaxHp = FinalBossRules.MaxHp;
            if (brain != null)
            {
                brain.LockHpOnEnter(snap);
                brain.NotifyEnter();
            }

            return new FinalBossCombat();
        }

        public static void ApplyDamage(FinalBossCombat combat, BossBrain brain, float amount)
        {
            if (combat != null)
                combat.ApplyDamage(amount);
            if (brain != null)
                brain.ApplyDamage(amount);
        }

        /// <summary>
        /// 与 BossFightDriver.Update 同一拍。进门第一拍只关门，不开招。
        /// 之后有玩家才把距离交给 FinalBossCombat。
        /// </summary>
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
            combat.SetPlayerPosition(playerX, playerY);
            combat.BodyAndCrownInView = bodyInView && crownInView;
            combat.PlayerInvulnerable = playerInvulnerable;
            combat.Tick(dt);
            return FinalBossFightClock.Ticked;
        }
    }
}
