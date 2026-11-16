using System;
using HeadlessClient;
using hds;
using Xunit;

public class OpenWorldOwnCombatFrameTests
{
    [Theory]
    [InlineData(0x00100000u)]
    [InlineData(0x01100000u)]
    public void ActualOwnCombatEntryHasAnExactTypedBoundary(uint flags)
    {
        var bytes=ServerPackets.BuildEnterCombatMode(flags);
        Assert.Equal(18,bytes.Length);
        Assert.Equal(Convert.FromHexString("020003010C008084008080808001"),bytes[..14]);
        Assert.True(MervFollowupViewFrames.TryRead(bytes,0,12,out var frame));
        Assert.Equal(18,frame.Length);
        Assert.Equal(MervFollowupViewFrames.Kind.Attribute,frame.Type);
        Assert.False(MervFollowupViewFrames.TryRead(bytes[..^1],0,12,out _));
        Assert.False(MervFollowupViewFrames.TryRead(bytes,0,599,out _));
        var foreign=(byte[])bytes.Clone();foreign[0]=3;
        Assert.False(MervFollowupViewFrames.TryRead(foreign,0,12,out _));
        var unknown=(byte[])bytes.Clone();unknown[4]=0x0d;
        Assert.False(MervFollowupViewFrames.TryRead(unknown,0,12,out _));
    }

    [Fact]
    public void RetainedAmbientUpdatesEndExactlyBeforeOwnCombatEntry()
    {
        // PR269 e3ff failed stream, section27: two NPC14 records, then own2 entry.
        var bytes=Convert.FromHexString("0E00020E007A008884460080F743000020450E00048080808080800200001000020003010C00808400808080800100001000");
        Assert.Equal(50,bytes.Length);
        int offset=0;
        foreach(var expected in new[]{(Goid:(ushort)599,Length:18),(Goid:(ushort)599,Length:14),(Goid:(ushort)12,Length:18)})
        {
            Assert.True(MervFollowupViewFrames.TryRead(bytes,offset,expected.Goid,out var frame));
            Assert.Equal(expected.Length,frame.Length);offset+=frame.Length;
        }
        Assert.Equal(bytes.Length,offset);
        Assert.Equal(ServerPackets.BuildEnterCombatMode(0x00100000),bytes[32..]);
    }
}
