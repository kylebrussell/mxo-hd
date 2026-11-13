using System;
using hds.shared;
using System.Globalization;
using System.Linq;
namespace hds
{
    public enum AbilityCastOrigin { AlreadyResolved, NormalFreeCast }
    /// <summary>PARTIAL: modeled deltas only, NPC neutral tactic, modern flags and 64-bit significand policy explicitly OURS.</summary>
    internal static class FreeCastViralContest
    {
        internal const string AttackQa="MXO_FREECAST_VIRAL_QA_ATTACK15",DefenseQa="MXO_FREECAST_VIRAL_QA_DEFENSE15";
        internal const uint HitLine=0x240000f1,DeflectLine=0x240000f2,DeflectFx=0x2800059c;
        internal sealed class Result
        {
            internal int Attack,Defense,AttackInput,DefenseInput; internal bool Forced;
            internal bool Hit => Attack>Defense;
        }
        internal static bool Applies(ushort ability,AbilityCastOrigin origin)
        {
            var a=AbilityCatalog.Find(ability);
            return origin==AbilityCastOrigin.NormalFreeCast && a!=null && a.Number("Ability/AbilityClass",-1)==0 &&
                a.Number("Ability/TestFunction",-1)==2 && a.Number("Ability/CastingTime")>0 && a.Number("Ability/CombatSpecialMove")==0 &&
                (a.Number("Ability/CastDirectObjects",-1)==0 || a.Number("Ability/CastDirectObjects",-1)==3);
        }
        internal static int Tactic(int total,int? tactic,bool defense)
        {
            double multiplier=1;
            if(tactic.HasValue)switch(tactic.Value)
            {
                case 0:multiplier=defense?0.9:1.15;break;
                case 1:multiplier=defense?1:0.9;break;
                case 2:multiplier=defense?1.3:1.05;break;
                case 3:multiplier=defense?1.5:0;break;
            }
            return (int)(total*multiplier);
        }
        internal static Result Evaluate(int attack,int defense,int ca,int cd,int? ta,int? td,int ra,int rd)
        {
            int a=Tactic(attack,ta,false),d=Tactic(defense,td,true);
            return new Result{AttackInput=a,DefenseInput=d,Attack=FreeCastViralRoll.Roll(a,ca,100,ra),Defense=FreeCastViralRoll.Roll(d,cd,69,rd)};
        }
        internal static bool QaAllowed(string dev,string database,uint character,bool live,bool dead,string enabled,string attack,string defense,out int a,out int d)
        {
            a=d=0;
            return enabled=="1" && QaLoadout.Allowed(dev,database,character,live,dead) &&
                int.TryParse(attack,NumberStyles.None,CultureInfo.InvariantCulture,out a) && a>=0 && a<=32767 &&
                int.TryParse(defense,NumberStyles.None,CultureInfo.InvariantCulture,out d) && d>=0 && d<=32767;
        }
        private static bool Ready(WorldClient c)=>c!=null && c.Alive && c.playerData.getUDPSEssionEstablished() && c.playerData.getOnWorld() && !c.playerData.worldChangePending &&
            !c.playerData.DistrictIsLA() && !c.playerData.isDead && !c.playerData.duelDeath;
        internal static bool TryQaDraw(WorldClient source,ushort ability,out int a,out int d)
        {
            a=d=0;return Ready(source) && AbilityPassives.Level(source,ability)>0 && !string.Equals(TimedEffects.PlayerName(source),"TheNeo",StringComparison.OrdinalIgnoreCase) &&
                QaAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,source.playerData.getCharID(),
                    source.playerData.getOnWorld(),source.playerData.isDead,Environment.GetEnvironmentVariable("MXO_FREECAST_VIRAL_QA"),
                    Environment.GetEnvironmentVariable(AttackQa),Environment.GetEnvironmentVariable(DefenseQa),out a,out d);
        }
        private static Result Roll(WorldClient source,ushort ability,int defense,int cd,int? tactic)
        {
            bool forced=TryQaDraw(source,ability,out int a,out int d);
            if(!forced){a=Random.Shared.Next(32768);d=Random.Shared.Next(32768);}
            var result=Evaluate(AbilityStats.Player(source,342),defense,AbilityStats.Player(source,637),cd,(int)source.playerData.combatTactic,tactic,a,d);
            result.Forced=forced;return result;
        }
        private static bool Visible(WorldClient c,ulong entity)
        {
            var v=c.viewMan.FindViewForEntity(entity);return v!=null && v.viewCreated && !v.viewNeedsToBeDeleted;
        }
        private static WorldClient[] Receivers(WorldClient source,WorldClient target,ulong entity)=>SocialHandler.OnlineWhere(c=>Ready(c) &&
            c.playerData.getDistrictId()==source.playerData.getDistrictId() && (ReferenceEquals(c,source) || ReferenceEquals(c,target) ||
                Visible(c,source.playerData.getEntityId()) || Visible(c,entity))).ToArray();
        internal static byte[] Message(ushort ability,string source,string target,Result r)=>AbilityMessages.AboutAbility(r.Hit?HitLine:DeflectLine,true,ability,source,target,r.Attack,0,r.Defense,0);
        internal static bool Npc(WorldClient source,AbilityItem ability,Mob target,ClientView view)
        {
            if(!Ready(source) || target==null || target.pet!=null || target.isFriendly || target.getIsDead() || target.getDistrict()!=source.playerData.getDistrictId() ||
                view==null || !view.viewCreated || view.viewNeedsToBeDeleted || !ReferenceEquals(source.viewMan.getViewById(view.ViewID),view))return false;
            var r=Roll(source,ability.getAbilityID(),AbilityStats.Npc(target,183),AbilityStats.Npc(target,637),null);
            byte[] message=Message(ability.getAbilityID(),TimedEffects.PlayerName(source),target.getName(),r);
            byte[] fx=r.Hit?null:target.BuildAbilityEffect(DeflectFx);
            foreach(var receiver in Receivers(source,null,target.getEntityId()))
            {
                if(!Ready(receiver) || receiver.playerData.getDistrictId()!=source.playerData.getDistrictId())continue;
                receiver.messageQueue.addRpcMessage(message);
                var v=receiver.viewMan.FindViewForEntity(target.getEntityId());
                if(fx!=null && v!=null && v.viewCreated && !v.viewNeedsToBeDeleted)new ServerPackets().SendNpcUpdateData(v.ViewID,receiver,fx);
                receiver.FlushQueue();
            }
            Log(source,ability,target.getName(),r);return r.Hit;
        }
        internal static bool Player(WorldClient source,AbilityItem ability,WorldClient target)
        {
            if(!Ready(source) || !Ready(target) || source.playerData.getDistrictId()!=target.playerData.getDistrictId() || !Visible(source,target.playerData.getEntityId()) || PvpCombat.Check(source,target)!=PvpRules.Verdict.Allowed)return false;
            var r=Roll(source,ability.getAbilityID(),AbilityStats.Player(target,183),AbilityStats.Player(target,637),(int)target.playerData.combatTactic);
            var message=Message(ability.getAbilityID(),TimedEffects.PlayerName(source),TimedEffects.PlayerName(target),r);
            var receivers=Receivers(source,target,target.playerData.getEntityId());
            byte counter=0;
            if(!r.Hit)lock(target.playerData)
            {
                counter=(byte)(target.playerInstance.EffectCounter.getValue()[0]+1);
                target.playerInstance.EffectID.setValue(DeflectFx);target.playerInstance.EffectCounter.setValue(counter);
            }
            foreach(var receiver in receivers)
            {
                if(!Ready(receiver) || receiver.playerData.getDistrictId()!=source.playerData.getDistrictId())continue;
                receiver.messageQueue.addRpcMessage(message);
                if(!r.Hit)
                {
                    if(ReferenceEquals(receiver,target))receiver.messageQueue.addObjectMessage(ServerPackets.BuildPlayerAbilityEffect(DeflectFx,counter,true),false);
                    else
                    {
                        var view=receiver.viewMan.FindViewForEntity(target.playerData.getEntityId());
                        if(view!=null && view.viewCreated && !view.viewNeedsToBeDeleted)receiver.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view.ViewID,ServerPackets.BuildPlayerAbilityEffect(DeflectFx,counter,false)),false);
                    }
                }
                receiver.FlushQueue();
            }
            Log(source,ability,TimedEffects.PlayerName(target),r);return r.Hit;
        }
        private static void Log(WorldClient source,AbilityItem ability,string target,Result r)=>Output.WriteDebugLog("[FREECAST-VIRAL] ability="+ability.getAbilityID()+" source="+TimedEffects.PlayerName(source)+" target="+target+" inputs="+r.AttackInput+"/"+r.DefenseInput+" rolls="+r.Attack+"/"+r.Defense+" hit="+r.Hit+" qa="+r.Forced);
    }
}
