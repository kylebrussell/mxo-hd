using System;
using System.Runtime.CompilerServices;

namespace hds
{
    /// <summary>The scoped, ten-second Mobius Code attack capabilities from MobiusCodeInit/Term.</summary>
    public static class MobiusCode
    {
        public const ushort Ability = 967;
        public const double Seconds = 10;
        internal sealed class Slot
        {
            internal volatile int RemovalGeneration;
            internal Invocation Active;
            internal double Expires;
        }
        internal sealed class Invocation
        {
            internal readonly WorldClient Owner;
            internal readonly ClientData Data;
            internal readonly int World, Life;
            internal readonly TimedEffects.Holder Holder;
            internal readonly Slot Slot;
            internal readonly int RemovalGeneration;
            internal Invocation(WorldClient owner)
            {
                Owner=owner;Data=owner.playerData;World=Data.worldGeneration;Life=Data.lifeGeneration;
                Holder=TimedEffects.ForPlayer(owner).Effects;
                lock(Holder)
                {
                    Slot=states.GetOrCreateValue(Holder);RemovalGeneration=Slot.RemovalGeneration;
                }
            }
            internal bool Current => ReferenceEquals(Data,Owner.playerData) && World==Data.worldGeneration &&
                Life==Data.lifeGeneration && Slot.RemovalGeneration==RemovalGeneration && Live(Owner);
        }
        private static readonly ConditionalWeakTable<TimedEffects.Holder, Slot> states = new ConditionalWeakTable<TimedEffects.Holder, Slot>();
        private static readonly Random random = new Random();

        public static bool ProcSucceeds(int roll) => roll >= 0 && roll < TempMods.MobiusImmunityChance;

        internal static Invocation CaptureForHeal(WorldClient caster,WorldClient target,ushort ability) =>
            ability==Ability && ReferenceEquals(caster,target) && target?.playerData!=null ? new Invocation(target) : null;

        private static int NaturalRoll() { lock(random)return random.Next(100); }
        internal static void Land(Invocation invocation)
        {
            if(invocation==null)return;
            bool success = Resolve(invocation, () => MobiusCodeQa.Roll(invocation.Owner, NaturalRoll), () => TimeUtils.GetSimTimeSeconds());
            if (invocation.Current) ServerPackets.SendPlayerAbilityEffect(invocation.Owner, HealingEffect(success));
        }
        internal static uint HealingEffect(bool success) => success ? 0x12463110u : 0x12463112u;

        // CLIENT_REQUEST_CLOSE_COMBAT body byte 4 is the decoded ForcedInterlock flag.
        internal static bool PreventsForcedInterlock(WorldClient target, ReadOnlySpan<byte> request) =>
            PreventsForcedInterlock(target, request, TimeUtils.GetSimTimeSeconds());

        internal static bool PreventsForcedInterlock(WorldClient target, ReadOnlySpan<byte> request, double now) =>
            request.Length > 4 && request[4] != 0 && PreventsFreeAttack(target, now);

        internal static bool PreventsNpcInterlock(WorldClient target) => PreventsFreeAttack(target);

        // Validation precedes even the chance draw: synchronous heal callbacks can invalidate the invocation.
        internal static bool Resolve(Invocation invocation,Func<int> draw,Func<double> now)
        {
            if(invocation==null || !invocation.Current)return false;
            int roll=draw();
            bool success = ProcSucceeds(roll) && Apply(invocation,now());
            Output.WriteDebugLog("[MOBIUS] PARTIAL967 callback roll="+roll+" proc="+success+" seconds="+(success?10:0));
            return success;
        }

        internal static bool Live(WorldClient owner) => owner?.playerData != null && owner.Alive &&
            owner.playerData.getOnWorld() && !owner.playerData.worldChangePending &&
            !owner.playerData.isDead && !owner.playerData.duelDeath;

        internal static bool Apply(WorldClient owner,double now) => Apply(new Invocation(owner),now);
        internal static bool Apply(Invocation invocation,double now)
        {
            lock(invocation.Holder)
            {
                if(!invocation.Current)return false;
                invocation.Slot.Active=invocation;
                invocation.Slot.Expires=now+Seconds;
                return true;
            }
        }

        public static bool PreventsFreeAttack(WorldClient target) => PreventsFreeAttack(target,TimeUtils.GetSimTimeSeconds());
        internal static bool PreventsFreeAttack(WorldClient target,double now)
        {
            if(target?.playerData==null)return false;
            var holder=TimedEffects.ForPlayer(target).Effects;
            lock(holder)
            {
                if(!states.TryGetValue(holder,out var state) || state.Active==null)return false;
                bool active=ReferenceEquals(state.Active.Owner,target) && state.Active.Current && now<state.Expires;
                if(!active)state.Active=null;
                return active;
            }
        }

        // Holder removal owns the capability; no expiry callback or sticky global flag is required.
        internal static void Removed(TimedEffects.Holder holder)
        {
            lock(holder)
            {
                var state=states.GetOrCreateValue(holder);
                state.RemovalGeneration++;state.Active=null;
            }
        }
    }
}
