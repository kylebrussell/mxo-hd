using HeadlessClient;
namespace hds.Tests;

public class ShadowReadyWireFixtureTests
{
    [Fact]
    public void ExactSourcePinIsMandatory()
    {
        string hash=new('A',64);ShadowReadyWireFixture.RequireSource(hash,hash.ToLowerInvariant());
        foreach(var wrong in new string?[]{null,"",new('A',63),new('Z',64),new('B',64)})Assert.Throws<InvalidDataException>(()=>ShadowReadyWireFixture.RequireSource(wrong,hash));
    }
    [Fact]
    public void ActualEncoderKeepsBaseAndMembershipDistinct()
    {
        byte[] basis=Convert.FromHexString("80B25D0400000802"),add=Convert.FromHexString("80BC45035D0400220100005D04000000000000000000");
        Assert.True(ShadowReadyWireFixture.Base(basis));Assert.True(ShadowReadyWireFixture.ChildBase(basis));
        Assert.True(ShadowReadyWireFixture.Member(add));Assert.True(ShadowReadyWireFixture.AnyMember(add));Assert.False(ShadowReadyWireFixture.ChildBase(add));
        byte[] off=Convert.FromHexString("80BC45035D0400220100005D04000000000100000000");
        Assert.True(ShadowReadyWireFixture.Member(off,true));Assert.False(ShadowReadyWireFixture.Member(off));
        foreach(var raw in new[]{basis[..^1],add[..^1],Convert.FromHexString("80B25D0432009701"),Convert.FromHexString("80B35D04")})Assert.False(ShadowReadyWireFixture.Base(raw));
        var nonzero=add.ToArray();nonzero[14]=1;Assert.False(ShadowReadyWireFixture.Member(nonzero));
    }
    [Fact]
    public void ModeAndWireUseSameOwnedParentsWithoutHiddenArchiveSeed()
    {
        var mode=new NativeQa.Modes.ShadowReadyPresentationMode();
        Assert.Equal(mode.Plan.Abilities,ShadowReadyWireFixture.Parents);
        Assert.DoesNotContain((ushort)1117,ShadowReadyWireFixture.Parents);Assert.DoesNotContain((ushort)1116,ShadowReadyWireFixture.Parents);
    }
}
