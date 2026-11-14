using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>Original tempmods.pyc RandomCombatStateChance: uniform100 &lt;10,
    /// then uniform3 chooses Daze/OffBalance/Staggered, each float(Value)=10 seconds.</summary>
    public static class AbilitySeverArteryStates
    {
        private static readonly Random random=new Random();
        private sealed class Setting {internal int Roll,Selector;internal Func<bool> Current;}
        private static readonly ConditionalWeakTable<WorldClient,Setting> settings=new ConditionalWeakTable<WorldClient,Setting>();
        public static string Dev(WorldClient client,string[] words)
        {
            string database=Store.config?.dbParams?.DatabaseName;
            if(!QaAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),database,Environment.GetEnvironmentVariable(Feature))||
                client==null||!QaLoadout.Allowed("1",database,client.playerData.getCharID(),client.Alive&&client.playerData.getOnWorld()&&!client.playerData.worldChangePending,client.playerData.isDead||client.playerData.duelDeath)||
                string.Equals(TimedEffects.PlayerName(client),"TheNeo",StringComparison.OrdinalIgnoreCase))return "Sever state QA requires dev1, strict disposable database, feature marker and a live owned QA actor.";
            if(words.Length==1&&words[0]=="off"){settings.Remove(client);return "Sever state QA normal callback RNG restored.";}
            if(words.Length!=2||!int.TryParse(words[0],out int roll)||(roll!=0&&roll!=99)||!int.TryParse(words[1],out int selector)||selector<0||selector>2)
                return "Usage: ?severstate <0|99> <0|1|2> | off; callback only, never cast/combat RNG.";
            settings.Remove(client);settings.Add(client,new Setting {Roll=roll,Selector=selector,Current=Lifetime(client)});
            return "Sever state QA callback roll="+roll+" selector="+selector+"; cast/round RNG unchanged.";
        }

        public const string Feature="MXO_SEVER_STATE_QA";
        internal static bool Modifier(ushort ability,AbilityCatalog.Modifier m)=>ability==341&&m.Target==710&&
            m.Text("StatModInitFunc")=="RandomCombatStateChance"&&m.Text("StatModUpdateFunc")=="RandomCombatStateChance"&&
            m.Number("WhenEnabled",-1)==2&&m.Number("DurationType",-1)==0&&m.Number("MinValue")==10&&m.Number("MaxValue")==10&&
            m.Number("Duration")==30&&m.Number("UpdateFuncInterval")==2.5;
        public static bool QaAllowed(string dev,string database,string marker)=>dev=="1"&&marker=="1"&&database!=null&&
            database.StartsWith("mxotest_",StringComparison.Ordinal)&&database.Length>8&&database.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_');
        public static int? QaDraw(string dev,string database,string marker,string raw,int maximum)=>QaAllowed(dev,database,marker)&&
            int.TryParse(raw,out int value)&&(maximum==100?(value==0||value==99):maximum==3&&value>=0&&value<3)?(int?)value:null;
        private static int Draw(WorldClient client,int maximum)
        {
            if(client!=null&&QaAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,Environment.GetEnvironmentVariable(Feature))&&
                settings.TryGetValue(client,out var setting)&&setting.Current())return maximum==100?setting.Roll:setting.Selector;
            string name=maximum==100?"MXO_SEVER_STATE_QA_ROLL":"MXO_SEVER_STATE_QA_SELECTOR";
            var qa=QaDraw(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                Environment.GetEnvironmentVariable(Feature),Environment.GetEnvironmentVariable(name),maximum);
            if(qa.HasValue)return qa.Value;
            lock(random)return random.Next(maximum);
        }
        internal static ushort Choose(Func<int,int> draw)
        {
            int chance=draw(100);if(chance<0||chance>=100)throw new ArgumentOutOfRangeException(nameof(draw));
            if(chance>=10)return 0;int index=draw(3);
            return index switch {0=>TempMods.DazeAbility,1=>TempMods.OffBalanceAbility,2=>TempMods.StaggeredAbility,_=>throw new ArgumentOutOfRangeException(nameof(draw))};
        }
        internal static Func<bool> Lifetime(WorldClient client,bool checkTransport=true)
        {
            if(client?.playerData==null)return ()=>false;
            var owner=client.playerData;int world=owner.worldGeneration,life=owner.lifeGeneration;uint character=owner.getCharID();
            long epoch=client.TransportFence.Capture();
            return ()=>ReferenceEquals(client.playerData,owner)&&owner.getCharID()==character&&client.Alive&&owner.getOnWorld()&&
                !owner.worldChangePending&&!owner.waitForRPCShutDown&&!owner.isDead&&!owner.duelDeath&&
                owner.worldGeneration==world&&owner.lifeGeneration==life&&(!checkTransport||client.TransportFence.Capture()==epoch);
        }
        internal static Func<bool> MobLifetime(Mob mob)
        {
            int death=mob.DeathGeneration;ulong entity=mob.getEntityId();ushort instance=mob.missionInstance;byte phase=mob.missionPhase;
            return ()=>mob.DeathGeneration==death&&mob.getEntityId()==entity&&mob.missionInstance==instance&&mob.missionPhase==phase&&
                !mob.getIsDead()&&mob.getHealthC()>0&&mob.getIsSpawned();
        }
        internal static float ChildSeconds(TimedEffects.Subject subject,TimedEffects.Active marker,ushort state)
        {
            float seconds=AbilityControlDurations.Scale(marker.Caster,state,marker.Value);
            // addStateMod supplies10; retain the normal class4/hostile duration policy used by Apply.
            if(marker.Caster!=null&&!(subject is PlayerEffects p&&ReferenceEquals(p.Context,marker.Caster))&&AbilityHostile.IsDebuff(marker.Parent))
                seconds=AbilityControlDurations.Mitigate(seconds,marker.Caster.playerInstance.Level.getValue()[0],subject.Determination);
            return seconds;
        }
        internal static void Bind(TimedEffects.Subject subject,TimedEffects.Active marker)
        {
            var source=Lifetime(marker.Caster);
            var recipient=subject is PlayerEffects?Lifetime(subject.Context):subject is MobEffects mob?MobLifetime(mob.Mob):new Func<bool>(()=>subject.Alive);
            marker.CallbackCurrent=()=>source()&&recipient()&&subject.Alive;
            var sourceState=Lifetime(marker.Caster,false);
            var recipientState=subject is PlayerEffects?Lifetime(subject.Context,false):subject is MobEffects mobState?MobLifetime(mobState.Mob):new Func<bool>(()=>subject.Alive);
            // Holder lock may never acquire the publication fence. Transport checks stay outside it.
            marker.CallbackStateCurrent=()=>sourceState()&&recipientState()&&subject.Alive;
        }
        internal static bool Execute(TimedEffects.Subject subject,TimedEffects.Active marker,double now,Func<int,int> draw=null)
        {
            if(marker.Parent!=341||marker.Proc!=TempMods.Proc.SeverState||!subject.Alive||
                marker.CallbackCurrent?.Invoke()!=true||now>=marker.Expires||!subject.Effects.Snapshot().Contains(marker))return false;
            ushort state=Choose(draw??(n=>Draw(marker.Caster,n)));if(state==0)return true;
            float seconds=ChildSeconds(subject,marker,state);if(seconds<=0)return true;
            var child=new TimedEffects.Active {Kind=TempMods.KindState,Target=state,Value=0,Seconds=seconds,Proc=TempMods.Proc.State,
                Caster=marker.Caster,CasterName=marker.CasterName,TargetView=marker.TargetView};
            if(!subject.Alive||marker.CallbackCurrent?.Invoke()!=true||!subject.Effects.ApplyCallback(marker,child,now,out var replaced))return false;
            // Same-target refresh only: DOT, slows, parent callback and other state siblings stay intact.
            if(marker.CallbackCurrent?.Invoke()!=true){subject.Effects.RollbackCallback(marker,child,replaced,now);return false;}
            TimedEffects.Track(subject);subject.Applied(new TempMods.Effect {Ability=341},new List<TimedEffects.Active>{child},null,replaced);
            Output.WriteDebugLog("[SEVER-STATE] 341 -> "+state+" inputSeconds=10 seconds="+seconds+" source="+marker.CasterName);
            return true;
        }
    }
}
