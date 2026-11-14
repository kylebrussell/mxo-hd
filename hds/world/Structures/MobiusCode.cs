using System;
using System.Runtime.CompilerServices;

namespace hds
{
    /// <summary>PARTIAL967: only the original ten-second incoming free-attack capability.
    /// Forced interlock entry, susceptibility and presentation remain unimplemented.</summary>
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

        internal static void Land(WorldClient caster, WorldClient target)
        {
            if (!ReferenceEquals(caster,target) || target?.playerData==null) return;
            var invocation=new Invocation(target);
            if(!invocation.Current)return;
            int roll;
            lock(random) roll = MobiusCodeQa.Roll(target, () => random.Next(100));
            bool success = ProcSucceeds(roll) && Apply(invocation,TimeUtils.GetSimTimeSeconds());
            Output.WriteDebugLog("[MOBIUS] PARTIAL967 callback roll="+roll+" proc="+success+" seconds="+(success?10:0));
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
