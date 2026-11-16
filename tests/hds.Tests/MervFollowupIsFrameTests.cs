using System.Text.Json;
using HeadlessClient;
namespace hds.Tests;
public class MervFollowupIsFrameTests
{
    static readonly byte[] Is=Convert.FromHexString("02000280808010D807");
    [Fact] public void LiteralOwnIsUsesTheExactCurrentCursorAndHasNoPosition()
    {
        Assert.True(MervFollowupViewFrames.TryRead(Is,0,12,out var frame));Assert.Equal(9,frame.Length);Assert.Equal((ushort)2,frame.View);Assert.Equal(MervFollowupViewFrames.Kind.Attribute,frame.Type);Assert.Equal(-1,frame.PositionOffset);
        var burst=Convert.FromHexString("FFFF").Concat(Is).ToArray();Assert.False(MervFollowupViewFrames.TryRead(burst,0,12,out _));Assert.True(MervFollowupViewFrames.TryRead(burst,2,12,out _));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)]
    public void EveryTruncationRefuses(int length)=>Assert.False(MervFollowupViewFrames.TryRead(Is[..length],0,12,out _));
    [Fact] public void ForeignViewWrongKindAndUnknownMaskCannotBorrowOwnIsFraming()
    {
        var foreign=Is.ToArray();foreign[0]=7;Assert.False(MervFollowupViewFrames.TryRead(foreign,0,12,out _));
        Assert.False(MervFollowupViewFrames.TryRead(Is,0,599,out _));Assert.False(MervFollowupViewFrames.TryRead(Is,0,null,out _));
        var unknown=Is.ToArray();unknown[6]=0x11;Assert.False(MervFollowupViewFrames.TryRead(unknown,0,12,out _));
    }
    static JsonDocument Fixture()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tests/hds.Tests/Fixtures/MervFollowupMeasuredDoor-60272b49.json")));
    }
    static OwnViewLifeLog Replay(JsonElement fixture)
    {
        var log=new OwnViewLifeLog();foreach(var e in fixture.GetProperty("Events").EnumerateArray())
        {var bytes=e.GetProperty("Bytes").GetBytesFromBase64();log.Observe(false,bytes,e.GetProperty("Section").GetInt32(),WireFormat.FindCreations(bytes));}
        return log;
    }
    [Fact] public void Full136SectionRetainedReplayKeepsExactLifeKeyQuaternionAndBirth()
    {
        using var doc=Fixture();var f=doc.RootElement;Assert.Equal("CA2041FCE03D40F630C85AA7D5784271AFE809E29DBB1CE5BFDB5C7ADBF998BF",f.GetProperty("RawProofSha256").GetString());
        Assert.Equal(136,f.GetProperty("Events").GetArrayLength());Assert.Equal(301204,f.GetProperty("Mission").GetInt32());Assert.Equal(1,f.GetProperty("Phase").GetInt32());
        var ride=JsonSerializer.Deserialize<MervFollowupElevatorFixture.Ride>(f.GetProperty("SelectedRide").GetRawText())!;
        var log=Replay(f);var life=log.Snapshot();Assert.Equal(f.GetProperty("OwnLife").GetInt64(),life.Life);Assert.Equal(f.GetProperty("OwnBirthSection").GetInt32(),life.OwnBirthSection);
        var observed=MervFollowupElevatorFixture.CurrentOverlay(life,ride);
        Assert.Equal(0x45e00228u,observed.View.RelatedStaticObjectId);Assert.Equal((ushort)32,observed.View.ViewId);Assert.Equal((byte)30,observed.View.SpawnId);Assert.Equal(124,observed.BirthSection);
        Assert.Equal(Convert.FromHexString("00000000FFFF7FBF00000000F3043533"),observed.View.Attributes["Orientation"]);
        Assert.Equal(new[]{40500d,145,10600},Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(observed.View.Attributes["Position"],i*8)));
        log.EndLife();Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.CurrentOverlay(life,ride));
    }
    [Fact] public void UnknownAfterKnownSectionsCannotRecoverAtALaterDoorBirth()
    {
        using var doc=Fixture();var f=doc.RootElement;var ride=JsonSerializer.Deserialize<MervFollowupElevatorFixture.Ride>(f.GetProperty("SelectedRide").GetRawText())!;
        var log=Replay(f);log.Observe(false,new byte[]{99,88},136,Array.Empty<ViewCreation>());
        var door=f.GetProperty("Events").EnumerateArray().Single(e=>e.GetProperty("Section").GetInt32()==124).GetProperty("Bytes").GetBytesFromBase64();
        log.Observe(false,door,137,WireFormat.FindCreations(door));Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.CurrentOverlay(log.Snapshot(),ride));
    }
}
