using HeadlessClient;
using MxoHd.PacketResearch;
using System.Text.Json;

namespace hds.Tests;

public class ProtocolFamilyDecoderTests
{
    private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

    [Fact]
    public void DiagnosticModeAddsNoPrivilegeOrRandomOverride()
    {
        var plan=new NativeQa.Modes.ProtocolFamilyMode().Plan;
        Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Contains((ushort)6,plan.Abilities);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor("protocol-family");
        Assert.Equal("",env["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL100"]);
    }

    [Fact]
    public void CapturedOverclockUsesNativeFieldWidthsRatherThanOverlappingFamilyWords()
    {
        // abilities_useoverclocked: existing AbilityBuffs capture anchor.
        var rpc=Hex("80 bc 15 00 42 01 00 47 01 00 00 ac 00 2b 00 00 00 00 00 00 a0 41");
        Assert.True(ManageBonusRecord.TryDecode(rpc,out var r));
        Assert.Equal((byte)0x15,r!.Type);Assert.Equal(0x14200u,r.Locator);
        Assert.Equal(327u,r.SourceId);Assert.Equal((ushort)172,r.TargetId);
        Assert.Equal(43,r.SignedValue);Assert.Equal(20f,r.Duration);Assert.Equal((byte)0,r.AddRemoveType);
        Assert.Equal((ushort)0x15,r.Family);Assert.Equal((ushort)0x142,r.RecordId);
    }

    [Fact]
    public void EveryNativeFieldOwnsItsExactBytesAndUnknownTypesAndNanBitsAreLossless()
    {
        var rpc=Hex("80 bc fe 81 23 45 67 89 ab cd ef 01 23 45 67 89 ab cd 01 00 c0 7f");
        Assert.True(ManageBonusRecord.TryDecode(rpc,out var r));
        Assert.Equal((byte)0xfe,r!.Type);Assert.Equal(0x67452381u,r.Locator);
        Assert.Equal(0xefcdab89u,r.SourceId);Assert.Equal((ushort)0x2301,r.TargetId);
        Assert.Equal(0xab896745u,r.ValueBits);Assert.Equal((byte)0xcd,r.AddRemoveType);
        Assert.Equal(0x7fc00001u,r.DurationBits);Assert.True(float.IsNaN(r.Duration));
        Assert.Contains("2143289345",JsonSerializer.Serialize(r));
    }

    [Fact]
    public void FixedRecordRejectsEveryTruncationAndTrailingBytes()
    {
        byte[] rpc=Hex("80bc150042010047010000ac002b000000000000a041");
        for(int length=0;length<rpc.Length;length++)Assert.False(ManageBonusRecord.TryDecode(rpc[..length],out _));
        Assert.False(ManageBonusRecord.TryDecode(rpc.Concat(new byte[]{0}).ToArray(),out _));
        rpc[1]=0xbd;Assert.False(ManageBonusRecord.TryDecode(rpc,out _));
    }

    [Fact]
    public void ExistingFramingAttachesNativeFieldsOnlyToExactLengthServerRecords()
    {
        byte[] packet=Hex("02 04 01 00 00 01 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00");
        var sample=Assert.Single(PacketResearcher.DetectManageBonusPayloads(packet,12,"server"));
        Assert.Equal(12,sample.Line);Assert.Equal((byte)0x45,sample.NativeFields!.Type);
        Assert.Equal(0x1103u,sample.NativeFields.Locator);Assert.Equal(50,sample.NativeFields.SignedValue);
        Assert.Empty(PacketResearcher.DetectManageBonusPayloads(packet,12,"client"));
    }

    [Fact]
    public void OpaqueLengthsRemainVisibleInExistingDetectionInsteadOfDisappearing()
    {
        var shortRpc=Hex("80bc150042010047010000ac002b000000000000a0");
        var sample=Assert.Single(PacketResearcher.DetectManageBonusPayloads(
            WireFormat.BuildRpcPacket(1,new[]{shortRpc}),5,"server"));
        Assert.Equal(19,sample.PayloadLength);Assert.Null(sample.NativeFields);
        Assert.Equal(19,sample.PayloadHex.Split(' ').Length);
    }

    [Fact]
    public void EmptyCreationCountDoesNotConsumeNextRecordAndUnknownSchemaIsRejected()
    {
        int offset=0;
        Assert.True(WireFormat.TryReadCreationAttributes(Hex("00 de ad"),12,ref offset,out var fields));
        Assert.Equal(1,offset);Assert.Empty(fields);
        offset=0;Assert.False(WireFormat.TryReadCreationAttributes(Hex("00 de ad"),65535,ref offset,out _));
        Assert.Equal(0,offset);
    }

    [Theory]
    [InlineData("01 81 11 22")] // declared count finished but continuation still set
    [InlineData("02 01 11 22")] // early end with count remaining
    [InlineData("01 03 11 22 33 44 55 66")] // extra active bit beyond declared count
    [InlineData("01 01 11")] // truncated field
    [InlineData("01 80")] // empty continuation without next mask
    public void MalformedCreationMasksCannotConsumeNeighborRecordBytes(string hex)
    {
        int offset=0;
        Assert.False(WireFormat.TryReadCreationAttributes(Hex(hex),12,ref offset,out var attributes));
        Assert.Equal(0,offset);Assert.Empty(attributes);
    }

    [Fact]
    public void ValidCreationMaskConsumesOnlyDeclaredAttributesAndLeavesSentinel()
    {
        byte[] bytes=Hex("aa 01 01 11 22 de ad");int offset=1;
        Assert.True(WireFormat.TryReadCreationAttributes(bytes,12,ref offset,out var attributes));
        Assert.Equal(5,offset);Assert.Single(attributes);Assert.Equal(Hex("11 22"),attributes["ReputationMerovingian"]);
        Assert.Equal(Hex("de ad"),bytes[offset..]);
        offset=-1;Assert.False(WireFormat.TryReadCreationAttributes(bytes,12,ref offset,out _));Assert.Equal(-1,offset);
    }
}
