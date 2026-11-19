using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedMobilityMode : NativeQaMode
{
    public override string Name => "ability-parity-awakened-mobility";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 11, 12 }, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            "UPDATE characters SET y=495,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112)",
            "UPDATE characters SET z=13984 WHERE charId IN (6111,6112)"
        },
        DevCommands: true, loadAbilityAncestors: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "After CSR enters, bring QAFriend into view with the helper chat request ?tp 25200 495 -1498 0 0 0. Activate Hyper-Speed 12 from stock Actions and capture the own and observer speed/state updates. While it is active bring QAFar into view using ?tp 24300 495 -1498 0 0 0, and capture its fresh view. Dismiss the buff and record both resets.",
            "Jump outdoors with Hyper-Jump 11 using stock aim. Record normal start, cancel and clock echo. Keep retail rank limits, trajectory and fee separate.",
            "Hyper-Strength 13 requires character level 99 in the shipped GOB. The fixture's disposable protocol proof seeds it separately for callback testing."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null)
        {
            // Helpers enter off-site so observers can be introduced after the stock client is ready.
            foreach (string name in new[] { "QAFriend", "QAFar" })
            {
                var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
                context.Helpers.Add(name, helper);
                await helper.EnterWorldAsync();
            }
            return Array.Empty<byte>();
        }
        var actor = context.StockProbe ?? throw new InvalidOperationException("Expected the stock-profile probe.");
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Expected a disposable database.");
        await database.ExecuteAsync("INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES(6111,300,2147496960,40,1,'HyperStrengthAbility')");
        await actor.EnterWorldAsync();
        using var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await observer.EnterWorldAsync();
        await Arrive(observer, 25200);
        var view = await observer.WaitForViewNamedAsync(actor.Handle);
        var proof = new List<object>();

        int cursor = observer.World.ViewSnapshot().Count;
        actor.SendRpc(Use(12));
        var speed = await actor.WaitForRpcAsync(IsSpeed, "Hyper-Speed captured movement modifier");
        proof.Add(new { Speed = speed.Hex, ObserverOn = await Scale(observer, view.ViewId, cursor, .45f) });
        using var late = await HeadlessPlayer.LoginAsync(context.Address, "QAFar", "QAFar");
        await late.EnterWorldAsync();
        await Arrive(late, 24300);
        var lateView = await late.WaitForViewNamedAsync(actor.Handle);
        Check.Equal(.45f, BitConverter.ToSingle(lateView.Attributes["MovementScale"]), "Late observer sees active movement scale");
        Check.That((BitConverter.ToUInt16(lateView.Attributes["DissemblingType"]) & 0x1000) != 0,
            "Late observer sees the active Hyper-Speed presentation bit");
        proof.Add(new { LateScale = Convert.ToHexString(lateView.Attributes["MovementScale"]),
            LateState = Convert.ToHexString(lateView.Attributes["DissemblingType"]) });
        cursor = observer.World.ViewSnapshot().Count;
        int lateCursor = late.World.ViewSnapshot().Count;
        actor.SendRpc("80 bb 0c 00");
        proof.Add(new { ObserverOff = await Scale(observer, view.ViewId, cursor, 0f), LateOff = await Scale(late, lateView.ViewId, lateCursor, 0f) });

        var from = Destination(24800);
        var to = Destination(24900);
        uint sample = 0xfffffff0;
        byte[] start = JumpRecords.Encode(new JumpRecords.Start(to, BitConverter.SingleToUInt32Bits(197), 1, sample));
        byte[] cancel = JumpRecords.Encode(new JumpRecords.Cancel(from));
        int rpc = actor.World.RpcSnapshot().Count;
        actor.SendRpc(start);
        var echo = await actor.WaitForRpcAsync("80 c3 f0 ff ff ff");
        actor.SendRpc(cancel);
        await Barrier(actor);
        Check.Equal(1, actor.World.RpcSnapshot().Skip(rpc).Count(packet => packet.Bytes.AsSpan().StartsWith(new byte[] { 0x80, 0xc3 })), "Cancel adds no clock echo");
        proof.Add(new { Start = Convert.ToHexString(start), Cancel = Convert.ToHexString(cancel), Echo = echo.Hex });

        cursor = observer.World.ViewSnapshot().Count;
        observer.SendRpc(Use(13));
        var strength = await observer.WaitForRpcAsync("80 bd 05 0d 00 00 00 00 00 00 01");
        var fx = BytePattern.Parse("d7 09 00 28");
        await observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(cursor).Count(fx.Occurs) >= 2,
            "Hyper-Strength activation and its first periodic effect", TimeSpan.FromSeconds(8));
        await observer.WaitUntilAsync(() => context.ActualServer!.DebugLog().Contains("[BUFF-DRAIN] char6111 ability13", StringComparison.Ordinal),
            "Hyper-Strength five-second drain", TimeSpan.FromSeconds(8));
        observer.SendRpc(Use(13));
        var off = await observer.WaitForRpcAsync("80 bd 05 0d 00 00 00 00 00 00 00");
        int effectCount = observer.World.ViewSnapshot().Skip(cursor).Count(fx.Occurs);
        await Task.Delay(TimeSpan.FromSeconds(3.2));
        Check.Equal(effectCount, observer.World.ViewSnapshot().Skip(cursor).Count(fx.Occurs),
            "Hyper-Strength stops emitting after switch-off");
        proof.Add(new { ForcedStrength = strength.Hex, Off = off.Hex, FxUpdates = effectCount });
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "awakened-mobility.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static bool IsSpeed(RpcRecord packet) =>
        ManageBonusRecord.TryDecode(packet.Bytes, out var modifier) && modifier!.SourceId == 12 &&
        modifier.TargetId == 115 && modifier.SignedValue == 45 && modifier.Duration == 600 && modifier.AddRemoveType == 0;

    private static byte[] Use(ushort id)
    {
        byte[] request = Convert.FromHexString("80b93a010000000000000000000000000000000000000000000000000000000000000000086b0000000000");
        BitConverter.GetBytes(id).CopyTo(request, 2);
        return request;
    }

    private static JumpRecords.Destination Destination(double x) => new(
        BitConverter.DoubleToUInt64Bits(x), BitConverter.DoubleToUInt64Bits(495),
        BitConverter.DoubleToUInt64Bits(-1498), 847, 20, 5);

    private static async Task<string> Scale(HeadlessPlayer observer, ushort view, int cursor, float expected)
    {
        var bytes = BitConverter.GetBytes(view).Concat(new byte[] { 0x02, 0x20 }).Concat(BitConverter.GetBytes(expected)).ToArray();
        var pattern = BytePattern.Parse(Convert.ToHexString(bytes));
        byte[]? record = null;
        await observer.WaitUntilAsync(() => (record = observer.World.ViewSnapshot().Skip(cursor).FirstOrDefault(pattern.Occurs)) != null,
            $"Observer movement scale {expected}", HeadlessPlayer.DefaultTimeout);
        return Convert.ToHexString(record!);
    }

    private static async Task Arrive(HeadlessPlayer observer, int x)
    {
        observer.SendChat(FormattableString.Invariant($"?tp {x} 495 -1498 0 0 0"));
        await observer.WaitForTextAsync("Teleported to 0/0");
    }

    private static async Task Barrier(HeadlessPlayer actor)
    {
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");
    }
}
