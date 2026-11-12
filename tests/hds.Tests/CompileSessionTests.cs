using hds;

namespace hds.Tests;

public class CompileSessionTests
{
    [Fact]
    public void NativeToolStartAndWindowCloseUseTheSameSlotWithDistinctFlags()
    {
        byte[] body=new byte[41]; BitConverter.GetBytes((ushort)969).CopyTo(body,0); body[35]=3; body[36]=1;
        Assert.Equal(((byte)3,true),CompileSession.Parse(body));
        body[36]=0; Assert.Equal(((byte)3,false),CompileSession.Parse(body));
        body[36]=2; Assert.Null(CompileSession.Parse(body));
        body[36]=1; body[35]=107; Assert.Null(CompileSession.Parse(body));
        body[35]=0; Assert.Null(CompileSession.Parse(body));
        body[35]=3; body[0]=0xca; Assert.Null(CompileSession.Parse(body));
        Assert.Null(CompileSession.Parse(new byte[4]));
        Assert.Null(CompileSession.Parse(new byte[42]));
    }

    [Fact]
    public void SessionAuthorityCannotSurviveToolLoadoutWorldOrEligibilityChanges()
    {
        var state=new CompileSession.State {Slot=3,District=1,World=4,Record=0x123};
        Assert.True(CompileSession.Matches(state,3,1,4,true,true,true));
        Assert.False(CompileSession.Matches(null!,3,1,4,true,true,true));
        Assert.False(CompileSession.Matches(state,4,1,4,true,true,true));
        Assert.False(CompileSession.Matches(state,3,2,4,true,true,true));
        Assert.False(CompileSession.Matches(state,3,1,5,true,true,true));
        Assert.False(CompileSession.Matches(state,3,1,4,false,true,true));
        Assert.False(CompileSession.Matches(state,3,1,4,true,false,true));
        Assert.False(CompileSession.Matches(state,3,1,4,true,true,false));
        Assert.Equal("80-BC-1D-00-23-01-00-C9-03-00-00-C8-03-00-00-00-00-00-00-00-00-00",BitConverter.ToString(CompileSession.Record(state,false)));
        byte[] off=CompileSession.Record(state,true); Assert.Equal(1,off[17]);
        Assert.Equal(0,AbilityCosts.Cost(CompileSession.Ability));
        Assert.Equal(969,CompileSession.Ability); Assert.Equal(968,CompileSession.Attribute);
    }
}
