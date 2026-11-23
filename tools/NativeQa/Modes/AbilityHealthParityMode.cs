using HeadlessClient;
using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace NativeQa.Modes;

/// <summary>Known level-50 caster and ordinary stock casts against nearby helpers.</summary>
public sealed class AbilityHealthParityMode : NativeQaMode
{
    public override string Name => "ability-health-parity";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 38, 40, 45, 46, 51, 52, 54, 55, 56, 57, 58, 60, 62, 67, 70, 71, 75, 77, 80,
            95, 97, 98, 169, 170, 233, 234, 235, 236, 327, 398, 399, 401, 404, 409, 416, 461, 466, 1006 },
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET innerStrC=1000,innerStrM=1000 WHERE charId IN (110,6111)",
            "UPDATE characters SET pvpflag=1 WHERE charId=6114",
            "DELETE FROM inventory WHERE charId=110" },
        DevCommands: true,
        environmentOverrides: new() { ["MXO_PVP_SERVER"] = "1", ["MXO_HEALTH_PARITY_QA"] = "1" },
        readyMessages: new[]
        {
            "On wounded QAFriend, Repair RSI 1.0 expects +10 HP with +15% healing influence. Emergency Repairs 1.0 expects five +3 HP ticks at 2.5 s intervals. Count the effect ticks separately from natural regeneration.",
            "Flag CSR, select Hacker Style and cast Plague Zone 2.0 on QAEnemy. After a hit, expect five ticks at 2.5 s; a deflection applies no DOT. Read the actual total and resistances in server logs.",
            "QAFriend and flagged QAEnemy are already in-world. Cast Rejuvenate RSI on dead QAFriend and approve via the helper. QAFriend starts at 620 max HP and 1100 max IS with loaded passives. Death Effect changes those totals during recovery. The helper prints current recipient maxima before approval and verifies full HP, full IS and an in-place stand. See docs/ABILITY-HEALTH-PARITY.md."
        }, loadAbilityAncestors: true);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (string name in new[] { "QAFriend", "QAEnemy" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
        }
        await context.Helpers["QAEnemy"].WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 01 00 00 00 01");
        var maxima = await ReadMaximaAsync(context.Helpers["QAFriend"]);
        Console.WriteLine($"Health fixture QAFriend live at start with max HP {maxima.Health} and max IS {maxima.InnerStrength}.");
        return Array.Empty<byte>();
    }

    internal static async Task<object> VerifyRejuvenateAsync(HeadlessPlayer caster, HeadlessPlayer recipient)
    {
        await caster.EnterWorldAsync();
        var target = await caster.WaitForViewNamedAsync(recipient.Handle);
        var aliveMaxima = await ReadMaximaAsync(recipient);
        Check.Equal(((ushort)620, (ushort)1100), aliveMaxima, "QAFriend loaded maxima before Death Effect");
        recipient.SendChat("?die");
        await recipient.WaitForViewUpdateAsync(2, "02 80 80 80 c1 01 00 00");
        // PR316 stock CSR_Tester capture, PacketLog 53a738c0add2:36645. Replace only the direct object handle.
        byte[] cast = Convert.FromHexString("80b9a0010d000b000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        BinaryPrimitives.WriteUInt32LittleEndian(cast.AsSpan(4), (uint)target.ViewId | ((uint)target.SpawnId << 16));
        caster.SendRpc(cast);
        var pending = await recipient.WaitForRpcAsync("81 88");
        byte[] reply = RejuvenateReply(pending.Bytes);
        Array.Copy(pending.Bytes, 2, reply, 2, 4);
        var maxima = await ReadMaximaAsync(recipient);
        int cursor = recipient.World.ViewSnapshot().Count;
        recipient.SendRpc(reply);
        var restored = await CheckRejuvenatedAsync(recipient, maxima, cursor);
        return new { BeforeDeath = new { aliveMaxima.Health, aliveMaxima.InnerStrength }, Restored = restored };
    }

    internal static async Task<(ushort Health, ushort InnerStrength)> ReadMaximaAsync(HeadlessPlayer actor)
    {
        actor.SendChat("?combatdepth rejuvenate-maxima");
        var record = await actor.WaitForTextAsync("Rejuvenate maxima HP=");
        var result = Regex.Match(Encoding.ASCII.GetString(record.Bytes), @"Rejuvenate maxima HP=(\d+) IS=(\d+)");
        Check.That(result.Success, "Rejuvenate fixture received live maxima");
        return (ushort.Parse(result.Groups[1].Value), ushort.Parse(result.Groups[2].Value));
    }

    internal static async Task<object> CheckRejuvenatedAsync(HeadlessPlayer actor, (ushort Health, ushort InnerStrength) maxima, int viewCursor)
    {
        static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));
        var stand = BytePattern.Parse("02 00 01 28 ?? 40 00 3f a0 01");
        var vitals = BytePattern.Parse($"02 00 02 80 80 80 d1 00 {Le(maxima.InnerStrength)} {Le(maxima.Health)}");
        await actor.WaitUntilAsync(() =>
        {
            var fresh = actor.World.ViewSnapshot().Skip(viewCursor).ToArray();
            return fresh.Any(stand.Occurs) && fresh.Any(vitals.Occurs);
        }, "Fresh Rejuvenate stand and full live maxima", HeadlessPlayer.DefaultTimeout);
        await actor.WaitForRpcAsync("81 67 ?? ?? ?? ?? 3e 28 00 c6 00 a0 01 00 00");
        Check.Equal(0, actor.World.Problems.Count, "Rejuvenate approval framing");
        return new { Ability = 416, maxima.Health, maxima.InnerStrength, StandMotion = 63 };
    }

    internal static byte[] RejuvenateReply(byte[] pending)
    {
        Check.That(pending.Length >= 6 && BinaryPrimitives.ReadUInt32LittleEndian(pending.AsSpan(2)) == 416,
            "Health parity fixture expects a Rejuvenate RSI offer");
        return Scenarios.NativeRejuvenateConsent();
    }
}
