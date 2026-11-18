using hds;
using HeadlessClient;
namespace hds.Tests;
public class ZionAfter1212RidePlanTests
{
    const string Q="0000000000000000000000000000803F";
    static readonly string[] Rows={"1,0,14000000,0,A101,0,11,12,13,0,0"};
    static MissionProgress.SavedState State(bool measured=true)
    {
        var s=new MissionProgress.SavedState{Version=2,Mission=101213,Phase=1,Identity="current",Started=123,Instance=7,
            SiteDoors=new[]{new StaticWorldObject{metrId=1,sectorID=209,type=BitConverter.GetBytes((ushort)417),mxoStaticId=10,pos_x=1,pos_y=2,pos_z=3}},
            OpenFloorCurrentSiteIndex=0,OpenFloorSites=new[]{new MissionOpenFloorSite(1,10,1,2,3,"fp")},
            Waypoints=new[]{new MissionWaypoint{doorHex="0A000000",x=measured?101:1,y=measured?102:2,z=measured?103:3}},
            Exits=new[]{measured?new double[]{111,112,113}:new double[]{11,12,13}}};
        if(measured)s.ElevatorWitnesses=new[]{Witness()};return s;
    }
    static MissionElevatorWitness Witness(string fp="fp",uint floor=10,uint lobby=99,string q=Q,double[]? position=null)=>
        new(1,fp,47,position??new double[]{101,102,103},new double[]{111,112,113},q,Q,"audited",Array.Empty<string>(),floor,lobby,1);
    [Fact] public void MeasuredLobbyKeyAndBothPosesComeFromSavedWitnessRatherThanCsv()
    {
        var s=State();var entry=ZionAfter1212RidePlan.Select(s,true,Array.Empty<string>());
        Assert.Equal(99u,entry.Key);Assert.Equal(new double[]{111,112,113},entry.Pose);Assert.Equal(Q,entry.Quaternion);
        var exit=ZionAfter1212RidePlan.Select(s,false,Array.Empty<string>());Assert.Equal(10u,exit.Key);Assert.Equal(new double[]{101,102,103},exit.Pose);
        Assert.Equal(1,s.SiteDoors[0].pos_x);Assert.Equal("0A000000",s.Waypoints[0].doorHex);
    }
    [Theory] [InlineData("missing")] [InlineData("duplicate")] [InlineData("fingerprint")] [InlineData("floorKey")]
    [InlineData("lobbyKey")] [InlineData("floorPose")] [InlineData("lobbyPose")] [InlineData("quaternion")]
    [InlineData("world")] [InlineData("nan")] [InlineData("index")] [InlineData("rawIdentity")] [InlineData("waypointKey")]
    public void CorruptOrMissingMeasuredEvidenceNeverUsesCsvFallback(string fault)
    {
        var s=State();switch(fault)
        {
            case "world":s.SiteDoors[0].metrId=2;s.OpenFloorSites=new[]{new MissionOpenFloorSite(2,10,1,2,3,"fp")};break;
            case "missing":s.ElevatorWitnesses=null;break;
            case "duplicate":s.ElevatorWitnesses=new[]{Witness(),Witness()};break;
            case "fingerprint":s.ElevatorWitnesses=new[]{Witness(fp:"other")};break;
            case "floorKey":s.ElevatorWitnesses=new[]{Witness(floor:11)};break;
            case "lobbyKey":s.ElevatorWitnesses=new[]{Witness(lobby:0)};break;
            case "floorPose":s.Waypoints[0].x++;break;
            case "lobbyPose":s.Exits[0][0]++;break;
            case "quaternion":s.ElevatorWitnesses=new[]{Witness(q:"00")};break;
            case "nan":s.ElevatorWitnesses=new[]{Witness(position:new[]{double.NaN,102,103})};break;
            case "index":s.OpenFloorCurrentSiteIndex=1;break;
            case "rawIdentity":s.SiteDoors[0].pos_x++;break;
            case "waypointKey":s.Waypoints[0].doorHex="0B000000";break;
        }
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.Select(s,true,Rows));
    }
    [Theory] [InlineData(1)] [InlineData(2)] public void GenuineLegacyUsesOnlyExactRawDoorAndUniqueCsvLobby(int version)
    {
        var s=State(false);s.Version=version;var plan=ZionAfter1212RidePlan.Select(s,true,Rows);
        Assert.Equal(20u,plan.Key);Assert.Null(plan.Witness);Assert.Equal(new double[]{11,12,13},plan.Pose);
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.Select(s,true,Array.Empty<string>()));
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.Select(s,true,Rows.Append(Rows[0].Replace("14000000","15000000"))));
    }
    [Theory] [InlineData("mission")] [InlineData("phase")] [InlineData("identity")] [InlineData("started")] [InlineData("instance")] [InlineData("pose")]
    public void BeforeWireComparisonRejectsChangedOwnershipStageOrDoorIdentity(string fault)
    {
        var before=State();var after=State();switch(fault){case "mission":after.Mission++;break;case "phase":after.Phase++;break;case "identity":after.Identity="replacement";break;case "started":after.Started++;break;case "instance":after.Instance++;break;case "pose":after.Exits[0][0]++;break;}
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.SameEntry(before,after));
    }
    [Fact] public void CurrentRawObjectDtoMustMatchTheExistingWitnessJsonSignature()
    {
        var state=State();var raw=new StaticWorldObject{metrId=1,type=BitConverter.GetBytes((ushort)548),quat=Q,mxoStaticId=123,pos_x=4,pos_y=5,pos_z=6};state.OpenFloorPhase=state.Phase;
        state.Objects.Add(new MissionProgress.SavedObject{Id=1,Goid=548,Spot=raw});
        string source=System.Text.Json.JsonSerializer.Serialize(raw,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        state.ElevatorWitnesses=new[]{new MissionElevatorWitness(1,"fp",47,new double[]{101,102,103},new double[]{111,112,113},Q,Q,"audited",new[]{source},10,99,1)};
        Assert.Equal(99u,ZionAfter1212RidePlan.Select(state,true,Rows,new ushort[]{1}).Key);
        raw.mxoStaticId++;
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.Select(state,true,Rows,new ushort[]{1}));
    }
    [Fact] public void BoundHeaderRetainsExactStaticIdentityWhileDynamicHeaderHasNone()
    {
        var bound=Assert.Single(WireFormat.FindCreations(Convert.FromHexString("010008A1016300000002CDAB00050000")));
        Assert.Equal(99u,bound.RelatedStaticObjectId);Assert.Equal((ushort)417,bound.Goid);Assert.Equal((byte)2,bound.SpawnId);
        var dynamic=Assert.Single(WireFormat.FindCreations(Convert.FromHexString("01000CA10102CDAB00050000")));
        Assert.Null(dynamic.RelatedStaticObjectId);
    }
    [Theory] [InlineData(99u)] [InlineData(100u)]
    public void SpatiallyIdenticalDoorCannotBorrowAnotherStaticKey(uint key)
    {
        var plan=ZionAfter1212RidePlan.Select(State(),true,Rows);byte[] position=new[]{111d,112d,113d}.SelectMany(BitConverter.GetBytes).ToArray();
        var view=new ViewCreation(5,417,2,new Dictionary<string,byte[]>{{"Position",position},{"Orientation",Convert.FromHexString(Q)}}){RelatedStaticObjectId=key};
        if(key==99)plan.RequireOverlay(view);else Assert.Throws<InvalidDataException>(()=>plan.RequireOverlay(view));
    }

    [Fact] public void HistoricalReusedWitnessAllowsNpcOnlyCurrentPhaseButNotAnAdoptedEmptySourceSet()
    {
        var state=State();state.OpenFloorPhase=0;
        var old=new StaticWorldObject{metrId=1,type=BitConverter.GetBytes((ushort)548),quat=Q,mxoStaticId=123,pos_x=4,pos_y=5,pos_z=6};
        string source=System.Text.Json.JsonSerializer.Serialize(old,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        state.ElevatorWitnesses=new[]{new MissionElevatorWitness(1,"fp",47,new double[]{101,102,103},new double[]{111,112,113},Q,Q,"audited",new[]{source},10,99,1)};
        Assert.Empty(state.Objects);Assert.Equal(99u,ZionAfter1212RidePlan.Select(state,true,Rows).Key);
        state.OpenFloorPhase=state.Phase;
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.Select(state,true,Rows));
    }

}
