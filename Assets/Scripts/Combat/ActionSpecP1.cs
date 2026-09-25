using RogueShooter.Art;
using RogueShooter.Player;
using RogueShooter.Spawning;

namespace RogueShooter.Combat
{
    public struct ActionClipDef
    {
        public string Root;
        public int Frames;
        public float Duration;
        public bool Loop;
        public int EventFrame;
        public string EventName;

        public float EventTime
        {
            get
            {
                if (EventFrame < 0 || Frames < 1 || Duration < 0.0001f)
                    return -1f;
                return EventFrame * (Duration / Frames);
            }
        }

        public int FrameAt(float elapsed)
        {
            if (Frames <= 1)
                return 0;
            if (Duration < 0.0001f)
                return 0;
            float u = elapsed / Duration;
            if (Loop)
            {
                u -= (int)u;
                if (u < 0f)
                    u = 0f;
                int f = (int)(u * Frames);
                return f >= Frames ? 0 : f;
            }

            if (u >= 1f)
                return Frames - 1;
            if (u <= 0f)
                return 0;
            int i = (int)(u * Frames);
            return i >= Frames ? Frames - 1 : i;
        }
    }

    /// <summary>
    /// ACTION_SPEC_P1_v01 clip table. 12 fps naming; missing PNGs fall back to idle.
    /// Roll i-frame numbers live in DodgeRules (producer draft, not locked).
    /// Charge combat times stay in ChargeShotRules / ChargeProfile (ring 0.70 at 0B; window 76%–84% of full).
    /// </summary>
    public static class ActionSpecP1
    {
        public const float Fps = 12f;
        public const string SpecFile = "Assets/Art/JianHai/_Spec/ACTION_SPEC_P1_v01.md";

        /// <summary>Pose-only full-draw mark from the action spec. Not a combat lock (ring fill stays 0.70s).</summary>
        public const float ChargeFullPoseSeconds = 0.90f;
        public const float ChargeWindupPoseSeconds = 0.14f;

        /// <summary>Roll i-frame window lives in DodgeRules (producer draft, unlocked).</summary>
        public static float PlayerRollIFrameStart => DodgeRules.IFrameStartSeconds;
        public static float PlayerRollIFrameEnd => DodgeRules.IFrameEndSeconds;
        public static int PlayerRollIFrameStartFrame => PlayerRoll.FrameAt(DodgeRules.IFrameStartSeconds);
        public static int PlayerRollIFrameEndFrame => PlayerRoll.FrameAt(DodgeRules.IFrameEndSeconds);

        // Clip root is the bare name (frames _00.._03); JianHaiArtCatalog.PlayerIdle is already frame _00.
        public static ActionClipDef PlayerIdle => Clip("jh_char_archer_idle", 4, 0.33f, true);
        public static ActionClipDef PlayerWalk => Clip("jh_char_archer_walk", 6, 0.50f, true);
        public static ActionClipDef PlayerCharge => Clip("jh_char_archer_charge", 6, ChargeFullPoseSeconds, true);
        public static ActionClipDef PlayerFire => Clip("jh_char_archer_atk", 4, 0.33f, false, 1, "OnFire");
        public static ActionClipDef PlayerRoll => Clip("jh_char_archer_roll", 8, DodgeRules.DurationSeconds, false);
        public static ActionClipDef PlayerHurt => Clip("jh_char_archer_hurt", 3, 0.25f, false);
        public static ActionClipDef PlayerDeath => Clip("jh_char_archer_die", 6, 0.50f, false);

        public static float PlayerOnFireSeconds => PlayerFire.EventTime;

        public static ActionClipDef EnemyIdle(string kindId)
        {
            return Clip(EnemyRoot(kindId) + "_idle", 4, 0.33f, true);
        }

        public static ActionClipDef EnemyWalk(string kindId)
        {
            bool dog = IsDog(kindId);
            return Clip(EnemyRoot(kindId) + "_walk", 6, dog ? 0.40f : 0.50f, true);
        }

        public static ActionClipDef EnemyAlert(string kindId)
        {
            bool dog = IsDog(kindId);
            return Clip(EnemyRoot(kindId) + "_alert", dog ? 2 : 3, dog ? 0.17f : 0.25f, false);
        }

        public static ActionClipDef EnemyChase(string kindId)
        {
            if (IsMage(kindId))
                return EnemyWalk(kindId);
            bool dog = IsDog(kindId);
            return Clip(EnemyRoot(kindId) + "_chase", 6, dog ? 0.40f : 0.50f, true);
        }

