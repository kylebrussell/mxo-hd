using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerConditionSweepMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498, StagingZ = 13984;
    private static readonly (string Handle, int X)[] Stands =
        { ("QAFriend", X + 400), ("QAFar", X + 1200), ("QAOther", X - 300), ("QAEnemy", X - 500) };

    public override string Name => "ability-parity-hacker-condition-sweeps";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 320, 424, 379, 378, 318, 86 }, X, Z,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            "UPDATE characters SET healthC=1000,healthM=5000,innerStrC=2000,innerStrM=2000,y=495,pvpflag=1 WHERE charId IN (110,6111,6112,6113,6114)",
            $"UPDATE characters SET x=CASE charId WHEN 6111 THEN {Stands[0].X} WHEN 6112 THEN {Stands[1].X} WHEN 6113 THEN {Stands[2].X} WHEN 6114 THEN {Stands[3].X} END,z={StagingZ},alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END WHERE charId BETWEEN 6111 AND 6114",
            "UPDATE characters SET alignment=1 WHERE charId=110"
        },
        DevCommands: true, loadAbilityAncestors: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "After CSR enters, bring each helper to the stand via the helper chat request for ?tp X 495 -1498 0 0 0. X positions are QAFriend 25200, QAFar 26000, QAOther 24500, QAEnemy 24300. Helpers have the same loadout; CSR's actions use stock input.",
            "Use Self Defense. Form a mission team with QAFriend and QAFar through the stock radial and ordinary helper accept. Leave QAOther and QAEnemy outside the team.",
            "From the normal Actions list cast 320 then 424 on self. Capture +18 for 40 s and +35 for 25 s, including replacement of 320. Cast 379 and capture caster and QAFriend +20 for 35 s. QAFar at 12 m and nearby QAOther should receive no sweep.",
            "Toggle Deflect Virus 1.0 (378) from Actions and observe the five-second 693 viral/melee defence records on CSR and QAFriend, followed by term or refresh records. Do not cast the internal 693 directly.",
            "Select Upgrade Artist Upgrade from the granted Actions list and capture 1095 (+5 IS cost reduction, +7 Concentration, +20 Combat Evasion and +10 sweep while enabled). Replace it with Guardian Healer Upgrade (1094) and check the original sweep removal.",
            "For successful cleanse presentation, have opposing flagged QAEnemy cast Slow 1.0 (86) on CSR or QAFriend through the helper cast request. Require the actual debuff ON record before starting the relevant sweep. Count a cleanse only if it has a matching removal message or server SWEEP event before natural expiry. Record misses as misses; never force a roll."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable condition-sweep server.");
        foreach (var (handle, _) in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle);
            context.Helpers.Add(handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.Server == null) return Array.Empty<byte>();

        var caster = context.StockProbe ?? throw new InvalidOperationException("Expected the stock-profile CSR probe.");
        await caster.EnterWorldAsync();
        await caster.WaitForViewNamedAsync("WinterFrost");
        foreach (var (handle, x) in Stands)
        {
            var helper = context.Helpers[handle];
            helper.SendChat(FormattableString.Invariant($"?tp {x} {Y} {Z} 0 0 0"));
            await helper.WaitForTextAsync("Teleported to 0/0");
            await caster.WaitForViewNamedAsync(handle);
        }
        var near = context.Helpers["QAFriend"];
        var far = context.Helpers["QAFar"];
        await HackerParitySweepMode.InviteTeamMembersAsync(caster, new[] { near, far });

        var grant = await caster.WaitForRpcAsync("80 b2 47 04 00 00 08 02");
        var proof = new List<string> { grant.Hex };
        HackerParitySweepMode.SendCast(caster, 320);
        proof.Add((await ModifierAsync(caster, 320, 320, 18, 40)).Hex);
        HackerParitySweepMode.SendCast(caster, 424);
        proof.Add((await ModifierAsync(caster, 320, 320, 18, 40, 1)).Hex);
        proof.Add((await ModifierAsync(caster, 424, 424, 35, 25)).Hex);
        HackerParitySweepMode.SendCast(caster, 379);
        proof.Add((await ModifierAsync(caster, 379, 379, 20, 35)).Hex);
        proof.Add((await ModifierAsync(near, 379, 379, 20, 35)).Hex);

        HackerParitySweepMode.SendCast(caster, 378);
        proof.Add((await ModifierAsync(caster, 693, 183, 47, 5)).Hex);
        proof.Add((await ModifierAsync(near, 693, 1009, 23, 5)).Hex);
        proof.Add((await ModifierAsync(near, 378, 378, 0x28000820, .2f)).Hex);
        proof.Add((await ModifierAsync(caster, 693, 183, 47, 5, 1)).Hex);
        HackerParitySweepMode.SendCast(caster, 1095);
        proof.Add((await ModifierAsync(caster, 1095, 564, 7, 0)).Hex);
        proof.Add((await ModifierAsync(caster, 1095, 117, 5, 0)).Hex);
        proof.Add((await ModifierAsync(caster, 1095, 520, 20, 0)).Hex);
        proof.Add((await ModifierAsync(caster, 1095, 1095, 10, 0)).Hex);
        HackerParitySweepMode.SendCast(caster, 1094);
        proof.Add((await ModifierAsync(caster, 1095, 1095, 10, 0, 1)).Hex);

        var outsider = context.Helpers["QAOther"];
        foreach (var excluded in new[] { far, outsider, context.Helpers["QAEnemy"] })
            Check.That(!excluded.World.RpcSnapshot().Any(packet =>
                ManageBonusRecord.TryDecode(packet.Bytes, out var record) && record!.SourceId is 379 or 693),
                $"No party sweep or Deflect Virus reached {excluded.Handle}");

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-condition-sweeps.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static Task<RpcRecord> ModifierAsync(HeadlessPlayer recipient, ushort id, ushort target, int value, float duration, byte operation = 0) =>
        recipient.WaitForRpcAsync(packet => ManageBonusRecord.TryDecode(packet.Bytes, out var record) &&
            record!.SourceId == id && record.TargetId == target && record.SignedValue == value &&
            record.Duration == duration && record.AddRemoveType == operation,
            $"{recipient.Handle} modifier {id}/{target} value {value}, duration {duration}, operation {operation}");
}
