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
    /// Hyper- defences). The proxy_024 capture confirms this switch for 378 and 485.
    /// </summary>
    public static class AbilityAuras
    {
        public const uint DrainDeactivated = 0xc60020b4;

        private sealed class State
        {
            public readonly Dictionary<ushort, Activation> On = new Dictionary<ushort, Activation>();
            public int Generation;
        }

        private sealed class Activation
        {
            internal readonly int Generation;
            internal readonly AbilityBuffs.DrainLifetime Lifetime;
            internal readonly (ushort Id, ushort Target, int Value)[] ParentRecords;

            internal Activation(ushort ability, int generation, AbilityBuffs.DrainLifetime lifetime)
            {
                Generation = generation;
                Lifetime = lifetime;
                ParentRecords = CapturedParentModifiers(ability)
                    .Select(modifier => (AbilityBuffs.NextRecordId(lifetime.Data), modifier.Target, (int)modifier.Number("MinValue")))
                    .ToArray();
            }

            internal bool Current => Lifetime.Current && !Lifetime.Data.isDead && !Lifetime.Data.duelDeath;
        }

        private static readonly ConditionalWeakTable<WorldClient, State> states = new ConditionalWeakTable<WorldClient, State>();

        private static AbilityCatalog.Modifier Callback(ushort id, string func) => AbilityCatalog.Find(id)?.Modifiers
            .FirstOrDefault(m => m.Number("WhenEnabled", -1) == 2 && m.Text("StatModUpdateFunc") == func);

        // proxy_024 records parent, aura/drain target, and value before the child pulse. Use the captured callback order.
        private static IEnumerable<AbilityCatalog.Modifier> CapturedParentModifiers(ushort id)
        {
            if (id != 378 && id != 485) yield break;
            yield return Callback(id, "ApplyAuraEffect");
            if (id == 485) yield return AbilityCatalog.Find(id).Modifiers.Single(m => m.Text("StatModTermFunc") == "AddRecastTimer");
            yield return Callback(id, "ISDrainWithAbilityDeactivation");
        }

        private static void SendParentRecords(WorldClient client, ushort id, Activation activation, bool off)
        {
            foreach (var record in activation.ParentRecords)
                client.messageQueue.AddWorldBoundRpc(
                    AbilityBuffs.BuildModifier(TempMods.KindLinked, record.Id, id, record.Target, record.Value, off, 0),
                    activation.Lifetime.World, activation.Lifetime.Transport);
        }

        /// <summary>Supported hostile, support and friendly simulacra auras.</summary>
        public static bool Handles(ushort id) => AbilityCatalog.Find(id)?.Number("Ability/IsAToggleAbility") == 1 && EffectOf(id) != 0 &&
            (AbilityHostile.IsDebuff(EffectOf(id)) || Friendly(id) || id == ProxyTechnician.Ability || id == DaemonBoost.Ability);

        // Explicit beneficial parents only. Deflect Virus's child term sweep is executed by its saved buff definition.
        // Beneficial parents retain the existing off-time cooldown policy; default native timing remains unverified.
        public static bool Friendly(ushort id) => id == 42 || id == 485 || id == 378 || id == 380;

        public static ushort EffectOf(ushort id) => (ushort)(Callback(id, "ApplyAuraEffect")?.Number("MinValue") ?? 0);

        public static int DrainOf(ushort id) => (int)(Callback(id, "ISDrainWithAbilityDeactivation")?.Number("MinValue") ?? 0);

        public static double IntervalOf(ushort id) => Callback(id, "ApplyAuraEffect")?.Number("UpdateFuncInterval", 5) ?? 5;

        public static bool IsOn(WorldClient client, ushort id)
        {
            var state = states.GetOrCreateValue(client);
            lock (state) return state.On.TryGetValue(AbilityCatalog.Canonical(id), out var activation) && activation.Current;
        }

        public static List<ushort> Active(WorldClient client)
        {
            var state = states.GetOrCreateValue(client);
            lock (state) return state.On.Where(entry => entry.Value.Current).Select(entry => entry.Key).ToList();
        }

        public static void Start(WorldClient client, ushort id)
        {
            id = AbilityCatalog.Canonical(id);
            if (!Handles(id) || !AbilityBuffBuffer.CanApply(client, id)) return;
            var lifetime = new AbilityBuffs.DrainLifetime(client);
            var state = states.GetOrCreateValue(client);
            int generation;
            Activation activation;
            lock (state)
            {
                if (!lifetime.Current || lifetime.Data.isDead || lifetime.Data.duelDeath ||
                    state.On.TryGetValue(id, out var existing) && existing.Current) return;
                generation = state.Generation + 1;
                if (id == ProxyTechnician.Ability && !ProxyTechnician.Begin(client,generation)) return;
                if (id == DaemonBoost.Ability && !DaemonBoost.Loaded(client)) return;
                state.Generation = generation;
                activation = new Activation(id, generation, lifetime);
                state.On[id] = activation;
            }
            client.messageQueue.AddWorldBoundRpc(AbilityBuffs.BuildExclusive(id, true), lifetime.World, lifetime.Transport);
            SendParentRecords(client, id, activation, false);
            var aura = Callback(id, "ApplyAuraEffect");
            uint onSelf = (uint)AbilityCatalog.Find(id).Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2)
                .Select(m => m.Number("ActivationStringOnSelf")).FirstOrDefault(line => line != 0);
            // "Your %1z ability will reduce the combat tactics of nearby enemies while active." (0xc600222f, Miasma 1.0)
            string name = TimedEffects.PlayerName(client);
            if (onSelf != 0)
            {
                // The retained Deflect Virus and Combat Aura 2.0 announcements carry these two values.
                int[] values = id == 378 ? new[] { (int)EffectOf(id), 0 } :
                    id == 485 ? new[] { DrainOf(id), 0 } : Array.Empty<int>();
                client.messageQueue.AddWorldBoundRpc(AbilityMessages.AboutAbility(onSelf, true, id, name, name, values),
                    lifetime.World, lifetime.Transport);
            }
            client.FlushQueue();
            Output.WriteDebugLog("[AURA] " + id + " on for " + name);
            if (aura?.Text("StatModInitFunc") == "ApplyAuraEffect") Pulse(client, id,generation);
            Schedule(client, id, generation);
        }

        private static void Schedule(WorldClient client, ushort id, int generation) =>
            DelayedActions.AfterFor(client, IntervalOf(id), () => Tick(client, id, generation));

        private static bool Current(WorldClient client, ushort id, int generation)
        {
            var state = states.GetOrCreateValue(client);
            lock (state) return state.On.TryGetValue(id, out var active) && active.Generation == generation && active.Current;
        }

        internal static void Tick(WorldClient client, ushort id, int generation)
        {
            if (!Current(client, id, generation))
            {
                Stop(client, id, generation, announce: false);
                return;
            }
            if (id == ProxyTechnician.Ability && !ProxyTechnician.Matches(client,generation)) { Stop(client,id,generation); return; }
            if (id == DaemonBoost.Ability && !DaemonBoost.Loaded(client)) { Stop(client,id,generation); return; }
            if (!PayDrain(client, id, generation)) return;
            Pulse(client, id, generation);
            if (Current(client, id, generation)) Schedule(client, id, generation);
        }

        internal static bool PayDrain(WorldClient client, ushort id, int? generation = null)
        {
            int drain = DrainOf(id);
            if (drain > 0)
            {
                int available;
                var state = states.GetOrCreateValue(client);
                lock (state)
                {
                    if (generation.HasValue && !Current(client, id, generation.Value)) return false;
                    available = AbilityHandler.CurrentInnerStrength(client);
                    // Publish the tick debit before releasing its generation. A later stop and
                    // cast cannot put an older balance after the cast's own debit.
                    if (available >= drain)
                        new ServerPackets().sendISCurrent(client, AbilityHandler.SpendInnerStrength(client, (ushort)drain));
                }
                if (available < drain)
                {
                    if (!generation.HasValue || Current(client, id, generation.Value))
                    {
                        string name = TimedEffects.PlayerName(client);
                        client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(DrainDeactivated, true, id, name, name, drain - available));
                        Stop(client, id, generation);
                    }
                    return false;
                }
                if (id == ProxyTechnician.Ability) Output.WriteDebugLog("[PROXYTECH] drain source="+TimedEffects.PlayerName(client)+" amount="+drain);
                if (id == DaemonBoost.Ability) Output.WriteDebugLog($"[DAEMON BOOST] 609 upkeep source={client.playerData.getCharID()} paid={drain} IS={available}->{available - drain}");
            }
            return true;
        }

        /// <summary>One ApplyAuraEffect invocation on the supported recipients around the caster.</summary>
        private static void Pulse(WorldClient client, ushort id, int generation)
        {
            if (!Current(client, id, generation) || PvpCombat.HealthOf(client) == 0) return;
            if (id == ProxyTechnician.Ability) { ProxyTechnician.Pulse(client,generation); return; }
            if (id == DaemonBoost.Ability)
            {
                PulseDaemonBoost(client, generation);
                return;
            }
            ushort effectId = EffectOf(id);
            var effect = AbilityCatalog.Find(effectId);
            if (effect == null) return;
            if (Friendly(id))
            {
                // Use the effect's radius, exactly as ApplyAuraEffect invokes the effect ability.
                AbilitySupport.Land(client, effectId, 2, target => Virii.ApplyDeflectEffect(id, client, target));
                Output.WriteDebugLog("[AURA] " + id + " friendly pulse " + effectId);
                return;
            }
            // These parents reapply a child that owns the viral test. Upkeep is paid once
            // before the eligible recipients roll independently.
            var contest = (effectId == 694 || effectId == 701 || id == 945 && effectId == 946) &&
                effect.Number("Ability/TestFunction") == 2
                ? new AbilityItem() : null;
            contest?.setAbilityID(effectId);
            double radius = effect.Number("Ability/RadiusOfEffect");
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            ushort damage = AbilityHostile.InstantDamage(client, effectId);
            uint fx = (uint)effect.Modifiers.Select(m => m.Number("ActivationFX")).FirstOrDefault(n => n > 0);
            string name = TimedEffects.PlayerName(client);
            int hits = 0;
            foreach (var (mob, view) in AbilityHostile.MobsInArea(client, x, y, z, radius))
            {
                if (!Current(client, id, generation)) break;
                if (contest != null && !FreeCastViralContest.Npc(client, contest, mob, client.viewMan.getViewById(view))) continue;
                TimedEffects.ApplyToMob(client, mob, view, effectId);
                if (damage > 0)
                {
                    ushort hit = TypedAbilityResistance.Npc(client, mob, effectId, PunishingBlows.Npc(client, mob, damage));
                    if (hit > 0) CombatManager.GetHandler(client).ApplyAbilityToNpc(mob, view, hit, fx);
                }
                hits++;
            }
            foreach (WorldClient target in AbilityHostile.PlayersInArea(client, x, y, z, radius))
            {
                if (!Current(client, id, generation)) break;
                if (contest != null && !FreeCastViralContest.Player(client, contest, target)) continue;
                PvpFlag.OnFightStarts(client, true);
                TimedEffects.ApplyToPlayer(target, effectId, name, client);
                if (effectId == 946) // Trojan Horse's instant Current Inner Strength modifier.
                    AbilityHostile.LandedOnPlayer(client, target, effectId);
                if (damage > 0)
                {
                    int raw = PunishingBlows.Player(client, target, damage);
                    ushort targetDamage = TypedAbilityResistance.Player(client, target, effectId, raw);
                    target.messageQueue.addRpcMessage(AbilityMessages.Damage(false, effectId, name,
                        TimedEffects.PlayerName(target), targetDamage, raw - targetDamage));
                    target.FlushQueue();
                    if (PvpCombat.Damage(target, targetDamage, fx)) PvpCombat.Defeated(target, client);
                }
                hits++;
            }
            Output.WriteDebugLog("[AURA] " + id + " pulse " + effectId + " reaches " + hits + " targets" + (damage > 0 ? " for " + damage : ""));
        }

        private static void PulseDaemonBoost(WorldClient client, int generation)
        {
            var state = states.GetOrCreateValue(client);
            double now = TimeUtils.GetSimTimeSeconds();
            foreach (var pet in CoderPets.SnapshotPets())
            {
                DaemonBoost.PulseEffect effect;
                lock (state)
                {
                    if (!Current(client, DaemonBoost.Ability, generation)) return;
                    effect = DaemonBoost.Project(client, pet, now);
                }
                if (effect != null) DaemonBoost.Publish(pet, effect);
            }
        }

        /// <summary>Switches the aura off (toggled again, IS shortfall, death, a Delete Upgrade). Returns whether it was on.</summary>
        public static bool Stop(WorldClient client, ushort id, int? generation = null, bool announce = true)
        {
            id = AbilityCatalog.Canonical(id);
            var state = states.GetOrCreateValue(client);
            Activation active;
            lock (state)
            {
                if (!state.On.TryGetValue(id, out active) || generation.HasValue && active.Generation != generation.Value) return false;
                state.On.Remove(id);
                if (id == ProxyTechnician.Ability) ProxyTechnician.End(client);
                if (!active.Lifetime.ScopeCurrent || !announce) return false;
            }
            SendParentRecords(client, id, active, true);
            client.messageQueue.AddWorldBoundRpc(AbilityBuffs.BuildExclusive(id, false), active.Lifetime.World, active.Lifetime.Transport);
            if (Friendly(id) && id != Virii.DeflectVirus1Ability) AbilityCooldowns.Start(client, id, TimeUtils.GetSimTimeSeconds());
            client.FlushQueue();
            Output.WriteDebugLog("[AURA] " + id + " off for " + TimedEffects.PlayerName(client));
            return true;
        }

        internal static void Clear(WorldClient client, bool announce)
        {
            if (!states.TryGetValue(client, out var state)) return;
            KeyValuePair<ushort, Activation>[] pending;
            lock (state) pending = state.On.ToArray();
            foreach (var entry in pending) Stop(client, entry.Key, entry.Value.Generation, announce);
        }
    }
}
