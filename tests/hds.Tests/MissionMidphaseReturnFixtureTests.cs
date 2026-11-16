using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
using NativeQa.Modes;
namespace hds.Tests;
public class MissionMidphaseReturnFixtureTests
{
    [Fact] public void SourcePinMustMatchTheActualServerBeforeSeeding()
    {
        string hash=new('A',64);MissionMidphaseReturnFixture.RequireSource(hash,hash.ToLowerInvariant());
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.RequireSource(null,hash));
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.RequireSource(new string('B',64),hash));
    }
    [Fact] public void CapturedBatchHasBothPinnedHashesAndExactOwnedCurrentState()
    {
        var s=MissionMidphaseReturnFixture.Verify(MissionMidphaseReturnFixture.Payload);
        Assert.Equal(101208,s.Mission);Assert.Equal(0,s.Phase);Assert.Equal(new[]{1,3,2},s.Done);
        Assert.Equal((ushort)6,s.Items[6831]);Assert.False(s.Flags["waitingForExit"]);
        MissionMidphaseReturnFixture.RequireUnchanged(s);
    }
    [Fact] public void RawBatchIsNotSilentlyTreatedAsNormalizedCheckpoint()
    {Assert.ThrowsAny<Exception>(()=>MissionMidphaseReturnFixture.Verify(MissionMidphaseReturnFixture.RawBatch));}
    [Fact] public void RawByteMutationCannotPassNormalization()
    {var bytes=MissionMidphaseReturnFixture.RawBatch;bytes[^1]^=1;Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.Normalize(bytes));}
    [Theory] [InlineData("Mission",101209)] [InlineData("Phase",1)] [InlineData("Instance",48)] [InlineData("Started",1794793134L)]
    public void ForeignScopeIsRefused(string field,long value)
    {
        var node=System.Text.Json.Nodes.JsonNode.Parse(MissionMidphaseReturnFixture.Payload)!;node[field]=value;
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.Verify(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }
    [Fact] public void CompletedUploadOrRemovedItemCannotPass()
    {
        var node=System.Text.Json.Nodes.JsonNode.Parse(MissionMidphaseReturnFixture.Payload)!;node["Done"]!.AsArray().Add(4);
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.Verify(Encoding.UTF8.GetBytes(node.ToJsonString())));
        node=System.Text.Json.Nodes.JsonNode.Parse(MissionMidphaseReturnFixture.Payload)!;node["Items"]!.AsObject().Clear();
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.Verify(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }
    [Theory] [InlineData("reality_hd")] [InlineData("mxotest_")] [InlineData("mxotest_x;DROP")] [InlineData("")]
    public void OnlyOwnedSchemasMayBeUsed(string schema)=>Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.RequireOwned(schema));
    [Fact] public void NativeSeedUsesExactCaptureAndEarlierReceiptsWithoutCurrentReceipt()
    {
        var mode=new MissionMidphaseReturnMode();Assert.Equal("mission-midphase-return",mode.Name);Assert.False(mode.Plan.DevCommands);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Equal(new ushort[]{2,17,1006,77},mode.Plan.Abilities);Assert.Equal(27,MissionMidphaseReturnFixture.EarlierReceipts.Length);Assert.DoesNotContain(101208,MissionMidphaseReturnFixture.EarlierReceipts);
        string sql=mode.Plan.AfterLoadout.Single();Assert.Contains("CONVERT(0x"+Convert.ToHexString(MissionMidphaseReturnFixture.Payload)+" USING utf8mb4)",sql);
        Assert.Contains("VALUES(110,6831,6,0,0)",sql);Assert.DoesNotContain("(110,101208)",sql);Assert.Contains("y=1295",sql);
    }
    [Theory] [InlineData(0u)] [InlineData(2u)] [InlineData(1002u)]
    public void SeedCannotTargetOtherCharacter(uint id)=>Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.ActiveSql(id));
    static byte[] Teleport(double y)
    {
        byte[] b=new byte[28];new byte[]{2,0,1,0x1c}.CopyTo(b,0);
        foreach(var v in new[]{(At:12,Value:3700f),(At:16,Value:(float)y),(At:20,Value:13100f)})BitConverter.GetBytes(v.Value).CopyTo(b,v.At);return b;
    }
    [Fact] public void OwnTeleportIsExactCompleteAndDestinationSpecific()
    {
        var b=Teleport(95);Assert.True(MissionMidphaseReturnFixture.ExactTeleport(b,new[]{3700d,95,13100}));
        Assert.False(MissionMidphaseReturnFixture.ExactTeleport(b,new[]{3700d,1295,13100}));
        Assert.False(MissionMidphaseReturnFixture.ExactTeleport(b[..27],new[]{3700d,95,13100}));
        b[0]=3;Assert.False(MissionMidphaseReturnFixture.ExactTeleport(b,new[]{3700d,95,13100}));
    }
    static byte[] Own()
    {
        var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();
    }
    static OwnViewLifeLog Life(byte[]? body=null)
    {
        var log=new OwnViewLifeLog();byte[] own=Own();log.Observe(false,own,0,WireFormat.FindCreations(own));
        if(body!=null)log.Observe(false,body,1,WireFormat.FindCreations(body));return log;
    }
    static NpcInteriorComputerState.Teleport Typed()
    {var log=Life();var before=log.Snapshot();var bytes=Teleport(95);log.Observe(false,bytes,1,WireFormat.FindCreations(bytes));return NpcInteriorComputerState.FreshTeleport(log.Snapshot(),before,1,new[]{3700d,95,13100});}
    static string Packets(ushort ack,byte[]? body=null)=>"[Server -> Client x PSS :0 CSEQ: 2 SSEQ: 37 ]\n[Receiver |CharId: 1001|]\n"+
        "00 03 "+Convert.ToHexString(body??Teleport(95))+" 00 00\n"+
        $"[Client->Server x PSS :0 CSEQ: 3 SSEQ: {ack} ]\n[Receiver |CharId: 1001|]\n00\n";
    [Fact] public void IndependentReceiptRequiresFreshExactOwnerAndAckOfTypedRaw()
    {
        var typed=Typed();var receipt=MissionMidphaseReturnFixture.RequireArrival(Packets(37),1001,0,typed);Assert.Equal((ushort)37,receipt.Sequence);
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.RequireArrival(Packets(36),1001,0,typed));
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.RequireArrival(Packets(37),1002,0,typed));
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.RequireArrival(Packets(37),1001,100,typed));
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.RequireArrival(Packets(37,Teleport(1295)),1001,0,typed));
    }
    [Fact] public void ProducerOwnBirthAndLiteralCoalescedOwnStateTeleportAndDoorDeleteAreTypedBeforeAck()
    {
        // Qualified PlayerPackets own9-byteIS, exact own28-byte011c, frameddelete; no gap search.
        byte[] body=Convert.FromHexString("020002808080100100").Concat(Teleport(95)).Concat(Convert.FromHexString("01000101000900")).ToArray();
        var log=Life();var before=log.Snapshot();log.Observe(false,body,1,WireFormat.FindCreations(body));
        var typed=NpcInteriorComputerState.FreshTeleport(log.Snapshot(),before,1,new[]{3700d,95,13100});Assert.Equal(9,typed.Offset);
        Assert.Equal((ushort)37,MissionMidphaseReturnFixture.RequireArrival(Packets(37,body),1001,0,typed).Sequence);
    }
    [Theory] [InlineData("unknown")] [InlineData("truncated")] [InlineData("foreignView")] [InlineData("oldCursor")] [InlineData("foreignLife")]
    public void TypedQualificationRefusesUnknownTruncatedForeignOrStaleArrival(string kind)
    {
        var log=Life();var before=log.Snapshot();byte[] body=Teleport(95);int cursor=1;
        if(kind=="unknown")body=new byte[]{99,88}.Concat(body).ToArray();
        if(kind=="truncated")body=body[..27];if(kind=="foreignView")body[0]=3;if(kind=="oldCursor")cursor=2;
        if(kind=="foreignLife")before=Life().Snapshot();log.Observe(false,body,1,WireFormat.FindCreations(body));
        Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.FreshTeleport(log.Snapshot(),before,cursor,new[]{3700d,95,13100}));
    }
    [Fact] public void DuplicatedCapturedMobIdentityIsRefused()
    {
        var node=System.Text.Json.Nodes.JsonNode.Parse(MissionMidphaseReturnFixture.Payload)!;node["Mobs"]!.AsArray().Add(node["Mobs"]![0]!.DeepClone());
        var state=JsonSerializer.Deserialize<hds.MissionProgress.SavedState>(node.ToJsonString(),new JsonSerializerOptions{IncludeFields=true})!;
        Assert.Throws<InvalidDataException>(()=>MissionMidphaseReturnFixture.RequireUnchanged(state));
    }
}
