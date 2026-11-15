using HeadlessClient;
namespace hds.Tests;
public class MissionLootOfferWireTests
{
    static byte[][] Captured()=>new[]{
        Convert.FromHexString("809500000000010001000002d1070000320000b4e70d0028"),
        Convert.FromHexString("8096010000000f0002d10700000001190054686520456e64206f662074686520426567696e6e696e6700"),
        Convert.FromHexString("80970100")};
    [Fact] public void NativePartialActuallyContainedAWellFormedSingleOffer()
    {
        // Native partial: PacketLog lines532-534. Accepting the bytes is not a native UI PASS.
        Assert.Equal((ushort)0,MissionLootOfferWire.Require(Captured(),1).Index);
    }
    [Theory]
    [InlineData(0,6,2)] // Wrong transaction.
    [InlineData(0,8,2)] // Listed count does not match the entries.
    [InlineData(1,6,14)] // Wrong title offset.
    [InlineData(1,15,24)] // Wrong string length.
    [InlineData(1,41,1)] // Missing terminator.
    [InlineData(2,2,2)] // End from another transaction.
    public void RefusesMalformedOrIncompleteNativeHeaderEvidence(int row,int offset,byte value)
    {
        var packets=Captured();packets[row][offset]=value;
        Assert.Throws<InvalidDataException>(()=>MissionLootOfferWire.Require(packets,1));
    }
}
