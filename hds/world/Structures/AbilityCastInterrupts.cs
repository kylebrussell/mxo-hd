using System;
using System.Runtime.CompilerServices;
using hds.shared;
namespace hds
{
    /// <summary>Known interruptible normal casts. Event qualification is server policy; packet/math are native sourced.</summary>
    public static class AbilityCastInterrupts
    {
        public const ushort Concentrate=628,Concentration=564;
        private static readonly Random random=new Random();
        private sealed class State { public long Generation; public Cast Current; }
        private static readonly ConditionalWeakTable<WorldClient,State> states=new ConditionalWeakTable<WorldClient,State>();
        public sealed class Cast
        {
            internal WorldClient Client; internal ClientData Data;
            internal ushort Ability; internal double End; internal float Seconds;
            internal int World,Life; internal long Generation; internal bool Completed;
        }
        // A separate immutable reference keeps ownership distinct from a later cast with equal timestamps.
        public static Cast Begin(WorldClient client,ushort ability,float seconds)
        {
            var data=client.playerData;
            lock(data.craftingSync)
            {
                var state=states.GetOrCreateValue(client);
                var cast=new Cast {Client=client,Data=data,Ability=ability,Seconds=seconds,
                    End=data.castEndsAt,World=data.worldGeneration,Life=data.lifeGeneration,Generation=++state.Generation};
                state.Current=cast;
                return cast;
            }
        }
        private static bool Ready(Cast cast) => ReferenceEquals(cast.Client.playerData,cast.Data) &&
            cast.Data.getOnWorld() && !cast.Data.worldChangePending && !cast.Data.DistrictIsLA() &&
            !cast.Data.isDead && !cast.Data.duelDeath && PvpCombat.HealthOf(cast.Client)>0 &&
            cast.Data.worldGeneration==cast.World && cast.Data.lifeGeneration==cast.Life &&
            cast.Data.castEndsAt==cast.End && ControlActions.CanUseAbilities(cast.Client);
        internal static bool Complete(Cast cast,Action action)
        {
            lock(cast.Data.craftingSync)
            {
                var state=states.GetOrCreateValue(cast.Client);
                if(state.Current!=cast || state.Generation!=cast.Generation || cast.Completed || !Ready(cast))
                { if(state.Current==cast)state.Current=null; return false; }
                // Commit all normal branches in one guarded action. Health callbacks during landing cannot cancel it halfway.
                cast.Completed=true;state.Current=null;
                action();return true;
            }
        }
        public static void After(Cast cast,Action action) => DelayedActions.AfterFor(cast.Client,cast.Seconds,()=>Complete(cast,action));
        public static bool KnownInterruptible(ushort ability) => AbilityCatalog.Find(ability)?.Number("Ability/IsInterruptable",-1)==1;
        public static int CastThroughPercent(int damage,ushort maximum,short concentration)
        {
            if(damage<0 || maximum==0)throw new ArgumentOutOfRangeException();
            // Native6258d960 supplies iiiii/PyInt; original2.3 uses BINARY_DIVIDE BEFORE float().
            double fraction=damage/maximum;
            double chance=Math.Clamp(0.75+0.005*concentration-0.75*fraction,0.33,0.9);
            return (int)(chance*100.0);
        }
        internal static int? QaInterruptRoll() => Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS")=="1" &&
            (Environment.GetEnvironmentVariable("MXO_DB_NAME")??"").StartsWith("mxotest_",StringComparison.Ordinal) &&
            int.TryParse(Environment.GetEnvironmentVariable("MXO_CAST_INTERRUPT_QA_ROLL"),out int value) && value>=1 && value<=99 ? value : (int?)null;
        internal static int Roll(Random supplied=null)
        {
            if(supplied==null && QaInterruptRoll() is int fixedRoll)return fixedRoll;
            var source=supplied??random;lock(source)return source.Next(1,100);
        }
        public static void OnDamage(WorldClient client,int actualLoss,Random supplied=null)
        {
            if(actualLoss<=0)return;
            var data=client.playerData;
            lock(data.craftingSync)
            {
                if(!states.TryGetValue(client,out var state))return;
                var cast=state.Current;
                if(cast==null || cast.Seconds<=0 || cast.Completed || !Ready(cast) ||
                    TimeUtils.GetSimTimeSeconds()>=cast.End || !KnownInterruptible(cast.Ability))return;
                ushort maximum=NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(),1);
                if(maximum==0)return;
                int chance=CastThroughPercent(actualLoss,maximum,unchecked((short)AbilityStats.Influence(client,Concentration)));
                int roll=Roll(supplied);
                Output.WriteDebugLog("[CAST-INTERRUPT] "+cast.Ability+" loss="+actualLoss+" concentration="+AbilityStats.Influence(client,Concentration)+" through="+chance+" roll="+roll);
                if(roll<=chance)return;
                state.Current=null;state.Generation++;
                // Release only this normal cast's stamp; never reset a replacement/item reservation.
                if(ReferenceEquals(client.playerData,data) && data.castEndsAt==cast.End)data.castEndsAt=-1;
                client.messageQueue.addRpcMessage(CancelPacket(cast.Ability,2));client.FlushQueue();
                Output.WriteDebugLog("[CAST-INTERRUPT] cancelled "+cast.Ability+" on "+TimedEffects.PlayerName(client));
            }
        }
        public static byte[] CancelPacket(ushort ability,byte reason)
        {
            // Native6213ad10:80ad +uint32 canonicalability +u8reason;62175b1c maps2 to damage text600000eb.
            var bytes=new byte[7];bytes[0]=0x80;bytes[1]=0xad;
            Buffer.BlockCopy(BitConverter.GetBytes((uint)ability),0,bytes,2,4);bytes[6]=reason;return bytes;
        }
    }
}
