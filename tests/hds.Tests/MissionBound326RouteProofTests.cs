using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    static readonly JsonSerializerOptions BoundQaFields=new(){IncludeFields=true};
    static JsonObject BoundRouteJson()=>JsonNode.Parse(File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/Fixtures/Bound326-101216-238-V2-derived.json")))!.AsObject();
    static MissionProgress.SavedState BoundRouteState(JsonObject raw)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(raw.ToJsonString(),BoundQaFields)!;
    static JsonObject BoundSelection(JsonObject raw)=>raw["StreetAccess"]!["Witnesses"]!.AsArray().Select(w=>w!.AsObject()).Single(w=>(int)w["Phase"]! == (int)raw["Phase"]!);
    static JsonObject BoundRow(JsonObject raw)=>BoundSelection(raw)["BoundRoutes"]!["Routes"]![0]!.AsObject();
    static MissionBound326Approach.Bounds BoundQaBounds()=>new(249562173,new[]{8455d,480,6495},new[]{8425d,400,6470},new[]{8485d,560,6520},"00000000F304353F00000000F304353F",50);
    static readonly double[] BoundQaRoot={11053.824541506416,495,6205.403424919894};
    static MissionBound326Approach.Sample? BoundQaLocate(double[] point)
    {
        if(point.SequenceEqual(BoundQaRoot))return new(point,238,41,47,400);
        if(point.SequenceEqual(new[]{8575d,495,6495}))return new(point,238,50,217,400);
        return null;
    }
    static MissionBound326RouteProof.Plan BoundQaPlan(JsonObject raw)=>MissionBound326RouteProof.Read(BoundRouteState(raw),1,BoundQaBounds(),new(BoundQaRoot,238,41,400),BoundQaLocate)!;
    [Fact] public void Captured238SourceUsesDerivedOwnedFixedRouteAndOwnGoidPortalWithoutProject()
    {
        // Original payload remains verbatim in the separate f4b fixture. V2 route is a DERIVED expected-shape test fixture, not a live restore/route claim.
        var raw=BoundRouteJson();var plan=BoundQaPlan(raw);var before=raw.ToJsonString();var steps=MissionBound326RouteProof.Steps(Repo,plan);
        Assert.Equal((ushort)50,plan.Region);Assert.Equal((ushort)217,plan.Polygon);Assert.Equal(new[]{8575d,495,6495},plan.Standing);
        Assert.All(steps,s=>Assert.Equal(495,s.Standing[1]));Assert.Equal(BoundQaRoot,steps[0].Standing);
        Assert.Contains(steps,s=>s.Doors.SequenceEqual(new[]{249561776u}));var door=Assert.Single(plan.Doors);Assert.Equal((ushort)6958,door.Goid);Assert.Equal(new[]{10300d,545,5600},door.Position);Assert.Equal("00000000FFFF7F3F00000000F3043533",door.Quaternion);
        Assert.Equal(before,raw.ToJsonString());MissionBound326RouteProof.RequireCurrent(plan,BoundRouteState(raw));
    }
    [Theory] [InlineData("version")] [InlineData("manifest")] [InlineData("digest")] [InlineData("owner")] [InlineData("phase")] [InlineData("source")] [InlineData("nativePosition")] [InlineData("nativeQuaternion")] [InlineData("standing")] [InlineData("target")] [InlineData("start")] [InlineData("missing")] [InlineData("duplicate")] [InlineData("unknown")]
    public void RecognizedBoundV2NeverFallsBackWhenOwnedAuthorityChanges(string change)
    {
        var raw=BoundRouteJson();var selected=BoundSelection(raw);var envelope=selected["BoundRoutes"]!.AsObject();var row=BoundRow(raw);
        if(change=="version")selected["Version"]=3;else if(change=="manifest")envelope["BoundManifest"]=new string('A',64);else if(change=="digest")envelope["EvidenceDigest"]=new string('A',64);
        else if(change=="owner")raw["Identity"]="11111111111111111111111111111111";else if(change=="phase")selected["Phase"]=1;
        else if(change=="source")row["RawSourceJson"]=row["RawSourceJson"]!.GetValue<string>().Replace("9705","9706");else if(change=="nativePosition")row["NativePosition"]![1]=481;
        else if(change=="nativeQuaternion")row["NativeQuaternion"]="0000000000000000000000000000803F";else if(change=="standing")row["Standing"]![0]=9000;
        else if(change=="target")row["Target"]!["Region"]=41;else if(change=="start")row["Nodes"]![0]!["Polygon"]=48;
        else if(change=="missing")envelope["Routes"]!.AsArray().Clear();else if(change=="duplicate")envelope["Routes"]!.AsArray().Add(row.DeepClone());else row["Unproved"]=true;
        Assert.Throws<InvalidDataException>(()=>BoundQaPlan(raw));
    }
    [Theory] [InlineData("doorMissing")] [InlineData("wrongOwnGoid")] [InlineData("oppositeYaw")] [InlineData("brokenEdge")]
    public void OriginalTopologyAndOwnGoidAreRequiredBeforeAnyCrossRegionMovement(string change)
    {
        var raw=BoundRouteJson();var row=BoundRow(raw);var door=row["Doors"]![0]!.AsObject();
        if(change=="doorMissing")row["Doors"]!.AsArray().Clear();
        else if(change=="oppositeYaw")door["Quaternion"]="00000000F304353300000000FFFF7F3F";
        else if(change=="brokenEdge")row["Nodes"]![1]!["Polygon"]=0;
        else{var spot=JsonNode.Parse(door["RawDoorJson"]!.GetValue<string>())!.AsObject();spot["type"]=Convert.ToBase64String(BitConverter.GetBytes((ushort)580));spot["pos_x"]=10600;spot["pos_z"]=5900;spot["rot"]=1.1620496565e-05;spot["quat"]="00000000f20435bf00000000f304353f";door["RawDoorJson"]=spot.ToJsonString();door["Goid"]=580;door["Quaternion"]="00000000F304353300000000FFFF7F3F";}
        Assert.Throws<InvalidDataException>(()=>MissionBound326RouteProof.Steps(Repo,BoundQaPlan(raw)));
    }
    [Fact] public void CurrentSelectionWinsOverSameContextHistoricalV1AndDuplicateCurrentRefuses()
    {
        var raw=BoundRouteJson();var selected=BoundSelection(raw);var old=selected.DeepClone().AsObject();old["Version"]=1;old.Remove("BoundRoutes");old["Phase"]=1;
        raw["StreetAccess"]!["Witnesses"]!.AsArray().Insert(0,old);Assert.NotNull(BoundQaPlan(raw));
        raw["StreetAccess"]!["Witnesses"]!.AsArray().Add(selected.DeepClone());Assert.Throws<InvalidDataException>(()=>BoundQaPlan(raw));
    }
    [Fact] public void OriginalV1CaptureDoesNotAcquireARouteOrCrossRegionWaiver()
    {
        var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/Fixtures/Bound326-101216-238-f4b06c4a2a.json")),BoundQaFields)!;
        Assert.Null(MissionBound326RouteProof.Read(state,1,BoundQaBounds(),new(BoundQaRoot,238,41,400),BoundQaLocate));int projects=0;
        Assert.Throws<InvalidDataException>(()=>MissionBound326Approach.Select(BoundQaBounds(),new(BoundQaRoot,238,41,400),new(BoundQaLocate,(p,r,origin)=>{projects++;return null;})));Assert.Equal(0,projects);
    }
    [Fact] public void LocatePolygonTieDoesNotRewriteIndependentlyContainedSavedTarget()
    {
        var raw=BoundRouteJson();var plan=MissionBound326RouteProof.Read(BoundRouteState(raw),1,BoundQaBounds(),new(BoundQaRoot,238,41,400),p=>{var n=BoundQaLocate(p);return n!=null&&n.Region==50?n with{Polygon=216}:n;})!;
        Assert.Equal((ushort)217,plan.Polygon);Assert.NotEmpty(MissionBound326RouteProof.Steps(Repo,plan));
    }
    [Fact] public void FreshUseLogMustBeAfterRequestCursorAndExactKeyOwnGoid()
    {
        var door=Assert.Single(BoundQaPlan(BoundRouteJson()).Doors);string old="[DOOR] Use of static 249561776 type 6958 (Door)\n";
        Assert.Throws<InvalidDataException>(()=>MissionBound326RouteProof.RequireDoorUse(old,old.Length,door));
        string fresh="[DOOR] Use of static 249561776 type 6958 (Door)\n";Assert.Equal(fresh.TrimEnd('\n'),MissionBound326RouteProof.RequireDoorUse(old+fresh,old.Length,door));
        Assert.Throws<InvalidDataException>(()=>MissionBound326RouteProof.RequireDoorUse(old+fresh.Replace("6958","580"),old.Length,door));
        Assert.Throws<InvalidDataException>(()=>MissionBound326RouteProof.RequireDoorUse(old+fresh.Replace("249561776","249561777"),old.Length,door));
        Assert.Throws<InvalidDataException>(()=>MissionBound326RouteProof.RequireDoorUse(old,-1,door));
        Assert.Throws<InvalidDataException>(()=>MissionBound326RouteProof.RequireDoorUse(old,old.Length+1,door));
    }
    static byte[] BoundPortalBirth(ushort goid=6958,uint state=0x835)
    {var obj=new AttributeClass364("Required own-GOID portal",goid);obj.SetRelatedStaticObjId(249561776);obj.DisableAllAttributes();obj.Position.enable();obj.Position.setValue(new[]{10300d,545,5600}.SelectMany(BitConverter.GetBytes).ToArray());obj.Orientation.enable();obj.Orientation.setValue(Convert.FromHexString("00000000FFFF7F3F00000000F3043533"));obj.CurrentState.enable();obj.CurrentState.setValue(BitConverter.GetBytes(state));return QaEncode(obj,9,5);}
    [Fact] public void OrdinaryDoorStateWriterQualifiesExistingCurrentBirthAfterOpenWithoutNewSpawn()
    {
        var plan=BoundQaPlan(BoundRouteJson());var expected=Assert.Single(plan.Doors);var log=QaLife(QaOwn(),BoundPortalBirth());var before=log.Snapshot();
        var update=BitConverter.GetBytes((ushort)9).Concat(ObjectStates.BuildStateUpdate(6958,0x834)).ToArray();log.Observe(false,update,2,WireFormat.FindCreations(update));
        var observed=MissionBound326RouteProof.RequireOpen(log.Snapshot(),before,expected);Assert.Equal((ushort)6958,observed.View.Goid);Assert.Equal(0x834u,observed.State);Assert.Equal(1,observed.Capability.Birth);
    }
    [Theory] [InlineData("wrongGoid")] [InlineData("closed")] [InlineData("deleted")] [InlineData("reused")] [InlineData("unknown")] [InlineData("oldLife")] [InlineData("foreignLife")]
    public void RequiredPortalOpenCannotBorrowAliasOrOldLife(string change)
    {
        var expected=Assert.Single(BoundQaPlan(BoundRouteJson()).Doors);var log=QaLife(QaOwn(),BoundPortalBirth(state:0x834));var before=log.Snapshot();
        if(change=="wrongGoid"){log=QaLife(QaOwn(),BoundPortalBirth(580,0x834));before=log.Snapshot();}else if(change=="closed"){log=QaLife(QaOwn(),BoundPortalBirth());before=log.Snapshot();}
        else if(change is "deleted" or "reused"){var bytes=Convert.FromHexString("01000101000900");log.Observe(false,bytes,2,WireFormat.FindCreations(bytes));if(change=="reused"){var birth=BoundPortalBirth(state:0x834);log.Observe(false,birth,3,WireFormat.FindCreations(birth));}}
        else if(change=="unknown")log.Observe(false,new byte[]{99,88},2,Array.Empty<ViewCreation>());else if(change=="oldLife")log.EndLife();else before=QaLife(QaOwn(),BoundPortalBirth(state:0x834)).Snapshot();
        Assert.Throws<InvalidDataException>(()=>MissionBound326RouteProof.RequireOpen(log.Snapshot(),before,expected));
    }
}
