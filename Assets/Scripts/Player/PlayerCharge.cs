using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Boss;
using RogueShooter.Build;
using RogueShooter.Demo;
using RogueShooter.Maze;
using RogueShooter.Vision;

namespace RogueShooter.Player
{
    /// <summary>
    /// Hold-to-charge bow. Ring full at 0.70s; no arrow under 0.30s; 0.30s can fire.
    /// Weak under 0.4s x0.50; weak-spot 0.67-0.73s. After a shot, 0.2s recovery.
    /// Movement x0.25 while charging.
    /// </summary>
    public class PlayerCharge : MonoBehaviour
    {
        [SerializeField] float hitRange = 8f;

        float _held;
        // Carry elapsed time across a window that fell entirely between two frames.
        float _deferredChargeSeconds;
        bool _charging;
        bool _mid;
        bool _green;
        bool _exited;
        float _recoverUntil;
        ChargeFxView _fx;
        GuaranteedCritActive _guaranteed;
        IList<string> _ownedRewards;
        Stage1Maze _maze;
        IReadOnlyList<GameObject> _mazeDoors;

        public bool IsCharging => _charging;
        public bool InRecovery => Time.time < _recoverUntil;
        public float HeldSeconds => _held;
        /// <summary>Where the bow is aimed right now (mouse). Updated while charging.</summary>
        public Vector3 AimDir { get; private set; }
        /// <summary>Visual aim used by the hero pose: from the body center toward the
        /// mouse point (the arrow's flight line). Falls back to AimDir when unset.</summary>
        public Vector3 VisualAimDir => _visualAim.sqrMagnitude > .01f ? _visualAim : AimDir;
        Vector3 _visualAim;
        public ChargeShotKind LastShot { get; private set; }
        public float LastDamage { get; private set; }
        public int WallStops { get; private set; }
        public int PathHits { get; private set; }
        public int ShotSerial { get; private set; }
        public event System.Action<ChargeShotKind, int> ShotFired;
        // Every actual release, including too-short charges that do not launch an arrow.
        public event System.Action<ChargeShotKind, float, int> ShotReleased;
        public ShotRead CurrentRead => ShotRead.From(_ownedRewards);

        public void BindOwnedRewards(IList<string> owned)
        {
            _ownedRewards = owned;
            var perks=GetComponent<ConditionalShotPerks>();if(perks==null)perks=gameObject.AddComponent<ConditionalShotPerks>();perks.Bind(owned);
        }

        public bool LastRandomCrit { get; private set; }

        public float DamageFor(ChargeShotKind kind, ShotRead read)
        {
            float dmg = ChargeShotRules.DamageWithBuild(kind, read.DamageProduct);
            if (kind == ChargeShotKind.Crit)
                dmg *= RewardStatHooks.ProductMul(_ownedRewards, "weak_damage");
            return dmg;
        }

        public void BindMaze(Stage1Maze maze, IReadOnlyList<GameObject> doors = null)
        {
            _maze = maze;
            _mazeDoors = doors;
        }

        void Awake()
        {
            _fx = GetComponent<ChargeFxView>();
            if (_fx == null)
                _fx = gameObject.AddComponent<ChargeFxView>();
            _guaranteed = GetComponent<GuaranteedCritActive>();
        }

        void Update()
        {
            if (RunPause.IsPaused || Down())
            {
                if (_charging)
                    CancelCharge();
                return;
            }

            bool hold = Input.GetMouseButton(0) || Input.GetKey(KeyCode.C);
            if (_guaranteed == null) _guaranteed = GetComponent<GuaranteedCritActive>();
            if (_guaranteed != null && _guaranteed.IsActive)
            {
                if (_charging) CancelCharge();
                if (hold) TryFocusShot(Vector3.zero);
                return;
            }
            if (!_charging)
            {
                if (hold && Time.time >= _recoverUntil && !RollLocked())
                    BeginCharge();
                return;
            }

            if (!hold)
            {
                ReleaseCharge();
                return;
            }

            AdvanceCharge(Time.deltaTime);
        }

