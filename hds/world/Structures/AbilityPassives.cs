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
            internal int Generation;
            internal readonly Dictionary<ushort, ushort> Loaded = new Dictionary<ushort, ushort>();
            internal readonly Dictionary<string, Active> Records = new Dictionary<string, Active>();
            internal Dictionary<ushort, int> Totals = new Dictionary<ushort, int>();
            internal AbilityShadowPresentation.Owned ShadowOwned;
            internal AbilityShadowPresentation.Emitted ShadowEmitted;
            internal (int World, long Transport)? Presentation;
            internal bool AvatarReady;
        }
        internal sealed class Active
        {
            internal ushort Ability, Stat, Record;
            internal int Value;
        }

        internal static bool BeginWorldPresentation(WorldClient client)
        {
            var owner = client.playerData;
            long transport = client.TransportFence.Capture();
            lock (owner.passives)
            {
                var stamp = (owner.worldGeneration, transport);
                if (owner.passives.Presentation == stamp) return false;
                owner.passives.Presentation = stamp;
                owner.passives.AvatarReady = false;
                return true;
            }
        }

        internal static void LoadingComplete(WorldClient client, byte[] body)
        {
            if (body?.Length != 1 || body[0] != 1) return;
            var owner = client.playerData;
            var state = owner.passives;
            lock (state)
            {
                var stamp = state.Presentation;
                if (state.AvatarReady || !stamp.HasValue ||
                    stamp.Value.World != owner.worldGeneration || !client.TransportFence.IsCurrent(stamp.Value.Transport) ||
                    !DeathPenalty.CanPresent(owner)) return;
                state.AvatarReady = true;
            }
            if (ReferenceEquals(client.playerData, owner)) Refresh(client);
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
        public static bool IsLoaded(WorldClient client, ushort ability)
        {
            State state=client.playerData.passives;
            lock(state)return state.Loaded.ContainsKey(AbilityCatalog.Canonical(ability));
        }

        public static void Load(WorldClient client, bool newWorld = false)
        {
            var rows = Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID());
            SetLoadedCore(client, LoadedArchiveAbilities(rows), newWorld, AbilityShadowPresentation.FromArchive(rows),default);
        }

        internal static IEnumerable<(ushort Ability,ushort Level)> LoadedArchiveAbilities(IEnumerable<MarginAbilityItem> rows) =>
            rows.Where(r=>r.getLoaded() && (unchecked((uint)r.getAbilityID()) & 0xfc000000u) == CodeStorage.AbilityFlag).Select(r=>(AbilityCatalog.IdOf(unchecked((uint)r.getAbilityID())),r.getLevel()));

        public static void SetLoaded(WorldClient client, IEnumerable<(ushort Ability, ushort Level)> loaded, bool newWorld = false)
            =>SetLoadedCore(client,loaded,newWorld,null,default);

        internal static void SetLoadedFromRows(WorldClient client,IEnumerable<AbilityMemory.Row> rows,byte[] emittedShadowBase=null)
        {
            var snapshot=rows.ToArray();
            SetLoadedCore(client,snapshot.Where(r=>AbilityMemory.IsAbility(r.Descriptor)).Select(r=>(r.Ability,r.Level)),false,
                AbilityShadowPresentation.FromRows(snapshot),AbilityShadowPresentation.Emitted.FromPacket(emittedShadowBase));
        }
        private static void SetLoadedCore(WorldClient client,IEnumerable<(ushort Ability,ushort Level)> loaded,bool newWorld,
            AbilityShadowPresentation.Owned? explicitShadow,AbilityShadowPresentation.Emitted emitted)
        {
            var rows=loaded.ToArray();
            State state = client.playerData.passives;
            var pet = CoderPets.Of(client);
            bool deactivateSummon;
            bool writeCodeAdded;
            lock (state)
            {
                state.Generation++;
                state.ShadowOwned=explicitShadow??state.ShadowOwned.Retain(rows);
                state.ShadowEmitted=emitted;
                bool hadMissionMap = state.Loaded.ContainsKey(UtilityAbilities.DownloadMissionMap);
                bool hadWriteCode = state.Loaded.ContainsKey(CraftingSession.WriteCode);
                bool hadSummon = pet != null && state.Loaded.ContainsKey(pet.Ability);
                state.Loaded.Clear();
                foreach (var row in rows)
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
                if (hadMissionMap && !state.Loaded.ContainsKey(UtilityAbilities.DownloadMissionMap))
                    UtilityAbilities.DisableMissionMap(client);
                deactivateSummon = hadSummon && !state.Loaded.ContainsKey(pet.Ability);
                writeCodeAdded = !hadWriteCode && state.Loaded.ContainsKey(CraftingSession.WriteCode);
                if (newWorld) state.Records.Clear(); // the client discarded the old world's records
            }
            if (deactivateSummon) CoderPets.DeactivateUnloadedSummon(client, pet);
            if (newWorld) CraftingSession.End(client, false);
            else
            {
                if (writeCodeAdded) CraftingSession.WriteCodeLoaded(client);
                CraftingSession.Check(client);
            }
            Refresh(client, !newWorld);
            EvasionShield.LoadoutChanged(client);
            AbilityDesperation.LoadoutChanged(client);
            ProxyTechnician.LoadoutChanged(client);
            AbilityGrantRegistration.LoadedChanged(client);
        }

        /// <summary>Recomputes totals and records. A style switch (CombatStyles.Select) orders them as retail did when Hacker
        /// Style replaced Self Defense (actions_combat_marketplace_abilitychangecoder 10:36:05): the old style's records
        /// off, its 80 bd 05 toggle off, the new style's records on, its toggle on.</summary>
        public static void Refresh(WorldClient client, bool present = true, (ushort From, ushort To)? styleSwitch = null)
        {
            var owner = client.playerData;
            State state = owner.passives;
            bool sendVitals = present && DeathPenalty.CanPresent(owner);
            (int World, long Transport)? stamp;
            var messages = new List<byte[]>();
            // Vital refreshes take these locks in this order too; record ids share the active-buff allocator.
            lock (owner.activeBuffs)
            lock (state)
            {
                stamp = state.Presentation;
                // A spawned self view is insufficient. Native 0c01 follows live-avatar
                // initialization, so only publish the loaded stats after that marker.
                bool send = sendVitals && (!stamp.HasValue ||
                    state.AvatarReady && stamp.Value.World == owner.worldGeneration &&
                    client.TransportFence.IsCurrent(stamp.Value.Transport));
                var current = new Dictionary<string, Active>();
                var totals = new Dictionary<ushort, int>();
                int characterLevel = client.playerInstance.Level.getValue()[0];
                // The active combat style's while-enabled modifiers join the loaded passives (CombatStyles).
                ushort style = state.Loaded.ContainsKey(client.playerData.combatStyle) ? client.playerData.combatStyle :
                    state.Loaded.ContainsKey(CombatStyles.SelfDefense) ? CombatStyles.SelfDefense : (ushort)0;
                var sources = state.Loaded.OrderBy(r => r.Key).SelectMany(r => Modifiers(r.Key).Select(m => (Row: r, Modifier: m)));
                if (style != 0)
                    sources = sources.Concat(CombatStyles.Modifiers(style).Select(m => (Row: new KeyValuePair<ushort, ushort>(style, state.Loaded[style]), Modifier: m)));
                foreach (var (row, modifier) in sources)
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
                    else active.Record = AbilityBuffs.NextRecordId(owner);
                    messages.Add(Record(active, false));
                }
                if (send)
                {
                    var removed = state.Records.Where(old => !current.ContainsKey(old.Key)).Select(old => old.Value).ToList();
                    if (styleSwitch.HasValue)
                    {
                        var first = removed.Where(r => r.Ability == styleSwitch.Value.From).Select(r => Record(r, true)).ToList();
                        first.Add(AbilityBuffs.BuildExclusive(styleSwitch.Value.From, false));
                        messages.InsertRange(0, first);
                        removed.RemoveAll(r => r.Ability == styleSwitch.Value.From);
                    }
                    foreach (var old in removed) messages.Add(Record(old, true));
                    if (styleSwitch.HasValue) messages.Add(AbilityBuffs.BuildExclusive(styleSwitch.Value.To, true));
                    state.Records.Clear();
                    foreach (var active in current) state.Records[active.Key] = active.Value;
                }
                state.Totals = totals;
            }
            if (!ReferenceEquals(client.playerData, owner)) return;
            foreach (byte[] message in messages)
                if (stamp is { } expected) client.messageQueue.AddWorldBoundRpc(message, expected.World, expected.Transport);
                else client.messageQueue.addRpcMessage(message);
            AbilityBuffs.RefreshVitals(client, sendVitals);
        }

        // Style records are kind 0x15 in the same capture; loaded passives keep 0x45
        private static byte[] Record(Active active, bool off) => AbilityBuffs.BuildModifier(
            CombatStyles.IsStyle(active.Ability) ? AbilityBuffs.KindStat : Kind, active.Record, active.Ability, active.Stat, active.Value, off, 0);
    }
}
