using System.Text.Json;
using MxoHd.PacketResearch;
using NativeQa;
using hds;
namespace hds.Tests;
public class AbilityUseRecordTests
{
    private static string Root {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact] public void ActualNativeDeviceRecordHasIndependentFlagAndSlotAndPreservesOpaqueTail()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/protocol/ability-use-native-device.json")));var d=doc.RootElement;
        Assert.Equal("cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a",d.GetProperty("ClientDllSha256").GetString());
        Assert.Equal("5112b6ebbd7b05ec0349e7b88207d41c5da1c9a1f39b18421fc5bdfbf506aba5",d.GetProperty("PacketLogSha256").GetString());
        byte[] bytes=Convert.FromHexString(d.GetProperty("RpcHex").GetString()!);Assert.Equal(43,bytes.Length);
        Assert.True(AbilityUseRecord.TryDecode(bytes,out var r));Assert.Equal((ushort)949,r!.Id);Assert.Equal(0u,r.Target);Assert.Equal(0u,r.ObjectTargetCreate);
        Assert.Equal((byte)1,r.TargetInventorySlot);Assert.Equal((byte)106,r.InvSlotOfToolUsed);Assert.Equal((byte)0,r.FromPill);Assert.Equal(0u,r.TargetMiniTeamMemberId);
        Assert.Equal(0ul,r.TargetGroundLocationWord0);Assert.Equal(0ul,r.TargetGroundLocationWord1);Assert.Equal(0x3b989620001af50cul,r.TargetGroundLocationWord2);
        Assert.Equal(bytes,r.Encode());Assert.True(AbilityUseRecord.TryDecodePayload(bytes.AsSpan(2),out var body));Assert.Equal(r,body);
        bytes[28]^=0xff;Assert.Equal(0x3b989620001af50cul,r.TargetGroundLocationWord2); // input is not retained mutably
    }
    [Fact] public void IndependentByteRampProvesEveryScalarOffsetAndUnknownValuesRemainLossless()
    {
        byte[] bytes=Enumerable.Range(0,43).Select(i=>(byte)i).ToArray();bytes[0]=0x80;bytes[1]=0xb9;
        Assert.True(AbilityUseRecord.TryDecode(bytes,out var r));Assert.Equal((ushort)0x0302,r!.Id);Assert.Equal(0x07060504u,r.Target);Assert.Equal(0x0b0a0908u,r.ObjectTargetCreate);
        Assert.Equal(0x131211100f0e0d0cul,r.TargetGroundLocationWord0);Assert.Equal(0x1b1a191817161514ul,r.TargetGroundLocationWord1);Assert.Equal(0x232221201f1e1d1cul,r.TargetGroundLocationWord2);
        Assert.Equal((byte)36,r.TargetInventorySlot);Assert.Equal((byte)37,r.InvSlotOfToolUsed);Assert.Equal((byte)38,r.FromPill);Assert.Equal(0x2a292827u,r.TargetMiniTeamMemberId);Assert.Equal(bytes,r.Encode());
        var unknown=new AbilityUseRecord(ushort.MaxValue,uint.MaxValue,0x80000000,0x7ff8000000000123ul,0xfff0000000000000ul,0x8000000000000000ul,255,255,254,uint.MaxValue);
        Assert.True(AbilityUseRecord.TryDecode(unknown.Encode(),out var round));Assert.Equal(unknown,round);
    }
    [Fact] public void EveryWrongLengthAndWrongOpcodeIsRefusedIncludingAbbreviatedHarnessRecords()
    {
        foreach(int n in Enumerable.Range(0,86).Where(n=>n!=43)){var b=new byte[n];if(n>1){b[0]=0x80;b[1]=0xb9;}Assert.False(AbilityUseRecord.TryDecode(b,out var r));Assert.Null(r);}
        foreach(int n in Enumerable.Range(0,84).Where(n=>n!=41))Assert.False(AbilityUseRecord.TryDecodePayload(new byte[n],out _));
        foreach(var op in new[]{new byte[]{0x81,0xb9},new byte[]{0x80,0xb8}}){var b=new byte[43];op.CopyTo(b,0);Assert.False(AbilityUseRecord.TryDecode(b,out _));}
        Assert.False(AbilityUseRecord.TryDecode(Convert.FromHexString("80b94d000200"),out _));
    }
    [Fact] public void NativeProfileIncludesActualPrerequisitesAndNoUnrelatedPrivileges()
    {
        var plan=NativeQaModes.Find("ability-wire-record").Plan;Assert.Equal(new ushort[]{2,17,45,1006,77},plan.Abilities);Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Contains(plan.AfterHelpers,s=>s.Contains("level=50,exp="+PlayerProgression.ExperienceForLevel(50)));
        var heal=AbilityCatalog.Find(77);Assert.Equal(1006u,(uint)AbilityCatalog.IdOf((uint)heal.Number("AbilityPrerequisite/ReqAbilityID")));Assert.Equal(45u,(uint)AbilityCatalog.IdOf((uint)heal.Number("AblReq_Spy/ReqAbilityID")));
    }
}
