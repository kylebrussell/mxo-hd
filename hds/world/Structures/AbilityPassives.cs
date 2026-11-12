using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Loaded ability modifiers, independent of cast buffs. Only owned/loadable abilities and their grants enter here.</summary>
    public static class AbilityPassives
    {
        public const ushort Kind = 0x45; // retail's while-enabled stat records: 80 bc 45 00 (active buffs use 0x15)
        public sealed class State
        {
            internal readonly Dictionary<ushort, ushort> Loaded = new Dictionary<ushort, ushort>();
            internal readonly Dictionary<string, Active> Records = new Dictionary<string, Active>();
            internal Dictionary<ushort, int> Totals = new Dictionary<ushort, int>();
        }
        internal sealed class Active
        {
            internal ushort Ability, Stat, Record;
            internal int Value;
        }

        public static IEnumerable<AbilityCatalog.Modifier> Modifiers(ushort ability)
        {
            var entry = AbilityCatalog.Find(ability);
            return entry == null || entry.Number("Ability/AbilityClass", -1) != 0
                ? Enumerable.Empty<AbilityCatalog.Modifier>()
                : entry.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 0 && m.Target != 0 &&
                    m.Number("DurationType", -1) == 2 && m.State == 0 && m.Number("TargetAbilityGrant") == 0);
        }

        public static int Total(WorldClient client, ushort stat)
        {
            State state = client.playerData.passives;
            lock (state) return state.Totals.TryGetValue(stat, out int value) ? value : 0;
        }

        public static int Level(WorldClient client, ushort ability)
        {
            State state = client.playerData.passives;
            lock (state) return state.Loaded.TryGetValue(AbilityCatalog.Canonical(ability), out ushort level) ? level : 0;
        }

        public static void Load(WorldClient client, bool newWorld = false)
        {
            var rows = Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID());
            SetLoaded(client, rows.Where(r => r.getLoaded()).Select(r =>
                (AbilityCatalog.IdOf(unchecked((uint)r.getAbilityID())), r.getLevel())), newWorld);
        }

        public static void SetLoaded(WorldClient client, IEnumerable<(ushort Ability, ushort Level)> loaded, bool newWorld = false)
        {
            State state = client.playerData.passives;
            lock (state)
            {
                state.Loaded.Clear();
                foreach (var row in loaded)
                {
                    ushort id = AbilityCatalog.Canonical(row.Ability);
                    var entry = AbilityCatalog.Find(id);
                    if (entry == null || entry.Number("Ability/AbilityClass", -1) != 0 ||
                        entry.Number("Ability/InventoryMemCost") <= 0 && entry.Number("Ability/IsDiscipline") == 0) continue;
                    ushort level = (ushort)Math.Clamp(row.Level, 0, (int)entry.Number("Ability/AbilityLevelCap", 50));
                    if (!state.Loaded.TryGetValue(id, out ushort previous) || previous < level) state.Loaded[id] = level;
                }
                // Resolve the closure once; a shared grant contributes once even when several disciplines grant it.
                var pending = new Queue<ushort>(state.Loaded.Keys);
                while (pending.Count > 0)
                {
                    ushort id = pending.Dequeue();
                    foreach (ushort grant in AbilityProgression.GrantsOf(id))
                        if (!state.Loaded.ContainsKey(grant))
                        {
                            state.Loaded[grant] = AbilityProgression.GrantLevel(id, state.Loaded[id]);
                            pending.Enqueue(grant);
                        }
                }
                if (newWorld) state.Records.Clear(); // the client discarded the old world's records
            }
            Refresh(client, !newWorld);
        }

        public static void Refresh(WorldClient client, bool present = true)
        {
            State state = client.playerData.passives;
            bool send = present && DeathPenalty.CanPresent(client.playerData);
            var messages = new List<byte[]>();
            // Vital refreshes take these locks in this order too; record ids share the active-buff allocator.
            lock (client.playerData.activeBuffs)
            lock (state)
            {
                var current = new Dictionary<string, Active>();
                var totals = new Dictionary<ushort, int>();
                int characterLevel = client.playerInstance.Level.getValue()[0];
                foreach (var row in state.Loaded.OrderBy(r => r.Key))
                foreach (var modifier in Modifiers(row.Key))
                {
                    if (!AbilityValues.TryValue(modifier, DeathPenalty.EffectiveLevel(client, row.Value), characterLevel, out int value)) continue;
                    string key = row.Key + ":" + modifier.Component;
                    var active = new Active { Ability = row.Key, Stat = modifier.Target, Value = value };
                    current[key] = active;
                    totals[active.Stat] = (totals.TryGetValue(active.Stat, out int total) ? total : 0) + value;
                    if (!send) continue;
                    if (state.Records.TryGetValue(key, out Active old))
                    {
                        active.Record = old.Record;
                        if (old.Value == value) continue;
                        messages.Add(Record(old, true));
                    }
                    else active.Record = AbilityBuffs.NextRecordId(client.playerData);
                    messages.Add(Record(active, false));
                }
                if (send)
                {
                    foreach (var old in state.Records)
                        if (!current.ContainsKey(old.Key)) messages.Add(Record(old.Value, true));
                    state.Records.Clear();
                    foreach (var active in current) state.Records[active.Key] = active.Value;
                }
                state.Totals = totals;
            }
            foreach (byte[] message in messages) client.messageQueue.addRpcMessage(message);
            AbilityBuffs.RefreshVitals(client, send);
        }

        private static byte[] Record(Active active, bool off) => AbilityBuffs.BuildModifier(Kind, active.Record,
            active.Ability, active.Stat, active.Value, off, 0);
    }
}
