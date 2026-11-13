using System;
using hds.shared;
namespace hds
{
    /// <summary>Fixed native-message display probes only; never performs or qualifies an ability.</summary>
    public static class AbilityRefusalQa
    {
        public static bool Allowed(string opt,string dev,string database,uint character,bool alive,bool onWorld,bool dead,bool pending,bool shutdown,bool la) =>
            opt=="1"&&character==110&&alive&&!pending&&!shutdown&&!la&&QaLoadout.Allowed(dev,database,character,onWorld,dead);
        public static bool TrySample(string name,out AbilityRefusals.Record record)
        {
            record=default;
            if(name=="reuse")record=new AbilityRefusals.Record(77,AbilityRefusals.ReuseWait);
            else if(name=="inner")record=new AbilityRefusals.Record(77,AbilityRefusals.InsufficientInnerStrength,15);
            else if(name=="unloaded")record=new AbilityRefusals.Record(77,AbilityRefusals.NotLoaded);
            else return false;
            return true;
        }
        internal sealed class Stamp
        {
            internal readonly ClientData Data;internal readonly int World,Life;internal readonly long Transport;
            internal Stamp(WorldClient client){Data=client.playerData;World=Data.worldGeneration;Life=Data.lifeGeneration;Transport=client.TransportFence.Capture();}
        }
        private static bool Ready(WorldClient client)=>ReferenceEquals(client,Store.currentClient)&&Allowed(
            Environment.GetEnvironmentVariable("MXO_ABILITY_REFUSAL_QA"),Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
            client.playerData.getCharID(),client.Alive,client.playerData.getOnWorld(),client.playerData.isDead,client.playerData.worldChangePending,client.playerData.waitForRPCShutDown,client.playerData.DistrictIsLA());
        internal static bool Enqueue(WorldClient client,string name,Stamp stamp)
        {
            if(!Ready(client)||!TrySample(name,out var sample))return false;
            string text="DISPLAY DIAGNOSTIC ONLY: ability 77, reason "+sample.ErrorID+". No loaded, resource or timer state changes. The unloaded example is renderer-only.";
            byte[] notice=ServerPackets.BuildSystemChatMessage(text,"SYSTEM"),record=AbilityRefusals.Encode(sample);
            bool sent=false;
            client.TransportFence.TryRun(stamp.Transport,()=>
            {
                if(!ReferenceEquals(stamp.Data,client.playerData)||stamp.World!=stamp.Data.worldGeneration||stamp.Life!=stamp.Data.lifeGeneration||
                   !client.Alive||stamp.Data.getCharID()!=110||!stamp.Data.getOnWorld()||stamp.Data.isDead||stamp.Data.worldChangePending||stamp.Data.waitForRPCShutDown||stamp.Data.DistrictIsLA())return;
                client.messageQueue.AddWorldBoundRpc(notice,stamp.World,stamp.Transport);client.messageQueue.AddWorldBoundRpc(record,stamp.World,stamp.Transport);sent=true;
            });
            if(sent)Output.WriteDebugLog("[ABILITY-REFUSAL-QA] displayOnly=true name="+name+" ability=77 reason="+sample.ErrorID+" deficit="+sample.InnerstrengthDeficit+" slot=107 loadedLevel="+AbilityPassives.Level(client,77));
            return sent;
        }
        public static bool Dev(WorldClient client,string[] words)
        {
            if(words.Length==0||words[0]!="?abilityrefusal")return false;
            if(!Ready(client)||words.Length!=2||!TrySample(words[1],out _))
            {ClientActionsHandler.Notice(client,"Ability refusal display QA requires its explicit disposable CSR110 mode. Usage: ?abilityrefusal reuse|inner|unloaded");return true;}
            if(Enqueue(client,words[1],new Stamp(client)))client.FlushQueue(); // No Flush or callback under the fence.
            return true;
        }
    }
}
