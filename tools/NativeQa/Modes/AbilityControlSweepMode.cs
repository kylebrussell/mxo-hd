using System.Buffers.Binary;
using System.Text.Json;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AbilityControlSweepMode : NativeQaMode
{
    private sealed record Sample(ushort Ability, string Label, string Npc, ushort State, float Seconds, int Immunity, float DebuffSeconds = 0);

    // Freeze System and Code Stop use the same Mez state. Both ability families are visible as Pacify.
    private static readonly Sample[] Samples =
    {
        new(40, "Code Freeze 1.0", "WinterFrost", 99, 25, 0),
        new(1081, "Ravager Downgrade", "Trainman", 91, 8, 0, DebuffSeconds: 30),
        new(43, "Disrupt Inputs 1.0", "Smithling", 90, 16, 0),
        new(333, "Stun 2.0", "Trainman", 102, 5, 10),
        new(393, "Code Stop 1.0", "Smithling", 85, 6, 12),
        new(48, "Freeze System 1.0", "Blackwoods", 85, 6, 12),
        new(355, "Vector", "WinterFrost", 96, 8.94f, 8, DebuffSeconds: 30),
    };

    public override string Name => "ability-controls-sweep";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 1006 }.Concat(Samples.Select(sample => sample.Ability)).ToArray(), 24800, -1498,
        Array.Empty<string>(),
        new[] { "UPDATE characters SET healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110" },
        new[] { "UPDATE characters SET x=24800,y=495,z=-1498,healthC=5000,healthM=5000 WHERE charId=6111" },
        DevCommands: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32767", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        readyMessages: Samples.Select(sample =>
                FormattableString.Invariant($"{sample.Label} ({sample.Ability}) on {sample.Npc}. Expect state {sample.State} for {sample.Seconds:g} s, immunity {sample.Immunity} s."))
            .Append("Use Actions in order. Confirm the cast and target FX, and the four immunity announcements. Wait out each immunity window before the next cast that grants immunity. The fixture fixes viral contest draws.")
            .Append("Four normal-size targets are spaced at least 3 m apart. WinterFrost and Trainman are west of the start, Blackwoods and Smithling are east.")
            .ToArray(), testMobFixture: "ability-control-targets.csv");

    private static byte[] Request(ushort ability, ViewCreation target)
    {
        // proxy_020, lines 140965–140967: outbound Stun 2.0. The stock sender writes the compact
        // ability at 621e5125 and the live direct-object handle at 621e521e. Retain the captured
        // late-ground bytes and inventory selector instead of assuming those bytes are zero.
        byte[] packet = Convert.FromHexString("80B94D011D00DB0000000000000000000000000000000000000000004D010000006B0000086B0000000000");
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ability);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), (uint)target.ViewId | ((uint)target.SpawnId << 16));
        return packet;
    }

    private static bool ImmunityMessage(byte[] packet, Sample sample)
    {
        if (packet.Length < 23 || packet[0] != 0x81 || packet[1] != 0x67 ||
            BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(6)) != hds.AbilityMessages.ImmunityCaster ||
            BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(11)) != sample.Ability)
            return false;
        ushort valuesAt = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(19));
        return packet.Length == valuesAt + 6 &&
            BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(valuesAt)) == 1 &&
            BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(valuesAt + 2)) == sample.Immunity;
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Control sweep requires the disposable QA server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await caster.EnterWorldAsync();
        var targets = new Dictionary<string, ViewCreation>();
        foreach (string name in Samples.Select(sample => sample.Npc).Distinct())
            targets.Add(name, await caster.WaitForViewNamedAsync(name));
        foreach (var first in targets.Values)
        foreach (var second in targets.Values.Where(view => view.ViewId > first.ViewId))
        {
            var a = EngageFoesFixture.Position(first);
            var b = EngageFoesFixture.Position(second);
            Check.That(a.Zip(b, (x, y) => (x - y) * (x - y)).Sum() >= 300 * 300,
                $"Control targets {first.Name} and {second.Name} have separate pick positions");
        }
        var proof = new List<object>();
        DateTime latestExpiry = DateTime.UtcNow;
        DateTime immunityExpiry = DateTime.UtcNow;
        foreach (Sample sample in Samples)
        {
            TimeSpan waitForImmunity = immunityExpiry - DateTime.UtcNow;
            if (sample.Immunity > 0 && waitForImmunity > TimeSpan.Zero)
                await Task.Delay(waitForImmunity);
            var target = targets[sample.Npc];
            int cursor = caster.World.RpcSnapshot().Count;
            byte[] request = Request(sample.Ability, target);
            caster.SendRpc(request);
            string state = FormattableString.Invariant($"State {sample.State} 0 {sample.Seconds:g} s");
            string npcEffects = $"[EFFECTS] {sample.Ability} on {sample.Npc}:";
            await caster.WaitUntilAsync(() => server.DebugLog().Split('\n')
                .Any(line => line.Contains(npcEffects, StringComparison.Ordinal) && line.Contains(state, StringComparison.Ordinal)),
                $"{sample.Label} applied its expected NPC control", TimeSpan.FromSeconds(12));
            if (sample.Immunity > 0)
                await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(cursor).Any(record => ImmunityMessage(record.Bytes, sample)),
                    $"{sample.Label} sent its expected immunity announcement", TimeSpan.FromSeconds(6));
            DateTime observed = DateTime.UtcNow;
            if (sample.Immunity > 0) immunityExpiry = observed.AddSeconds(sample.Immunity + 0.5);
            DateTime expiry = observed.AddSeconds(Math.Max(sample.DebuffSeconds, Math.Max(sample.Seconds, sample.Immunity)));
            if (expiry > latestExpiry) latestExpiry = expiry;
            proof.Add(new { sample.Ability, sample.Npc, sample.State, sample.Seconds, sample.Immunity, Request = Convert.ToHexString(request) });
        }
        Check.Equal(0, caster.World.Problems.Count, "Control sweep wire framing");
        TimeSpan remaining = latestExpiry.AddSeconds(1) - DateTime.UtcNow;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "ability-controls-bootstrap.json"),
            JsonSerializer.Serialize(new { Passed = true, Cases = proof }, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }
}