        public const float FocusShotInterval = .75f;
        public bool TryFocusShot(Vector3 aim)
        {
            if (_guaranteed == null) _guaranteed = GetComponent<GuaranteedCritActive>();
            if (_guaranteed == null || !_guaranteed.IsActive || RunPause.IsPaused || Down() || RollLocked() || InRecovery) return false;
            if (_charging) CancelCharge();
            return FireAtHeld(0f, aim) == ChargeShotKind.Crit;
        }

        void AdvanceCharge(float deltaTime)
        {
            float previous = _held;
            _held += Mathf.Max(0f, deltaTime) + _deferredChargeSeconds;
            _deferredChargeSeconds = 0f;
            AimDir = AimDirection();
            ShotRead read = CurrentRead;
            if (!_green && !_exited && previous < read.GreenEnter && _held > read.GreenExit)
            {
                // Present one actionable green frame. Release resolves from this same
                // held time; holding another frame consumes the overflow and exits.
                float presented = (read.GreenEnter + read.GreenExit) * .5f;
                _deferredChargeSeconds = _held - presented;
                _held = presented;
            }
            if (!_mid && _held >= ChargeFxHooks.MidAt)
            {
                _mid = true;
                ChargeFxHooks.ChargeMid();
                RogueShooter.Demo.GameTrace.Log("[ChargeFx] OnChargeMid");
            }

            if (!_green && !_exited && _held >= read.GreenEnter)
            {
                _green = true;
                ChargeFxHooks.ChargeEnterGreen();
                RogueShooter.Audio.JianHaiAudio.Emit("jh_bow_window_enter",this);
                RogueShooter.Demo.GameTrace.Log("[ChargeFx] OnChargeEnterGreen");
            }

            if (_green && !_exited && _held > read.GreenExit)
            {
                _exited = true;
                _green = false;
                ChargeFxHooks.ChargeExitGreen();
                RogueShooter.Demo.GameTrace.Log("[ChargeFx] OnChargeExitGreen");
            }

            if (_fx != null)
                _fx.SetChargeProgress(read.Progress(_held), _green);
        }

        void BeginCharge()
        {
            _charging = true;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_bow_charge_start",this);
            RogueShooter.Audio.JianHaiAudio.Emit("jh_bow_charge_loop",this);
            _held = 0f;
            _deferredChargeSeconds = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            LastShot = ChargeShotKind.None;
            LastDamage = 0f;
            AimDir = AimDirection();
            if (_fx != null)
                _fx.SetChargeProgress(0f, false);
        }

        void ReleaseCharge()
        {
            if (RollLocked())
            {
                CancelCharge();
                LastShot = ChargeShotKind.None;
                LastDamage = 0f;
                LastRandomCrit = false;
                return;
            }

            float held = _held;
            _charging = false;
            _held = 0f;
            _deferredChargeSeconds = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            Fire(held);
        }

        /// <summary>Test/helper: resolve and fire as if released after heldSeconds.</summary>
        public ChargeShotKind FireAtHeld(float heldSeconds)
        {
            return FireAtHeld(heldSeconds, Vector3.zero);
        }

        /// <summary>Same as FireAtHeld, along a world aim. Zero uses the mouse.</summary>
        public ChargeShotKind FireAtHeld(float heldSeconds, Vector3 aim)
        {
            if(RunPause.IsPaused || RollLocked() || InRecovery){LastShot=ChargeShotKind.None;LastDamage=0;LastRandomCrit=false;return ChargeShotKind.None;}
            if (Down())
            {
                CancelCharge();
                LastShot = ChargeShotKind.None;
                LastDamage = 0f;
                LastRandomCrit = false;
                return ChargeShotKind.None;
            }

            _charging = false;
            _held = 0f;
            _deferredChargeSeconds = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            return Fire(heldSeconds, aim);
        }

        ChargeShotKind Fire(float heldSeconds)
        {
            return Fire(heldSeconds, Vector3.zero);
        }

