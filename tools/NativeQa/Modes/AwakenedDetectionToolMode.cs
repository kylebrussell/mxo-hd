using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedDetectionToolMode : NativeQaMode
{
    private const int Level = 18;
    private const ushort Detection = 298, Invisibility = 1116;
    private const uint DetectorTool = 2388, LocatorTool = 422;
    private static readonly (string Name, int X)[] Stands = { ("QAEnemy", 18000), ("QAFar", 18400) };

    public override string Name => "ability-parity-awakened-detection-tool";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, Detection }, 17000, 2300,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,innerStrC=200,innerStrM=200,cash=10000,alignment=CASE WHEN charId=110 THEN 1 ELSE 2 END,pvpflag=0 WHERE charId IN (110,6112,6114)",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM inventory WHERE charId=110 AND goid IN (2388,422)",
            "UPDATE characters SET x=31000+100*(charId-6111),z=2300 WHERE charId IN (6112,6114)",
            "DELETE FROM char_abilities WHERE char_id IN (6112,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES (6112,0,2147662848,1,1,'InvisibilityAbility'),(6114,0,2147662848,1,1,'InvisibilityAbility')"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10"
        }, readyMessages: new[]
        {
            "At level 18, use loaded Detection from Actions without an inventory tool. Record whether stock suppresses the click. If a request is sent, expect the tool-required line and no IS or reuse. In chat ?clientactions vendor opens real vendor 98. Buy a Handheld Data Node Locator, close or deselect the vendor and try Detection again. A locator does not satisfy it.",
            "Buy the Concealment Countermeasure Tool from the same window and verify it is in the bag. Send helper chat ?tp 18000 495 2300 0 0 0 to QAEnemy and ?tp 18400 495 2300 0 0 0 to QAFar. After both appear send each ?stealth invis. Both should vanish; then immediately use Detection from Actions with no target.",
            "Expect 5 IS, 10 s reuse, QAEnemy revealed with the concealment counter effect, and QAFar still invisible. The reveal roll is natural. Existing helper concealment commands only establish the witnesses."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Expected the disposable Detection schema.");
        await caster.EnterWorldAsync();
        byte[] request = new AbilityUseRecord(Detection, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
        var passive = await StockAbilityProfile.ObserveAsync(caster, 0,
            rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var modifier) && modifier!.SourceId == Detection &&
                modifier.TargetId == ConcealmentDetection.Detection && modifier.SignedValue == 72 && modifier.AddRemoveType == 0,
            "even without a tool the loaded Detection passive contributes +72 Detect");
        var noTool = await Refused(caster, request);
        caster.SendChat("?clientactions vendor");
        await caster.WaitForRpcAsync("81 0d");
        var locator = await Buy(caster, LocatorTool);
        var wrongTool = await Refused(caster, request);
        var detector = await Buy(caster, DetectorTool);
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid=2388 AND slot<107"),
            "Normal vendor purchase adds the matching tool to owned live inventory");

        var concealed = new List<(HeadlessPlayer Player, int Cursor)>();
        foreach (var stand in Stands)
        {
            var helper = context.Helpers[stand.Name];
            helper.SendChat($"?tp {stand.X} 495 2300 0 0 0");
            var view = await caster.WaitForViewNamedAsync(stand.Name);
            int cursor = helper.World.RpcSnapshot().Count;
            helper.SendChat("?stealth invis");
            await StockAbilityProfile.ObserveAsync(helper, cursor, rpc => StealthRecord(rpc, false), $"{stand.Name} enters actual invisibility");
            await caster.WaitForViewUpdateAsync(1, $"01 01 00 {WireFormat.ToHex(BitConverter.GetBytes(view.ViewId))}");
            concealed.Add((helper, cursor));
        }

        int useAt = caster.World.RpcSnapshot().Count;
        caster.SendRpc(request);
        var near = concealed[0];
        var far = concealed[1];
        var revealed = await StockAbilityProfile.ObserveAsync(near.Player, near.Cursor,
            rpc => StealthRecord(rpc, true), "Detection cancels the invisible enemy 10 m away without a target view");
        await near.Player.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 9d 05 00 28 ??");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Count(view => view.Name == near.Player.Handle) >= 2,
            "The detector regains the revealed enemy's view", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.TimerAsync(caster, useAt, Detection, 4, 10);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 10 c3 00");
        Check.That(!far.Player.World.RpcSnapshot().Skip(far.Cursor).Any(rpc => StealthRecord(rpc, true)),
            "The invisible enemy 14 m away stays concealed");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid=2388 AND slot<107"),
            "Detection keeps the reusable tool");
        Check.Equal(0, caster.World.Problems.Count + near.Player.World.Problems.Count + far.Player.World.Problems.Count, "Detection packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "awakened-detection-tool.json"),
            JsonSerializer.Serialize(new { Request = Convert.ToHexString(request), Passive = passive.Hex, NoTool = noTool.Hex,
                WrongTool = wrongTool.Hex, LocatorPurchase = locator.Hex, DetectorPurchase = detector.Hex, Revealed = revealed.Hex }));
        return Array.Empty<byte>();
    }

    private static bool StealthRecord(RpcRecord rpc, bool remove) => ManageBonusRecord.TryDecode(rpc.Bytes, out var mod) &&
        mod!.SourceId == Invisibility && mod.TargetId == 115 && mod.AddRemoveType == (remove ? 1 : 0);

    private static async Task<RpcRecord> Buy(HeadlessPlayer caster, uint goid)
    {
        int start = caster.World.RpcSnapshot().Count;
        string item = WireFormat.ToHex(BitConverter.GetBytes(goid));
        caster.SendRpc($"81 0e {item} 00");
        var ack = await StockAbilityProfile.ObserveAsync(caster, start,
            rpc => BytePattern.Parse($"81 0f {item}").IsMatch(rpc.Bytes), $"ordinary vendor purchase {goid}");
        Check.That(caster.World.RpcSnapshot().Skip(start).Any(rpc => rpc.Bytes[0] == 0x5e), "Vendor buys publish a bag item");
        return ack;
    }

    private static async Task<RpcRecord> Refused(HeadlessPlayer caster, byte[] request)
    {
        int start = caster.World.RpcSnapshot().Count, viewStart = caster.World.ViewSnapshot().Count;
        caster.SendRpc(request);
        var refusal = await StockAbilityProfile.ObserveAsync(caster, start,
            rpc => rpc.Bytes.AsSpan().SequenceEqual(DuelPackets.BuildSystemString(UtilityAbilities.ToolFail)), "client-native missing tool string");
        int barrier = caster.World.RpcSnapshot().Count;
        caster.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(caster, barrier, rpc => BytePattern.Parse("81 49").IsMatch(rpc.Bytes), "post-refusal barrier");
        Check.That(!caster.World.RpcSnapshot().Skip(start).Any(rpc => BytePattern.Parse("80 ac 2a 01").IsMatch(rpc.Bytes) ||
            BytePattern.Parse("80 bd 01 2a 01").IsMatch(rpc.Bytes)), "Tool refusal starts neither cast nor reuse");
        Check.That(!caster.World.ViewSnapshot().Skip(viewStart).Any(bytes =>
            BuffDrainLifetimeFixture.TryInnerStrength(bytes, out ushort value) && value < 200), "Tool refusal spends no IS");
        return refusal;
    }
}
