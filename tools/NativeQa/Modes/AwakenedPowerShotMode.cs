using System.Buffers.Binary;
using System.Text.Json;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AwakenedPowerShotMode : NativeQaMode
{
    public override string Name => "ability-parity-awakened-power-shot";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 14 }, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=100,innerStrM=2000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Start with a substantial Inner Strength deficit. Select Melee Dummy at 3m west and enter Self Defense interlock. The stock Power Shot icon is grey at rest. Then use Power Shot and retain the cast, queue, enemy hit, own IS boost line and 60s reuse.",
            "If sampling a miss after reuse, retain the resolved round and verify no IS boost. Capture named playback if available.",
            "The effects roll independently. Record actual damage and actual caster IS delta. This test samples the landed mechanics, not a complete native distribution."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Power Shot server.");
        await actor.EnterWorldAsync();
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        string target = WireFormat.ToHex(BitConverter.GetBytes(handle));
        actor.World.SendRpcs(Convert.FromHexString($"80b91100{target}0000000000000000000000000000000000000034097f00000000ff086b0000000000"),
            WireFormat.FromHex($"40 {target} 01 6b"));
        await actor.WaitForRpcAsync($"40 {target} 00 6b");
        await actor.WaitForRpcAsync("47");
        int start = actor.World.RpcSnapshot().Count;
        int viewStart = actor.World.ViewSnapshot().Count;
        // PR386 stock hotbar packet, PacketLog 1694–95, with the live target handle.
        byte[] request = Convert.FromHexString("80b90e000400020000000000000000000000000000000000000000000e000000006b0000086b0000000000");
        Check.Equal(43, request.Length, "Captured Power Shot request length");
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), handle);
        actor.SendRpc(request);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[COMBAT] PowerShotAbility is the next move"),
            "Power Shot queues for the active interlock", TimeSpan.FromSeconds(10));
        var boost = await actor.WaitForRpcAsync("81 67 ?? ?? ?? ?? af 01 00 c4 01 0e 00 00 00");
        int gain = BitConverter.ToInt32(boost.Bytes, boost.Bytes.Length - sizeof(int));
        Check.That(gain is >= 10 and <= 100, "Power Shot publishes the actual independent IS gain");
        await EvidenceRetestWire.TimerAsync(actor, start, 14, 4, 60);
        var vitals = new List<ushort>();
        await actor.WaitUntilAsync(() =>
        {
            vitals.Clear();
            byte[] prefix = WireFormat.FromHex("02 00 02 80 80 80 10");
            foreach (var frame in actor.World.ViewSnapshot().Skip(viewStart))
            {
                var section = frame.AsSpan();
                while (section.IndexOf(prefix) is int offset && offset >= 0 && offset + prefix.Length + 2 <= section.Length)
                {
                    int valueAt = offset + prefix.Length;
                    vitals.Add(BitConverter.ToUInt16(section[valueAt..]));
                    section = section[(valueAt + 2)..];
                }
            }
            return vitals.Zip(vitals.Skip(1), (before, after) => after - before).Contains(gain);
        }, "The published Inner Strength gain matches adjacent authoritative own-view IS values", TimeSpan.FromSeconds(10));
        var hp = await actor.WaitForViewUpdateAsync(dummy.ViewId, "04 80 80 80 c0 ?? ?? c0 26 07 00 28");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 28 07 00 28 ??");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains($"[ABILITY] 14 lands on view {dummy.ViewId} for "),
            "Power Shot lands against a live enemy through the usual path", TimeSpan.FromSeconds(10));
        Check.Equal(0, actor.World.Problems.Count, "Power Shot framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "power-shot.json"),
            JsonSerializer.Serialize(new { Request = Convert.ToHexString(request), Gain = gain, InnerStrength = vitals, Target = Convert.ToHexString(hp) }));
        return Array.Empty<byte>();
    }
}
