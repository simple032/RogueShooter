using UnityEngine;using System.Collections.Generic;
namespace RogueShooter.Player
{
    /// <summary>Per-player, per-run state; conditional additions are consumed on release.</summary>
    public sealed class ConditionalShotPerks : MonoBehaviour
    {
        public const string ReturnId="R16M",GuardId="R17M",EchoId="R18H";
        public const float ReturnWindow=1.5f,ReturnBonus=.4f,GuardWait=3f,GuardBonus=.2f;
        IList<string> _owned;float _returnAt=float.PositiveInfinity,_returnUntil=float.NegativeInfinity,_guardAt;bool _hadGuard;
        public bool Owns(string id)=>_owned!=null&&_owned.Contains(id);
        public bool ReturnReady=>Owns(ReturnId)&&Time.time>=_returnAt&&Time.time<=_returnUntil;
        public bool GuardReady=>Owns(GuardId)&&Time.time>=_guardAt;
        public void Bind(IList<string> owned){_owned=owned;bool has=Owns(GuardId);if(has&&!_hadGuard)_guardAt=Time.time+GuardWait;_hadGuard=has;}
        public void NotifyHurt(){_guardAt=Time.time+GuardWait;}
        public void NotifyRollStarted(){NotifyHurt();_returnAt=float.PositiveInfinity;_returnUntil=float.NegativeInfinity;}
        public void NotifyRollFinished(float recoverySeconds=PlayerRoll.RecoverSeconds){_returnAt=Time.time+recoverySeconds;_returnUntil=_returnAt+ReturnWindow;}
        public float ConsumeShotBonus(ChargeShotKind kind)
        {
            if(kind==ChargeShotKind.None)return 0;float add=0;
            if(ReturnReady){add+=ReturnBonus;_returnAt=float.PositiveInfinity;_returnUntil=float.NegativeInfinity;}
            if((kind==ChargeShotKind.Full||kind==ChargeShotKind.Crit)&&GuardReady){add+=GuardBonus;_guardAt=Time.time+GuardWait;}
            return add;
        }
        public void NotifyWeakArrow(GuaranteedCritActive skill,int activation)
        {if(Owns(EchoId)&&skill!=null)skill.NotifyWeakArrowHit(activation);}
    }
}
