using UnityEngine;
using RogueShooter.Player;
using RogueShooter.Ai;

namespace RogueShooter.Demo
{
    /// <summary>Presentation follows movement and action state; gameplay owns displacement and damage.</summary>
    public sealed class Stage1IsoActor : MonoBehaviour
    {
        static readonly string[] Directions = { "e", "ne", "n", "nw", "w", "sw", "s", "se" };
        string _family = "skel", _facing = "s";
        SpriteRenderer _renderer;
        RogueShooter.Iso.HeroMeshyPresentation _hero;
        Vector3 _last;
        float _clock, _walkPhase, _shieldGuardT, _shieldBreakT = -1f, _shieldBlockT = -1f;
        int _seenBreak, _seenBlock;
        string _breakFacing;
        bool _moving;
        PlayerRoll _roll;
        PlayerCharge _charge;
        MobFourStateAi _ai;
        float _attackT = -1f;
        bool _attackHit, _bowRelease;
        System.Action _onAttackHit;
        int _seenHurt;
        float _hurtLeft, _deathT = -1f;

        public const float WalkFps = 12f;
        public const float AttackFps = 12f;
        public const int AttackHitFrame = 2;
        public static int AttackFrames(string family) { return family == "dog" ? 6 : family == "mage" ? 8 : family == "archer" ? 5 : 4; }
        public static float AttackClipSeconds(string family) { return AttackFrames(family) / AttackFps; }
        public string Family => _family;
        public string Facing => _facing;
        public bool Moving => _moving;
        public bool Attacking => _attackT >= 0f;
        public string CurrentAction { get; private set; }
        public int CurrentFrame { get; private set; }
        public float WalkPhase => _hero!=null?_hero.Phase:_walkPhase;

        public bool TryBeginAttack(System.Action onHit)
        {
            if(_roll==null)_roll=GetComponent<PlayerRoll>();
            if (_attackT >= 0f || (_roll != null && _roll.LocksActions)) return false;
            StartAttack(onHit);
            return true;
        }
        public void BeginBowRelease(Vector3 aim,float progress=1f)
        {
            if (_roll != null && _roll.LocksActions) return;
            if(_family=="archer"&&RogueShooter.Iso.HeroMeshyArt.Available)
            {
                EnsureHero();CancelAttack();
                _hero.BeginRelease(aim,progress);return;
            }
            if (aim.sqrMagnitude > .001f) _facing = FacingFrom(aim);
            StartAttack(null); _bowRelease = true;
            Show("atk", 1, 5);
        }
        void StartAttack(System.Action onHit) { _attackT = 0f; _attackHit = false; _bowRelease = false; _onAttackHit = onHit; }
        void CancelAttack() { _attackT = -1f; _onAttackHit = null; _bowRelease = false; }

        public void Bind(string family)
        {
            _family = string.IsNullOrEmpty(family) ? "skel" : family;
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = 0; _renderer.color = Color.white;
            _renderer.sortingLayerName = RogueShooter.Art.JianHaiArtCatalog.LayerEntity;
            _renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            if (_family == "shield" && GetComponent<RogueShooter.Art.ShieldLungeTelegraph>() == null)
                gameObject.AddComponent<RogueShooter.Art.ShieldLungeTelegraph>();
            transform.localScale = Vector3.one; _last = transform.position;
            _moving = false; _walkPhase = _clock = _shieldGuardT = 0f;
            _seenBreak = _seenBlock = 0; _shieldBreakT = _shieldBlockT = -1f;
            CancelAttack(); Show("idle", 0, 4);
            _hurtLeft = 0f; _deathT = -1f;
            if(_family=="archer"&&RogueShooter.Iso.HeroMeshyArt.Available){EnsureHero();_hero.Bind(_renderer);}
            var vitals = GetComponent<PlayerVitals>(); _seenHurt = vitals != null ? vitals.DamageSerial : 0;
        }

