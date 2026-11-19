using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityHackerClassUpgradesMode : NativeQaMode
{
    private static readonly ushort[] FirstRoots = { 2, 17, 64, 483, 67, 356 };
    private static readonly ushort[] SecondRoots = { 2, 17, 67, 490 };
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Mods)[] Upgrades =
    {
        (64, 1078, new (ushort, int)[] { (564, 7), (114, 10), (342, 4) }),
        (483, 1080, new (ushort, int)[] { (564, 7), (114, 12), (342, 9) }),
        (67, 1085, new (ushort, int)[] { (564, 7), (177, 10), (342, 4) }),
        (356, 1087, new (ushort, int)[] { (342, 13), (172, 8), (564, 7), (177, 17) }),
        (490, 1091, new (ushort, int)[] { (564, 7), (117, 10) })
    };

    public override string Name => "ability-parity-hacker-class-upgrades";
    public override NativeQaModePlan Plan => MakePlan();

    private static HashSet<ushort> Profile(IEnumerable<ushort> roots)
    {
        var ids = roots.ToHashSet();
        var pending = new Queue<ushort>(ids);
        while (pending.TryDequeue(out ushort id))
        {
            var entry = AbilityCatalog.Find(id);
            var required = AbilityPrerequisites.Current.Closure(id).Keys
                .Append(AbilityCatalog.IdOf((uint)entry.Number("Ability/DisciplineBelongsTo")))
                .Append(AbilityCatalog.IdOf((uint)entry.Number("Ability/CastRequiredCombatStance")));
            foreach (ushort next in required)
                if (next != 0 && ids.Add(next))
                    pending.Enqueue(next);
        }
        return ids;
    }

    private static NativeQaModePlan MakePlan()
    {
        var first = Profile(FirstRoots);
        var second = Profile(SecondRoots);
        string staged = string.Join(",", second.Except(first).Order().Select(Descriptor));
        return new(first.Concat(second).Distinct().Order().ToArray(),
            CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
            Array.Empty<string>(),
            new[] { $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id IN ({staged})" },
            new[] { $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110" },
            environmentOverrides: new() { ["MXO_AGGRO"] = "0" },
            readyMessages: new[]
            {
                "Stage 1 uses 94/100 memory. With Class Upgrade Target selected, cast the granted Network Hacker, Network Attacker, Pathogenist and Howitzer upgrades in that order. Capture caster-only bonus adds, replacement removals and 30-second reuse.",
                "At the Hardline or Load Area, unload the first-stage-only entries listed in the manual, retain the shared entries and load the Force Multiplier chain. Stage 2 uses 89/100 memory. Select Class Upgrade Target again and cast the granted Force Multiplier action. Capture its own cost and concentration modifiers, replacement, and reuse."
            },
            extraQaMetadata: new()
            {
                ["InitialMemory"] = first.Sum(AbilityMemory.Cost),
                ["SecondStageMemory"] = second.Sum(AbilityMemory.Cost),
                ["UnloadForForceMultiplier"] = string.Join(",", first.Except(second).Order()),
                ["LoadForForceMultiplier"] = string.Join(",", second.Except(first).Order()),
                ["NativeCloseableRows"] = 5,
                ["NativeCloseableIds"] = "1078,1080,1085,1087,1091"
            },
            testMobFixture: "ability-parity-hacker-class-upgrades.csv");
    }

    private static uint Descriptor(ushort id) => 0x80000000u | ((uint)id << 10);
    private static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var actor = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester",
            stockCharacterLoad: true);
        var first = Profile(FirstRoots);
        var second = Profile(SecondRoots);
        Check.Equal(94, first.Sum(AbilityMemory.Cost), "First legal memory profile");
        Check.Equal(89, second.Sum(AbilityMemory.Cost), "Second legal memory profile");
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[7])
            .ToDictionary(item => AbilityCatalog.IdOf(item.Descriptor));
        Check.That(rows.Keys.ToHashSet().SetEquals(first.Concat(second)), "Stock Archive owns both profiles and no granted child");
        var initial = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Select(item => AbilityCatalog.IdOf(item.Descriptor)).ToHashSet();
        Check.That(initial.SetEquals(first), "Margin initially loads only the first legal profile");

        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync("Class Upgrade Target");
        async Task Bonus(ushort source, (ushort Stat, int Value) expected, int cursor, bool remove)
        {
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(rpc =>
                ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.Family == 0x15 &&
                bonus.SourceId == source && bonus.TargetId == expected.Stat && bonus.SignedValue == expected.Value &&
                bonus.AddRemoveType == (remove ? 1 : 0) && bonus.Duration == 0),
                $"Class upgrade {source}, modifier {expected.Stat}, remove={remove}", HeadlessPlayer.DefaultTimeout);
        }

        async Task Grant(int index, int cursor)
        {
            var (parent, child, _) = Upgrades[index];
            byte[] hidden = ServerPackets.BuildAbilityLoad(child, 0, AbilityProgression.HiddenSlot);
            await actor.WaitUntilAsync(() =>
            {
                var records = actor.World.RpcSnapshot().Skip(cursor).ToArray();
                return records.Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(hidden)) &&
                    records.Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) && record!.Family == 0x345 &&
                        record.RecordId == child && record.SourceId == parent && record.TargetId == child &&
                        record.ValueBits == 0 && record.AddRemoveType == 0 && record.DurationBits == 0);
            }, $"Sourced grant {parent}→{child}", HeadlessPlayer.DefaultTimeout);
        }

        async Task Stage(bool load, IEnumerable<ushort> ids)
        {
            var entries = ids.Order().ToArray();
            string operation = load ? "00 00 0a 00" : "0a 00 00 00";
            string entriesHex = string.Join(" ", entries.Select(id => $"00 {Le(rows[id].Slot)} " +
                $"{WireFormat.ToHex(BitConverter.GetBytes((uint)id))} {Le(rows[id].Level)}"));
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc($"80 ae 00 00 00 00 {operation} {Le((ushort)entries.Length)} {entriesHex}");
            var acks = entries.Select(id => BytePattern.Parse($"80 {(load ? "b2" : "b3")} {Le(id)}")).ToArray();
            await actor.WaitUntilAsync(() =>
            {
                var records = actor.World.RpcSnapshot().Skip(cursor).ToArray();
                return acks.All(ack => records.Any(rpc => ack.IsMatch(rpc.Bytes)));
            }, $"Legal staged batch: load={load}, {entries.Length} entries", HeadlessPlayer.DefaultTimeout);
        }

        for (int index = 0; index < Upgrades.Length; index++)
        {
            int transitionCursor = actor.World.RpcSnapshot().Count;
            int grantCursor = 0;
            if (index == Upgrades.Length - 1)
            {
                await Stage(load: false, first.Except(second));
                grantCursor = actor.World.RpcSnapshot().Count;
                await Stage(load: true, second.Except(first));
            }
            await Grant(index, grantCursor);
            var (_, child, mods) = Upgrades[index];
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc($"80 b9 {Le(child)} {Le(target.ViewId)}");
            foreach (var modifier in mods)
                await Bonus(child, modifier, cursor, remove: false);
            byte[] reuse = WireFormat.FromHex($"80 bd 01 {Le(child)} 04 00 00 f0 41 01");
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor)
                .Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(reuse)), $"Reuse for {child}", HeadlessPlayer.DefaultTimeout);
            if (index > 0)
            {
                var (_, previous, priorMods) = Upgrades[index - 1];
                foreach (var modifier in priorMods)
                    await Bonus(previous, modifier, transitionCursor, remove: true);
            }
        }
        Console.WriteLine("  Five granted upgrades targeted the caster across 94/100 and 89/100 ordinary loadouts");
        return Array.Empty<byte>();
    }
}