        ChargeShotKind Fire(float heldSeconds, Vector3 aim)
        {
            RogueShooter.Audio.JianHaiAudio.Stop("jh_bow_charge_loop",this);
            if (RunPause.IsPaused)
            {
                CancelCharge();
                LastShot = ChargeShotKind.None;
                LastDamage = 0f;
                LastRandomCrit = false;
                return ChargeShotKind.None;
            }

            AimDir = aim.sqrMagnitude >= .01f ? aim.normalized : AimDirection();
            ShotRead read = CurrentRead;
            ChargeShotKind kind = read.Resolve(heldSeconds);
            if (_guaranteed == null)
                _guaranteed = GetComponent<GuaranteedCritActive>();
            if (_guaranteed != null && _guaranteed.TryForceCrit(kind, out ChargeShotKind forced))
                kind = forced;

            float dmg = DamageFor(kind, read);
            float chance = RewardStatHooks.CritChance(_ownedRewards);
            LastRandomCrit = kind != ChargeShotKind.None && (chance >= 1f || (chance > 0f && UnityEngine.Random.value < chance));
            if (LastRandomCrit) dmg *= RewardStatHooks.CritMultiplier(_ownedRewards);
            var conditional=GetComponent<ConditionalShotPerks>();if(conditional!=null)dmg*=1f+conditional.ConsumeShotBonus(kind);
            LastShot = kind;
            LastDamage = dmg;
            float p = read.Progress(heldSeconds);

            if (kind == ChargeShotKind.None)
            {
                ShotReleased?.Invoke(kind, heldSeconds, ShotSerial);
                RogueShooter.Audio.JianHaiAudio.Emit("jh_bow_charge_cancel",this);
                RogueShooter.Demo.GameTrace.Log($"[ChargeShot] NO_SHOT held={heldSeconds:0.000}s p={p:0.00} (<{ChargeShotRules.MinChargeSeconds:0.00}s)");
                if (_fx != null)
                    _fx.HideAll();
                return kind;
            }

            if (kind == ChargeShotKind.Crit)
            {
                ChargeFxHooks.CritConfirm();
                RogueShooter.Demo.GameTrace.Log("[ChargeFx] OnCritConfirm");
            }
            else if (_fx != null)
                _fx.HideAll();

            _recoverUntil = Time.time + ((_guaranteed != null && _guaranteed.IsActive) ? FocusShotInterval : ChargeShotRules.RecoverSeconds);
            RogueShooter.Demo.GameTrace.Log($"[ChargeShot] {kind} dmg={dmg:0.0} held={heldSeconds:0.000}s p={p:0.00} " +
                      $"recover={ChargeShotRules.RecoverSeconds:0.00}s " +
                      $"(weak×{ChargeShotRules.WeakMul:0.00} full×{ChargeShotRules.FullMul:0.00} crit×{ChargeShotRules.CritMul:0.00}) " +
                      $"read dmg×{read.DamageProduct:0.00} charge={read.ChargeSeconds:0.000} " +
                      $"window={read.GreenEnter:0.000}-{read.GreenExit:0.000}");

            RogueShooter.Audio.JianHaiAudio.Emit(kind==ChargeShotKind.Crit?"jh_bow_release_weakspot":kind==ChargeShotKind.Weak?"jh_bow_release_weak":"jh_bow_release_full",this);
            int serial = ++ShotSerial;
            LaunchArrow(dmg, kind, heldSeconds, read, aim, serial);
            ShotFired?.Invoke(kind, serial);
            ShotReleased?.Invoke(kind, heldSeconds, serial);
            return kind;
        }

        bool RollLocked()
        {
            PlayerRoll roll = GetComponent<PlayerRoll>();
            return roll != null && roll.LocksActions;
        }

        bool Down()
        {
            PlayerVitals vitals = GetComponent<PlayerVitals>();
            return vitals != null && vitals.IsDown;
        }