        void LateUpdate()
        {
            if (_renderer == null) return;
            if(_family=="archer"&&RogueShooter.Iso.HeroMeshyArt.Available)
            {
                var vitalsForHero=GetComponent<PlayerVitals>();var heroRoll=GetComponent<PlayerRoll>();var heroRitual=GetComponent<HeroTorchRitual>();var heroCharge=GetComponent<PlayerCharge>();
                if(vitalsForHero!=null&&vitalsForHero.IsDown||heroRoll!=null&&heroRoll.LocksActions||heroRitual!=null&&heroRitual.Busy||heroCharge!=null&&heroCharge.IsCharging)CancelAttack();
                else if(_attackT>=0&&!RunPause.IsPaused&&Time.deltaTime>0)
                {
                    // Keep the existing F debug-strike callback clock; the new art does not add a melee skill.
                    _attackT+=Time.deltaTime;int strikeFrame=Mathf.FloorToInt(_attackT*AttackFps);
                    if(!_attackHit&&strikeFrame>=AttackHitFrame){_attackHit=true;var hit=_onAttackHit;_onAttackHit=null;hit?.Invoke();}
                    if(_attackT>=AttackClipSeconds(_family))CancelAttack();
                }
                EnsureHero();_hero.Step();_facing=_hero.Facing;_moving=_hero.Moving;CurrentAction=_hero.CurrentClip;CurrentFrame=_hero.CurrentFrame;_last=transform.position;return;
            }
            if (_roll == null) _roll = GetComponent<PlayerRoll>();
            if (_charge == null && _family == "archer") _charge = GetComponent<PlayerCharge>();
            if (_ai == null) _ai = GetComponent<MobFourStateAi>();
            var vitals = GetComponent<PlayerVitals>();
            // Settlement freezes gameplay/timeScale, but the last death pose still finishes.
            if (_family == "archer" && vitals != null && vitals.IsDown)
            {
                if (_deathT < 0f) { _deathT = 0f; _hurtLeft = 0f; CancelAttack(); }
                _last = transform.position; _moving = false; _renderer.color = Color.white;
                Show("die", Mathf.Min(5, Mathf.FloorToInt(_deathT*12f)), 6);
                if (!RunPause.MenuOpen) _deathT = Mathf.Min(.5f, _deathT+Time.unscaledDeltaTime);
                return;
            }
            if (_family == "archer" && vitals != null)
            {
                _deathT = -1f;
                if (vitals.DamageSerial != _seenHurt) { _seenHurt = vitals.DamageSerial; _hurtLeft = .12f; }
                if (!RunPause.IsPaused) _hurtLeft = Mathf.Max(0f, _hurtLeft-Time.deltaTime);
                // Charge, release and roll keep their pose and gameplay clock; hurt is a tint only there.
                // One red flash in .12s; no pose interruption and no extra invulnerability.
                _renderer.color = _hurtLeft > 0f ? new Color(1f,.28f,.22f) : Color.white;
            }
            var ritual=GetComponent<HeroTorchRitual>();
            if(ritual!=null&&ritual.Busy){_last=transform.position;CancelAttack();_facing=FacingFrom(ritual.Aim);Show("atk",Mathf.Clamp(Mathf.FloorToInt(ritual.Progress*4f),0,3),5);return;}
            if (RunPause.IsPaused || Time.deltaTime <= 0f || (vitals != null && vitals.IsDown)) { _last = transform.position; return; }

            Vector3 delta = transform.position - _last; delta.z = 0f; _last = transform.position;
            if (_roll != null && _roll.LocksActions)
            {
                CancelAttack(); _moving = _roll.IsRolling;
                _facing = FacingFrom((Vector3)_roll.RollDirection);
                Sprite roll = Stage1IsoArt.RollSprite(_facing, Mathf.Clamp(Mathf.FloorToInt(_roll.RollElapsed * 20f), 0, 7));
                if (roll != null) { _renderer.sprite = roll; CurrentAction = "roll"; CurrentFrame = Mathf.Clamp(Mathf.FloorToInt(_roll.RollElapsed * 20f), 0, 7); }
                _renderer.flipX = false; return;
            }

            float distance = delta.magnitude;
            // Keep stride phase through draws/releases as long as the body actually moves.
            bool walking = distance > .0001f && distance < Mathf.Max(.5f,Time.deltaTime*30f)
                && (_ai == null || (!_ai.IsStaggered && !_ai.IsKnocking && !_ai.IsRooted));
            if (walking)
            {
                float referenceSpeed = _family == "archer" ? 6f : _ai != null ? Mathf.Max(.1f,_ai.ChaseSpeed) : 3.6f;
                float fps = _family == "archer" || _family == "dog" ? WalkFps : 8f;
                _walkPhase = Mathf.Repeat(_walkPhase + distance*fps/(referenceSpeed*6f), 1f);
            }

            if (_ai != null && _family == "shield")
            {
                if (_ai.ShieldBreakSerial != _seenBreak)
                {
                    _seenBreak = _ai.ShieldBreakSerial; _shieldBreakT = _ai.ShieldBroken ? 0f : -1f; _shieldBlockT = -1f;
                    _breakFacing = _ai.Facing.sqrMagnitude > .001f ? FacingFrom(_ai.Facing) : _facing;
                    CancelAttack();
                }
                if (_ai.ShieldBlockSerial != _seenBlock)
                { bool newBlock = _ai.ShieldBlockSerial > _seenBlock; _seenBlock = _ai.ShieldBlockSerial; _shieldBlockT = newBlock && !_ai.ShieldBroken ? 0f : -1f; }
                if (_shieldBreakT >= 0f && _shieldBreakT < .5f)
                {
                    _moving = false; _facing = _breakFacing;
                    Show("break", Mathf.Min(5, Mathf.FloorToInt(_shieldBreakT*12f)), 6);
                    _shieldBreakT += Time.deltaTime; return;
                }
                if (!_ai.ShieldBroken && !_ai.IsStaggered && !string.IsNullOrEmpty(_ai.LungePhase))
                {
                    _moving = _ai.LungePhase == "冲";
                    _facing = FacingFrom(_ai.LungeAim);
                    float progress = _ai.LungeProgress;
                    int frame = _ai.LungePhase == "跟踪" ? Mathf.Min(5, Mathf.FloorToInt(progress*6f))
                        : _ai.LungePhase == "锁定" ? 6+Mathf.Min(7, Mathf.FloorToInt(progress*8f))
                        : _ai.LungePhase == "冲" ? 14+Mathf.Min(5, Mathf.FloorToInt(progress*6f))
                        : 20+Mathf.Min(11, Mathf.FloorToInt(progress*12f));
                    Show("dash", frame, 32);
                    return;
                }
                if (_ai.InMeleeWindup && !_ai.IsStaggered)
                {
                    _moving = false; _facing = FacingFrom(_ai.Facing);
                    Show("atk", _ai.WindupProgress < .6f ? 0 : 1, 4); return;
                }
                float releaseAge = Time.time - _ai.MeleeReleasedAt;
                if (!_ai.IsStaggered && releaseAge >= 0f && releaseAge < .18f)
                { _moving = false; Show("atk", releaseAge < .09f ? 2 : 3, 4); return; }
                if (!_ai.ShieldBroken && _shieldBlockT >= 0f && _shieldBlockT < .18f)
                {
                    _shieldBlockT += Time.deltaTime; _moving = false;
                    _facing = FacingFrom(_ai.Facing);
                    Show("guard", _shieldBlockT < .09f ? 2 : 1, GuardFrames()); return;
                }
            }
            if (_ai != null && !_ai.IsStaggered)
            {
                if (_ai.InMeleeWindup || _ai.InCastWindup)
                {
                    if (_ai.Facing.sqrMagnitude > .001f) _facing = FacingFrom(_ai.Facing);
                    _moving = false;
                    int readyFrames = _ai.InCastWindup ? 4 : 2;
                    Show(_ai.InCastWindup ? "cast" : "atk", Mathf.Min(readyFrames-1, Mathf.FloorToInt(_ai.WindupProgress*readyFrames)), AttackFrames(_family));
                    return;
                }
                bool cast = _family == "mage";
                float age = Time.time - (cast ? _ai.CastReleasedAt : _ai.MeleeReleasedAt);
                int first = cast ? 4 : 2;
                int span = AttackFrames(_family);
                float releaseFps=_family=="dog"?20f:AttackFps;
                if (age >= 0f && age < (span-first)/releaseFps)
                {
                    _moving = false;
                    Show(cast ? "cast" : "atk", first+Mathf.Min(span-first-1, Mathf.FloorToInt(age*releaseFps)), span);
                    return;
                }
            }

            // A draw advances once with gameplay charge and holds at full draw, including overcharge.
            if (_family == "archer" && _charge != null && _charge.IsCharging)
            {
                CancelAttack(); _moving = false;
                if (_charge.VisualAimDir.sqrMagnitude > .001f) _facing = FacingFrom(_charge.VisualAimDir);
                int frame = Mathf.Min(5, Mathf.FloorToInt(Mathf.Clamp01(_charge.CurrentRead.Progress(_charge.HeldSeconds))*6f));
                Show("charge", frame, 6); return;
            }
            if (_bowRelease && _attackT >= 0f)
            {
                // The arrow already exists: show release, then recovery, without redrawing.
                _moving = false;
                Show("atk", _attackT < 1f/AttackFps ? 1 : _attackT < 2f/AttackFps ? 3 : 4, 5);
                _attackT += Time.deltaTime;
                if (_attackT >= 3f/AttackFps) CancelAttack();
                return;
            }
            if (_attackT >= 0f)
            {
                _attackT += Time.deltaTime;
                int frame = Mathf.FloorToInt(_attackT*AttackFps);
                if (!_attackHit && frame >= AttackHitFrame)
                {
                    _attackHit = true; var hit = _onAttackHit; _onAttackHit = null; hit?.Invoke();
                }
                _moving = false;
                Show("atk", Mathf.Min(frame,AttackFrames(_family)-1), AttackFrames(_family));
                if (_attackT >= AttackClipSeconds(_family)) CancelAttack();
                return;
            }

            if (walking) _facing = StableFacing(delta);
            else if (_ai != null && _ai.Facing.sqrMagnitude > .001f) _facing = StableFacing(_ai.Facing);
            _moving = walking;
            if (_family == "archer" && _hurtLeft > 0f)
            { Show("hurt", Mathf.Min(2, Mathf.FloorToInt((.25f-_hurtLeft)*12f)), 3); return; }
            if (walking)
            {
                bool raised = _family == "shield" && _ai != null && _ai.ShieldRaised;
                int frames = raised ? 4 : 6;
                _shieldGuardT = 0f;
                Show(raised ? "walk_shield" : "walk", Mathf.Min(frames-1, Mathf.FloorToInt(_walkPhase*frames)), frames);
            }
            else
            {
                _clock += Time.deltaTime*4f;
                if (_family == "shield" && _ai != null && _ai.ShieldRaised)
                { _shieldGuardT += Time.deltaTime; Show("guard", _shieldGuardT < .08f ? 0 : 1, GuardFrames()); }
                else { _shieldGuardT = 0f; Show("idle",Mathf.FloorToInt(_clock)%4,4); }
            }
        }

