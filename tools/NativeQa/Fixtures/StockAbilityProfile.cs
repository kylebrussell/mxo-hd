using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa;

internal static class StockAbilityProfile
{
    internal static HashSet<ushort> Profile(params ushort[] roots)
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
                if (next != 0 && ids.Add(next)) pending.Enqueue(next);
        }
        return ids;
    }

    internal static string LittleEndianHex(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));

    internal static async Task<int> ChangeLoadoutAsync(HeadlessPlayer actor, IReadOnlyDictionary<ushort, AbilityMemoryUiFixture.Tuple> rows,
        ushort[] ids, bool load)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        string operation = load ? "00 00 0a 00" : "0a 00 00 00";
        string entries = string.Join(" ", ids.Select(id => $"00 {LittleEndianHex(rows[id].Slot)} {WireFormat.ToHex(BitConverter.GetBytes((uint)id))} {LittleEndianHex(rows[id].Level)}"));
        actor.SendRpc($"80 ae 00 00 00 00 {operation} {LittleEndianHex((ushort)ids.Length)} {entries}");
        foreach (ushort id in ids)
        {
            var ack = BytePattern.Parse($"80 {(load ? "b2" : "b3")} {LittleEndianHex(id)}");
            await ObserveAsync(actor, cursor, packet => ack.IsMatch(packet.Bytes), $"{id} loaded={load}");
        }
        return cursor;
    }

    internal static async Task CheckPassivesAsync(HeadlessPlayer actor, List<object> proof,
        (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] passives, int cursor, bool remove)
    {
        foreach (var (parent, child, stats) in passives)
        {
            foreach (var (stat, value) in stats)
            {
                var modifier = await ObserveAsync(actor, cursor, packet => Bonus(packet, parent, stat, value, 0, remove),
                    $"passive {parent}/{stat} remove={remove}");
                proof.Add(new { Ability = parent, Stat = stat, Remove = remove, Record = modifier.Hex });
            }
            if (child == 0) continue;
            var grant = await ObserveAsync(actor, cursor, GrantRecord(child, remove), $"grant {parent}/{child} remove={remove}");
            proof.Add(new { Parent = parent, Child = child, Remove = remove, Grant = grant.Hex });
            var registration = await ObserveAsync(actor, cursor, packet =>
                ManageBonusRecord.TryDecode(packet.Bytes, out var record) && record!.Type == 0x45 &&
                record.Locator == ((uint)child << 8 | 3) && record.SourceId == parent && record.TargetId == child &&
                record.ValueBits == 0 && record.DurationBits == 0 && record.AddRemoveType == (remove ? 1 : 0),
                $"registration {parent}/{child} remove={remove}");
            proof.Add(new { Parent = parent, Child = child, Remove = remove, Registration = registration.Hex });
        }
    }

    internal static Func<RpcRecord, bool> GrantRecord(ushort child, bool remove)
    {
        byte[] record = remove ? ServerPackets.BuildAbilityUnload(child)
            : ServerPackets.BuildAbilityLoad(child, 0, AbilityProgression.HiddenSlot);
        return packet => packet.Bytes.AsSpan().SequenceEqual(record);
    }

    internal static bool Bonus(RpcRecord packet, ushort source, ushort stat, int value, float duration, bool remove) =>
        ManageBonusRecord.TryDecode(packet.Bytes, out var record) && record!.SourceId == source && record.TargetId == stat &&
        record.SignedValue == value && record.Duration == duration && record.AddRemoveType == (remove ? 1 : 0);

    internal static async Task<RpcRecord> ObserveAsync(HeadlessPlayer actor, int cursor, Func<RpcRecord, bool> predicate, string description)
    {
        RpcRecord? result = null;
        await actor.WaitUntilAsync(() => (result = actor.World.RpcSnapshot().Skip(cursor).FirstOrDefault(predicate)) != null,
            description, HeadlessPlayer.DefaultTimeout);
        return result!;
    }
}
