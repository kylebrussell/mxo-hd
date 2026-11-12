using System;
using System.Runtime.CompilerServices;

namespace hds
{
    /// <summary>Evade Combat217's child EvasionState624. This is state/presentation only: the retail shield
    /// health baseline and ForceCombat/EvadeInterlock contest are not reconstructed.</summary>
    public static class EvasionShield
    {
        public const ushort Parent = 217, StateAbility = 624;
        public const uint StateFlag = 0x00080000;
        private sealed class State
        {
            internal int Generation, World, Life;
            internal bool Presented;
        }
        private static readonly ConditionalWeakTable<WorldClient, State> states = new ConditionalWeakTable<WorldClient, State>();
        private static bool Suppressed(WorldClient target) => TimedEffects.ForPlayer(target).Effects.Snapshot().Exists(m =>
            m.Parent == StealthShieldAbilities.DisableEvasion && m.Target == StealthShieldAbilities.NullTarget &&
            m.Expires > TimeUtils.GetSimTimeSeconds());
        public static bool HasState(WorldClient target)
        {
            if (!states.TryGetValue(target, out var state)) return false;
            lock (target.playerData.activeBuffs)
                return target.playerData.activeBuffs.TryGetValue(Parent, out var active) && active.Generation == state.Generation &&
                    target.playerData.worldGeneration == state.World && target.playerData.lifeGeneration == state.Life &&
                    !target.playerData.isDead && !target.playerData.duelDeath && !Suppressed(target);
        }
        internal static void Applied(WorldClient target, int generation)
        {
            states.Remove(target);
            states.Add(target,new State { Generation=generation,World=target.playerData.worldGeneration,Life=target.playerData.lifeGeneration });
            Present(target,HasState(target),false);
        }
        internal static void Removed(WorldClient target)
        {
            Present(target,false,false); states.Remove(target);
        }
        public static void Refresh(WorldClient target)
        {
            lock(target.playerData.upgradeSync) RefreshCore(target);
        }
        private static void RefreshCore(WorldClient target)
        {
            if (!states.TryGetValue(target,out var state)) return;
            bool on=HasState(target);
            lock (target.playerData.activeBuffs)
            {
                if (!target.playerData.activeBuffs.TryGetValue(Parent,out var active) || active.Generation != state.Generation) return;
                if (state.Presented == on) return;
                if (AbilityBuffs.TryGet(Parent,out var buff))
                    foreach(var packet in AbilityBuffs.BuildRecords(Parent,buff,active.RecordIds,!on)) target.messageQueue.addRpcMessage(packet);
            }
            Present(target,on,true);
        }
        private static void Present(WorldClient target,bool on,bool flush)
        {
            if (states.TryGetValue(target,out var state)) state.Presented=on;
            byte health=on?(byte)255:(byte)0;
            target.playerInstance.EvadeShieldHealth.setValue(health);
            lock(target.playerData.conditionTokens)
            {
                if(on)target.playerData.conditionStateFlags |= StateFlag;
                else target.playerData.conditionStateFlags &= ~StateFlag;
            }
            uint flags=PvpFlag.FlagsOf(target);
            target.playerInstance.ConditionStateFlags.setValue(flags);
            target.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((30,PlayerViewAttributes.U8(health)),
                (PlayerViewAttributes.SelfConditionStateFlags,PlayerViewAttributes.U32(flags))),false);
            PvpFlag.ToWatchers(target,PlayerViewAttributes.Build((21,PlayerViewAttributes.U8(health)),
                (PlayerViewAttributes.OtherConditionStateFlags,PlayerViewAttributes.U32(flags))));
            Output.WriteDebugLog("[EVASION] " + TimedEffects.PlayerName(target) + " state " + (on?"on":"off") +
                "; parent217="+AbilityBuffs.IsToggledOn(target,Parent));
            if(flush)target.FlushQueue();
        }
        public static void AfterSpawn(WorldClient observer,WorldClient target,ushort view)
        {
            if (!states.TryGetValue(target,out _)) return;
            byte health=HasState(target)?(byte)255:(byte)0;
            observer.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view,PlayerViewAttributes.Build(
                (21,PlayerViewAttributes.U8(health)),(PlayerViewAttributes.OtherConditionStateFlags,PlayerViewAttributes.U32(PvpFlag.FlagsOf(target))))),false);
        }
        internal static void Cleared(WorldClient target) { if(states.TryGetValue(target,out _))AbilityBuffs.Remove(target,Parent); }
        internal static void LoadoutChanged(WorldClient target)
        {
            if (states.TryGetValue(target,out _) && AbilityPassives.Level(target,Parent)==0) AbilityBuffs.Remove(target,Parent);
        }
    }
}