        int GuardFrames() { bool f; string s; RogueShooter.Iso.IsoFacing.TrySkeleton(_facing, out s, out f); return s == "e" ? 4 : s == "n" ? 5 : 6; }
        void EnsureHero(){if(_hero==null){_hero=GetComponent<RogueShooter.Iso.HeroMeshyPresentation>();if(_hero==null)_hero=gameObject.AddComponent<RogueShooter.Iso.HeroMeshyPresentation>();}}

        void Show(string action,int frame,int span)
        {
            if (_family == "shield" && _ai != null && _ai.ShieldBroken && action != "break")
            {
                // Shield broken: remap to unshielded clips. walk_shield/guard have no
                // unshielded counterpart (no shield to raise or hold) so they fall to
                // unshielded_idle. walk/atk play their unshielded versions directly.
                if (action == "walk_shield" || action == "guard")
                { action = "idle"; frame = Mathf.FloorToInt(_clock) % 4; span = 4; }
                action = "unshielded_" + action;
                if (!Stage1IsoArt.HasActorClip(_family, action, _facing, span))
                { action = "break"; frame = 5; span = 6; }
            }
            bool flip; Sprite sprite = Stage1IsoArt.ActorAction(_family,action,_facing,frame,span,out flip);
            if (sprite != null) { _renderer.sprite = sprite; CurrentAction = action; CurrentFrame = frame; }
            _renderer.flipX = flip;
        }
        string StableFacing(Vector3 direction)
        {
            float angle = Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            float current = FacingAngle(_facing);
            return Mathf.Abs(Mathf.DeltaAngle(current,angle)) <= 27f ? _facing : FacingFrom(direction);
        }
        static float FacingAngle(string f) { switch(f) { case "e":return 0;case "ne":return 45;case "n":return 90;case "nw":return 135;case "w":return 180;case "sw":return -135;case "s":return -90;default:return -45; } }
        static string FacingFrom(Vector3 v)
        {
            int idx=(Mathf.RoundToInt(Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg/45f)%8+8)%8;
            return Directions[idx];
        }
    }
}
