using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityOperativeSpyMode : NativeQaMode
{
    private static readonly (ushort Stat, int Value)[] Style =
    {
        (522, 59), (159, 20), (1009, 47), (1010, 47), (183, 47), (1011, 47), (1032, 66), (1034, 29), (1005, 72)
    };

    public override string Name => "ability-parity-operative-spy";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 151, 152, 155, 1112 },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0" },
        readyMessages: new[]
        {
            "On initial stock login, first confirm Status is populated. The Spy discipline is loaded and Spy Upgrade 1113 is absent from owned Archive. In Actions confirm that the Spy Upgrade action appears under Spy. Keep the hidden load and 155-to-1113 grant-add records.",
            "Select Spy Style from Actions and inspect the nine new source-1112 records. Switch to Self Defense and record all nine removal records. Compare the combat input deltas when the client exposes them; use PacketLog for fields absent from Status.",
            "Optional samples: select Spy Target and activate the granted Spy Upgrade, recording caster-only +3 Melee Defense, +3 Thrown Accuracy, +2 Thrown Damage and +8 Stealth. If available in active Spy interlock, queue Spinning Backsweep and record the animation and outcome.",
            "Potential scoped rows are Spy (155) and Spy Style (1112). Spy Upgrade's native stealth contest and Backsweep admission and full damage parity remain open."
        },
        extraQaMetadata: new() { ["NativeCloseableRows"] = 2, ["NativeCloseableIds"] = "155,1112" },
        testMobFixture: "ability-parity-operative-spy.csv", loadAbilityAncestors: true);

    internal async Task VerifyHeadlessAsync(HeadlessPlayer actor)
    {
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]);
        Check.That(archive.All(row => row.Descriptor != (0x80000000u | (1113u << 10))),
            "The granted Spy Upgrade is absent from the owned archive");
        await actor.EnterWorldAsync(loadingComplete: false);
        var target = await actor.WaitForViewNamedAsync("Spy Target");
        byte[] grant = Convert.FromHexString("80bc45035904009b0000005904000000000000000000");
        byte[] load = ServerPackets.BuildAbilityLoad(1113, 0, AbilityProgression.HiddenSlot);
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");
        Check.That(!actor.World.RpcSnapshot().Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(grant)),
            "The opaque grant is held until client readiness");
        actor.SendRpc("0c 01");
        await actor.WaitForRpcAsync(rpc => rpc.Bytes.AsSpan().SequenceEqual(load), "Spy Upgrade hidden load");
        await actor.WaitForRpcAsync(rpc => rpc.Bytes.AsSpan().SequenceEqual(grant), "Spy discipline sourced grant");

        static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));
        async Task Modifier(ushort id, ushort stat, int value, bool off = false)
        {
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Any(rpc =>
                ManageBonusRecord.TryDecode(rpc.Bytes, out var m) && m!.SourceId == id && m.TargetId == stat &&
                m.SignedValue == value && m.Duration == 0 && m.AddRemoveType == (off ? 1 : 0)),
                $"operative modifier {id}/{stat} {value} (off={off})", HeadlessPlayer.DefaultTimeout);
        }
        actor.SendRpc("80 b9 58 04 00 00");
        foreach (var (stat, value) in Style) await Modifier(1112, stat, value);
        actor.SendRpc("80 b9 11 00 00 00");
        foreach (var (stat, value) in Style) await Modifier(1112, stat, value, off: true);

        actor.SendRpc($"80 b9 59 04 {Le(target.ViewId)}");
        await Modifier(1113, 1009, 3);
        await Modifier(1113, 1032, 3);
        await Modifier(1113, 1034, 2);
        await Modifier(1113, 209, 8);
        Console.WriteLine("  Spy grant readiness, nine style additions and removals, and granted self upgrade verified");
    }
}
