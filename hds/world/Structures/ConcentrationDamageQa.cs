using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;
namespace hds
{
    /// <summary>Self-only disposable fixture: arms real damage before the next known interruptible cast.</summary>
    public static class ConcentrationDamageQa
    {
        private sealed class Arm { internal ClientData Data; internal int World,Life,Amount; internal uint Character,District; }
        private sealed class State { internal Arm Pending; }
        private static readonly ConditionalWeakTable<WorldClient,State> states=new ConditionalWeakTable<WorldClient,State>();
        public static bool Allowed(string dev,string database,string enabled,uint character,bool world,bool dead,string name) =>
            enabled=="1" && QaLoadout.Allowed(dev,database,character,world,dead) &&
            !string.Equals(name,"TheNeo",StringComparison.OrdinalIgnoreCase);
        private static bool Ready(WorldClient client)
        {
            if(client==null || !ReferenceEquals(Store.currentClient,client) || client.IsBot || !client.Alive ||
                client.playerData==null || !client.playerData.getUDPSEssionEstablished() || client.playerData.worldChangePending ||
                client.playerData.DistrictIsLA() || client.playerData.duelDeath || PvpCombat.HealthOf(client)<=1 ||
                !Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                    Environment.GetEnvironmentVariable("MXO_CONCENTRATION_QA_DAMAGE"),client.playerData.getCharID(),
                    client.playerData.getOnWorld(),client.playerData.isDead,TimedEffects.PlayerName(client)))return false;
            if(WorldServer.Clients==null)return false;
            lock(WorldServer.Clients)return WorldServer.Clients.Values.Any(c=>ReferenceEquals(c,client));
        }
        public static string Run(WorldClient client,string[] args)
        {
            if(!Ready(client))return "Concentration damage requires its enabled disposable mode and an authenticated live QA character other than 0/2/TheNeo.";
            if(args==null || args.Length!=3 || args[0]!="hurtme" || args[2]!="next" ||
                !int.TryParse(args[1],NumberStyles.None,CultureInfo.InvariantCulture,out int amount) || amount<1 || amount>500)
                return "Usage: ?combatdepth hurtme <1..500> next (self only).";
            var data=client.playerData;
            lock(data.craftingSync)
            {
                if(!Ready(client) || amount>=PvpCombat.HealthOf(client) || AbilityCooldowns.IsCasting(data,TimeUtils.GetSimTimeSeconds()))
                    return "Arm before casting, with strictly more current HP than the requested damage.";
                states.GetOrCreateValue(client).Pending=new Arm{Data=data,World=data.worldGeneration,Life=data.lifeGeneration,
                    Character=data.getCharID(),District=data.getDistrictId(),Amount=amount};
            }
            return "Concentration damage armed: "+amount+" on next interruptible cast after 1s.";
        }
        internal static void Bind(AbilityCastInterrupts.Cast cast)
        {
            if(!states.TryGetValue(cast.Client,out var state) || state.Pending==null || cast.Seconds<=1 ||
                !AbilityCastInterrupts.KnownInterruptible(cast.Ability))return;
            var arm=state.Pending;state.Pending=null; // exactly one eligible attempt, including a refused/stale context
            if(!Matches(arm,cast) || !Ready(cast.Client))return;
            DelayedActions.AfterFor(cast.Client,1,()=>Fire(cast,arm));
        }
        private static bool Matches(Arm arm,AbilityCastInterrupts.Cast cast) => ReferenceEquals(arm.Data,cast.Data) &&
            arm.World==cast.World && arm.Life==cast.Life && arm.District==cast.Data.getDistrictId() && arm.Character==cast.Data.getCharID();
        private static void Fire(AbilityCastInterrupts.Cast cast,Arm arm)
        {
            lock(cast.Data.craftingSync)
            {
                if(!Matches(arm,cast) || !Ready(cast.Client) || !AbilityCastInterrupts.IsCurrent(cast) ||
                    arm.Amount>=PvpCombat.HealthOf(cast.Client))return;
                // The ordinary damage event owns health, callbacks, interruption and wire presentation.
                int before=PvpCombat.HealthOf(cast.Client);
                PvpCombat.Damage(cast.Client,arm.Amount);
                Output.WriteDebugLog("[CAST-INTERRUPT-QA] real self damage "+before+"->"+PvpCombat.HealthOf(cast.Client)+
                    " during "+cast.Ability+"; one-shot consumed");
            }
        }
    }
}
