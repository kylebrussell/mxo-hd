using hds;
using HeadlessClient;
namespace hds.Tests;
public class MervFollowupElevatorFixtureTests
{
    const string Q="0000000000000000000000000000803F";
    static StaticWorldObject RawSource(ushort world=1)=>new(){metrId=world,sectorID=209,staticId=441,mxoStaticId=0x0d1001b9,type=BitConverter.GetBytes((ushort)1408),pos_x=27500,pos_y=945,pos_z=13350,quat=Q};
    static string SourceSignature(ushort world=1)=>System.Text.Json.JsonSerializer.Serialize(RawSource(world),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
    static MissionElevatorWitness Witness(string site="exact-site",uint floor=0x0d100024,uint lobby=0x0d100028,string q=Q,double[]? at=null)=>
        new(1,site,48,at??new[]{28500d,945,14400},new[]{28500d,545,14400},q,Q,"existing audited association",new[]{SourceSignature()},floor,lobby,49);
    static MissionProgress.SavedState State(bool measured=true)=>new()
    {
        Version=measured?2:1,Mission=301206,Phase=0,Instance=23,Identity=new string('a',32),Started=100,
        SiteDoors=new[]{new StaticWorldObject{metrId=1,sectorID=209,staticId=36,mxoStaticId=0x0d100024,type=BitConverter.GetBytes((ushort)419),pos_x=27800,pos_y=945,pos_z=13700,quat=Q}},
        Waypoints=new[]{new MissionWaypoint{x=measured?28500:27800,y=945,z=measured?14400:13700,doorHex="2400100D"}},
        Exits=new[]{measured?new[]{28500d,545,14400}:new[]{27800d,545,13700}},
        OpenFloorSites=measured?new[]{new MissionOpenFloorSite(1,0x0d100024,27800,945,13700,"exact-site")}:null,
        OpenFloorCurrentSiteIndex=measured?0:null,ElevatorWitnesses=measured?new[]{Witness()}:null
    };
    [Fact]public void MeasuredSelectedLobbyKeyNeedsNoRawCsvMatchAndExitKeepsSelectedFloorKey()
    {
        var state=State();var before=System.Text.Json.JsonSerializer.Serialize(state,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        var entry=MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>());
        Assert.Equal(0x0d100028u,entry.Key);Assert.Equal(new[]{28500d,545,14400},entry.Pose);
        Assert.Equal(0x0d100024u,MervFollowupElevatorFixture.Select(state,false,Array.Empty<string>()).Key);
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(state,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
    }
    [Theory][InlineData("missing")][InlineData("duplicate")][InlineData("site")][InlineData("key")][InlineData("waypoint")][InlineData("lobby")][InlineData("index")][InlineData("nan")][InlineData("quaternion")]
    public void BadSelectedWitnessCannotFallBackToAnotherRawDoor(string change)
    {
        var state=State();switch(change)
        {
            case "missing":state.ElevatorWitnesses=null;break;case "duplicate":state.ElevatorWitnesses=new[]{Witness(),Witness()};break;
            case "site":state.ElevatorWitnesses=new[]{Witness(site:"other")};break;case "key":state.ElevatorWitnesses=new[]{Witness(lobby:0)};break;
            case "waypoint":state.Waypoints[0].x++;break;case "lobby":state.Exits[0][0]++;break;
            case "index":state.OpenFloorCurrentSiteIndex=1;break;case "nan":state.ElevatorWitnesses=new[]{Witness(at:new[]{double.NaN,945,14400})};break;
            case "quaternion":state.ElevatorWitnesses=new[]{Witness(q:"FF")};break;
        }
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.Select(state,true,new[]{"1,209,FF00100D,FF000000,A3010000,False,28500,545,14400,0,"+Q}));
    }
    [Fact]public void OnlyGenuineLegacyRawSiteUsesOriginalUniqueCsvLobbyLookup()
    {
        var state=State(false);string row="1,209,2800100D,28000000,A3010000,False,27800,545,13700,0,"+Q;
        var ride=MervFollowupElevatorFixture.Select(state,true,new[]{row});Assert.Null(ride.Witness);Assert.Equal(0x0d100028u,ride.Key);
        state.Waypoints[0].x=28500;Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.Select(state,true,new[]{row}));
    }
    [Fact]public void MeasuredOverlayAndNpcOnlyRootMustMatchSelectedPoseAndActualNavRegion()
    {
        var ride=MervFollowupElevatorFixture.Select(State(),true,Array.Empty<string>());
        ViewCreation Body(double x=28500,ushort goid=419)=>new(9,goid,7,new Dictionary<string,byte[]>{{"Position",new[]{x,545d,14400d}.SelectMany(BitConverter.GetBytes).ToArray()},{"Orientation",Convert.FromHexString(Q)}}){RelatedStaticObjectId=ride.Key};
        MervFollowupElevatorFixture.RequireOverlay(ride,Body());
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.RequireOverlay(ride,Body(27800)));
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.RequireOverlay(ride,Body(goid:3)));
        var root=new[]{28500d,895,14200};MervFollowupElevatorFixture.RequireRoot(ride,true,root,209,48,800,Array.Empty<double[]>());
        MervFollowupElevatorFixture.RequireRoot(ride,true,root,209,48,800,new[]{root});
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.RequireRoot(ride,true,root,209,42,800,Array.Empty<double[]>()));
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.RequireRoot(ride,true,root,210,48,800,Array.Empty<double[]>()));
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.RequireRoot(ride,true,root,209,48,400,Array.Empty<double[]>()));
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.RequireRoot(ride,true,root,209,48,800,new[]{new[]{28500d,895,14300}}));
        MervFollowupElevatorFixture.RequireRoot(ride,false,new[]{28500d,495,14200},209,49,400,Array.Empty<double[]>());
    }
    [Theory][InlineData("valid")][InlineData("identity")][InlineData("started")][InlineData("instance")][InlineData("phase")][InlineData("pose")][InlineData("witness")]
    public void PreRequestEntrySnapshotRejectsChangedOwnerOrSelectedGeometry(string change)
    {
        var before=State();var current=State();switch(change)
        {case "identity":current.Identity=new string('b',32);break;case "started":current.Started++;break;
         case "instance":current.Instance++;break;case "phase":current.Phase++;break;
         case "pose":current.Exits[0][0]++;break;case "witness":current.ElevatorWitnesses=new[]{Witness(lobby:0x0d10002c)};break;}
        Assert.Equal(change=="valid",MervFollowupElevatorFixture.SameEntry(before,current));Assert.False(MervFollowupElevatorFixture.SameEntry(before,null));
    }

    [Fact]public void ExactPrimaryCurrentSourceSignaturesAndFixtureWorldAreRequired()
    {
        var state=State();state.OpenFloorPhase=0;state.Objects=new(){new(){Id=1,Goid=357,Spot=RawSource()}};
        Assert.NotNull(MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>(),new ushort[]{1}).Witness);
        state.Objects[0].Spot.pos_x++;Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>(),new ushort[]{1}));
        state=State();state.Objects=new(){new(){Id=1,Goid=357,Spot=RawSource(2)}};
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>()));
        state=State();state.SiteDoors[0].metrId=2;state.OpenFloorSites=new[]{new MissionOpenFloorSite(2,0x0d100024,27800,945,13700,"exact-site")};
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>()));
    }
    static byte[] Own()
    {var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();return new byte[]{1,0}.Concat(new hds.resources.gameobjects.ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Door(uint key=0x0d100028,ushort view=9,byte spawn=4,bool dynamic=false)
    {
        var field=typeof(hds.GameObjectDefinitions).GetField("Object419")!;var o=(GameObject)Activator.CreateInstance(field.FieldType,field.Name,(ushort)419)!;
        if(dynamic)o.UnsetRelatedStaticObjId();else o.SetRelatedStaticObjId(key);o.DisableAllAttributes();
        foreach(var pair in new[]{("Position",new[]{28500d,545,14400}.SelectMany(BitConverter.GetBytes).ToArray()),("Orientation",Convert.FromHexString(Q)),("CurrentState",BitConverter.GetBytes(0x834u))})
        {var attr=(hds.Attribute)o.GetType().GetField(pair.Item1)!.GetValue(o)!;attr.enable();attr.setValue(pair.Item2);}
        return new byte[]{1,0}.Concat(new hds.resources.gameobjects.ObjectManager().GenerateCreationPacket(o,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    }
    static OwnViewLifeSnapshot Life(params byte[][] sections)
    {var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log.Snapshot();}
    [Fact]public void AlreadyCreatedExactBoundDoorRemainsCurrentWithoutNewCreation()
    {
        var ride=MervFollowupElevatorFixture.Select(State(),true,Array.Empty<string>());
        var old=Door();var stateUpdate=new byte[]{9,0}.Concat(DoorLifecycle.BuildStateUpdate(0x834)).ToArray();
        var observation=MervFollowupElevatorFixture.CurrentOverlay(Life(Own(),old,stateUpdate),ride);
        Assert.Equal(0x834u,observation.State);
        var closing=Life(Own(),old,new byte[]{9,0}.Concat(DoorLifecycle.BuildStateUpdate(0x838)).ToArray());
        Assert.Null(MervFollowupElevatorFixture.CurrentOverlay(closing,ride).State);
        Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.CurrentDoor(closing,new MissionNpcInteriorProof.Door(ride.Key,419,ride.Pose,ride.Quaternion!,null,"")));
        Assert.Equal(1,observation.BirthSection);Assert.Equal(ride.Key,observation.View.RelatedStaticObjectId);
        Assert.Equal((ushort)9,MervFollowupElevatorFixture.CurrentOverlay(Life(Own(),Door(key:0x0d100020,view:10,spawn:5),old),ride).View.ViewId);
        Assert.Equal((byte)5,MervFollowupElevatorFixture.CurrentOverlay(Life(Own(),old,new byte[]{1,0,1,1,0,9,0},Door(spawn:5)),ride).View.SpawnId);
    }
    [Fact]public void PortalAlreadyOpenIsReusedButClosedDeletedOrReusedCannotSkipNormalOpen()
    {
        var door=new MissionNpcInteriorProof.Door(0x0d100028,419,new[]{28500d,545,14400},Q,null,"");
        var birth=Door();var current=Life(Own(),birth);
        Assert.NotNull(MervFollowupElevatorFixture.CurrentOpenPortal(current,door));
        // Ordinary door use toggles state: a repeated route must not toggle a proven OPEN door closed.
        var closed=Life(Own(),birth,new byte[]{9,0}.Concat(DoorLifecycle.BuildStateUpdate(0x838)).ToArray());
        Assert.Null(MervFollowupElevatorFixture.CurrentOpenPortal(closed,door));
        var delete=new byte[]{1,0,1,1,0,9,0};
        Assert.Null(MervFollowupElevatorFixture.CurrentOpenPortal(Life(Own(),birth,delete),door));
        Assert.Null(MervFollowupElevatorFixture.CurrentOpenPortal(Life(Own(),birth,delete,birth),door));
        Assert.Null(MervFollowupElevatorFixture.CurrentOpenPortal(Life(Own(),Door(key:0x0d100020)),door));
    }
    [Fact]public void SamePoseWrongKeyDeletedReusedAmbiguousUnknownOrOldLifeCannotSupplyDoor()
    {
        var ride=MervFollowupElevatorFixture.Select(State(),true,Array.Empty<string>());var birth=Door();var delete=new byte[]{1,0,1,1,0,9,0};
        foreach(var rows in new[]{new[]{Own(),Door(key:0x0d100020)},new[]{Own(),Door(dynamic:true)},new[]{Own(),birth,delete},new[]{Own(),birth,delete,birth},
            new[]{Own(),birth,Door(view:10,spawn:5)},new[]{Own(),new byte[]{99,88},birth}})
            Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.CurrentOverlay(Life(rows),ride));
        var log=new OwnViewLifeLog();var own=Own();log.Observe(false,own,0,WireFormat.FindCreations(own));log.Observe(false,birth,1,WireFormat.FindCreations(birth));var stale=log.Snapshot();log.Observe(true,null,2,Array.Empty<ViewCreation>());
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.CurrentOverlay(stale,ride));
    }

    [Fact]public void SharedRideCallerUsesPersistedSelectionAndLiveObservationWithoutDroppingExitGuards()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        string source=File.ReadAllText(Path.Combine(root,"tools","HeadlessClient","ScenariosMervFollowupApproach.cs"));
        Assert.Contains("MervFollowupElevatorFixture.Select(saved,enter",source);Assert.Contains("MervFollowupElevatorFixture.SameEntry",source);
        Assert.Contains("MervFollowupElevatorFixture.CurrentOverlay(p.World.CurrentOwnViewLife()",source);
        Assert.DoesNotContain("MervFollowupApproachFixture.DoorAt(",source);Assert.DoesNotContain("CreationSnapshot().Skip(beforeArrival.Count)",source);
        Assert.Contains("MervFollowupWaitEscortExit",source);Assert.Contains("MervFollowupApproachFixture.CanDepart",source);
    }

    [Fact]public void HistoricalSelectedWitnessAllowsNpcOnlyOrDifferentNonadoptedCurrentSources()
    {
        var state=State();state.Phase=1;state.OpenFloorPhase=0;
        Assert.Empty(state.Objects);Assert.NotNull(MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>(),Array.Empty<ushort>()).Witness);
        var other=RawSource();other.pos_x+=100;
        state.Objects=new(){new(){Id=7,Goid=357,Spot=other}};
        Assert.NotNull(MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>(),new ushort[]{7}).Witness);
        // If the same phase is currently adopted, exactly that different source must fail.
        state.OpenFloorPhase=1;
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>(),new ushort[]{7}));
        state.OpenFloorPhase=0;var old=state.ElevatorWitnesses![0];
        state.ElevatorWitnesses=new[]{new MissionElevatorWitness(1,old.SiteFingerprint,old.Region,old.Floor,old.Lobby,old.FloorQuaternion,old.LobbyQuaternion,
            old.Evidence,new[]{"{\"metrId\":1}"},old.FloorKey,old.LobbyKey,old.LobbyRegion)};
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>(),new ushort[]{7}));
    }

}
