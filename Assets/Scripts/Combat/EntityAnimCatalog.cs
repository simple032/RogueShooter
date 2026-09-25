using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RogueShooter.Art;
using RogueShooter.Spawning;

namespace RogueShooter.Combat
{
    public enum EntityAnimState
    {
        Idle = 0,
        Walk = 1,
        Charge = 2,
        Attack = 3,
        Dodge = 4,
        Hurt = 5,
        Death = 6,
        Alert = 7,
        Chase = 8,
        Cast = 9
    }

    /// <summary>
    /// ACTION_SPEC_P1 clip hooks. Present() is file-based under Assets/Art/JianHai/.
    /// Missing walk/atk PNGs fall back to idle — do not invent frames.
    /// placeholders_p1 (132×64) drop in as numbered jh_*_##; same-name true art replaces.
    /// </summary>
    public static class EntityAnimCatalog
    {
        /// <summary>INDEX clip roots (mage chase is on disk but ACTION Chase reuses walk).</summary>
        public static readonly string[] PlaceholderClipRoots =
        {
            "jh_char_archer_idle",
            "jh_char_archer_walk",
            "jh_char_archer_charge",
            "jh_char_archer_atk",
            "jh_char_archer_roll",
            "jh_char_archer_hurt",
            "jh_char_archer_die",
            "jh_enemy_e1_skel_idle",
            "jh_enemy_e1_skel_walk",
            "jh_enemy_e1_skel_alert",
            "jh_enemy_e1_skel_chase",
            "jh_enemy_e1_skel_atk",
            "jh_enemy_e1_skel_hurt",
            "jh_enemy_e1_skel_die",
            "jh_enemy_dog_idle",
            "jh_enemy_dog_walk",
            "jh_enemy_dog_alert",
            "jh_enemy_dog_chase",
            "jh_enemy_dog_atk",
            "jh_enemy_dog_hurt",
            "jh_enemy_dog_die",
            "jh_enemy_mage_idle",
            "jh_enemy_mage_walk",
            "jh_enemy_mage_alert",
            "jh_enemy_mage_cast",
            "jh_enemy_mage_hurt",
            "jh_enemy_mage_die"
        };

        public static string PlayerIdle => JianHaiArtCatalog.PlayerIdle;
        public static string EnemyIdle => JianHaiArtCatalog.EnemyE1Idle;

        public const string PlayerWalk = "jh_char_archer_walk";
        public const string PlayerRoll = "jh_char_archer_roll";
        public const string PlayerDodge = PlayerRoll;
        public const string PlayerCharge = "jh_char_archer_charge";
        public const string PlayerAtk = "jh_char_archer_atk";
        public const string PlayerHurt = "jh_char_archer_hurt";
        public const string PlayerDie = "jh_char_archer_die";

        public static string ResolvePlayer(EntityAnimState state)
        {
            string wanted = PlayerSprite(state);
            return Present(wanted) ? wanted : PlayerIdle;
        }

        public static string ResolveEnemy(EntityAnimState state)
        {
            return ResolveEnemy(state, EnemyKindIds.Normal);
        }

        public static string ResolveEnemy(EntityAnimState state, string kindId)
        {
            string wanted = EnemySprite(state, kindId);
            string idle = ActionSpecP1.EnemyIdleRoot(kindId);
            if (Present(wanted))
                return wanted;
            return ResolveEnemyIdle(kindId);
        }

        public static string ResolveEnemyIdle(string kindId)
        {
            // Framed first frame (root_00), never the bare root: jh_enemy_e1_skel_idle.png is a
            // solid-colour placeholder that renders as a red square.
            string idle = ActionSpecP1.EnemyIdleRoot(kindId);
            return JianHaiSprites.ResolveClipArt(idle, null, 0, EnemyIdle);
        }

        public static string PlayerSprite(EntityAnimState state)
        {
            switch (state)
            {
                case EntityAnimState.Walk: return PlayerWalk;
                case EntityAnimState.Dodge: return PlayerRoll;
                case EntityAnimState.Charge: return PlayerCharge;
                case EntityAnimState.Attack: return PlayerAtk;
                case EntityAnimState.Hurt: return PlayerHurt;
                case EntityAnimState.Death: return PlayerDie;
                default: return PlayerIdle;
            }
        }

        public static string EnemySprite(EntityAnimState state, string kindId)
        {
            switch (state)
            {
                case EntityAnimState.Walk: return ActionSpecP1.EnemyWalk(kindId).Root;
                case EntityAnimState.Alert: return ActionSpecP1.EnemyAlert(kindId).Root;
                case EntityAnimState.Chase: return ActionSpecP1.EnemyChase(kindId).Root;
                case EntityAnimState.Attack:
                case EntityAnimState.Cast: return ActionSpecP1.EnemyAttack(kindId).Root;
                case EntityAnimState.Hurt: return ActionSpecP1.EnemyHurt(kindId).Root;
                case EntityAnimState.Death: return ActionSpecP1.EnemyDeath(kindId).Root;
                default: return ActionSpecP1.EnemyIdleRoot(kindId);
            }
        }

        public static bool Present(string artId)
        {
            return JianHaiSprites.HasClip(artId);
        }

