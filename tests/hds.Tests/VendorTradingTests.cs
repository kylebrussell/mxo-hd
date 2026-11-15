using hds;
namespace hds.Tests;
public class VendorTradingTests
{
    [Theory]
    [InlineData(0,false)][InlineData(1,true)]
    public void AppraiseIsIndependentOfSlot(byte flag,bool expected)
    {
        Assert.True(VendorTrading.TrySellRequest(new[]{flag,(byte)11},out bool appraise,out byte slot));
        Assert.Equal(expected,appraise);Assert.Equal((byte)11,slot);
    }
    [Theory]
    [InlineData("000b00")][InlineData("020b")][InlineData("0061")][InlineData("")]
    public void UnsupportedSellRequestsDoNotAddressAnotherRow(string body)=>Assert.False(VendorTrading.TrySellRequest(Convert.FromHexString(body),out _,out _));
    [Theory]
    [InlineData("03000101",3,true,true)][InlineData("90010001",400,false,true)][InlineData("03000100",3,true,false)]
    public void RecycleFieldsStaySeparate(string body,ushort expectedSlot,bool expectedPersonal,bool expectedRecycle)
    {
        Assert.True(VendorTrading.TryRecycleRequest(Convert.FromHexString(body),out ushort slot,out bool personal,out bool recycle));
        Assert.Equal(expectedSlot,slot);Assert.Equal(expectedPersonal,personal);Assert.Equal(expectedRecycle,recycle);
    }
    [Theory]
    [InlineData("0300")][InlineData("03000201")][InlineData("03000102")][InlineData("61000101")][InlineData("08020001")]
    public void InvalidRecycleShapesCannotMutateAnAccount(string body)=>Assert.False(VendorTrading.TryRecycleRequest(Convert.FromHexString(body),out _,out _,out _));
    [Theory]
    [InlineData(0u,0u)][InlineData(1u,50u)][InlineData(42709u,0u)][InlineData(2147490819u,500u)]
    public void RecycleDistinguishesZeroMissingAndNormalizedAbility(uint goid,uint price)=>Assert.Equal(price,VendorPricing.GetRecyclePrice(goid));
    [Theory]
    [InlineData(0,5u)][InlineData(1,5u)][InlineData(8,40u)][InlineData(32,160u)]
    public void OrdinaryRepresentableStackPaysPerItem(int count,uint value)=>Assert.Equal(value,VendorPricing.GetRecyclePrice(7332,count));
    [Fact]
    public void LargeStackCannotOverflowQuote()=>Assert.Equal(uint.MaxValue,VendorPricing.GetRecyclePrice(11666,int.MaxValue));
    [Fact]
    public void SignedAccountCreditsOnlyActualRemainingSpace()
    {
        Assert.Equal((uint)int.MaxValue,VendorTrading.Credit(int.MaxValue-5,680,out uint credited));Assert.Equal(5u,credited);
        Assert.Equal(50u,VendorTrading.Credit(-2,50,out credited));Assert.Equal(50u,credited);
    }
    [Fact]
    public void AppraisalEchoesFlagAndPreservesRepresentedItem()
    {
        var reply=ServerPackets.BuildVendorItemSellAck(7332,8,10,true);
        Assert.Equal(Convert.FromHexString("811201a41c000000001c100a000000"),reply);
        reply=ServerPackets.BuildVendorItemSellAck(0x80001c03,1,12600,true);
        Assert.Equal(1,reply[2]);Assert.Equal(46u,BitConverter.ToUInt32(reply,3));
        Assert.Equal(AbilityProgression.AbilityCodeValue(0x80001c03),BitConverter.ToUInt32(reply,7));
    }
}
