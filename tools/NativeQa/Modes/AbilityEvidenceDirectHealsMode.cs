using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityEvidenceDirectHealsMode : NativeQaMode
{
    private const ushort StartingHealth = 100;
    private const string HealthReportPrefix = "Current health: ";

    private sealed record Sample(ushort Id, int Expected, float Cast, float Reuse, ushort Family = 0);
    private static readonly Sample[] Samples =
    {
        new(80, 15, 2, 4, 685), new(413, 84, 2, 4, 685),
        new(234, 24, 2, 4, 686), new(235, 57, 2, 4, 686), new(414, 91, 2, 4, 686),
        new(513, 15, 4, 6)
    };

    public override string Name => "ability-evidence-direct-heals";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17 }.Concat(Samples.Select(sample => sample.Id)).ToArray(), 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET healthC={StartingHealth},healthM=5000,innerStrC=2000,innerStrM=2000,y=495 WHERE charId IN (110,6111,6112)",
            "UPDATE characters SET x=CASE WHEN charId=6111 THEN 24600 ELSE 24800 END,z=-1498 WHERE charId IN (6111,6112)",
            "DELETE FROM inventory WHERE charId=110"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        },
        readyMessages: new[]
        {
            $"QAFriend starts at {StartingHealth} HP, 2 m away. This disposable fixture holds natural HP regen so the wound survives setup. Keep Self Defense active and leave the seeded +20% Heal Damage loadout intact.",
            "Select QAFriend and cast Restore Health 1.0 (80, +15), Restore Health 3.0 (413, +84), Repair RSI ranks 2–4 (234 +24, 235 +57, 414 +91) and Quick Coding (513, +15) from Actions. Wait for the shared timer to clear between casts.",
            "Before each cast, request live HP with {\"actor\":\"QAFriend\",\"action\":\"direct-heal-health\"}. The console and response JSON show the current actual health.",
            "For each cast retain the stock request, cast bar, reuse records, and the Healed before/after log line. Expected HP in order is 100 → 115 → 199 → 223 → 280 → 371 → 386. See docs/ABILITY-EVIDENCE-RETESTS.md."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var ally = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", ally);
        await ally.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        var server = context.ActualServer ?? throw new InvalidOperationException("This replay requires the isolated QA server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "QAFar", "QAFar");
        await caster.EnterWorldAsync();
        var target = await caster.WaitForViewNamedAsync(ally.Handle);
        ushort expectedHealth = StartingHealth;
        var proof = new List<object>();
        foreach (var sample in Samples)
        {
            ushort liveHealth = await ReadHealthAsync(ally);
            Check.Equal(expectedHealth, liveHealth, "QAFriend stays wounded between casts");
            int logAt = server.DebugLog().Length;
            int allyViewAt = ally.World.ViewSnapshot().Count;
            int timerAt = caster.World.RpcSnapshot().Count;
            byte[] request = EvidenceRetestWire.Cast(caster, sample.Id, target);
            await EvidenceRetestWire.CastBarAsync(caster, sample.Id, sample.Cast);
            await EvidenceRetestWire.TimerAsync(caster, timerAt, sample.Id, 4, sample.Reuse);
            if (sample.Family != 0) await EvidenceRetestWire.TimerAsync(caster, timerAt, sample.Family, 0, sample.Reuse);
            string line = $"[ABILITY] Heal {sample.Id} -> {ally.Handle} for {sample.Expected}";
            Match delta = Match.Empty;
            await caster.WaitUntilAsync(() =>
            {
                delta = Regex.Match(server.DebugLog()[logAt..],
                    @"\[ABILITY\] Healed (\d+) -> (\d+)\r?\n[^\r\n]*" + Regex.Escape(line) + @"(?:\r?\n|$)");
                return delta.Success;
            }, line, TimeSpan.FromSeconds(12));
            ushort before = ushort.Parse(delta.Groups[1].Value);
            ushort after = ushort.Parse(delta.Groups[2].Value);
            Check.Equal(liveHealth, before, "Reported live HP matches the value before the heal");
            Check.Equal(sample.Expected, after - before, "Exact recipient heal delta before natural regeneration");
            await EvidenceRetestWire.ViewSinceAsync(ally, allyViewAt, 2, "02 80 80 80 40 " + EvidenceRetestWire.Le(after));
            if (sample.Id is 80 or 234)
            {
                ushort sibling = sample.Id == 80 ? (ushort)413 : (ushort)235;
                EvidenceRetestWire.Cast(caster, sibling, target);
                byte[] refusal = hds.AbilityCooldowns.BuildDenied(sibling, hds.AbilityCooldowns.DeniedTimerRunning);
                await caster.WaitForRpcAsync(WireFormat.ToHex(refusal));
                Check.Equal(after, await ReadHealthAsync(ally), "The shared timer refuses the sibling heal without changing HP");
            }
            if (sample.Family != 0) await EvidenceRetestWire.TimerAsync(caster, timerAt, sample.Family, 0, sample.Reuse, ended: true);
            expectedHealth = after;
            proof.Add(new { sample.Id, Before = before, After = after, sample.Expected, RpcHex = Convert.ToHexString(request) });
        }
        Check.Equal(0, ally.World.Problems.Count + caster.World.Problems.Count, "Direct-heal packet framing");
        int resetAt = ally.World.ViewSnapshot().Count;
        ally.SendChat($"?health {StartingHealth}");
        await EvidenceRetestWire.ViewSinceAsync(ally, resetAt, 2, "02 80 80 80 40 " + EvidenceRetestWire.Le(StartingHealth));
        Check.Equal(StartingHealth, await ReadHealthAsync(ally), "Native QAFriend starts from the verified wound");
        NativeQaReadiness.RequireNoAmbientAggro(server, ally);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "ability-evidence-direct-heals.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    internal static async Task<ushort> ReadHealthAsync(HeadlessPlayer ally)
    {
        ally.SkipReceived();
        ally.SendChat("?health");
        var report = await ally.WaitForTextAsync(HealthReportPrefix);
        Match health = Regex.Match(Encoding.Latin1.GetString(report.Bytes), HealthReportPrefix + @"([0-9]+)");
        return ushort.Parse(health.Groups[1].Value);
    }
}

internal static class EvidenceRetestWire
{
    internal static string Le(ushort number) => WireFormat.ToHex(BitConverter.GetBytes(number));

    internal static byte[] Cast(HeadlessPlayer caster, ushort ability, ViewCreation? target = null)
    {
        byte[] request = CastRequest(ability, target);
        caster.SendRpc(request);
        return request;
    }

    internal static byte[] CastRequest(ushort ability, ViewCreation? target = null)
    {
        // Ordinary full-length targeted 80 b9 format from the stock PR316 health cast.
        byte[] request = Convert.FromHexString("80b9a0010d000b000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), target == null ? 0u : target.ViewId | ((uint)target.SpawnId << 16));
        return request;
    }

    internal static async Task CastBarAsync(HeadlessPlayer caster, ushort ability, float seconds)
    {
        var bar = await caster.WaitForRpcAsync("80 ac " + Le(ability));
        Check.Equal(seconds, BitConverter.ToSingle(bar.Bytes, bar.Bytes.Length - 4), $"Cast bar for {ability}");
    }

    internal static Task TimerAsync(HeadlessPlayer caster, int cursor, ushort ability, byte type, float seconds, bool ended = false)
    {
        string payload = $"80 bd 01 {Le(ability)} {type:x2} {WireFormat.ToHex(BitConverter.GetBytes(seconds))} {(ended ? "00" : "01")}";
        var pattern = BytePattern.Parse(payload);
        return caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(cursor).Any(rpc => pattern.IsMatch(rpc.Bytes)),
            $"fresh timer {payload}", HeadlessPlayer.DefaultTimeout);
    }

    internal static Task ViewSinceAsync(HeadlessPlayer player, int cursor, ushort viewId, string payload)
    {
        var pattern = BytePattern.Parse(Le(viewId) + " " + payload);
        return player.WaitUntilAsync(() => player.World.ViewSnapshot().Skip(cursor).Any(pattern.Occurs),
            $"fresh view {viewId} update {payload}", HeadlessPlayer.DefaultTimeout);
    }
}
