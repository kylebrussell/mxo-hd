using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedHyperSprintMode : NativeQaMode
{
    private const ushort Sprint = 184;
    private const string On = "80 bd 05 b8 00 00 00 00 00 00 01";
    private const string Off = "80 bd 05 b8 00 00 00 00 00 00 00";
    // Complete stock self-use request, PacketLog 11701b9dd84e at 18:40:50.
    private const string Use = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-awakened-hyper-sprint";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Sprint }, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level=5,exp={PlayerProgression.ExperienceForLevel(5)},y=495,innerStrC=48,innerStrM=48 WHERE charId=110",
            "UPDATE characters SET y=495,z=13984 WHERE charId=6111"
        },
        DevCommands: true, loadAbilityAncestors: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "CSR110, Mara outdoors, 48 maximum Inner Strength. Add Hyper-Sprint from Actions to the hotbar. Start with no target or other movement buff. Activate: expect the sprint effect, faster movement and 25 IS initial cost.",
            "Wait for the first five-second drain, then use the same hotbar icon to switch off. Confirm speed returns to baseline, the modifier disappears and a five-second reuse starts.",
            "When reuse clears, activate again and let it run until the natural shortfall (allow 40 seconds). Look for one localized insufficient-IS deactivation notice and speed returning to baseline. Check the server cost/drain and the outbound owner and observer resets.",
            "Optional observer: bring QAFriend into view by tell ?tp 25200 495 -1498 0 0 0 before activation. Save the PacketLog for both runs."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
            context.Helpers.Add("QAFriend", helper);
            await helper.EnterWorldAsync();
            return Array.Empty<byte>();
        }

        var actor = context.StockProbe ?? throw new InvalidOperationException("Expected stock-profile probe.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        await actor.EnterWorldAsync();
        using var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await observer.EnterWorldAsync();
        observer.SendChat("?tp 25200 495 -1498 0 0 0");
        await observer.WaitForTextAsync("Teleported to 0/0");
        ushort view = (await observer.WaitForViewNamedAsync(actor.Handle)).ViewId;
        byte[] use = Convert.FromHexString(Use);
        int observerCursor = observer.World.ViewSnapshot().Count;
        int ownerCursor = actor.World.ViewSnapshot().Count;
        int records = actor.World.RpcSnapshot().Count;
        actor.SendRpc(use);
        var started = await actor.WaitForRpcAsync(On);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[BUFF-DRAIN] char110 ability184", StringComparison.Ordinal),
            "Hyper-Sprint first five-second debit", TimeSpan.FromSeconds(8));
        var movementOn = await Observed(observer, view, observerCursor, .15f, 0x0400);
        string ownerOn = await ViewPacket(actor, ownerCursor, BytePattern.Parse("02 00 02 80 88 9a 99 19 3e 80 90 17 00 b0 19 31 46 12 ?? 02 00 04"),
            "Own activation effect, scale, state and initial 25 IS payment");
        observerCursor = observer.World.ViewSnapshot().Count;
        actor.SendRpc(use);
        var manualOff = await actor.WaitForRpcAsync(Off);
        var movementManualOff = await Observed(observer, view, observerCursor, 0f, 0);
        var reuse = AbilityCooldowns.BuildTimer(Sprint, AbilityCooldowns.OwnTimer, 5, false);
        await actor.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(reuse), "Hyper-Sprint reuse ready");

        observerCursor = observer.World.ViewSnapshot().Count;
        ownerCursor = actor.World.ViewSnapshot().Count;
        actor.SendRpc(use);
        await actor.WaitForRpcAsync(On);
        var drainedOff = await actor.WaitForRpcAsync(Off, TimeSpan.FromSeconds(40));
        var movementDrainedOff = await Observed(observer, view, observerCursor, 0f, 0);
        string ownerOff = await ViewPacket(actor, ownerCursor, BytePattern.Parse("02 00 02 80 88 00 00 00 00 80 80 80 02 00 00"),
            "Own shortfall reset");
        var responses = actor.World.RpcSnapshot().Skip(records).ToArray();
        var shortagePattern = BytePattern.Parse("b4 20 00 c6 01 b8 00");
        var shortage = responses.Single(packet => packet.Bytes.AsSpan().StartsWith(new byte[] { 0x81, 0x67 }) &&
            shortagePattern.Occurs(packet.Bytes));
        var descriptions = responses.Select(record =>
            ManageBonusRecord.TryDecode(record.Bytes, out var decoded) ? decoded : null)
            .Where(record => record?.SourceId == Sprint).ToArray();
        Check.Equal(8, descriptions.Count(record => record!.AddRemoveType == 0), "Four adds on each activation");
        Check.Equal(8, descriptions.Count(record => record!.AddRemoveType == 1), "Four removals on each stop");
        string[] debits = server.DebugLog().Split('\n').Where(line => line.Contains("[BUFF-DRAIN] char110 ability184", StringComparison.Ordinal))
            .Select(line => line.Trim()).ToArray();
        Check.That(debits.All(line => line.Contains(" amount25 ", StringComparison.Ordinal)), "Every sprint debit is 25 IS");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "awakened-hyper-sprint.json"),
            JsonSerializer.Serialize(new { Request = Use, Started = started.Hex, OwnerOn = ownerOn, ManualOff = manualOff.Hex,
                DrainedOff = drainedOff.Hex, OwnerOff = ownerOff, Shortage = shortage.Hex, Debits = debits,
                MovementOn = movementOn, MovementManualOff = movementManualOff, MovementDrainedOff = movementDrainedOff,
                ModifierUpdates = descriptions.Length }));
        return Array.Empty<byte>();
    }

    internal static Task<string> Observed(HeadlessPlayer observer, ushort view, int cursor, float speed, ushort bits)
    {
        var update = PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(speed)), (27, PlayerViewAttributes.U16(bits)));
        var pattern = BytePattern.Parse(Convert.ToHexString(BitConverter.GetBytes(view).Concat(update).ToArray()));
        return ViewPacket(observer, cursor, pattern, $"Observer scale {speed} and state {bits:x4}");
    }

    private static async Task<string> ViewPacket(HeadlessPlayer player, int cursor, BytePattern pattern, string description)
    {
        byte[]? record = null;
        await player.WaitUntilAsync(() => (record = player.World.ViewSnapshot().Skip(cursor).FirstOrDefault(pattern.Occurs)) != null,
            description, HeadlessPlayer.DefaultTimeout);
        return Convert.ToHexString(record!);
    }
}
