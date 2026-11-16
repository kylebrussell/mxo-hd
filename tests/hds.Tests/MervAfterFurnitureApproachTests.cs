using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public sealed class MervAfterFurnitureApproachTests
{
    static string Repo=>NativeBoundFurnitureFixture.FindRepository();
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    // Existing literal captured1450 fixture is a parser/control input, not new whole-chain proof.
    static MissionProgress.SavedState State()=>NativeBoundFurnitureFixture.Prepare(Repo).State;
    [Fact] public void Captured1450V3UsesExactArrivalFloorBeforeNativeRoute()
    {
        var s=State();Assert.True(MervAfterFurnitureApproach.NeedsArrival(s));
        var p=MissionBoundFurnitureRouteProof.Read(Repo,s,1)!;
        var floor=p.Selection.GetProperty("Context").GetProperty("Floor");
        var raw=JsonSerializer.Deserialize<StaticWorldObject>(p.Floor.Raw,Fields)!;
        MervAfterFurnitureApproach.RequireRoot(p,p.Floor.Standing!,raw.sectorID,floor.GetProperty("Node").GetProperty("Region").GetUInt16(),p.FloorY);
        Assert.NotEqual(p.Floor.Key,p.Lobby.Key);Assert.Equal((ushort)1450,p.Goid);
        Assert.Contains(MissionBoundFurnitureRouteProof.Steps(Repo,p),step=>step.Doors.Contains(1032848039u));
    }
    [Theory][InlineData("lobby")][InlineData("root")][InlineData("sector")][InlineData("region")][InlineData("floor")]
    public void ArrivalCannotBorrowLobbyOtherRegionOrDifferentFloor(string fault)
    {
        var s=State();var p=MissionBoundFurnitureRouteProof.Read(Repo,s,1)!;var c=p.Selection.GetProperty("Context").GetProperty("Floor");
        var raw=JsonSerializer.Deserialize<StaticWorldObject>(p.Floor.Raw,Fields)!;
        var root=p.Floor.Standing!.ToArray();ushort sector=raw.sectorID,region=c.GetProperty("Node").GetProperty("Region").GetUInt16();double y=p.FloorY;
        switch(fault){case"lobby":root=p.Lobby.Standing!;break;case"root":root[0]+=1;break;case"sector":sector++;break;case"region":region++;break;case"floor":y+=1;break;}
        Assert.Throws<InvalidDataException>(()=>MervAfterFurnitureApproach.RequireRoot(p,root,sector,region,y));
    }
    [Theory][InlineData(1)][InlineData(2)]
    public void OldSelectionDoesNotEnterNewFurnitureArrivalBranch(int version)
    {
        var s=State();var e=JsonNode.Parse(s.StreetAccess.GetRawText())!;var row=e["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==s.Phase)!;
        row["Version"]=version;row.AsObject().Remove("FurnitureRoutes");s.StreetAccess=JsonSerializer.SerializeToElement(e);
        Assert.False(MervAfterFurnitureApproach.NeedsArrival(s));
    }
    [Fact] public void RecognizedMissingV3RoutesCannotFallBackToFivePointSampler()
    {
        var s=State();var e=JsonNode.Parse(s.StreetAccess.GetRawText())!;e["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==s.Phase)!.AsObject().Remove("FurnitureRoutes");s.StreetAccess=JsonSerializer.SerializeToElement(e);
        Assert.True(MervAfterFurnitureApproach.NeedsArrival(s));Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.Read(Repo,s,1));
    }
    [Fact] public void V3ArrivalCannotTransferAcrossCurrentOwnLifeOwnersOrReset()
    {
        var s=State();var plan=MissionBoundFurnitureRouteProof.Read(Repo,s,1)!;
        var raw=JsonSerializer.Deserialize<StaticWorldObject>(plan.Floor.Raw,Fields)!;
        var nav=new NavLocation{sector=raw.sectorID,region=plan.Selection.GetProperty("Context").GetProperty("Floor").GetProperty("Node").GetProperty("Region").GetUInt16(),floorY=plan.FloorY};
        var own=new PlayerCharacter();own.UnsetRelatedStaticObjId();own.DisableAllAttributes();
        var section=new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(own,0,0).getBytes()).Concat(new byte[]{2,0,0}).ToArray();
        OwnViewLifeLog Log(){var log=new OwnViewLifeLog();log.Observe(false,section,0,WireFormat.FindCreations(section));return log;}
        var original=Log();var life=original.Snapshot();var arrival=MervAfterFurnitureApproach.Capture(Repo,s,plan.Floor.Standing!,nav,life);
        MervAfterFurnitureApproach.Require(plan,s,arrival,original.Snapshot());
        Assert.Throws<InvalidDataException>(()=>MervAfterFurnitureApproach.Require(plan,s,arrival,Log().Snapshot()));
        original.Observe(true,null,1,Array.Empty<ViewCreation>());
        Assert.Throws<InvalidDataException>(()=>MervAfterFurnitureApproach.Require(plan,s,arrival,original.Snapshot()));
    }

}