        public static ActionClipDef PlayerClip(EntityAnimState state)
        {
            switch (state)
            {
                case EntityAnimState.Walk: return ActionSpecP1.PlayerWalk;
                case EntityAnimState.Dodge: return ActionSpecP1.PlayerRoll;
                case EntityAnimState.Charge: return ActionSpecP1.PlayerCharge;
                case EntityAnimState.Attack: return ActionSpecP1.PlayerFire;
                case EntityAnimState.Hurt: return ActionSpecP1.PlayerHurt;
                case EntityAnimState.Death: return ActionSpecP1.PlayerDeath;
                default: return ActionSpecP1.PlayerIdle;
            }
        }

        public static ActionClipDef EnemyClip(EntityAnimState state, string kindId)
        {
            switch (state)
            {
                case EntityAnimState.Walk: return ActionSpecP1.EnemyWalk(kindId);
                case EntityAnimState.Alert: return ActionSpecP1.EnemyAlert(kindId);
                case EntityAnimState.Chase: return ActionSpecP1.EnemyChase(kindId);
                case EntityAnimState.Attack:
                case EntityAnimState.Cast: return ActionSpecP1.EnemyAttack(kindId);
                case EntityAnimState.Hurt: return ActionSpecP1.EnemyHurt(kindId);
                case EntityAnimState.Death: return ActionSpecP1.EnemyDeath(kindId);
                default: return ActionSpecP1.EnemyIdle(kindId);
            }
        }

        static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>
        /// Exact frame for a clip (root_dir_ff → root_ff → root_s_ff). When that frame is missing,
        /// the entity's idle_00. Never the bare root (jh_enemy_e1_skel_idle.png is a solid red
        /// square) and never a generated placeholder: null means "keep the current sprite".
        /// </summary>
        public static string ResolveFrame(string root, string dir, int frame, bool player, string kindId)
        {
            string exact = ExactFrame(root, dir, frame);
            if (exact != null)
                return exact;
            string idle = IdleFrame0(player, kindId);
            WarnOnce("frame " + root + " f" + frame + " dir=" + (dir ?? "-"),
                "[Anim] missing frame " + root + "_" + Two(frame) + " (dir " + (dir ?? "-") + ") → " + (idle ?? "keep current"));
            return idle;
        }

        public static string ExactFrame(string root, string dir, int frame)
        {
            if (string.IsNullOrEmpty(root))
                return null;
            string ff = Two(frame < 0 ? 0 : frame);
            if (!string.IsNullOrEmpty(dir) && JianHaiSprites.HasSourceFile(root + "_" + dir + "_" + ff))
                return root + "_" + dir + "_" + ff;
            if (JianHaiSprites.HasSourceFile(root + "_" + ff))
                return root + "_" + ff;
            if (dir != "s" && JianHaiSprites.HasSourceFile(root + "_s_" + ff))
                return root + "_s_" + ff;
            return null;
        }

        /// <summary>idle_00 of this entity (framed), then the generic player / E1 idle_00; null if none on disk.</summary>
        public static string IdleFrame0(bool player, string kindId)
        {
            string root = player ? ActionSpecP1.PlayerIdle.Root : ActionSpecP1.EnemyIdleRoot(kindId);
            if (JianHaiSprites.HasSourceFile(root + "_00"))
                return root + "_00";
            if (JianHaiSprites.HasSourceFile(root + "_s_00"))
                return root + "_s_00";
            string generic = player ? JianHaiArtCatalog.PlayerIdle : JianHaiArtCatalog.EnemyE1Idle;
            return JianHaiSprites.HasSourceFile(generic) ? generic : null;
        }

        /// <summary>Grip 1→2 transition art for this hold time, or null (outside the window or not delivered).</summary>
        public static string GripArt(float heldSeconds)
        {
            int f = ActionSpecP1.GripFrameAt(heldSeconds);
            if (f <= 0)
                return null;
            string id = ActionSpecP1.GripArtId(f);
            if (JianHaiSprites.HasSourceFile(id))
                return id;
            WarnOnce("grip " + id, "[Anim] " + id + " not delivered — grip transition skipped (charge _01 → _02)");
            return null;
        }

        /// <summary>Test hook: how many distinct missing-art warnings were issued.</summary>
        public static int WarnedCount => Warned.Count;

        static void WarnOnce(string key, string message)
        {
            if (!Warned.Add(key))
                return;
            Debug.LogWarning(message);
        }

        static string Two(int f)
        {
            return f < 10 ? "0" + f : f.ToString();
        }

        public static string GapNote()
        {
            var sb = new StringBuilder();
            sb.Append("ACTION_SPEC_P1 + PHASE1_PLAYABLE + placeholders_p1 wired; missing PNGs fall back to idle. ");
            sb.Append("player: idle/walk_s/charge/atk/roll/hurt/die present; missing walk n/e/w (s only; flipX mirrors). ");
            sb.Append("S1 e1_skel/dog/mage idle/walk/alert/chase|cast/atk/hurt/die present. ");
            sb.Append("mage chase_00 unused (ACTION Chase reuses walk). ");
            sb.Append("same-name PNG replaces; no Animator; arrow=jh_proj_arrow_fly; orb=jh_proj_orb_mage_fly");
            return sb.ToString();
        }
    }
}
