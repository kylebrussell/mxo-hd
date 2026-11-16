using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    const string C9RetiredMoves="0D00020E00A4008E80470000D2C25BB512C60C00020E00A4008E80470000D2C25BB512C6";
    [Fact] public void CapturedC9DeletedNpcMovementDoesNotPoisonStillLiveExactDoor22()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"hds.sln"))&&!Directory.Exists(Path.Combine(root.FullName,".git")))root=root.Parent;
        Assert.NotNull(root);
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName,"tests/hds.Tests/Fixtures/Zion-C9-deleted-npc-schema.json")));
        var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(fixture.RootElement.GetProperty("Payload").GetString()!,new JsonSerializerOptions{IncludeFields=true})!;
        var plan=ZionAfter1212RidePlan.Select(state,true,Array.Empty<string>(),Array.Empty<ushort>());
        Assert.Equal(933233667u,plan.Key);var views=fixture.RootElement.GetProperty("Views").EnumerateArray().Select(v=>Convert.FromHexString(v.GetString()!)).ToArray();
        Assert.Equal(C9RetiredMoves,Convert.ToHexString(views[61]));
        var log=new OwnViewLifeLog();
        for(int i=0;i<views.Length;i++)
        {
            log.Observe(false,views[i],i,WireFormat.FindCreations(views[i]));
            if(i>=56){var door=ZionCurrentDoorOverlay.Require(log.Snapshot(),plan);Assert.Equal((ushort)22,door.ViewId);Assert.Equal((byte)20,door.SpawnId);Assert.Equal((ushort)419,door.Goid);}
        }
        var deleted=Convert.FromHexString("01000101001600");log.Observe(false,deleted,views.Length,WireFormat.FindCreations(deleted));
        Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),plan));
    }
    [Fact] public void DeletedActualNpcSchemaFramesOnlyClosedMovementWithoutRestoringAuthority()
    {
        var log=QaLife(QaOwn(),QaNpc(QaState(),13,4),QaNpc(QaState(),12,5),QaDoor(state:0x834),Convert.FromHexString("01000101000D0001000101000C00"),Convert.FromHexString(C9RetiredMoves));
        Assert.Equal(0x834u,NpcInteriorComputerState.CurrentDoor(log.Snapshot(),QaExpectedDoor()).State);
        // A deleted requested object remains unavailable even after its known state update.
        var deletedDoor=Convert.FromHexString("01000101000900");
        log.Observe(false,deletedDoor,6,WireFormat.FindCreations(deletedDoor));
        var update=BitConverter.GetBytes((ushort)9).Concat(ObjectStates.BuildStateUpdate(346,0x834)).ToArray();
        log.Observe(false,update,7,WireFormat.FindCreations(update));
        Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.CurrentDoor(log.Snapshot(),QaExpectedDoor()));
    }
    [Theory] [InlineData("nohistory")] [InlineData("truncated")] [InlineData("reuse")] [InlineData("unknown")] [InlineData("oldlife")] [InlineData("deletedDoor")]
    public void SchemaHistoryCannotAuthorizeUnknownReusedOrDeletedViews(string change)
    {
        var sections=new List<byte[]>{QaOwn(),QaNpc(QaState(),13,4),QaDoor(state:0x834),Convert.FromHexString("01000101000D00")};
        if(change=="nohistory")sections.RemoveAt(1);
        if(change=="reuse")sections.Add(QaDoor(view:13,spawn:6,key:0x2260056c));
        if(change=="unknown")sections.Add(new byte[]{99,88});
        var move=Convert.FromHexString(C9RetiredMoves[..36]);sections.Add(change=="truncated"?move[..^1]:move);
        if(change=="deletedDoor")sections.Add(Convert.FromHexString("01000101000900"));
        var log=QaLife(sections.ToArray());var snapshot=log.Snapshot();if(change=="oldlife")log.EndLife();
        Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.CurrentDoor(snapshot,QaExpectedDoor()));
    }
}