        void LaunchArrow(float damage, ChargeShotKind kind, float heldSeconds, ShotRead read, Vector3 aim, int serial)
        {
            RewardStatHooks.SyncClock(Time.time);
            Vector3 origin = transform.position;
            bool mouseShot = aim.sqrMagnitude < 0.01f;
            if (mouseShot)
                aim = AimDirection();
            aim.z = 0f;
            if (aim.sqrMagnitude < 0.01f)
                aim = Vector3.right;
            aim.Normalize();
            // The arrow leaves from the drawn bow, not from the side of the body:
            // at bow height (the arc sits ~0.87 above the feet), just ahead of the limbs.
            const float BowForward = 0.55f;
            const float BowHeight = 0.87f;
            var heroView=GetComponent<RogueShooter.Iso.HeroMeshyPresentation>();
            float visualProgress=(_guaranteed!=null&&_guaranteed.IsActive)?1f:Mathf.Clamp01(read.Progress(heldSeconds));
            // The mouse indicates direction only. The aim line starts at the body
            // center; the arrow exits where that line passes the drawn bow and
            // keeps its own fixed attack range — it never stops at the cursor.
            Vector3 dirV = aim;
            bool centerLine = false;
            if (mouseShot)
            {
                Camera aimCam = Camera.main;
                if (aimCam != null)
                {
                    Vector3 point = CameraViewMath.ScreenToWorldOnPlayPlane(aimCam, Input.mousePosition);
                    Vector3 magnet;
                    if (StartTrainingTarget.TryAimPoint(transform, point, out magnet))
                        point = magnet;
                    Vector3 toPoint = point - (origin + Vector3.up * BowHeight);
                    toPoint.z = 0f;
                    if (toPoint.sqrMagnitude > 0.01f)
                    {
                        dirV = toPoint.normalized;
                        centerLine = true;
                    }
                }
            }
            // The bow is drawn for the visual aim, so its muzzle follows the same line.
            Vector3 muzzle = origin + dirV * BowForward + Vector3.up * BowHeight;
            if(heroView!=null&&RogueShooter.Iso.HeroMeshyArt.Available)
                muzzle=origin+heroView.LaunchOffset(dirV,visualProgress);
            if (centerLine)
            {
                Vector3 center = origin + Vector3.up * BowHeight;
                muzzle = center + dirV * Mathf.Max(0.2f, Vector3.Dot(muzzle - center, dirV));
            }
            float muzzleAlong = (muzzle.x - origin.x) * aim.x + (muzzle.y - origin.y) * aim.y;
            float alongRate = Mathf.Max(0.15f, dirV.x * aim.x + dirV.y * aim.y);
            float length = Mathf.Max(0.5f, (BowForward + hitRange - muzzleAlong) / alongRate);
            float locked = damage;
            float ring = read.ChargeSeconds;
            float pierce = RewardStatHooks.PierceBackAdd(_ownedRewards);
            int hits = 0;
            var seen = new HashSet<int>();
            Stage1Maze maze = _maze;
            Vector3 shotOrigin = origin;
            Vector3 shotAim = aim;
            bool firstSweep = true;
            bool echoCounted=false;int focusActivation=_guaranteed!=null&&_guaranteed.IsActive?_guaranteed.ActivationId:0;
            var conditional=GetComponent<ConditionalShotPerks>();
            var doors = _mazeDoors;

            // Draw the bow-release pose along the arrow's visual flight line.
            var actor = GetComponent<Stage1IsoActor>();
            if (actor != null)
                actor.BeginBowRelease(dirV,visualProgress);

            var go = new GameObject("Arrow");
            var fly = go.AddComponent<ArrowFly>();
            fly.Launch(muzzle, dirV, length, (a, b) =>
            {
                // Art flies from the bow at the reticle; collision always resolves on
                // the floor aim plane, starting just in front of the shooter.
                a = firstSweep ? shotOrigin : shotOrigin + shotAim * Mathf.Max(0.15f, (a.x - shotOrigin.x) * shotAim.x + (a.y - shotOrigin.y) * shotAim.y);
                b = shotOrigin + shotAim * Mathf.Max(0.15f, (b.x - shotOrigin.x) * shotAim.x + (b.y - shotOrigin.y) * shotAim.y);
                firstSweep = false;
                float wallT = MazeCollision.FirstBlockT(maze, doors, a, b);
                if (maze != null && !maze.OpenAt(a.x, a.y))
                {
                    WallStops++;
                    RogueShooter.Audio.JianHaiAudio.EmitAt("jh_hit_stone",a,this);
                    RogueShooter.Demo.GameTrace.Log("[ChargeShot] wall");
                    return ArrowPathStop.Wall;
                }

                float bodyT;
                MobFourStateAi mob;
                BossFightDriver boss;
                bool bodyFound = ClosestBody(a, b, wallT, seen, out mob, out boss, out bodyT);
                float trainingT;
                var training = StartTrainingTarget.FirstHit(transform, a, b, Mathf.Min(wallT,bodyT), out trainingT);
                if (training != null)
                {
                    training.Receive(kind, serial, shotOrigin);
                    PathHits++;
                    return ArrowPathStop.Hit;
                }
                if (!bodyFound)
                {
                    if (wallT < 1.5f)
                    {
                        WallStops++;
                        RogueShooter.Audio.JianHaiAudio.EmitAt("jh_hit_stone",Vector3.Lerp(a,b,wallT),this);
                        RogueShooter.Demo.GameTrace.Log("[ChargeShot] wall");
                        return ArrowPathStop.Wall;
                    }

                    return ArrowPathStop.Clear;
                }

                bool piercePacket = hits > 0;
                float packet = piercePacket ? locked * pierce : locked;
                if (mob != null)
                {
                    seen.Add(mob.GetInstanceID());
                    float dealt = HitMob(mob, packet, kind, heldSeconds, shotOrigin, shotAim, piercePacket, ring);
                    if(!echoCounted&&kind==ChargeShotKind.Crit){echoCounted=true;if(conditional!=null)conditional.NotifyWeakArrow(_guaranteed,focusActivation);}
                    ApplyLifesteal(dealt);
                }
                else if (boss != null)
                {
                    seen.Add(boss.GetInstanceID());
                    HitBoss(boss, packet, kind, heldSeconds, (b-a).normalized, piercePacket, ring, bodyT);
                    if(!echoCounted&&kind==ChargeShotKind.Crit){echoCounted=true;if(conditional!=null)conditional.NotifyWeakArrow(_guaranteed,focusActivation);}
                }

                hits++;
                PathHits++;
                bool more = !piercePacket && pierce > 0.0001f && wallT > 1.5f;
                return more ? ArrowPathStop.Clear : ArrowPathStop.Hit;
            }, () => RogueShooter.Demo.GameTrace.Log($"[ChargeShot] miss kind={kind}"));
        }

