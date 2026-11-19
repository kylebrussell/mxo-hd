using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityHackerDisciplinesMode : NativeQaMode
{
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Mods)[] Disciplines =
    {
        (310, 1083, new (ushort, int)[] { (564, 7), (172, 4), (342, 6) }),
        (334, 1084, new (ushort, int)[] { (564, 7), (172, 10), (342, 9) }),
        (335, 1086, new (ushort, int)[] { (564, 7), (172, 2), (177, 10), (342, 6) }),
        (390, 1079, new (ushort, int)[] { (564, 7), (114, 10), (342, 6) })
    };

    public override string Name => "ability-parity-hacker-disciplines";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 310, 334, 335, 390 },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110" },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0" },
        readyMessages: new[]
        {
            "At a Hardline or Load Area, check each owned parent and its granted Actions entry: Arbalest → 1083, Ballista → 1084, Artillerist → 1086, Skript Kiddie → 1079. Children should be absent from owned Archive. Keep each source-qualified grant add, remove and reload.",
            "With Discipline Target selected, activate the granted Arbalest, Ballista and Artillerist upgrades in turn. Capture each outgoing request, the caster-only bonus records, the replacement removals and the selected target view. Skript Kiddie Upgrade is an additional sample."
        },
        extraQaMetadata: new()
        {
            ["NativeCloseableRows"] = 7,
            ["NativeCloseableIds"] = "310,334,335,390,1083,1084,1086",
            ["LoadedMemory"] = 97
        },
        testMobFixture: "ability-parity-hacker-disciplines.csv",
        loadAbilityAncestors: true);

    private static bool IsGrant(byte[] rpc, ushort parent, ushort child, bool remove = false) =>
        ManageBonusRecord.TryDecode(rpc, out var record) && record!.Family == 0x345 &&
        record.RecordId == child && record.SourceId == parent && record.TargetId == child &&
        record.ValueBits == 0 && record.AddRemoveType == (remove ? 1 : 0) && record.Duration == 0;

    internal async Task VerifyHeadlessAsync(HeadlessPlayer actor)
    {
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]);
        int memory = archive.Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .Select(row => AbilityCatalog.IdOf(row.Descriptor)).Distinct().Sum(AbilityMemory.Cost);
        Check.Equal(97, memory, "The seeded complete profile fits the standard 100-memory limit");
        foreach (var (_, child, _) in Disciplines)
            Check.That(archive.All(row => row.Descriptor != (0x80000000u | ((uint)child << 10))),
                $"Grant {child} is absent from owned Archive");

        await actor.EnterWorldAsync(loadingComplete: false);
        var target = await actor.WaitForViewNamedAsync("Discipline Target");
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");
        foreach (var (parent, child, _) in Disciplines)
            Check.That(!actor.World.RpcSnapshot().Any(rpc => IsGrant(rpc.Bytes, parent, child)),
                $"Grant {parent}→{child} waits for avatar readiness");

        actor.SendRpc("0c 01");
        foreach (var (parent, child, _) in Disciplines)
        {
            byte[] load = ServerPackets.BuildAbilityLoad(child, 0, AbilityProgression.HiddenSlot);
            await actor.WaitUntilAsync(() =>
            {
                var records = actor.World.RpcSnapshot();
                return records.Any(rpc => IsGrant(rpc.Bytes, parent, child)) &&
                    records.Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(load));
            }, $"grant {parent}→{child} and hidden load", HeadlessPlayer.DefaultTimeout);
        }

        static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));
        async Task Modifier(ushort source, ushort stat, int value, int cursor = 0, bool remove = false)
        {
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(rpc =>
                ManageBonusRecord.TryDecode(rpc.Bytes, out var m) && m!.SourceId == source && m.TargetId == stat &&
                m.SignedValue == value && m.Duration == 0 && m.AddRemoveType == (remove ? 1 : 0)),
                $"discipline bonus {source}/{stat} {value}", HeadlessPlayer.DefaultTimeout);
        }

        await Modifier(310, 1031, 1);
        await Modifier(334, 1031, 5);
        await Modifier(390, 1027, 2);
        await Modifier(390, 1143, 2);

        foreach (var (parent, child, _) in Disciplines)
        {
            var row = archive.Single(item => item.Descriptor == (0x80000000u | ((uint)parent << 10)));
            foreach (bool loaded in new[] { false, true })
            {
                int cursor = actor.World.RpcSnapshot().Count;
                string operation = loaded ? "00 00 0a 00" : "0a 00 00 00";
                string id = WireFormat.ToHex(BitConverter.GetBytes((uint)parent));
                actor.SendRpc($"80 ae 00 00 00 00 {operation} 01 00 00 {Le(row.Slot)} {id} {Le(row.Level)}");
                await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor)
                    .Any(rpc => IsGrant(rpc.Bytes, parent, child, remove: !loaded)),
                    $"grant {parent}→{child} after changing the owned loadout (loaded={loaded})",
                    HeadlessPlayer.DefaultTimeout);
            }
        }

        for (int index = 0; index < Disciplines.Length; index++)
        {
            var (_, child, mods) = Disciplines[index];
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc($"80 b9 {Le(child)} {Le(target.ViewId)}");
            if (index > 0)
            {
                var (_, previous, priorMods) = Disciplines[index - 1];
                foreach (var (stat, value) in priorMods)
                    await Modifier(previous, stat, value, cursor, remove: true);
            }
            foreach (var (stat, value) in mods)
                await Modifier(child, stat, value, cursor);
            byte[] reuse = WireFormat.FromHex($"80 bd 01 {Le(child)} 04 00 00 f0 41 01");
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor)
                .Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(reuse)),
                $"30-second reuse on granted action {child}", HeadlessPlayer.DefaultTimeout);
        }
        Console.WriteLine("  Four source-qualified readiness grants and granted casts with replacement verified");
    }
}
