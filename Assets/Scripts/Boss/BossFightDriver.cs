using UnityEngine;
using RogueShooter.Art;
using RogueShooter.Balance;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Boss
{
    /// <summary>
    /// Room-local final-boss runner. One clock on BossBrain, executed by FinalBossCombat.
    /// Enter HP uses the locked 1200 formula. Does not attach MobFourStateAi / MobAiBrain.
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
        float _introLeft;
        float _savedTimeScale = 1f;
        bool _ownsTimeScale;
        Vector3 _knockDir;
        float _knockLeft;
        float _knockSpeed;

        public bool IsStaggered => Brain != null && Brain.Transitioning;

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

        /// <summary>Enter with Build + wall minutes. Max HP comes from the locked 1200 enter formula.</summary>
        public void BeginEnter(int buildCount, float wallMinutes)
        {
            if (FightStarted)
                return;
            float tm = TimePressure.AttrMul(wallMinutes);
            float bm = SpawnWaveCatalog.BuildMul(buildCount);
            var snap = BossScaleTable.Resolve(buildCount, wallMinutes, tm, bm);
            Combat = FinalBossLive.Begin(Brain, ref snap);
            Combat.SetArena(transform.position.x, transform.position.y, 14f, 11f);
            Combat.SetBossPosition(transform.position.x, transform.position.y);
            LastScale = snap;
            FightStarted = true;
            FightWon = false;
            FightSettled = false;
            _lastWallMinutes = wallMinutes;
            _introLeft = FinalBossRules.IntroSeconds;
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
            if (!FightStarted || Brain == null || !Brain.HpLocked || Brain.Transitioning)
                return;
            float hpBefore = Brain.Hp;
            float maxBefore = Brain.MaxHp;
            float dmgBefore = Brain.LiveDmgMul;
            Brain.RefreshDamage(TimePressure.AttrMul(wallMinutes));
            if (Mathf.Abs(dmgBefore - Brain.LiveDmgMul) <= 0.0001f)
                return;
            Debug.Log(string.Format(
                "[BossFight] PRESSURE dmg={0:0.00}→{1:0.00} hp={2:0}/{3:0} (hp unchanged={4})",
                dmgBefore, Brain.LiveDmgMul, Brain.Hp, Brain.MaxHp,
                Mathf.Approximately(hpBefore, Brain.Hp) && Mathf.Approximately(maxBefore, Brain.MaxHp)));
        }

        public float DealDamage(float amount)
        {
            return DealDamage(amount, Vector3.zero, false);
        }

        public float DealDamage(float amount, Vector3 incoming, bool weak)
        {
            if (Brain == null || !FightStarted || FightSettled)
                return 0f;
            int cuts = Brain.Phase2Transitions;
            float dealt = FinalBossLive.ApplyDamage(Combat, Brain, amount, incoming.x, incoming.y, weak);
            if (Brain.Phase2Transitions > cuts && Brain.Phase != BossPhase.Defeated)
                StartTransition();
            if (Brain.Phase == BossPhase.Defeated || (Combat != null && Combat.Hp <= 0f))
                Finish(BossSettleOutcome.Win);
            return dealt;
        }

        /// <summary>A weak hit during recovery extends that recovery by 0.50s. It does not cancel the skill.</summary>
        public void ApplyWeakSpotStagger(float seconds)
        {
            if (!FightStarted || FightSettled || Brain == null || Brain.Transitioning)
                return;
            if (Brain.TryWeakSpot())
                Debug.Log("[BossFight] weak-spot recovery +" + FinalBossRules.RecoveryExtension.ToString("0.00") + "s");
        }

        public void ApplyKnockback(Vector3 shotAway, float distance)
        {
            if (!FightStarted || FightSettled || distance < 0.01f || Brain == null || Brain.Transitioning)
                return;
            if (Brain.BlocksDisplacement)
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
            EndTransition();
            if (Combat != null)
                Combat.ClearShots();
            LastSettle = BossSettleReport.From(outcome, Brain, _lastWallMinutes);
            if (Settle != null)
                Settle.Show(LastSettle);
            Debug.Log("[BossFight] SETTLE " + LastSettle.FormatLines());
        }

        void StartTransition()
        {
            if (Combat != null)
                Combat.ClearShots();
            _knockLeft = 0f;
            if (!_ownsTimeScale)
            {
                _savedTimeScale = Time.timeScale;
                _ownsTimeScale = true;
                Time.timeScale = 0f;
            }

            Debug.Log("[BossFight] P2 transition hp=" + Brain.Hp.ToString("0") + " no heal");
        }

        void EndTransition()
        {
            if (!_ownsTimeScale)
                return;
            _ownsTimeScale = false;
            Time.timeScale = _savedTimeScale;
        }

        void Update()
        {
            if (!FightStarted || Brain == null || FightSettled)
                return;
            if (Brain.Transitioning)
            {
                Brain.DrainTransition(Time.unscaledDeltaTime);
                if (Combat != null)
                    Combat.AllowActions = false;
                if (!Brain.Transitioning)
                {
                    EndTransition();
                    if (Combat != null)
                        Combat.AllowActions = true;
                }

                return;
            }

            if (_introLeft > 0f)
            {
                _introLeft -= Time.deltaTime;
                if (Combat != null)
                    Combat.AllowActions = false;
            }
            else if (Combat != null)
            {
                Combat.AllowActions = true;
            }

            TickKnockback(Time.deltaTime);
            var before = Brain.Phase;
            bool doorBefore = Brain.DoorClosed;
            BossMoveId moveBefore = Combat != null ? Combat.CurrentMove : BossMoveId.None;
            int indexBefore = Brain.MoveIndex;
            bool hasPlayer = TrySensePlayer(out float px, out float py, out bool body, out bool crown, out bool iframe);
            if (Combat != null && _player != null)
            {
                var roll = _player.GetComponent<PlayerRoll>();
                var dodge = _player.GetComponent<PlayerDodge>();
                Combat.AcceptWalkSample = (roll == null || !roll.IsRolling) && (dodge == null || !dodge.IsRolling);
            }

            FinalBossLive.Step(
                Brain, Combat, Time.deltaTime, hasPlayer,
                transform.position.x, transform.position.y,
                px, py, body, crown, iframe);
            if (Combat != null)
            {
                transform.position = new Vector3(Combat.BossX, Combat.BossY, transform.position.z);
                FlushHits();
            }

            if (!doorBefore && Brain.DoorClosed)
            {
                ApplyDoorVisual(true);
                Debug.Log("[BossFight] DOOR_SEALED " + Brain.Snapshot());
            }

            if (before != Brain.Phase)
                Debug.Log("[BossFight] PHASE " + before + "→" + Brain.Phase + " " + Brain.Snapshot());

            if (Combat != null && (Combat.CurrentMove != moveBefore || Brain.MoveIndex != indexBefore))
            {
                Debug.Log("[BossFight] BEAT " + Combat.Phase + " " + Combat.CurrentMove
                    + " dist=" + Combat.Distance.ToString("0.00")
                    + " shots=" + Combat.LiveShots
                    + " hp=" + Brain.Hp.ToString("0") + "/" + Brain.MaxHp.ToString("0"));
            }

            if (Brain.Phase == BossPhase.Defeated || (Combat != null && Combat.Hp <= 0f))
                Finish(BossSettleOutcome.Win);
        }

        void FlushHits()
        {
            var hits = Combat.ConsumeHits();
            if (hits == null || hits.Count == 0 || _player == null)
                return;
            PlayerVitals vitals = _player.GetComponent<PlayerVitals>();
            if (vitals == null)
                return;
            for (int i = 0; i < hits.Count; i++)
                vitals.ApplyHit(hits[i].Damage, hits[i].Kind);
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
