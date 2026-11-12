using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace hds
{
    /// <summary>Stored attributes from the shipped 7.6005 GOB. Missing fields remain unknown engine defaults.</summary>
    public static class AbilityCatalog
    {
        public sealed class Entry
        {
            public UInt16 Id { get; internal set; }
            public string Name { get; internal set; }
            internal JsonElement Fields;
            public bool Has(string field) => Fields.TryGetProperty(field, out _);
            public double Number(string field, double fallback = 0) => Fields.TryGetProperty(field, out var value) &&
                value.ValueKind == JsonValueKind.Number ? value.GetDouble() : fallback;
            public string Text(string field) => Fields.TryGetProperty(field, out var value) &&
                value.ValueKind == JsonValueKind.String ? value.GetString() : "";
            public IEnumerable<Modifier> Modifiers => Fields.EnumerateObject()
                .Where(p => p.Name.EndsWith("/WhenEnabled", StringComparison.Ordinal))
                .Select(p => new Modifier(this, p.Name.Substring(0, p.Name.IndexOf('/'))));
            public bool Healing => Modifiers.Any(m => m.Target == 108 && m.Number("DamageType", -1) == 3 || m.Text("StatModUpdateFunc") == "HOT") || Id == 967;
            public bool DirectDamage => Modifiers.Any(m => m.Target == 108 && m.Number("DamageType", -1) != 3);
            public bool DamageOverTime => Modifiers.Any(m => m.Text("StatModUpdateFunc").EndsWith("DOT", StringComparison.Ordinal));
            public bool FriendlyTarget => Number("Ability/CastValidTarget", -1) == 2;
            public float CastSeconds => (float)Number("Ability/CastingTime");
        }

        public sealed class Modifier
        {
            private readonly Entry entry;
            public readonly string Component;
            internal Modifier(Entry entry, string component) { this.entry = entry; Component = component; }
            public bool Has(string field) => entry.Has(Component + "/" + field);
            public double Number(string field, double fallback = 0) => entry.Number(Component + "/" + field, fallback);
            public string Text(string field) => entry.Text(Component + "/" + field);
            public UInt16 Target => IdOf((UInt32)Number("TargetAttribute"));
            public UInt16 State => IdOf((UInt32)Number("TargetState"));
        }

        private static readonly Lazy<IReadOnlyDictionary<UInt16, Entry>> entries = new Lazy<IReadOnlyDictionary<UInt16, Entry>>(
            () => Read(DataLoader.DataPath("ability_catalog.json")));
        public static IReadOnlyDictionary<UInt16, Entry> All => entries.Value;
        public static UInt16 Canonical(UInt16 id) => (UInt16)(id % 4096);
        public static UInt16 IdOf(UInt32 goid) => (goid & 0x80000000) != 0 ? (UInt16)((goid & 0x7fffffff) >> 10) : (UInt16)0;
        public static Entry Find(UInt16 id) => All.TryGetValue(Canonical(id), out var entry) ? entry : null;
        public static IReadOnlyDictionary<UInt16, Entry> Read(string path)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.GetProperty("schema_version").GetInt32() != 1) throw new InvalidDataException("Ability catalog schema");
            return document.RootElement.GetProperty("abilities").EnumerateArray().Select(row => new Entry
            {
                Id = row.GetProperty("id").GetUInt16(), Name = row.GetProperty("name").GetString(),
                Fields = row.GetProperty("fields").Clone()
            }).ToDictionary(e => e.Id);
        }

        /// <summary>Only timed states with explicit duration are executable here; native scaling is not guessed.</summary>
        public static TempMods.Effect Controls(UInt16 id)
        {
            Entry entry = Find(id);
            if (entry == null) return null;
            var mods = new List<TempMods.Mod>();
            float immunity = 0;
            foreach (Modifier m in entry.Modifiers)
            {
                if (m.Number("WhenEnabled", -1) != 2) continue;
                if (m.Text("StatModTermFunc") == "AddControlImmunityTimerVal") immunity = (float)m.Number("MinValue");
                else if (m.Text("StatModInitFunc") == "AddControlImmunityTimer") immunity = (float)m.Number("Duration");
                if (m.Text("StatModTermFunc") == "ApplyStunEffectIfDazed" &&
                    m.Number("DurationType", -1) == 0 && m.Number("Duration") > 0 &&
                    AbilityValues.TryValue(m, 0, 0, out int stunSeconds))
                {
                    mods.Add(new TempMods.Mod { Kind = TempMods.KindFx, Target = id,
                        Proc = TempMods.Proc.Terminal, Seconds = (float)m.Number("Duration"),
                        MinValue = stunSeconds, MaxValue = stunSeconds });
                }
                UInt16 state = m.State;
                bool rootChance = m.Text("StatModInitFunc") == "EnableRootWithChance";
                if (rootChance) state = TempMods.RootedAbility;
                float seconds = (float)m.Number("Duration");
                if (state == 0 || seconds <= 0 || m.Number("DurationType", -1) != 0) continue;
                // States with bespoke engine/script behavior remain in the audit, not this generic executor.
                if (state != 85 && state != 90 && state != 91 && state != 93 && state != 96 && state != 99 &&
                    state != 102 && state != 199 && state != 200 && state != 201 && state != 231) continue;
                var mod = new TempMods.Mod { Kind = TempMods.KindState, Target = state, Seconds = seconds,
                    Proc = TempMods.Proc.State, Chance = (float)m.Number("ProcTriggerChance", 1) };
                string health = m.Text("StatModHealthChangedFunc");
                mod.Break = state == TempMods.MezAbility || health == "OnHealthReduceDeactivateAbility" ? TempMods.BreakRule.Damage :
                    health == "OnHealthChangeDeactivateAbility" ? TempMods.BreakRule.HealthChange : TempMods.BreakRule.None;
                if (rootChance || health == "OnHealthReduceDeactivateChance")
                {
                    Modifier chance = rootChance ? m : entry.Modifiers.FirstOrDefault(p => p.Text("StatModHealthChangedFunc") ==
                        "OnHealthReduceDeactivateChance" && p.Number("MinValue") != 0) ?? m;
                    int value = (int)chance.Number("MinValue");
                    mod.Break = TempMods.BreakRule.Chance;
                    mod.BreakChance = Math.Abs(value);
                    mod.ProtectFirstHealthChange = rootChance || value < 0;
                }
                mods.Add(mod);
            }
            return mods.Count == 0 ? null : new TempMods.Effect { Ability = id, Mods = mods.ToArray(), ImmunitySeconds = immunity };
        }
    }
}
