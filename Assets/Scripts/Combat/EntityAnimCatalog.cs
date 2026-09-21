using System.Text;
using RogueShooter.Art;

namespace RogueShooter.Combat
{
    public enum EntityAnimState
    {
        Idle = 0,
        Walk = 1,
        Charge = 2,
        Attack = 3,
        Dodge = 4
    }

    /// <summary>
    /// Animation clip hooks. Present() is file-based under Assets/Art/JianHai/.
    /// Missing walk/atk/dodge PNGs fall back to idle — do not invent frames here.
    /// </summary>
    public static class EntityAnimCatalog
    {
        public static string PlayerIdle => JianHaiArtCatalog.PlayerIdle;
        public static string EnemyIdle => JianHaiArtCatalog.EnemyE1Idle;

        public const string PlayerWalk = "jh_char_archer_walk";
        public const string PlayerDodge = "jh_char_archer_dodge";
        public const string PlayerCharge = "jh_char_archer_charge";
        public const string PlayerAtk = "jh_char_archer_atk";
        public const string EnemyWalk = "jh_enemy_e1_skel_walk";
        public const string EnemyAtk = "jh_enemy_e1_skel_atk";

        public static string ResolvePlayer(EntityAnimState state)
        {
            string wanted = PlayerSprite(state);
            return Present(wanted) ? wanted : PlayerIdle;
        }

        public static string ResolveEnemy(EntityAnimState state)
        {
            string wanted = EnemySprite(state);
            return Present(wanted) ? wanted : EnemyIdle;
        }

        public static string PlayerSprite(EntityAnimState state)
        {
            switch (state)
            {
                case EntityAnimState.Walk: return PlayerWalk;
                case EntityAnimState.Dodge: return PlayerDodge;
                case EntityAnimState.Charge: return PlayerCharge;
                case EntityAnimState.Attack: return PlayerAtk;
                default: return PlayerIdle;
            }
        }

        public static string EnemySprite(EntityAnimState state)
        {
            switch (state)
            {
                case EntityAnimState.Walk: return EnemyWalk;
                case EntityAnimState.Attack: return EnemyAtk;
                default: return EnemyIdle;
            }
        }

        public static bool Present(string artId)
        {
            return JianHaiSprites.HasSourceFile(artId);
        }

        public static string GapNote()
        {
            var sb = new StringBuilder();
            sb.Append("missing clips: player walk/dodge/charge/atk + dirs (idle PNG present); ");
            sb.Append("enemy walk/atk (only jh_enemy_e1_skel_idle); ");
            sb.Append("no Animator; ");
            sb.Append("orb PNG present (jh_fx_mage_orb Provide-style 16px); ");
            sb.Append("arrow flight reuses jh_fx_charge_arrow_tip (no dedicated projectile sprite)");
            return sb.ToString();
        }
    }
}
