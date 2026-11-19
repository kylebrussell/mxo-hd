using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityHackerPassiveRetestMode : NativeQaMode
{
    private sealed record Sample(ushort Ability, ushort Grant, (ushort Stat, int Value)[] Modifiers);
    private static readonly Sample[] Samples =
    {
        new(310, 1083, new (ushort, int)[] { (1031, 1) }),
        new(334, 1084, new (ushort, int)[] { (1031, 5) }),
        new(335, 1086, Array.Empty<(ushort, int)>()),
        new(390, 1079, new (ushort, int)[] { (1143, 2), (1027, 2) }),
        new(398, 0, new (ushort, int)[] { (1143, 5), (1027, 5), (1042, 15) })
    };
    private static readonly ushort[] Loadout = new ushort[] { 2, 17 }
        .Concat(Samples.Select(sample => sample.Ability))
        .Concat(Samples.SelectMany(sample => AbilityPrerequisites.Current.Closure(sample.Ability).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-hacker-passive-retest";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), DevCommands: true,
        readyMessages: new[]
        {
            "Capture initial Actions and Status on the 99/100 memory loadout. Retest only parents and Repel Combat. No hotbar casts are required.",
            "Use ?qaunload 310 then ?qaload 310. Expect Arbalest Upgrade 1083 off/on and raw viral damage +1 off/on. Repeat for 334: Ballista Upgrade 1084 and raw viral damage +5.",
            "Repeat for 335: Artillerist Upgrade 1086 off/on. Repeat for 390: Skript Kiddie Upgrade 1079 and raw melee/ballistic defense +2 each.",
            "Repeat for 398: Repel Combat raw melee/ballistic defense +5 each and Powerless Duration +15. Keep the raw modifier records when Status combines or omits an input.",
            "Finish each parent reload before starting the next. Keep both normal loadout acknowledgements and exact source-specific grants or modifier records. See docs/ABILITY-HACKER-PASSIVE-RETEST.md."
        },
        extraQaMetadata: new()
        {
            ["LoadedMemory"] = 99,
            ["NativeCloseableRows"] = 5,
            ["NativeCloseableIds"] = "310,334,335,390,398"
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var actor = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester",
            stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]);
        var active = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]);
        Check.That(active.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout),
            "Margin initially loads only the legal five-parent profile");
        Check.Equal(99, Loadout.Sum(AbilityMemory.Cost), "The complete loaded profile fits under 100 memory");
        foreach (var sample in Samples.Where(sample => sample.Grant != 0))
            Check.That(archive.All(row => AbilityCatalog.IdOf(row.Descriptor) != sample.Grant),
                $"Grant {sample.Grant} is absent from the owned Archive");

        await actor.EnterWorldAsync();
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");
        foreach (var sample in Samples)
        {
            if (sample.Grant != 0)
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

        Console.WriteLine("  Five parent/passive unload and reload cycles preserved normal acknowledgements and all sourced transitions");
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
