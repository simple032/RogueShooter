using UnityEngine;

namespace RogueShooter.Player
{
    /// <summary>
    /// Pure 凝神窥机 timer/rules (no Unity time) so acceptance checks can drive it headless.
    /// Duration / cooldown / min-charge come from GuaranteedCritActive constants.
    /// </summary>
    public sealed class FocusSkillState
    {
        public float BuffLeft { get; private set; }
        public float CooldownLeft { get; private set; }
        public bool IsReady => CooldownLeft <= 0f && BuffLeft <= 0f;
        public bool IsActive => BuffLeft > 0f;

        /// <summary>Returns true when the window just ended on this tick.</summary>
        public bool Tick(float dt, bool paused)
        {
            if (paused || dt <= 0f)
                return false;
            bool ended = false;
            if (BuffLeft > 0f)
            {
                BuffLeft -= dt;
                if (BuffLeft <= 0f)
                {
                    BuffLeft = 0f;
                    ended = true;
                }
            }

            if (CooldownLeft > 0f)
            {
                CooldownLeft -= dt;
                if (CooldownLeft < 0f)
                    CooldownLeft = 0f;
            }

            return ended;
        }

        public bool TryActivate()
        {
            if (BuffLeft > 0f || CooldownLeft > 0f)
                return false;
            BuffLeft = GuaranteedCritActive.DurationSeconds;
            CooldownLeft = GuaranteedCritActive.CooldownSeconds; // CD counts from activation
            return true;
        }

        /// <summary>
        /// Shot kind while the skill may be up. Active: held &lt; FocusMinChargeSeconds → None
        /// (no shot; GDD §5.1 最短蓄力 不足不发射 — §10-5 does not say otherwise); any other shot → Crit
        /// (必中弱点). Charge progress / weak-spot window are not touched (no auto-fill).
        /// Inactive: base kind unchanged.
        /// </summary>
        public ChargeShotKind ResolveShot(float heldSeconds, ChargeShotKind baseKind)
        {
            if (BuffLeft <= 0f)
                return baseKind;
            if (heldSeconds < GuaranteedCritActive.FocusMinChargeSeconds || baseKind == ChargeShotKind.None)
                return ChargeShotKind.None;
            return ChargeShotKind.Crit;
        }

        public void DebugSet(float buffLeft, float cooldownLeft)
        {
            BuffLeft = Mathf.Max(0f, buffLeft);
            CooldownLeft = Mathf.Max(0f, cooldownLeft);
        }
    }

    /// <summary>
    /// crit2 凝神窥机: Q — 15s force 命中弱点 (ChargeShotKind.Crit ×2.0). CD 120s from activation.
    /// Needs ≥ FocusMinChargeSeconds (0.3s) while active; recover stays ChargeShotRules.RecoverSeconds;
    /// does not auto-fill the weak-spot window. HUD (duration + CD) and a code-only tint on the
    /// player sprite + reticle while active. Runs after EntityAnimView so the tint survives rebinds.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class GuaranteedCritActive : MonoBehaviour
    {
        public const KeyCode DefaultKey = KeyCode.Q;
        public const float DurationSeconds = 15f;
        public const float CooldownSeconds = 120f;
        /// <summary>GDD §5.4 / §10-5: 仍须最短蓄力 0.3s while the skill is active (normal shots keep ChargeShotRules.MinChargeSeconds).</summary>
        public const float FocusMinChargeSeconds = 0.30f;

        /// <summary>Player tint while active (warm gold, pulses slightly).</summary>
        public static readonly Color ActiveTint = new Color(1f, 0.84f, 0.50f, 1f);
        public const float TintPulseHz = 2f;

        /// <summary>True while any player's window is up (reticle recolour reads this).</summary>
        public static bool AnyActive { get; private set; }

        readonly FocusSkillState _state = new FocusSkillState();
        SpriteRenderer _sr;
        bool _tinted;

        public FocusSkillState State => _state;
        public bool IsReady => _state.IsReady;
        public bool IsActive => _state.IsActive;
        public float BuffLeft => _state.BuffLeft;
        public float CooldownLeft => _state.CooldownLeft;

        void Update()
        {
            if (_state.Tick(Time.deltaTime, RunPause.IsPaused))
                Debug.Log("[凝神窥机] WINDOW END cd=" + _state.CooldownLeft.ToString("0.0") + "s");
            AnyActive = _state.IsActive;
            if (RunPause.IsPaused)
                return;
            if (Input.GetKeyDown(DefaultKey))
                TryActivate();
        }

        void LateUpdate()
        {
            if (_sr == null)
                _sr = GetComponent<SpriteRenderer>();
            if (_sr == null)
                return;
            if (_state.IsActive)
            {
                _sr.color = TintAt(Time.time);
                _tinted = true;
            }
            else if (_tinted)
            {
                _sr.color = Color.white;
                _tinted = false;
            }
        }

        void OnDisable()
        {
            AnyActive = false;
            if (_tinted && _sr != null)
                _sr.color = Color.white;
            _tinted = false;
        }

        public static Color TintAt(float time)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(time * TintPulseHz * Mathf.PI * 2f);
            return Color.Lerp(ActiveTint, Color.Lerp(ActiveTint, Color.white, 0.35f), k);
        }

