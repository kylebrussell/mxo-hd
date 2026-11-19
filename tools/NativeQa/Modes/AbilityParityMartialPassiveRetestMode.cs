using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityMartialPassiveRetestMode : NativeQaMode
{
    private sealed record Sample(ushort Ability, ushort Grant, (ushort Stat, int Value)[] Modifiers);
    private static readonly Sample[] Samples =
    {
        new(136, 0, new (ushort, int)[] { (997, 5), (1142, 5) }),
        new(137, 1062, new (ushort, int)[] { (1046, 2) }),
        new(257, 1063, new (ushort, int)[] { (1046, 1), (1142, 1) }),
        new(258, 0, new (ushort, int)[] { (1057, 5), (997, 7), (1142, 3) }),
        new(264, 0, new (ushort, int)[] { (1057, 5), (997, 7), (1142, 3) }),
        new(269, 0, new (ushort, int)[] { (997, 5), (1057, 15), (1142, 3) }),
        new(431, 0, new (ushort, int)[] { (1142, 1), (1143, 10), (995, 10), (1030, 6) })
    };
    private static readonly ushort[] Loadout = new ushort[] { 2, 17 }
        .Concat(Samples.Select(sample => sample.Ability))
        .Concat(Samples.SelectMany(sample => AbilityPrerequisites.Current.Closure(sample.Ability).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-martial-passive-retest";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110" },
        DevCommands: true, testMobFixture: "ability-parity-martial-passive-retest.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0" },
        readyMessages: new[]
        {
            "Capture initial Actions and Status on the 59/100 memory loadout. Use ?qaunload and ?qaload for 136, 137, 257, 258, 264, 269 and 431 in order. Finish each reload before starting the next.",
            "Capture every raw off/on modifier, plus the disappearance and return of Martial Arts Initiate Upgrade (1062) under 137 and Kung Fu Master Upgrade (1063) under 257.",
            "After the parent cycles, select Martial Passive Target and activate the two granted hotbar abilities 1062 then 1063. Capture the caster-only upgrade modifiers and the first upgrade's removal. See docs/ABILITY-MARTIAL-PASSIVE-RETEST.md."
        },
        extraQaMetadata: new()
        {
            ["LoadedMemory"] = 59,
            ["NativeCloseableRows"] = 7,
            ["NativeCloseableIds"] = "136,137,257,258,264,269,431"
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var actor = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester",
            stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]);
        var active = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]);
        Check.That(active.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout),
            "Margin initially loads only the legal seven-sample profile");
        Check.Equal(59, Loadout.Sum(AbilityMemory.Cost), "The complete loaded profile fits under 100 memory");
        foreach (var sample in Samples.Where(sample => sample.Grant != 0))
            Check.That(archive.All(row => AbilityCatalog.IdOf(row.Descriptor) != sample.Grant),
                $"Grant {sample.Grant} is absent from the owned Archive");

        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync("Martial Passive Target");
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");
        foreach (var sample in Samples)
        {
            await RequireGrantAndModifiers(actor, sample, cursor: 0, unload: false);

            foreach (bool unload in new[] { true, false })
            {
                int cursor = actor.World.RpcSnapshot().Count;
                string ability = WireFormat.ToHex(BitConverter.GetBytes(sample.Ability));
                var ack = BytePattern.Parse($"80 {(unload ? "b3" : "b2")} {ability}");
                actor.SendChat($"?qa{(unload ? "unload" : "load")} {sample.Ability}");
                await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(rpc => ack.IsMatch(rpc.Bytes)),
                    $"ordinary loadout acknowledgement for {sample.Ability} (unload={unload})", HeadlessPlayer.DefaultTimeout);
                await RequireGrantAndModifiers(actor, sample, cursor, unload);
            }
        }

        var initiate = new (ushort Stat, int Value)[] { (106, 70), (522, 4), (159, 2), (1009, 4) };
        var master = new (ushort Stat, int Value)[] { (106, 90), (522, 6), (159, 2), (1009, 4) };
        async Task Upgrade(ushort id, (ushort Stat, int Value)[] effects, int cursor, bool remove = false)
        {
            await actor.WaitUntilAsync(() => effects.All(change => actor.World.RpcSnapshot().Skip(cursor).Any(rpc =>
                ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.Family == 0x15 &&
                bonus.SourceId == id && bonus.TargetId == change.Stat && bonus.SignedValue == change.Value &&
                bonus.DurationBits == 0 && bonus.AddRemoveType == (remove ? 1 : 0))),
                $"granted caster upgrade {id}, remove={remove}", HeadlessPlayer.DefaultTimeout);
        }

        string selected = WireFormat.ToHex(BitConverter.GetBytes(target.ViewId));
        int activation = actor.World.RpcSnapshot().Count;
        actor.SendRpc($"80 b9 26 04 {selected}");
        await Upgrade(1062, initiate, activation);
        activation = actor.World.RpcSnapshot().Count;
        actor.SendRpc($"80 b9 27 04 {selected}");
        await Upgrade(1062, initiate, activation, remove: true);
        await Upgrade(1063, master, activation);
        Console.WriteLine("  Seven passive unload and reload cycles and both live granted upgrades verified");
        return Array.Empty<byte>();
    }

    private static Task RequireGrantAndModifiers(HeadlessPlayer actor, Sample sample, int cursor, bool unload)
    {
        int operation = unload ? 1 : 0;
        byte[]? action = sample.Grant == 0 ? null : unload
            ? ServerPackets.BuildAbilityUnload(sample.Grant)
            : ServerPackets.BuildAbilityLoad(sample.Grant, 0, AbilityProgression.HiddenSlot);
        return actor.WaitUntilAsync(() =>
        {
            var packets = actor.World.RpcSnapshot().Skip(cursor).ToArray();
            var mods = packets.Select(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) ? bonus : null)
                .Where(bonus => bonus != null).ToArray();
            bool grant = action == null || packets.Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(action)) &&
                mods.Any(bonus => bonus!.Family == 0x345 && bonus.RecordId == sample.Grant &&
                    bonus.SourceId == sample.Ability && bonus.TargetId == sample.Grant &&
                    bonus.ValueBits == 0 && bonus.AddRemoveType == operation && bonus.DurationBits == 0);
            return grant && sample.Modifiers.All(expected => mods.Any(bonus =>
                bonus!.Family == 0x45 && bonus.SourceId == sample.Ability && bonus.TargetId == expected.Stat &&
                bonus.SignedValue == expected.Value && bonus.AddRemoveType == operation && bonus.DurationBits == 0));
        }, $"sourced grant and passive changes for {sample.Ability} (unload={unload})", HeadlessPlayer.DefaultTimeout);
    }
}
