using System.Buffers.Binary;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task ModifierDatagramCap(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("ModifierBatch") {
            X = 17000, Z = 2300, Level = 50,
            OwnedLoadedAbilities = new[] { ((ushort)2, (ushort)50), ((ushort)17, (ushort)50) },
            Items = new[] { (5, 47107u, 1), (6, 47111u, 1), (7, 47112u, 1), (8, 47113u, 1) }
        });
        await ModifierBatchProof.RunAsync(actor);
    }
}

public static class ModifierBatchProof
{
    // These four effects coexist for five minutes. Their death cleanup emits twelve records.
    private static readonly (int Slot, uint Source, int Count)[] Buffs =
        { (5, 1274, 5), (6, 1275, 1), (7, 1276, 1), (8, 1277, 5) };

    public static async Task RunAsync(HeadlessPlayer actor)
    {
        await actor.EnterWorldAsync();
        await Barrier();
        var entry = Modifiers(0);
        Check.That(entry.Length >= 12, $"Cold entry publishes at least twelve modifiers, got {entry.Length}");
        CheckDatagrams(entry, "cold entry");

        int applyStart = actor.World.RpcSnapshot().Count;
        foreach (var (slot, source, count) in Buffs)
        {
            actor.SendRpc($"80 c6 {slot:x2} 00");
            await actor.WaitUntilAsync(() => Modifiers(applyStart).Count(r => Source(r) == source && r.Bytes[17] == 0) >= count,
                $"{count} adds for item ability {source}", HeadlessPlayer.DefaultTimeout);
        }
        var added = Modifiers(applyStart).Where(record => Buffs.Any(buff => Source(record) == buff.Source) && record.Bytes[17] == 0)
            .Select(record => AsRemoval(record.Bytes)).Order().ToArray();
        Check.Equal(12, added.Length, "Four distinct item effects add twelve modifiers");
        CheckDatagrams(Modifiers(applyStart), "item apply");

        int deathStart = actor.World.RpcSnapshot().Count;
        actor.SendChat("?die");
        await actor.WaitUntilAsync(() => Removals().Length >= added.Length, "all twelve death removals", HeadlessPlayer.DefaultTimeout);
        await Barrier();
        Check.That(added.SequenceEqual(Removals()), "Death removes each item modifier with the same record ID and value");
        CheckDatagrams(Modifiers(deathStart), "death cleanup");
        Check.Equal(0, actor.World.Problems.Count, "Entry, death and subsequent RPCs are in order");

        RpcRecord[] Modifiers(int start) => actor.World.RpcSnapshot().Skip(start)
            .Where(r => r.Opcode == 0xbc).ToArray();

        string[] Removals() => Modifiers(deathStart)
            .Where(record => Buffs.Any(buff => Source(record) == buff.Source) && record.Bytes[17] == 1)
            .Select(record => record.Hex).Order().ToArray();

        async Task Barrier()
        {
            actor.SkipReceived();
            actor.SendRpc("81 48");
            await actor.WaitForRpcAsync("81 49");
        }
    }

    private static uint Source(RpcRecord record) => BinaryPrimitives.ReadUInt32LittleEndian(record.Bytes.AsSpan(7));

    private static string AsRemoval(byte[] add)
    {
        byte[] removal = add.ToArray();
        removal[17] = 1;
        return WireFormat.ToHex(removal);
    }

    private static void CheckDatagrams(IEnumerable<RpcRecord> records, string phase)
    {
        foreach (var datagram in records.GroupBy(record => record.DatagramSequence))
            Check.That(datagram.Key.HasValue && datagram.Count() <= 6,
                $"{phase} datagram {datagram.Key} has {datagram.Count()} modifiers. Expected at most six.");
    }
}