        static float WallDistance(Stage1Maze maze, Vector3 a, Vector3 b)
        {
            if (maze == null)
                return 2f;
            float len = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                float x = Mathf.Lerp(a.x, b.x, u);
                float y = Mathf.Lerp(a.y, b.y, u);
                if (!maze.OpenAt(x, y))
                {
                    if (i == 1 && maze.OpenAt(a.x, a.y))
                        return 0.5f / n;
                    return (i - 1) / (float)n;
                }
            }

            return 2f;
        }

        static bool ClosestBody(Vector3 a, Vector3 b, float wallT, HashSet<int> seen,
            out MobFourStateAi mob, out BossFightDriver boss, out float bodyT)
        {
            mob = null;
            boss = null;
            bodyT = wallT;
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                MobFourStateAi candidate = all[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;
                if (seen != null && seen.Contains(candidate.GetInstanceID()))
                    continue;
                StubEnemy stub = candidate.GetComponent<StubEnemy>();
                if (stub != null && stub.IsDead)
                    continue;
                // Lateral radius follows the kind's real silhouette and the applied
                // visual scale, so an arrow grazing the body edge always lands.
                float radius = stub != null
                    ? RogueShooter.Spawning.EnemyKindCatalog.HitRadiusFor(stub.KindId) * stub.VisualScale
                    : Stage1Coin.BodyRadius;
                float t = BodyTubeEntryT(candidate.transform.position, a, b, radius, Stage1Coin.BodyHeight);
                if (t > 1f || t >= wallT - 0.0001f)
                    continue;
                if (t < bodyT)
                {
                    bodyT = t;
                    mob = candidate;
                    boss = null;
                }
            }

            BossFightDriver live = BossFightDriver.Live;
            if (live != null && live.FightStarted && !live.FightSettled
                && live.gameObject.activeInHierarchy
                && (seen == null || !seen.Contains(live.GetInstanceID())))
            {
                float t = BodyTubeEntryT(live.transform.position, a, b, FinalBossRules.HitRadius, FinalBossRules.HitBodyHeight);
                if (t <= 1f && t < wallT - 0.0001f && t < bodyT)
                {
                    bodyT = t;
                    mob = null;
                    boss = live;
                }
            }

            return mob != null || boss != null;
        }

