using HeadlessClient;
namespace hds.Tests;

// ObjectManager.GenerateCreationPacket: bound08 GOID u32LE(static) spawn cdab attributes view00.
public class RelatedStaticCreationIdentityTests
{
    [Fact] public void BoundHeaderPreservesFullLittleEndianKeyAndExistingCreationFields()
    {
        var view=Assert.Single(WireFormat.FindCreations(Convert.FromHexString("010008A1017856341202CDAB00050000")));
        Assert.Equal(0x12345678u,view.RelatedStaticObjectId);Assert.Equal((ushort)417,view.Goid);
        Assert.Equal((ushort)5,view.ViewId);Assert.Equal((byte)2,view.SpawnId);Assert.Empty(view.Attributes);
    }
    [Fact] public void FullyDynamicHeaderDoesNotInventRelatedStaticIdentity()
    {
        var view=Assert.Single(WireFormat.FindCreations(Convert.FromHexString("01000CA10102CDAB00050000")));
        Assert.Null(view.RelatedStaticObjectId);Assert.Equal((ushort)417,view.Goid);Assert.Equal((ushort)5,view.ViewId);
    }
    [Theory] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    public void TruncatedBoundHeaderOrCreationCannotExposeAStaticIdentity(int length)
    {
        byte[] full=Convert.FromHexString("010008A1017856341202CDAB00050000");
        Assert.Empty(WireFormat.FindCreations(full.Take(length).ToArray()));
    }
}
