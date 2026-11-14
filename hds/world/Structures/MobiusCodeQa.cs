using System;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>One next967 callback chance only; never overrides combat/hit/damage RNG.</summary>
    public static class MobiusCodeQa
    {
        private sealed class Arm
        {
            internal ClientData Data;
            internal int World,Life,Roll;
            internal long Transport;
        }
        private static readonly ConditionalWeakTable<WorldClient,Arm> arms=new ConditionalWeakTable<WorldClient,Arm>();
        public static bool Allowed(string dev,string database,string feature,uint character,bool world,bool dead,string name) =>
            feature=="1" && character==110 && QaLoadout.Allowed(dev,database,character,world,dead) &&
            !string.Equals(name,"TheNeo",StringComparison.OrdinalIgnoreCase);
        private static bool Ready(WorldClient c)
        {
            if(!MobiusCode.Live(c) || !ReferenceEquals(Store.currentClient,c) || c.IsBot ||
                !c.playerData.getUDPSEssionEstablished() || c.playerData.waitForRPCShutDown || c.playerData.DistrictIsLA() ||
                !Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                    Environment.GetEnvironmentVariable("MXO_MOBIUS_QA"),c.playerData.getCharID(),c.playerData.getOnWorld(),
                    c.playerData.isDead,TimedEffects.PlayerName(c)) || AbilityPassives.Level(c,MobiusCode.Ability)<=0)return false;
            if(WorldServer.Clients==null)return false;
            lock(WorldServer.Clients)return WorldServer.Clients.Values.Any(v=>ReferenceEquals(v,c));
        }
        public static string Run(WorldClient c,string[] args)
        {
            if(!Ready(c))return "Mobius callback QA requires enabled disposable CSR110 mode and normally owned/loaded967.";
            if(args.Length!=3 || args[0]!="mobius" || args[2]!="next" || (args[1]!="positive" && args[1]!="negative"))
                return "Usage: ?combatdepth mobius positive|negative next (next successful normal967 callback only).";
            var arm=new Arm {Data=c.playerData,World=c.playerData.worldGeneration,Life=c.playerData.lifeGeneration,
                Transport=c.TransportFence.Capture(),Roll=args[1]=="positive"?0:99};
            lock(arms)
            {
                arms.Remove(c);
                arms.Add(c,arm);
            }
            return "Mobius callback chance armed once: "+args[1]+". Cast967 normally; no cooldown or combat roll changed.";
        }
        internal static int Roll(WorldClient c,Func<int> natural)
        {
            Arm arm;
            lock(arms)
            {
                arms.TryGetValue(c,out arm);
                arms.Remove(c);
            }
            if(arm!=null && Ready(c) && ReferenceEquals(arm.Data,c.playerData) && arm.World==c.playerData.worldGeneration &&
                arm.Life==c.playerData.lifeGeneration && arm.Transport==c.TransportFence.Capture())
            {
                Output.WriteDebugLog("[MOBIUS-QA] callback-only one-shot consumed roll="+arm.Roll);
                return arm.Roll;
            }
            return natural();
        }
    }
}