        /// <summary>
        /// The shot line converges on the cursor point, so aiming at a body silhouette
        /// must hit even though the volume sits above the feet. The standing body is
        /// sampled as stacked circles from the feet up to its visible height; lateral
        /// radius is per kind (EnemyKindCatalog.HitRadiusFor) times the stub visual scale.
        /// </summary>
        static float BodyTubeEntryT(Vector3 feet, Vector3 a, Vector3 b, float radius, float bodyHeight)
        {
            float t = MazeCollision.CircleEntryT(feet, a, b, radius);
            if (bodyHeight <= 0.01f)
                return t;
            int layers = Mathf.Max(1, Mathf.CeilToInt(bodyHeight / (radius * 2f)));
            for (int i = 1; i <= layers; i++)
            {
                float higher = MazeCollision.CircleEntryT(feet + Vector3.up * (bodyHeight * i / layers), a, b, radius);
                if (higher < t)
                    t = higher;
            }
            return t;
        }

        static float SegmentT(Vector3 point, Vector3 a, Vector3 b, out float dist)
        {
            Vector3 ab = b - a;
            ab.z = 0f;
            Vector3 ap = point - a;
            ap.z = 0f;
            float len2 = ab.sqrMagnitude;
            float t = 0f;
            if (len2 > 0.000001f)
                t = (ap.x * ab.x + ap.y * ab.y) / len2;
            if (t < 0f)
                t = 0f;
            if (t > 1f)
                t = 1f;
            Vector3 q = a + (b - a) * t;
            q.z = 0f;
            point.z = 0f;
            dist = Vector3.Distance(point, q);
            return t;
        }

        void HitBoss(BossFightDriver boss, float damage, ChargeShotKind kind, float heldSeconds,
            Vector3 aim, bool piercePacket, float ringFill, float along)
        {
            RewardStatHooks.SyncClock(Time.time);
            bool bossWeak = kind == ChargeShotKind.Crit;
            // Only weak-spot hits confirm with the cyan-green diamond and only they
            // shove the boss; ordinary hits stay audio-only with no VFX or knockback.
            if (boss.AcceptsDamage)
            {
                if (bossWeak)
                    RogueShooter.Art.ArrowImpactFeedback.Confirm(boss.gameObject, transform.position, true, false);
                else
                    RogueShooter.Audio.JianHaiAudio.Emit("jh_hit_body", boss);
            }
            bool bossFull = boss.Brain != null && boss.Brain.Hp >= boss.Brain.MaxHp - 0.001f;
            float scaled = RewardStatHooks.ModifyOutgoing(_ownedRewards, damage, bossFull, Time.time);
            float dealt=boss.DealDamage(scaled,aim,bossWeak);
            ApplyLifesteal(dealt);
            if (bossWeak && !piercePacket)
                boss.ApplyWeakSpotStagger(ChargeShotRules.WeakSpotStaggerSeconds);
            if (bossWeak && !piercePacket && FullChargeKnockback.Applies(kind, heldSeconds, ringFill))
            {
                float kb = FullChargeKnockback.HitDistance(
                    null, false, true, bossWeak, _ownedRewards);
                boss.ApplyKnockback(aim, kb);
            }

            float hp = boss.Brain != null ? boss.Brain.Hp : 0f;
            float max = boss.Brain != null ? boss.Brain.MaxHp : 0f;
            RogueShooter.Demo.GameTrace.Log($"[ChargeShot] hit BOSS kind={kind} dmg={scaled:0.0} along={along:0.00} " +
                      $"hp={hp:0}/{max:0}");
        }