        public static ActionClipDef EnemyAttack(string kindId)
        {
            if (IsMage(kindId))
                return Clip(EnemyRoot(kindId) + "_cast", 8, 0.67f, false, 4, "OnOrbSpawn");
            if (IsDog(kindId))
                return Clip(EnemyRoot(kindId) + "_atk", 6, 0.50f, false, 2, "OnHitOpen");
            return Clip(EnemyRoot(kindId) + "_atk", 8, 0.67f, false, 3, "OnHitOpen");
        }

        public static ActionClipDef EnemyHurt(string kindId)
        {
            bool dog = IsDog(kindId);
            return Clip(EnemyRoot(kindId) + "_hurt", dog ? 2 : 3, dog ? 0.17f : 0.25f, false);
        }

        public static ActionClipDef EnemyDeath(string kindId)
        {
            bool dog = IsDog(kindId);
            return Clip(EnemyRoot(kindId) + "_die", dog ? 4 : 5, dog ? 0.33f : 0.42f, false);
        }

        public static string EnemyRoot(string kindId)
        {
            if (IsDog(kindId))
                return "jh_enemy_dog";
            if (IsMage(kindId))
                return "jh_enemy_mage";
            return "jh_enemy_e1_skel";
        }

        public static string EnemyIdleRoot(string kindId)
        {
            return EnemyIdle(kindId).Root;
        }

        /// <summary>§1.3 charge poses at 0B. Green = the combat weak-spot window, not a new window.</summary>
        public static int ChargePoseFrame(float heldSeconds)
        {
            return ChargePoseFrame(heldSeconds, ChargeProfile.Base);
        }

        /// <summary>Poses for a loadout: windup _00–_01, draw loop _02/_03 until the window, _04 in the window, _05 after.</summary>
        public static int ChargePoseFrame(float heldSeconds, ChargeProfile profile)
        {
            if (heldSeconds < ChargeWindupPoseSeconds && heldSeconds + ChargeShotRules.EdgeEpsilon < profile.WindowEnter)
                return heldSeconds < ChargeWindupPoseSeconds * 0.5f ? 0 : 1;
            if (heldSeconds + ChargeShotRules.EdgeEpsilon < profile.WindowEnter)
                return 2 + ((int)(heldSeconds * Fps) & 1);
            if (profile.InWindow(heldSeconds))
                return 4;
            return 5;
        }

        /// <summary>
        /// Archer grip transition between charge stage 1 (windup _00–_01) and stage 2 (draw _02–_03).
        /// Frames grip_1to2_01/02 are not delivered yet: play when present, skip when missing.
        /// Pose only at the spec 12 fps; combat timing (ChargeShotRules) is unchanged.
        /// </summary>
        public const string PlayerGrip1to2Root = "jh_char_archer_grip_1to2";
        public const int PlayerGrip1to2Frames = 2;

        /// <summary>1-based grip frame (1 = _01, 2 = _02) right after the windup pose ends, else 0.</summary>
        public static int GripFrameAt(float heldSeconds)
        {
            return GripFrameAt(heldSeconds, ChargeProfile.Base);
        }

        /// <summary>Grip slot never overlaps the weak-spot window (疾张 can pull the window early).</summary>
        public static int GripFrameAt(float heldSeconds, ChargeProfile profile)
        {
            if (heldSeconds + ChargeShotRules.EdgeEpsilon >= profile.WindowEnter)
                return 0;
            if (heldSeconds < ChargeWindupPoseSeconds)
                return 0;
            int i = (int)((heldSeconds - ChargeWindupPoseSeconds) * Fps);
            return i >= 0 && i < PlayerGrip1to2Frames ? i + 1 : 0;
        }

        public static string GripArtId(int frame)
        {
            return PlayerGrip1to2Root + "_" + (frame < 10 ? "0" + frame : frame.ToString());
        }

        public static bool IsMageKind(string kindId)
        {
            return IsMage(kindId);
        }

        public static string Cardinal(float x, float y)
        {
            if (x * x + y * y < 0.0001f)
                return "s";
            if (Abs(x) > Abs(y))
                return x >= 0f ? "e" : "w";
            return y >= 0f ? "n" : "s";
        }

        static ActionClipDef Clip(string root, int frames, float duration, bool loop, int eventFrame = -1, string eventName = null)
        {
            return new ActionClipDef
            {
                Root = root,
                Frames = frames < 1 ? 1 : frames,
                Duration = duration < 0.01f ? 0.01f : duration,
                Loop = loop,
                EventFrame = eventFrame,
                EventName = eventName
            };
        }

        static bool IsDog(string kindId)
        {
            return kindId == EnemyKindIds.Dog || kindId == "E2";
        }

        static bool IsMage(string kindId)
        {
            return kindId == EnemyKindIds.CultMage || kindId == EnemyKindIds.GrandMage
                || kindId == "E3" || kindId == "GRAND";
        }

        static float Abs(float v)
        {
            return v < 0f ? -v : v;
        }
    }
}
