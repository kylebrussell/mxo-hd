using HeadlessClient;
namespace hds.Tests;
public class CharacterCreationWireTests
{
    [Fact] public void ReservationMatchesNativeThreeByteFixedBodyAndSizedName()
    {
        Assert.Equal(Convert.FromHexString("0A030004004E656F00"),CharacterCreationWire.Reserve("Neo"));
    }
    [Fact] public void CreationCarriesMappedU32TraitAndAllFourNativeOffsetStrings()
    {
        byte[] body=CharacterCreationWire.Create(308,"First","Last","Background","account");
        Assert.Equal(0x0c,body[0]);Assert.Equal(308u,BitConverter.ToUInt32(body,65));
        Assert.Equal(106u,BitConverter.ToUInt32(body,5));Assert.Equal(5907u,BitConverter.ToUInt32(body,41));
        int end=77;
        foreach(int field in new[]{69,71,73,75})
        {
            int offset=BitConverter.ToUInt16(body,field);Assert.Equal(end,offset);
            int length=BitConverter.ToUInt16(body,offset);Assert.True(length>0);Assert.Equal(0,body[offset+1+length]);end=offset+2+length;
        }
        Assert.Equal(body.Length,end);
        Assert.Equal(0x34,body[65]);Assert.Equal(1,body[66]);Assert.Equal(0,body[67]);Assert.Equal(0,body[68]);
    }
    [Theory][InlineData("bad\0name")][InlineData("nonasciié")]
    public void FixtureRejectsNonNativeText(string input)=>Assert.Throws<ArgumentException>(()=>CharacterCreationWire.Reserve(input));
}