        float HitMob(MobFourStateAi best, float damage, ChargeShotKind kind, float heldSeconds,
            Vector3 origin, Vector3 aim, bool piercePacket, float ringFill)
        {
            RewardStatHooks.SyncClock(Time.time);
            bool weak = kind == ChargeShotKind.Crit;
            bool raised = best.ShieldRaised;
            StubEnemy stub = best.GetComponent<StubEnemy>();
            bool full = stub != null && stub.IsFullHp;
            float scaled = RewardStatHooks.ModifyOutgoing(_ownedRewards, damage, full, Time.time);
            int amount = Mathf.Max(1, Mathf.RoundToInt(scaled));
            float stagger = ChargeShotRules.WeakSpotStaggerSeconds;
            bool blocked = raised && !weak && EnemyCombatRules.HitFromFront(
                best.Facing.x, best.Facing.y, origin.x, origin.y, best.transform.position.x, best.transform.position.y);
            amount = best.ModifyIncomingShot(origin, weak, amount, out stagger);
            RogueShooter.Art.ArrowImpactFeedback.Confirm(best.gameObject, origin, weak, blocked);
            int hpBefore = stub != null ? stub.Hp : 0;
            if (stub != null)
                stub.TakeDamage(amount);
            else
                best.NotifyDamaged();
            if (weak && !piercePacket)
                best.ApplyWeakSpotStagger(stagger);
            if (!piercePacket && FullChargeKnockback.Applies(kind, heldSeconds, ringFill))
            {
                if (FullChargeKnockback.RootsOnBodyHit(best.KindId, raised, weak))
                    best.ApplyRoot(FullChargeKnockback.ShieldRaisedRootSeconds);
                else
                {
                    float kb = FullChargeKnockback.HitDistance(
                        best.KindId, raised, false, weak, _ownedRewards);
                    best.ApplyKnockback(aim, kb);
                }
            }

            RogueShooter.Demo.GameTrace.Log($"[ChargeShot] hit {best.name} kind={kind} dmg={amount:0.0}" +
                      $" pierce={(piercePacket ? 1 : 0)} " +
                      $"held={heldSeconds:0.00} shieldFront={(best.ShieldRaised ? 1 : 0)} stagger={stagger:0.00}s" +
                      $" zhenshi×{KnockbackRewardDraft.DistPctProduct(_ownedRewards):0.00}");
            return stub != null ? Mathf.Max(0, hpBefore - stub.Hp) : 0;
        }

        void ApplyLifesteal(float damageDealt)
        {
            float heal = RewardStatHooks.LifestealHeal(_ownedRewards, damageDealt);
            if (heal <= 0f)
                return;
            PlayerVitals vitals = GetComponent<PlayerVitals>();
            if (vitals != null)
                vitals.Heal(heal);
        }

        Vector3 AimDirection()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 world = CameraViewMath.ScreenToWorldOnPlayPlane(cam, Input.mousePosition);
                UpdateVisualAim(world);
                Vector3 trainingAim;
                if (StartTrainingTarget.TryAim(transform, world, out trainingAim)) return trainingAim;
                Vector3 dir = world - transform.position;
                dir.z = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    return dir.normalized;
            }

            _visualAim = Vector3.zero;
            return Vector3.right;
        }

        void UpdateVisualAim(Vector3 mouseWorld)
        {
            Vector3 face;
            if (StartTrainingTarget.TryAimPoint(transform, mouseWorld, out face))
                mouseWorld = face;
            Vector3 v = mouseWorld - (transform.position + Vector3.up * .87f);
            v.z = 0f;
            _visualAim = v.sqrMagnitude > 0.01f ? v.normalized : Vector3.zero;
        }

        void CancelCharge()
        {
            if(_charging)RogueShooter.Audio.JianHaiAudio.Emit("jh_bow_charge_cancel",this);
            RogueShooter.Audio.JianHaiAudio.Stop("jh_bow_charge_loop",this);
            _charging = false;
            _held = 0f;
            _deferredChargeSeconds = 0f;
            _mid = false;
            _green = false;
            _exited = false;
            if (_fx != null)
                _fx.HideAll();
        }
    }
}
