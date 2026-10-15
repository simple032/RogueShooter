using UnityEngine;

namespace RogueShooter.Player
{
    /// <summary>
    /// crit2 凝神窥机: Q — 7.5s force 命中弱点 (ChargeShotKind.Crit ×2.0). CD 120s.
    /// Immediate weak-point shots, paced by PlayerCharge at 0.75s intervals.
    /// </summary>
    public class GuaranteedCritActive : MonoBehaviour
    {
        public const KeyCode DefaultKey = KeyCode.Q;
        public const float DurationSeconds = 7.5f;
        public const float CooldownSeconds = 120f;

        /// <summary>Maintained by the run driver (Stage1MazeDemo) every frame: the time-pressure
        /// clock is held right now (start room / lit altar room / menu pause). While held the
        /// cooldown freezes in sync with the stopped clock; the buff window and gameplay keep running.</summary>
        public static bool TimePressureHeld;

        int _weakArrows,_echoAdded;
        public int ActivationId {get;private set;}
        public event System.Action Activated;
        public int EchoAdded=>_echoAdded;
        float _buffLeft;
        float _cdLeft;

        public bool IsReady => _cdLeft <= 0f && _buffLeft <= 0f;
        public bool IsActive => _buffLeft > 0f;
        public float BuffLeft => _buffLeft;
        public float CooldownLeft => _cdLeft;

        /// <summary>Same copy as the committed focus HUD: ready / active / cooling.</summary>
        public static string HudText(float buffLeft, float cooldownLeft)
        {
            if (buffLeft > 0f)
                return "凝神窥机 生效 " + buffLeft.ToString("0.0") + "s · 冷却 " + Mathf.CeilToInt(cooldownLeft) + "s";
            if (cooldownLeft > 0f)
                return "凝神窥机 冷却 " + Mathf.CeilToInt(cooldownLeft) + "s";
            return "凝神窥机 [Q] 就绪";
        }

        public string HudLine => HudText(_buffLeft, _cdLeft);

        void OnEnable()
        {
            // Fresh player per run: never inherit a stale hold from a previous run/scene.
            TimePressureHeld = false;
        }

        void Update()
        {
            PlayerVitals vitals = GetComponent<PlayerVitals>();
            if (RunPause.IsPaused || (vitals != null && vitals.IsDown))
                return;

            if (_buffLeft > 0f)
            {
                float prev = _buffLeft;
                _buffLeft -= Time.deltaTime;
                if (prev > 0f && _buffLeft <= 0f)
                {
                    _buffLeft = 0f;
                    Debug.Log("[凝神窥机] WINDOW END");
                    RogueShooter.Audio.JianHaiAudio.Emit("jh_focus_end",this);
                }
            }

            if (_cdLeft > 0f && !TimePressureHeld)
            {
                _cdLeft -= Time.deltaTime;
                if(_cdLeft<=0f)RogueShooter.Audio.JianHaiAudio.Emit("jh_focus_ready",this);
            }

            if (Input.GetKeyDown(DefaultKey))
                TryActivate();
        }

        public bool TryActivate()
        {
            var tutorial = GetComponent<RogueShooter.Demo.StartRoomTutorial>();
            if (tutorial != null && !tutorial.AllowSkillActivation) return false;
            var vitals=GetComponent<PlayerVitals>();if(RunPause.IsPaused||vitals!=null&&vitals.IsDown)return false;
            if (_buffLeft > 0f)
                return false;
            if (_cdLeft > 0f)
                return false;

            ActivationId++;_weakArrows=0;_echoAdded=0;
            _buffLeft = DurationSeconds;
            _cdLeft = CooldownSeconds;
            Debug.Log($"[凝神窥机] ACTIVE {DurationSeconds:0.#}s CD={CooldownSeconds:0}s");
            RogueShooter.Audio.JianHaiAudio.Emit("jh_focus_start",this);
            Activated?.Invoke();
            return true;
        }

        public void ResetAfterTraining()
        {
            _buffLeft = _cdLeft = 0f;
            _weakArrows = _echoAdded = 0;
        }

        public bool NotifyWeakArrowHit(int activation)
        {
            if(!IsActive||activation==0||activation!=ActivationId||_echoAdded>=3)return false;
            _weakArrows++;if(_weakArrows%3!=0)return false;_buffLeft+=1f;_echoAdded++;return true;
        }

        public bool TryForceCrit(ChargeShotKind resolved, out ChargeShotKind forced)
        {
            forced = resolved;
            if (_buffLeft <= 0f)
                return false;

            forced = ChargeShotKind.Crit;
            return true;
        }

        public void DebugSetBuff(float seconds)
        {
            _buffLeft = Mathf.Max(0f, seconds);
        }

        public void DebugSetCooldown(float seconds)
        {
            _cdLeft = Mathf.Max(0f, seconds);
        }
    }
}
