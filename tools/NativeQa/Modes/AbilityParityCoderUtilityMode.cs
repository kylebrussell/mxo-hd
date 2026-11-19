using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityCoderUtilityMode : NativeQaMode
{
    // PR312 PacketLog 88a1d93e477a:3167, the complete ordinary self-ability request.
    private const string StockSelf =
        "80b95202000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly (ushort Stat, int Value)[] Technician = { (106, 100), (180, 20), (181, 20), (1009, 3) };
    private static readonly (ushort Stat, int Value)[] Master = { (106, 150), (180, 20), (181, 20), (1009, 6) };

    public override string Name => "ability-parity-coder-utility";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 517, 608, 609, 611, 612, 613, 620, 1008 },
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET y=495,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111)" },
        readyMessages: new[]
        {
            "In Actions, open Utility Master and confirm the Utility Master action appears after jack-in. Capture its 80 b2 hidden load and the 620-to-1108 type-45 grant add. 1108 is absent from owned Archive entries.",
            "Activate Utility Technician, then put the granted Utility Master action on the hotbar and activate it. Capture self-only +100/+150 maximum HP, both +20 resistances, +3/+6 melee defense, and Technician removal before Master adds. If the action is absent, record the Actions tree and stop the replacement check.",
            "Summon Logic Daemon 3.0 and Patch Daemon 2.0 outside combat. Expect levels 34 and 43 at caster level 50, eight-second casts and 10-second reuse. Record registration and deactivation.",
            "While an owned daemon is attached, toggle Daemon Boost 1.0 on and off. Compare the lone 10 IS activation debit, the immediate pulse and the first 5 IS upkeep about five seconds after the pulse."
        }, loadAbilityAncestors: true);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("The utility sweep requires a disposable server.");
        using var owner = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(owner.CharacterData[6]);
        uint ownedMaster = 0x80000000u | (1108u << 10);
        Check.That(archive.All(row => row.Descriptor != ownedMaster), "Utility Master action is absent from the owned archive");
        await owner.EnterWorldAsync(loadingComplete: false);
        byte[] load = hds.ServerPackets.BuildAbilityLoad(1108, 0, hds.AbilityProgression.HiddenSlot);
        byte[] grant = Convert.FromHexString("80bc45035404006c0200005404000000000000000000");
        bool IsGrant(RpcRecord rpc) => rpc.Bytes.AsSpan().SequenceEqual(grant);
        bool IsLoad(RpcRecord rpc) => rpc.Bytes.AsSpan().SequenceEqual(load);
        owner.SendRpc("81 48");
        await owner.WaitForRpcAsync("81 49");
        Check.That(!owner.World.RpcSnapshot().Any(rpc => IsGrant(rpc) || IsLoad(rpc)),
            "Self spawn alone must not register the hidden action");
        owner.SendRpc("0c 01");
        await owner.WaitForRpcAsync(IsLoad, "Hidden-slot Utility Master ability load");
        await owner.WaitForRpcAsync(IsGrant, "Source-qualified 620-to-1108 grant add after readiness");
        owner.SendRpc("0c 01");
        owner.SendRpc("81 48");
        await owner.WaitForRpcAsync("81 49");
        Check.Equal(1, owner.World.RpcSnapshot().Count(IsGrant), "Duplicate readiness does not add 1108 twice");
        Check.Equal(1, owner.World.RpcSnapshot().Count(IsLoad), "Duplicate readiness does not reload 1108");
        await RequireModifiers(owner, 517, new[] { ((ushort)655, 50) }, 0, 0x45, false);
        await RequireModifiers(owner, 608, new[] { ((ushort)1139, 5), ((ushort)1142, 5) }, 0, 0x45, false);
        await RequireModifiers(owner, 620, new[] { ((ushort)997, 5) }, 0, 0x45, false);

        SendSelf(owner, 613);
        await RequireModifiers(owner, 613, Technician, 0, 0x15, false);
        await owner.WaitForRpcAsync("80 bd 01 65 02 04 00 00 f0 41 01");
        int replace = owner.World.RpcSnapshot().Count;
        SendSelf(owner, 1108); // 1108 is never seeded as owned. The loaded 620 parent is its only authority.
        await RequireModifiers(owner, 613, Technician, replace, 0x15, true);
        await RequireModifiers(owner, 1108, Master, replace, 0x15, false);
        var transition = owner.World.RpcSnapshot().Skip(replace)
            .Select(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) ? record : null)
            .Where(record => record is { Family: 0x15, SourceId: 613 or 1108 }).ToArray();
        Check.That(transition.Length == 8 && transition.Take(4).All(record => record!.SourceId == 613 && record.AddRemoveType == 1),
            "All Technician removals precede Master application");
        await owner.WaitForRpcAsync("80 bd 01 54 04 04 00 00 f0 41 01");

        var pets = new List<object>();
        foreach (var (ability, level, name) in new (ushort, byte, string)[]
                 { (611, 34, "Nuker Simulacrum"), (612, 43, "Healer Simulacrum") })
        {
            int viewStart = owner.World.CreationSnapshot().Count;
            SendSelf(owner, ability);
            var cast = await owner.WaitForRpcAsync($"80 ac {Le(ability)}");
            Check.Equal(8f, BitConverter.ToSingle(cast.Bytes, cast.Bytes.Length - 4), "The summons use the shipped eight-second cast");
            var attached = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(16));
            byte id = attached.Bytes[3];
            ViewCreation? pet = null;
            await owner.WaitUntilAsync(() =>
            {
                pet = owner.World.CreationSnapshot().Skip(viewStart).FirstOrDefault(view =>
                    view.Goid == 40376 && view.Attributes["SimulacraID"][0] == id && view.Name == name &&
                    view.Attributes["Level"][0] == level && BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == owner.CharId);
                return pet != null;
            }, $"{name} matches the shipped level and current owner", HeadlessPlayer.DefaultTimeout);
            await owner.WaitForRpcAsync($"80 bd 01 {Le(ability)} 04 00 00 20 41 01");
            if (ability == 611) await VerifyUpkeepAsync(owner, pet!);
            owner.SendRpc("81 7a fe 01");
            var removal = await owner.WaitForRpcAsync($"81 77 {id:x2}");
            pets.Add(new { Ability = ability, Level = level, Name = name, Attach = attached.Hex, Removal = removal.Hex });
        }

        Check.Equal(0, owner.World.Problems.Count, "Utility casts and summon windows decode cleanly");
        NativeQaReadiness.RequireNoAmbientAggro(server, owner);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "coder-utility-bootstrap.json"),
            JsonSerializer.Serialize(pets, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static async Task VerifyUpkeepAsync(HeadlessPlayer owner, ViewCreation pet)
    {
        int start = owner.World.ViewSnapshot().Count;
        var timer = Stopwatch.StartNew();
        SendSelf(owner, 609);
        await owner.WaitForRpcAsync("80 bd 01 61 02 04 00 00 70 41 01");
        await owner.WaitForViewUpdateAsync(pet.ViewId, "04 80 80 80 c0 ?? ?? c0 44 08 00 28");
        Check.That(DebitsSince(owner, start).SequenceEqual(new[] { 10 }),
            "The initial Daemon Boost cast spends 10 IS with no immediate upkeep");
        await owner.WaitUntilAsync(() => DebitsSince(owner, start).Count == 2,
            "Daemon Boost spends its first periodic upkeep", TimeSpan.FromSeconds(9));
        Check.That(DebitsSince(owner, start).SequenceEqual(new[] { 10, 5 }), "The first update spends exactly 5 IS");
        Check.That(timer.Elapsed.TotalSeconds >= 6.5, "The first upkeep follows the two-second cast and five-second interval");
        SendSelf(owner, 609);
        await Task.Delay(TimeSpan.FromSeconds(6));
        Check.That(DebitsSince(owner, start).SequenceEqual(new[] { 10, 5 }), "Toggling off stops subsequent upkeep");
    }

    private static List<int> DebitsSince(HeadlessPlayer owner, int start)
    {
        var views = owner.World.ViewSnapshot();
        var strengths = views.Take(start).SelectMany(InnerStrength).TakeLast(1)
            .Concat(views.Skip(start).SelectMany(InnerStrength)).ToArray();
        return strengths.Zip(strengths.Skip(1), (oldValue, newValue) => oldValue - newValue).Where(delta => delta > 0).ToList();
    }

    private static IEnumerable<ushort> InnerStrength(byte[] packet)
    {
        byte[] header = { 2, 0, 2, 0x80, 0x80, 0x80, 0x10 };
        for (int pos = BytePattern.IndexOf(packet, header, 0);
             pos >= 0 && pos + header.Length + 2 <= packet.Length;
             pos = BytePattern.IndexOf(packet, header, pos + header.Length + 2))
            yield return BitConverter.ToUInt16(packet, pos + header.Length);
    }

    private static Task RequireModifiers(HeadlessPlayer owner, ushort ability, (ushort Stat, int Value)[] expected,
        int start, ushort family, bool remove) => owner.WaitUntilAsync(() => expected.All(mod =>
        owner.World.RpcSnapshot().Skip(start).Any(packet => ManageBonusRecord.TryDecode(packet.Bytes, out var record) &&
            record!.Family == family && record.SourceId == ability && record.TargetId == mod.Stat &&
            record.SignedValue == mod.Value && record.AddRemoveType == (remove ? 1 : 0))),
        $"Ability {ability} publishes every expected modifier (removed={remove})", HeadlessPlayer.DefaultTimeout);

    private static void SendSelf(HeadlessPlayer owner, ushort id)
    {
        byte[] request = Convert.FromHexString(StockSelf);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), id);
        owner.SendRpc(request);
    }

    private static string Le(ushort id) => WireFormat.ToHex(BitConverter.GetBytes(id));
}
