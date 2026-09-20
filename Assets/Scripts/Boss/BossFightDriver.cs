using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Spawning;

namespace RogueShooter.Boss
{
    /// <summary>
    /// Room-local BOSS fight runner. Owns door seal + brain ticks + W3-02 HP lock + W3-03 settle.
    /// Does not attach MobFourStateAi / MobAiBrain.
    /// </summary>
    public sealed class BossFightDriver : MonoBehaviour
    {
        public BossBrain Brain { get; private set; }
        public BossSettlePanel Settle { get; private set; }
        public bool FightStarted { get; private set; }
        public bool FightWon { get; private set; }
        public bool FightSettled { get; private set; }
        public BossScaleSnapshot LastScale { get; private set; }
        public BossSettleReport LastSettle { get; private set; }
        public static BossFightDriver Live { get; private set; }

        [SerializeField] float stubMaxHp = BossBrain.DefaultMaxHp;
        [SerializeField] Transform doorVisual;

        float _lastWallMinutes;
        int _lastTimeTier = -1;

        void Awake()
        {
            Brain = new BossBrain();
            Brain.Configure(stubMaxHp);
            Settle = GetComponent<BossSettlePanel>();
            if (Settle == null)
                Settle = gameObject.AddComponent<BossSettlePanel>();
        }

        void OnEnable()
        {
            Live = this;
        }

        void OnDisable()
        {
            if (Live == this)
                Live = null;
        }

        public void BindDoor(Transform door)
        {
            doorVisual = door;
            if (doorVisual != null)
                doorVisual.gameObject.SetActive(Brain != null && Brain.DoorClosed);
        }

        /// <summary>Enter with Build + wall minutes → lock HP from CSV A–E, then seal fight.</summary>
        public void BeginEnter(int buildCount, float wallMinutes)
        {
            if (FightStarted)
                return;
            float tm = TimePressure.AttrMul(wallMinutes);
            float bm = SpawnWaveCatalog.BuildMul(buildCount);
            var snap = BossScaleTable.Resolve(buildCount, wallMinutes, tm, bm);
            LastScale = snap;
            Brain.LockHpOnEnter(snap);
            FightStarted = true;
            FightWon = false;
            FightSettled = false;
            _lastWallMinutes = wallMinutes;
            _lastTimeTier = BossScaleTable.TimeTierFromMinutes(wallMinutes);
            Brain.NotifyEnter();
            ApplyDoorVisual(false);
            Debug.Log(string.Format(
                "[BossFight] ENTER lock anchor={0} MaxHP={1:0} dmg={2:0.00} B={3} T={4} Tm={5:0.00} Bm={6:0.00} | {7}",
                snap.AnchorId, snap.MaxHp, snap.DmgMul, snap.BuildCount, snap.TimeTier, tm, bm, Brain.Snapshot()));
        }

        public void BeginEnter()
        {
            BeginEnter(0, 0f);
        }

        public void SetWallMinutes(float wallMinutes)
        {
            _lastWallMinutes = wallMinutes;
            if (!FightStarted || Brain == null || !Brain.HpLocked)
                return;
            int tier = BossScaleTable.TimeTierFromMinutes(wallMinutes);
            if (tier == _lastTimeTier)
                return;
            float hpBefore = Brain.Hp;
            float maxBefore = Brain.MaxHp;
            Brain.NotifyTimeCross(wallMinutes);
            _lastTimeTier = tier;
            Debug.Log(string.Format(
                "[BossFight] CROSS_SEG T→{0} dmg={1:0.00} hp={2:0}/{3:0} (unchanged={4})",
                tier, Brain.LiveDmgMul, Brain.Hp, Brain.MaxHp,
                Mathf.Approximately(hpBefore, Brain.Hp) && Mathf.Approximately(maxBefore, Brain.MaxHp)));
        }

        public void DealDamage(float amount)
        {
            if (Brain == null || !FightStarted || FightSettled)
                return;
            Brain.ApplyDamage(amount);
            if (Brain.Phase == BossPhase.Defeated)
                Finish(BossSettleOutcome.Win);
        }

        public void NotifyPlayerDead()
        {
            if (!FightStarted || FightSettled)
                return;
            Finish(BossSettleOutcome.Lose);
        }

        /// <summary>Outgoing boss hit = base × LiveDmgMul (cross-seg may raise this).</summary>
        public float OutgoingDamage(float baseDamage)
        {
            if (Brain == null)
                return baseDamage;
            return baseDamage * Brain.LiveDmgMul;
        }

        void Finish(BossSettleOutcome outcome)
        {
            if (FightSettled)
                return;
            FightSettled = true;
            FightWon = outcome == BossSettleOutcome.Win;
            LastSettle = BossSettleReport.From(outcome, Brain, _lastWallMinutes);
            if (Settle != null)
                Settle.Show(LastSettle);
            Debug.Log("[BossFight] SETTLE " + LastSettle.FormatLines());
        }

        void Update()
        {
            if (!FightStarted || Brain == null || FightSettled)
                return;
            var before = Brain.Phase;
            bool doorBefore = Brain.DoorClosed;
            Brain.Tick(Time.deltaTime);
            if (!doorBefore && Brain.DoorClosed)
            {
                ApplyDoorVisual(true);
                Debug.Log("[BossFight] DOOR_SEALED " + Brain.Snapshot());
            }

            if (before != Brain.Phase)
                Debug.Log("[BossFight] PHASE " + before + "→" + Brain.Phase + " " + Brain.Snapshot());

            if (Brain.Phase == BossPhase.Defeated)
                Finish(BossSettleOutcome.Win);
        }

        void ApplyDoorVisual(bool closed)
        {
            if (doorVisual == null)
                return;
            doorVisual.gameObject.SetActive(closed);
        }
    }
}
