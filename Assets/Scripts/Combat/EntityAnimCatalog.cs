using System.Text;
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
            if (Present(idle))
                return idle;
            return EnemyIdle;
        }

        public static string ResolveEnemyIdle(string kindId)
        {
            string idle = ActionSpecP1.EnemyIdleRoot(kindId);
            return Present(idle) ? idle : EnemyIdle;
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
