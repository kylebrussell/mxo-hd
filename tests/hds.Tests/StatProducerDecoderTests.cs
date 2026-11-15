using MxoHd.PacketResearch;
using HeadlessClient;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class StatProducerDecoderTests
{
    static byte[] Hex(string s)=>Convert.FromHexString(s.Replace(" ",""));
    [Fact] public void BaseKeepsValueAndSlotSeparateAndRejectsWrongShapes()
    {
        byte[] b=Hex("80b24e0008000802"); // retail login_stamos_new: Belief value8, CSSlot520
        Assert.True(BaseAttributeRecord.TryDecode(b,out var n));Assert.Equal(new BaseAttributeRecord(78,8,520),n);
        for(int i=0;i<b.Length;i++)Assert.False(BaseAttributeRecord.TryDecode(b.AsSpan(0,i),out _));
        Assert.False(BaseAttributeRecord.TryDecode(b.Concat(new byte[]{0}).ToArray(),out _));
        b[1]=0xb3;Assert.False(BaseAttributeRecord.TryDecode(b,out _));
        Assert.True(BaseAttributeRecord.TryDecode(Hex("80b2ffffffffffff"),out var max));Assert.Equal(ushort.MaxValue,max!.CsSlot);
    }
    [Theory]
    [InlineData("80bc15004603003f040000b100110000000000000000",1087,177,17,0)]
    [InlineData("80bc15003c01003f040000b100110000000100000000",1087,177,17,1)]
    [InlineData("80bc45000900004b0000001f04190000000100000000",75,1055,25,1)]
    [InlineData("80bc1500760500ee03000056016c0000000000000000",1006,342,108,0)]
    [InlineData("80bc45005d0000200000007b04090000000100000000",32,1147,9,1)]
    public void LiteralRetailRecordsDoNotConflateRawLinksAndDirectTargets(string hex,uint source,ushort target,int value,byte op)
    {
        Assert.True(ManageBonusRecord.TryDecode(Hex(hex),out var n));Assert.Equal(source,n!.SourceId);Assert.Equal(target,n.TargetId);
        Assert.Equal(value,n.SignedValue);Assert.Equal((IndividualBonusOperation)op,n.IndividualOperation);
    }
    [Fact] public void UnsupportedKindsAndOperationsStayLosslessWithoutInventedMeaning()
    {
        Assert.True(ManageBonusRecord.TryDecode(Hex("80bcfe0323456789abcdef0123456789abcd0100c07f"),out var n));
        Assert.Null(n!.IndividualOperation);Assert.Equal(0x67452303u,n.Locator);Assert.Equal(0x7fc00001u,n.DurationBits);
        var update=n with {Locator=0x123400,AddRemoveType=2};Assert.Equal(IndividualBonusOperation.Update,update.IndividualOperation);
        Assert.Null((update with {AddRemoveType=3}).IndividualOperation);
        byte[] b=Hex("80bc15004603003f040000b100110000000000000000");
        for(int i=0;i<b.Length;i++)Assert.False(ManageBonusRecord.TryDecode(b.AsSpan(0,i),out _));
        Assert.False(ManageBonusRecord.TryDecode(b.Concat(new byte[]{0}).ToArray(),out _));
    }
    [Fact] public void FramedBaseDecoderPreservesUnsupportedRawPayload()
    {
        byte[] good=Hex("80b24e0008000802"),bad=Hex("80b24e00080008");
        var samples=PacketResearcher.DetectPlayerAttributePayloads(WireFormat.BuildRpcPacket(1,new[]{good,bad}),7,"server").ToArray();
        Assert.Equal(2,samples.Length);Assert.Equal(new BaseAttributeRecord(78,8,520),samples[0].NativeFields);
        Assert.Null(samples[1].NativeFields);Assert.Equal("4e 00 08 00 08",samples[1].PayloadHex);
        Assert.Empty(PacketResearcher.DetectPlayerAttributePayloads(WireFormat.BuildRpcPacket(1,new[]{good}),7,"client"));
    }
    [Fact] public void ExactOwnedFixtureLevelProducesKnownRawPassiveRecords()
    {
        var c=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.playerInstance.Level.setValue((byte)50);
        c.messageQueue=new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(32,50),(75,50),(356,50)});
        Assert.Equal(50,AbilityPassives.Level(c,32));Assert.Equal(10,AbilityPassives.Total(c,1147));
        Assert.Equal(25,AbilityPassives.Total(c,1055)-5); //356 owns a separate+5 passive
    }
    [Fact] public void ModeUsesOwnedNormalProducerAndNoRandomOverride()
    {
        var p=new NativeQa.Modes.StatProducersMode().Plan;
        Assert.Contains((ushort)356,p.Abilities);Assert.DoesNotContain((ushort)1087,p.Abilities);
        Assert.Contains((ushort)32,p.Abilities);Assert.Contains((ushort)75,p.Abilities);Assert.False(p.FixedCallbackRolls);Assert.False(p.GmCharacter110);
        Assert.Empty(p.EnvironmentOverrides);
        Assert.Contains((ushort)1087,AbilityProgression.GrantsOf(356));
        foreach(ushort id in new ushort[]{32,75,356,314,1087})Assert.Equal(0,AbilityCatalog.Find(id).Number("Ability/AbilityClass",-1));
        Assert.True(AbilityBuffs.Retail[1087].Exclusive);Assert.False(AbilityBuffs.Retail[1087].Toggle);Assert.True(AbilityBuffs.CooldownStartsOnUse(1087));Assert.Equal(0,AbilityBuffs.Retail[1087].Seconds);
        Assert.Equal(20,AbilityBuffs.Retail[314].Seconds);
    }
}