        public bool TryActivate()
        {
            if (!_state.TryActivate())
                return false;
            AnyActive = true;
            Debug.Log($"[凝神窥机] ACTIVE {DurationSeconds:0}s CD={CooldownSeconds:0}s minCharge={FocusMinChargeSeconds:0.00}s");
            return true;
        }

        /// <summary>Kept for callers: true when the kind was changed by the skill.</summary>
        public bool TryForceCrit(ChargeShotKind resolved, out ChargeShotKind forced)
        {
            return TryForceCrit(resolved, float.MaxValue, out forced);
        }

        public bool TryForceCrit(ChargeShotKind resolved, float heldSeconds, out ChargeShotKind forced)
        {
            forced = _state.ResolveShot(heldSeconds, resolved);
            return forced != resolved;
        }

        public void DebugSetBuff(float seconds)
        {
            _state.DebugSet(seconds, _state.CooldownLeft);
            AnyActive = _state.IsActive;
        }

        public void DebugSetCooldown(float seconds)
        {
            _state.DebugSet(_state.BuffLeft, seconds);
        }

        /// <summary>HUD copy: ready / active (duration + CD) / cooling.</summary>
        public static string HudText(float buffLeft, float cooldownLeft)
        {
            if (buffLeft > 0f)
                return "凝神窥机 生效 " + buffLeft.ToString("0.0") + "s · 冷却 " + Mathf.CeilToInt(cooldownLeft) + "s";
            if (cooldownLeft > 0f)
                return "凝神窥机 冷却 " + Mathf.CeilToInt(cooldownLeft) + "s";
            return "凝神窥机 [Q] 就绪";
        }

        public string HudLine => HudText(_state.BuffLeft, _state.CooldownLeft);

        void OnGUI()
        {
            // Player HUD, bottom-left. Hidden while a reward/shop panel pauses the run.
            if (RunPause.IsPaused)
                return;
            const float w = 300f;
            const float h = 30f;
            var rect = new Rect(12f, Screen.height - h - 12f, w, h);
            GUI.Box(rect, "");
            if (_state.IsActive)
            {
                float fill = Mathf.Clamp01(_state.BuffLeft / DurationSeconds);
                Color prev = GUI.color;
                GUI.color = new Color(ActiveTint.r, ActiveTint.g, ActiveTint.b, 0.55f);
                GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (w - 4f) * fill, h - 4f), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            else if (_state.CooldownLeft > 0f)
            {
                float fill = 1f - Mathf.Clamp01(_state.CooldownLeft / CooldownSeconds);
                Color prev = GUI.color;
                GUI.color = new Color(0.5f, 0.55f, 0.62f, 0.45f);
                GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (w - 4f) * fill, h - 4f), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = _state.IsActive ? new Color(1f, 0.92f, 0.7f) : Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y, w - 12f, h), HudLine, style);
        }
    }
}
