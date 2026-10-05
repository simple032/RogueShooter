using UnityEngine;
using RogueShooter.Art;
using RogueShooter.Balance;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Boss
{
    /// <summary>
    /// Room-local BOSS fight runner. Door seal and the locked HP stay on BossBrain.
    /// Each new beat is FinalBossLive, so the scene fight uses the distance switch.
    /// Does not attach MobFourStateAi / MobAiBrain.
    /// </summary>
    public sealed class BossFightDriver : MonoBehaviour
    {
        public BossBrain Brain { get; private set; }
        public FinalBossCombat Combat { get; private set; }
        public BossSettlePanel Settle { get; private set; }
        public bool FightStarted { get; private set; }
        public bool FightWon { get; private set; }
        public bool FightSettled { get; private set; }
        public BossScaleSnapshot LastScale { get; private set; }
        public BossSettleReport LastSettle { get; private set; }
        public static BossFightDriver Live { get; private set; }

        [SerializeField] float stubMaxHp = FinalBossRules.MaxHp;
        [SerializeField] Transform doorVisual;

        PlayerMotor2D _player;
        float _lastWallMinutes;
        int _lastTimeTier = -1;
        float _staggerUntil;
        Vector3 _knockDir;
        float _knockLeft;
        float _knockSpeed;

        public bool IsStaggered => Time.time < _staggerUntil;

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

        /// <summary>Enter with Build + wall minutes. This fight locks MaxHP at the locked 1200, not the scale table; damage mul still comes from the scale snapshot.</summary>
        public void BeginEnter(int buildCount, float wallMinutes)
        {
            if (FightStarted)
                return;
            float tm = TimePressure.AttrMul(wallMinutes);
            float bm = SpawnWaveCatalog.BuildMul(buildCount);
            var snap = BossScaleTable.Resolve(buildCount, wallMinutes, tm, bm);
            Combat = FinalBossLive.Begin(Brain, ref snap);
            LastScale = snap;
            FightStarted = true;
            FightWon = false;
            FightSettled = false;
            _lastWallMinutes = wallMinutes;
            _lastTimeTier = BossScaleTable.TimeTierFromMinutes(wallMinutes);
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
            FinalBossLive.ApplyDamage(Combat, Brain, amount);
            if ((Combat != null && Combat.Hp <= 0f) || Brain.Phase == BossPhase.Defeated)
                Finish(BossSettleOutcome.Win);
        }

        /// <summary>Spec §4 弱点命中硬直: freeze the live beat clock. The beat already playing is not cancelled.</summary>
        public void ApplyWeakSpotStagger(float seconds)
        {
            if (!FightStarted || FightSettled || Brain == null)
                return;
            float dur = seconds > 0.01f ? seconds : 0.50f;
            _staggerUntil = Time.time + dur;
            Debug.Log($"[BossFight] weak-spot stagger {dur:0.00}s");
        }

        public void ApplyKnockback(Vector3 shotAway, float distance)
        {
            if (!FightStarted || FightSettled || distance < 0.01f)
                return;
            shotAway.z = 0f;
            if (shotAway.sqrMagnitude < 0.0001f)
                shotAway = Vector3.up;
            _knockDir = shotAway.normalized;
            float dur = FullChargeKnockback.SlideSeconds(distance, true);
            _knockLeft = dur;
            _knockSpeed = dur > 0.001f ? distance / dur : 0f;
            Debug.Log("[Knockback] DRAFT_NOT_LOCKED kind=BOSS dist=" + distance.ToString("0.00")
                      + " t=" + dur.ToString("0.00") + "s (no move×0.6)");
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
            TickKnockback(Time.deltaTime);
            if (IsStaggered)
                return;

            var before = Brain.Phase;
            bool doorBefore = Brain.DoorClosed;
            FinalBossMoveId moveBefore = Combat != null ? Combat.CurrentMove : FinalBossMoveId.None;
            FinalBossPhase phaseBefore = Combat != null ? Combat.Phase : FinalBossPhase.P1;
            bool hasPlayer = TrySensePlayer(out float px, out float py, out bool body, out bool crown, out bool iframe);
            FinalBossLive.Step(
                Brain, Combat, Time.deltaTime, hasPlayer,
                transform.position.x, transform.position.y,
                px, py, body, crown, iframe);

            if (!doorBefore && Brain.DoorClosed)
            {
                ApplyDoorVisual(true);
                Debug.Log("[BossFight] DOOR_SEALED " + Brain.Snapshot());
            }

            if (before != Brain.Phase)
                Debug.Log("[BossFight] PHASE " + before + "→" + Brain.Phase + " " + Brain.Snapshot());

            if (Combat != null && (Combat.CurrentMove != moveBefore || Combat.Phase != phaseBefore))
            {
                Debug.Log("[BossFight] BEAT " + Combat.Phase + " " + Combat.CurrentMove
                    + " replaced=" + Combat.ReplacedMove
                    + " dist=" + Combat.Distance.ToString("0.00")
                    + " proj=" + Combat.BeatProjectiles
                    + " hp=" + Brain.Hp.ToString("0") + "/" + Brain.MaxHp.ToString("0"));
            }

            if (Brain.Phase == BossPhase.Defeated || (Combat != null && Combat.Hp <= 0f))
                Finish(BossSettleOutcome.Win);
        }

        bool TrySensePlayer(out float px, out float py, out bool bodyInView, out bool crownInView, out bool iframe)
        {
            px = 0f;
            py = 0f;
            bodyInView = false;
            crownInView = false;
            iframe = false;
            if (_player == null)
                _player = FindObjectOfType<PlayerMotor2D>();
            if (_player == null)
                return false;

            Vector3 feet = _player.transform.position;
            px = feet.x;
            py = feet.y;
            var dodge = _player.GetComponent<PlayerDodge>();
            iframe = dodge != null && dodge.IsInvulnerable;

            Vector3 bossFeet = transform.position;
            bodyInView = ViewSpace.InViewQuad(bossFeet);
            crownInView = ViewSpace.InViewQuad(bossFeet + Vector3.up * CrownAboveFeet());
            return true;
        }

        /// <summary>现有 Boss 画布脚到冠的高度。不引用被否的近战帧。</summary>
        static float CrownAboveFeet()
        {
            const float canvas = 128f;
            return (1f - JianHaiArtCatalog.PivotBoss.y)
                * (canvas / JianHaiArtCatalog.Ppu)
                * JianHaiArtCatalog.EntityStubWorldScale;
        }

        void TickKnockback(float dt)
        {
            if (_knockLeft <= 0f)
                return;
            float step = _knockSpeed * dt;
            float max = _knockSpeed * _knockLeft;
            if (step > max)
                step = max;
            transform.position += _knockDir * step;
            _knockLeft -= dt;
            if (_knockLeft < 0f)
                _knockLeft = 0f;
        }

        void ApplyDoorVisual(bool closed)
        {
            if (doorVisual == null)
                return;
            doorVisual.gameObject.SetActive(closed);
        }
    }
}
