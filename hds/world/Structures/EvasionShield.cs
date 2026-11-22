using System;
using System.Runtime.CompilerServices;

namespace hds
{
    /// <summary>Evade Combat 217's child state, resource costs and visible remaining shield.
    /// The current entry predicate is separate from the shield damage calculation.</summary>
    public static class EvasionShield
    {
        public const ushort Parent = 217, StateAbility = 624;
        public const uint StateFlag = 0x00080000;
        private sealed class State
        {
            internal int Generation, World, Life;
            internal bool Presented;
            internal byte Health = byte.MaxValue;
        }
        private static readonly ConditionalWeakTable<WorldClient, State> states = new ConditionalWeakTable<WorldClient, State>();
        private static bool Suppressed(WorldClient target) => TimedEffects.ForPlayer(target).Effects.Snapshot().Exists(m =>
            m.Parent == StealthShieldAbilities.DisableEvasion && m.Target == StealthShieldAbilities.NullTarget &&
            m.Expires > TimeUtils.GetSimTimeSeconds());
        public static bool HasState(WorldClient target) => ActiveGeneration(target).HasValue;

        internal static int? ActiveGeneration(WorldClient target)
        {
            var data = target.playerData;
            lock (data.activeBuffs)
            {
                if (!states.TryGetValue(target, out var state) ||
                    !data.activeBuffs.TryGetValue(Parent, out var active) || active.Generation != state.Generation ||
                    data.worldGeneration != state.World || data.lifeGeneration != state.Life ||
                    data.isDead || data.duelDeath || Suppressed(target))
                    return null;
                return state.Generation;
            }
        }

        // Shipped combat_calculations.py GetShieldResistanceISDrainValue.
        internal static ushort ResistanceCost(byte attackerLevel, byte defenderLevel) =>
            (ushort)Math.Clamp(10 - Math.Max(0, defenderLevel - attackerLevel - 5), 2, 10);

        // combat_calculations.py GetShieldDamageValue, with Python's truncation toward zero.
        internal static int ShieldDamage(byte attackerLevel, byte defenderLevel, int forceCombatBonus)
        {
            int difference = defenderLevel - attackerLevel;
            double basis = difference <= 0 ? (4 - difference) * 7.3 + 6 : (difference - 9) * -2 + 22;
            return (int)Math.Clamp(basis + Math.Truncate(basis * (forceCombatBonus / 100.0)), 10, 500);
        }

        // A rounded byte delta reproduces the retained retail meter steps of 89, 102 and 128.
        internal static byte ReduceHealth(byte health, int damage)
        {
            int loss = (damage * byte.MaxValue + 50) / 100;
            return (byte)Math.Max(0, health - loss);
        }

        internal static bool ResistEntry(WorldClient source, WorldClient target, int generation,
            CombatInsightEngagement.Scope scope)
        {
            var data = target.playerData;
            byte attackerLevel = source.playerInstance.Level.getValue()[0];
            byte defenderLevel = target.playerInstance.Level.getValue()[0];
            ushort cost = ResistanceCost(attackerLevel, defenderLevel);
            int damage = ShieldDamage(attackerLevel, defenderLevel, AbilityStats.Player(source, 1048));
            ushort before, after;
            byte healthBefore, healthAfter;
            lock (data.activeBuffs)
            {
                if (!scope.Current) return false;
                if (ActiveGeneration(target) != generation) return !HasState(target);
                if (!states.TryGetValue(target, out var state) || state.Health == 0) return false;
                before = AbilityHandler.CurrentInnerStrength(target);
                healthBefore = state.Health;

                // A shortfall terminates the activation without taking a partial payment.
                after = before;
                healthAfter = 0;
                if (before >= cost)
                {
                    after = AbilityHandler.SpendInnerStrength(target, cost);
                    healthAfter = ReduceHealth(healthBefore, damage);
                    target.messageQueue.addObjectMessage(
                        PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfInnerStrength, PlayerViewAttributes.U16(after)),
                            (30, PlayerViewAttributes.U8(healthAfter))), false);
                }
                state.Health = healthAfter;
                target.playerInstance.EvadeShieldHealth.setValue(healthAfter);
            }

            string name = TimedEffects.PlayerName(target);
            if (before < cost)
            {
                if (scope.Current && AbilityBuffs.Remove(target, Parent, generation: generation))
                    Output.WriteDebugLog($"[EVASION] {name} resistance shortfall required={cost} available={before}, shield released");
                return false;
            }

            Output.WriteDebugLog($"[EVASION] {name} resistance IS {before}->{after} cost={cost}; shield {healthBefore}->{healthAfter} damage={damage}");
            if (scope.Current && healthAfter == 0)
                AbilityBuffs.Remove(target, Parent, generation: generation);
            else if (ActiveGeneration(target) == generation && target.playerInstance.EvadeShieldHealth.getValue()[0] == healthAfter)
                PvpFlag.ToWatchers(target, PlayerViewAttributes.Build((21, PlayerViewAttributes.U8(healthAfter))));
            target.FlushQueue();
            return false;
        }

        internal static void Applied(WorldClient target, int generation)
        {
            states.Remove(target);
            states.Add(target,new State { Generation=generation,World=target.playerData.worldGeneration,Life=target.playerData.lifeGeneration });
            bool on = HasState(target);
            Present(target, on, false);
            if (on)
            {
                // EvadeCombatInit. evadecombat.txt:75 has both names set to the caster and two zero values.
                string name = TimedEffects.PlayerName(target);
                target.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(0x7c0001b9, true, Parent, name, name, 0, 0));
            }
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
            byte health = 0;
            if (states.TryGetValue(target, out var state))
            {
                state.Presented = on;
                if (on) health = state.Health;
            }
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
            byte health = HasState(target) ? target.playerInstance.EvadeShieldHealth.getValue()[0] : (byte)0;
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
