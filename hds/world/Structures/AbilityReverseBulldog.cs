using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;
namespace hds
{
    /// <summary>Only388's stored terminal ApplyStunEffect and AddControlImmunityTimerVal callbacks.</summary>
    public static class AbilityReverseBulldog
    {
        public const ushort Ability=388;
        internal const ushort TimerMarker=710;
        internal static bool Markers(AbilityCatalog.Entry entry,out TempMods.Effect effect)
        {
            effect=null;if(entry?.Id!=Ability)return false;
            var stun=entry.Modifiers.SingleOrDefault(m=>m.Text("StatModTermFunc")=="ApplyStunEffect");
            var timer=entry.Modifiers.SingleOrDefault(m=>m.Text("StatModTermFunc")=="AddControlImmunityTimerVal");
            bool Valid(AbilityCatalog.Modifier m)=>m!=null && m.Number("WhenEnabled",-1)==2 && m.Number("DurationType",-1)==0 &&
                m.Number("Duration")>0 && m.Number("MinValue")>0 && m.Number("MinValue")==m.Number("MaxValue") &&
                !m.Has("ValueScale") && m.Number("ValueFunction")==0;
            if(!Valid(stun)||!Valid(timer))return false;
            effect=new TempMods.Effect {Ability=Ability,Mods=new[]{
                new TempMods.Mod {Kind=TempMods.KindFx,Target=Ability,Proc=TempMods.Proc.Terminal,Seconds=(float)stun.Number("Duration"),MinValue=(int)stun.Number("MinValue"),MaxValue=(int)stun.Number("MaxValue")},
                new TempMods.Mod {Kind=TempMods.KindFx,Target=TimerMarker,Proc=TempMods.Proc.Terminal,Seconds=(float)timer.Number("Duration"),MinValue=(int)timer.Number("MinValue"),MaxValue=(int)timer.Number("MaxValue")}}};
            return true;
        }
        public sealed class Cast
        {
            internal readonly WorldClient Source,Player;
            internal readonly AbilityCastInterrupts.Cast Normal;
            internal readonly ClientView View;
            internal readonly Mob Mob;
            internal readonly ClientData TargetData;
            internal readonly long SourceTransport,TargetTransport;
            internal readonly int TargetWorld,TargetLife,Death;
            internal readonly uint Spawn,Character,District,TargetCharacter;
            internal readonly ulong Entity;
            internal Cast(WorldClient source,ushort view,AbilityCastInterrupts.Cast normal)
            {
                Source=source;Normal=normal;View=source.viewMan.getViewById(view);Mob=AbilityTargets.FindMob(source,view,out _);
                Player=Mob==null?PvpCombat.PlayerInView(source,view):null;TargetData=Player?.playerData;
                SourceTransport=source.TransportFence.Capture();TargetTransport=Player?.TransportFence.Capture()??0;
                TargetWorld=TargetData?.worldGeneration??0;TargetLife=TargetData?.lifeGeneration??0;TargetCharacter=TargetData?.getCharID()??0;
                Character=source.playerData.getCharID();District=source.playerData.getDistrictId();
                Spawn=View?.spawnId??0;Entity=View?.entityId??0;Death=Mob?.DeathGeneration??0;
            }
            private static bool Live(WorldClient client,ClientData data,int world,int life,long transport)=>client!=null && ReferenceEquals(client.playerData,data) &&
                client.Alive && data.getOnWorld() && !data.worldChangePending && !data.DistrictIsLA() && !data.isDead && !data.duelDeath &&
                data.worldGeneration==world && data.lifeGeneration==life && client.TransportFence.IsCurrent(transport) && PvpCombat.HealthOf(client)>0;
            internal bool Current=>Normal!=null && Normal.Completed && ReferenceEquals(Normal.Client,Source) &&
                Live(Source,Normal.Data,Normal.World,Normal.Life,SourceTransport) && Source.playerData.getCharID()==Character && Source.playerData.getDistrictId()==District &&
                View!=null && View.viewCreated && !View.viewNeedsToBeDeleted && View.spawnId==Spawn && View.entityId==Entity &&
                ReferenceEquals(Source.viewMan.getViewById(View.ViewID),View) &&
                (Mob!=null? !Mob.isFriendly && !Mob.getIsDead() && Mob.getHealthC()>0 && Mob.DeathGeneration==Death && Mob.getDistrict()==District &&
                    ReferenceEquals(AbilityTargets.FindMob(Source,View.ViewID,out _),Mob):Live(Player,TargetData,TargetWorld,TargetLife,TargetTransport) && TargetData.getCharID()==TargetCharacter && TargetData.getDistrictId()==District &&
                    ReferenceEquals(PvpCombat.PlayerInView(Source,View.ViewID),Player));
            internal bool Owns(TimedEffects.Subject subject)=>Mob!=null ? subject is MobEffects mobs && ReferenceEquals(mobs.Mob,Mob):
                subject is PlayerEffects && ReferenceEquals(subject.Context,Player);
        }
        internal static Cast Capture(WorldClient source,ushort ability,ushort view,AbilityCastInterrupts.Cast normal)=>ability==Ability?new Cast(source,view,normal):null;
        private sealed class State { internal Receipt Current; }
        private static readonly ConditionalWeakTable<TimedEffects.Subject,State> states=new ConditionalWeakTable<TimedEffects.Subject,State>();
        private sealed class Receipt
        {
            internal readonly Cast Cast;internal readonly State State;internal readonly TimedEffects.Subject Subject;
            internal TimedEffects.Active Stun,Timer;internal bool StunDone,TimerDone;
            internal Receipt(Cast cast,State state,TimedEffects.Subject subject){Cast=cast;State=state;Subject=subject;}
            internal bool Current {get {lock(State)return ReferenceEquals(State.Current,this) && Cast.Current && Cast.Owns(Subject);}}
            internal void Expired(TimedEffects.Subject subject,TimedEffects.Active marker,double now)
            {
                bool stun=ReferenceEquals(marker,Stun),timer=ReferenceEquals(marker,Timer);
                if(!ReferenceEquals(subject,Subject) || (!stun&&!timer) || now<marker.Expires || Subject.Effects.Snapshot().Contains(marker))return;
                lock(State)
                {
                    if(stun?StunDone:TimerDone)return;
                    if(stun)StunDone=true;else TimerDone=true;
                }
                try {if(Current)Apply(this,marker,stun,now);}
                finally {lock(State)if(StunDone&&TimerDone&&ReferenceEquals(State.Current,this))State.Current=null;}
            }
        }
        internal static Action<TimedEffects.Active> Bind(Cast cast,TimedEffects.Subject subject,ushort ability)
        {
            if(ability!=Ability)return null;
            var state=states.GetOrCreateValue(subject);Receipt receipt=null;
            lock(state)state.Current=cast!=null&&cast.Current&&cast.Owns(subject)?receipt=new Receipt(cast,state,subject):null;
            return marker=>
            {
                if(receipt==null || marker.Proc!=TempMods.Proc.Terminal)return;
                if(marker.Target==Ability)receipt.Stun=marker;else if(marker.Target==TimerMarker)receipt.Timer=marker;else return;
                marker.OnExpiredAt=receipt.Expired;
            };
        }
        internal static void Cancel(TimedEffects.Subject subject)
        {if(states.TryGetValue(subject,out var state))lock(state)state.Current=null;}
        private static void Apply(Receipt receipt,TimedEffects.Active marker,bool stun,double now)
        {
            var subject=receipt.Subject;var cast=receipt.Cast;
            float seconds=marker.Value;
            if(stun)
            {
                seconds=AbilityControlDurations.Scale(cast.Source,TempMods.StunAbility,seconds);
                if(!(subject is PlayerEffects && ReferenceEquals(subject.Context,cast.Source)) && AbilityHostile.IsDebuff(Ability))
                    seconds=DeflectCode.Duration(seconds,cast.Source.playerInstance.Level.getValue()[0],subject.Determination,subject.DeterminationBonus,TempMods.StunAbility);
            }
            if(seconds<=0 || subject.Effects.HasControlImmunity(now))return;
            var child=new TimedEffects.Active {Kind=stun?TempMods.KindState:TempMods.KindPlain,Target=stun?TempMods.StunAbility:TempMods.ISDrainTargetAbility,
                Seconds=seconds,Proc=TempMods.Proc.State,Caster=cast.Source,CasterName=TimedEffects.PlayerName(cast.Source),TargetView=cast.View.ViewID};
            ushort parent=stun?Ability:TempMods.ControlImmunityTimerAbility;
            bool Current()=>receipt.Current && !subject.Effects.HasControlImmunity(now, child);
            if(!subject.Effects.AppendCallback(parent,child,now,Current,out var replaced))return;
            if(!Current()){subject.Effects.RollbackCallback(child,replaced,now);return;}
            // Output is bound to the captured world/transport, never owner work under that publication gate.
            if(!Publish(receipt,child,replaced,stun,now))return;
            TimedEffects.Track(subject);
            Output.WriteDebugLog("[REVERSE-BULLDOG] source="+cast.Character+" view="+cast.View.ViewID+" spawn="+cast.Spawn+
                " state="+child.Target+" seconds="+child.Seconds+" callback="+(stun?"ApplyStunEffect":"AddControlImmunityTimerVal"));
        }
        private static bool Publish(Receipt receipt,TimedEffects.Active child,List<TimedEffects.Active> replaced,bool stun,double now)
        {
            var cast=receipt.Cast;
            if(!receipt.Current){receipt.Subject.Effects.RollbackCallback(child,replaced,now);return false;}
            if(cast.Player!=null)
            {
                var target=cast.Player;child.RecordId=AbilityBuffs.NextRecordId(cast.TargetData);
                if(stun)child.LinkedRecordId=AbilityBuffs.NextRecordId(cast.TargetData);
                foreach(var old in replaced)
                {
                    target.messageQueue.AddWorldBoundRpc(PlayerEffects.Record(old,true),cast.TargetWorld,cast.TargetTransport);
                    if(old.Kind==TempMods.KindState)target.messageQueue.AddWorldBoundRpc(PlayerEffects.LinkedRecord(old,true),cast.TargetWorld,cast.TargetTransport);
                }
                target.messageQueue.AddWorldBoundRpc(PlayerEffects.Record(child,false),cast.TargetWorld,cast.TargetTransport);
                if(stun)target.messageQueue.AddWorldBoundRpc(PlayerEffects.LinkedRecord(child,false),cast.TargetWorld,cast.TargetTransport);
                uint targetLine=stun?AbilityMessages.StunTarget:AbilityMessages.ImmunityTarget;
                target.messageQueue.AddWorldBoundRpc(AbilityMessages.AboutAbility(targetLine,true,Ability,child.CasterName,
                    TimedEffects.PlayerName(target),stun?0:(int)child.Seconds),cast.TargetWorld,cast.TargetTransport);
                target.FlushQueue();
            }
            uint line=stun?AbilityMessages.CasterStateLine(TempMods.StunAbility,out _):AbilityMessages.ImmunityCaster;
            if(line!=0)cast.Source.messageQueue.AddWorldBoundRpc(AbilityMessages.AboutAbility(line,true,Ability,child.CasterName,
                cast.Mob?.getName()??TimedEffects.PlayerName(cast.Player),stun?0:(int)child.Seconds),cast.Normal.World,cast.SourceTransport);
            cast.Source.FlushQueue();return true;
        }
    }
}
