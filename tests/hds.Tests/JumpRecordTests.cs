using hds;
using hds.shared;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class JumpRecordTests
{
    const string RetailStart="80c200000000ce42edc00000000000c05740000000003feecf40f7021e00010000f0204601bf97be00";
    const string RetailCancel="80c4000000a0d28ff0c00000000000c0574000000080e8b9d0404f0314000500";
    [Fact] public void LiteralRetailRecordsKeepNavigationSeparateFromTickSample()
    {
        var start=Convert.FromHexString(RetailStart);var cancel=Convert.FromHexString(RetailCancel);
        Assert.True(JumpRecords.TryStart(start,out var s));Assert.True(JumpRecords.TryCancel(cancel,out var c));
        Assert.Equal(0x00be97bfu,s.TimeStamp);Assert.Equal(10300f,BitConverter.Int32BitsToSingle(unchecked((int)s.PeakBits)));
        Assert.Equal((ushort)759,s.Destination.Sector);Assert.Equal((ushort)30,s.Destination.Prefab);Assert.Equal((ushort)1,s.Destination.NavPoly);
        Assert.Equal((ushort)847,c.Destination.Sector);Assert.Equal((ushort)20,c.Destination.Prefab);Assert.Equal((ushort)5,c.Destination.NavPoly);
        Assert.Equal(start,JumpRecords.Encode(s));Assert.Equal(cancel,JumpRecords.Encode(c));
    }
    [Fact] public void RawFloatingBitsAndFullWidthFieldsAreLosslessWithoutNewAdmissionRules()
    {
        var destination=new JumpRecords.Destination(0x7ff8000000001234,0x8000000000000000,0xfff0000000000000,65535,65534,65533);
        var s=new JumpRecords.Start(destination,0x7fc01234,0xfe,uint.MaxValue);
        Assert.True(JumpRecords.TryStart(JumpRecords.Encode(s),out var actual));Assert.Equal(s,actual);
        var c=new JumpRecords.Cancel(destination);Assert.True(JumpRecords.TryCancel(JumpRecords.Encode(c),out var other));Assert.Equal(c,other);
    }
    [Fact] public void ExactFullAndBodyLengthsRejectTruncationTrailingDataAndWrongHeaders()
    {
        var start=Convert.FromHexString(RetailStart);var cancel=Convert.FromHexString(RetailCancel);
        for(int i=0;i<start.Length;i++)Assert.False(JumpRecords.TryStart(start.AsSpan(0,i),out _));
        for(int i=0;i<cancel.Length;i++)Assert.False(JumpRecords.TryCancel(cancel.AsSpan(0,i),out _));
        Assert.False(JumpRecords.TryStart(start.Concat(new byte[]{0}).ToArray(),out _));Assert.False(JumpRecords.TryCancel(cancel.Concat(new byte[]{0}).ToArray(),out _));
        Assert.True(JumpRecords.TryStartBody(start.AsSpan(2),out _));Assert.True(JumpRecords.TryCancelBody(cancel.AsSpan(2),out _));
        Assert.False(JumpRecords.TryStartBody(start,out _));Assert.False(JumpRecords.TryCancelBody(cancel,out _));
        start[1]=0xc4;cancel[0]=0;Assert.False(JumpRecords.TryStart(start,out _));Assert.False(JumpRecords.TryCancel(cancel,out _));
    }
    [Theory]
    [InlineData(0u,"80c300000000")]
    [InlineData(0xffffffffu,"80c3ffffffff")]
    [InlineData(0x80000000u,"80c300000080")]
    public void ClockEchoKeepsWrappedAndHighSamples(uint sample,string literal)
    {
        Assert.Equal(Convert.FromHexString(literal),JumpRecords.EncodeClockEcho(sample));
        var start=new JumpRecords.Start(default,0,1,sample);
        Assert.True(JumpRecords.TryStart(JumpRecords.Encode(start),out var decoded));Assert.Equal(sample,decoded.TimeStamp);
    }
    [Theory]
    [InlineData(0,0xfffffff0u)] [InlineData(1,0x80000000u)] [InlineData(284,0u)] [InlineData(65535,0xffffffffu)]
    public void ActualMovementPreservesResourcesAndCancelDoesNotResampleClock(int inner,uint sample)
    {
        var oldClient=Store.currentClient;var oldWorld=Store.world;
        try
        {
            var c=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            c.playerData=new();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
            c.playerInstance=new();c.messageQueue=new();c.viewMan=new();c.playerInstance.InnerStrengthAvailable.setValue((ushort)inner);
            Store.currentClient=c;Store.world=(WorldServer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            byte[] start=Convert.FromHexString(RetailStart)[2..],cancel=Convert.FromHexString(RetailCancel)[2..];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(start.AsSpan(35),sample);
            var handler=new AbilityHandler();handler.ProcessJump(ref start);
            Assert.Equal((ushort)inner,AbilityHandler.CurrentInnerStrength(c));
            byte[] movement=Assert.Single(c.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>()).content;
            int mask=movement.AsSpan().IndexOf(Convert.FromHexString("0200030108008080bc"));Assert.True(mask>=0);
            int resource=mask+9+4+2+4+24;Assert.Equal(0x10,movement[resource]);
            Assert.Equal((ushort)inner,BitConverter.ToUInt16(movement,resource+1));
            handler.processHyperJumpCancel(ref cancel);
            Assert.Equal((ushort)inner,AbilityHandler.CurrentInnerStrength(c));
            var rpc=Assert.Single(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>());
            Assert.Equal(new byte[]{0x80,0xc3}.Concat(BitConverter.GetBytes(sample)).ToArray(),rpc.content.Skip(1).ToArray());
            double x=0,y=0,z=0;NumericalUtils.LtVector3dToDoubles(c.playerInstance.Position.getValue(),ref x,ref y,ref z);
            JumpRecords.TryCancel(Convert.FromHexString(RetailCancel),out var expected);
            Assert.Equal(expected.Destination.X,x);Assert.Equal(expected.Destination.Y,y);Assert.Equal(expected.Destination.Z,z);
            c.messageQueue=new();handler.processHyperJumpCancel(ref cancel);Assert.Empty(c.messageQueue.RPCMessagesQueue);
            Assert.NotEmpty(c.messageQueue.ObjectMessagesQueue);
        }
        finally{Store.currentClient=oldClient;Store.world=oldWorld;}
    }
}
