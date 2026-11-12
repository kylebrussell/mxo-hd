using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace hds
{
    /// <summary>
    /// Toggled hostile auras (Miasma 1.0 61, Static Field 89). Their shipped templates are self-cast toggles whose
    /// "while enabled" modifiers run two update callbacks every UpdateFuncInterval (5 s):
    /// <list type="bullet">
    /// <item><c>ISDrainWithAbilityDeactivation</c> (tempmods.py 995): takes Value IS, or deactivates the ability with
    /// "Your %1z has been deactivated.  You needed %4I more Inner Strength to maintain it." (0xc60020b4) when short.</item>
    /// <item><c>ApplyAuraEffect</c> (tempmods.py 1308), also the init callback: while the caster has health it re-applies the
    /// effect ability named by Value (Miasma 701: -14 melee/ballistic defence for 5 s; Static Field 694: viral health
    /// raw 1, instant) to hostile targets within the effect's RadiusOfEffect of the caster.</item>
    /// </list>
    /// Because the effect lasts one pulse, a target that leaves the radius keeps it for at most one interval.
    /// The <c>80 bd 05</c> on/off switch is the one retail sent for other "while enabled" toggles (Evade Combat, the
    /// Hyper- defences); no aura capture exists.
    /// </summary>
    public static class AbilityAuras
    {
        public const uint DrainDeactivated = 0xc60020b4;

        private sealed class State
        {
            public readonly Dictionary<ushort, int> On = new Dictionary<ushort, int>();
            public int Generation;
        }

        private static readonly ConditionalWeakTable<WorldClient, State> states = new ConditionalWeakTable<WorldClient, State>();

        private static AbilityCatalog.Modifier Callback(ushort id, string func) => AbilityCatalog.Find(id)?.Modifiers
            .FirstOrDefault(m => m.Number("WhenEnabled", -1) == 2 && m.Text("StatModUpdateFunc") == func);

        public static bool Handles(ushort id) => AbilityCatalog.Find(id)?.Number("Ability/IsAToggleAbility") == 1 && EffectOf(id) != 0;

        public static ushort EffectOf(ushort id) => (ushort)(Callback(id, "ApplyAuraEffect")?.Number("MinValue") ?? 0);

        public static int DrainOf(ushort id) => (int)(Callback(id, "ISDrainWithAbilityDeactivation")?.Number("MinValue") ?? 0);

        public static double IntervalOf(ushort id) => Callback(id, "ApplyAuraEffect")?.Number("UpdateFuncInterval", 5) ?? 5;

        public static bool IsOn(WorldClient client, ushort id)
        {
            var state = states.GetOrCreateValue(client);
            lock (state) return state.On.ContainsKey(AbilityCatalog.Canonical(id));
        }

        public static List<ushort> Active(WorldClient client)
        {
            var state = states.GetOrCreateValue(client);
            lock (state) return state.On.Keys.ToList();
        }

        public static void Start(WorldClient client, ushort id)
        {
            id = AbilityCatalog.Canonical(id);
            if (!Handles(id)) return;
            var state = states.GetOrCreateValue(client);
            int generation;
            lock (state) { generation = ++state.Generation; state.On[id] = generation; }
            client.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(id, true));
            var aura = Callback(id, "ApplyAuraEffect");
            uint onSelf = (uint)(aura?.Number("ActivationStringOnSelf") ?? 0);
            // "Your %1z ability will reduce the combat tactics of nearby enemies while active." (0xc600222f, Miasma 1.0)
            string name = TimedEffects.PlayerName(client);
            if (onSelf != 0) client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(onSelf, true, id, name, name));
            client.FlushQueue();
            Output.WriteDebugLog("[AURA] " + id + " on for " + name);
            if (aura?.Text("StatModInitFunc") == "ApplyAuraEffect") Pulse(client, id);
            Schedule(client, id, generation);
        }

        private static void Schedule(WorldClient client, ushort id, int generation) =>
            DelayedActions.AfterFor(client, IntervalOf(id), () => Tick(client, id, generation));

        private static bool Current(WorldClient client, ushort id, int generation)
        {
            var state = states.GetOrCreateValue(client);
            lock (state) return state.On.TryGetValue(id, out int g) && g == generation;
        }

        internal static void Tick(WorldClient client, ushort id, int generation)
        {
            if (!Current(client, id, generation)) return;
            if (client.playerData.isDead || client.playerData.duelDeath || !SocialHandler.OnlineWhere(c => c == client).Any())
            {
                Stop(client, id);
                return;
            }
            int drain = DrainOf(id);
            if (drain > 0)
            {
                int available = AbilityHandler.CurrentInnerStrength(client);
                if (available < drain)
                {
                    string name = TimedEffects.PlayerName(client);
                    client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(DrainDeactivated, true, id, name, name, drain - available));
                    Stop(client, id);
                    return;
                }
                new ServerPackets().sendISCurrent(client, AbilityHandler.SpendInnerStrength(client, (ushort)drain));
            }
            Pulse(client, id);
            Schedule(client, id, generation);
        }

        /// <summary>One ApplyAuraEffect: the effect ability on hostile NPCs and PvP-legal players around the caster.</summary>
        public static void Pulse(WorldClient client, ushort id)
        {
            if (PvpCombat.HealthOf(client) == 0) return;
            ushort effectId = EffectOf(id);
            var effect = AbilityCatalog.Find(effectId);
            if (effect == null) return;
            double radius = effect.Number("Ability/RadiusOfEffect");
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            ushort damage = AbilityHostile.InstantDamage(client, effectId);
            uint fx = (uint)effect.Modifiers.Select(m => m.Number("ActivationFX")).FirstOrDefault(n => n > 0);
            string name = TimedEffects.PlayerName(client);
            int hits = 0;
            foreach (var (mob, view) in AbilityHostile.MobsInArea(client, x, y, z, radius))
            {
                TimedEffects.ApplyToMob(client, mob, view, effectId);
                if (damage > 0) CombatManager.GetHandler(client).ApplyAbilityToNpc(mob, view, damage, fx);
                hits++;
            }
            foreach (WorldClient target in AbilityHostile.PlayersInArea(client, x, y, z, radius))
            {
                PvpFlag.OnFightStarts(client, true);
                TimedEffects.ApplyToPlayer(target, effectId, name, client);
                if (damage > 0)
                {
                    target.messageQueue.addRpcMessage(AbilityMessages.Damage(false, effectId, name, TimedEffects.PlayerName(target), damage, 0));
                    target.FlushQueue();
                    if (PvpCombat.Damage(target, damage, fx)) PvpCombat.Defeated(target, client);
                }
                hits++;
            }
            Output.WriteDebugLog("[AURA] " + id + " pulse " + effectId + " reaches " + hits + " targets" + (damage > 0 ? " for " + damage : ""));
        }

        /// <summary>Switches the aura off (toggled again, IS shortfall, death, a Delete Upgrade). Returns whether it was on.</summary>
        public static bool Stop(WorldClient client, ushort id)
        {
            id = AbilityCatalog.Canonical(id);
            var state = states.GetOrCreateValue(client);
            lock (state) if (!state.On.Remove(id)) return false;
            client.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(id, false));
            client.FlushQueue();
            Output.WriteDebugLog("[AURA] " + id + " off for " + TimedEffects.PlayerName(client));
            return true;
        }
    }
}
