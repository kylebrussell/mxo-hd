using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MissionProxyIntroductionTests:IDisposable
{
    private readonly WorldServer previous=Store.world;
    private readonly WorldClient previousClient=Store.currentClient;
    private readonly Dictionary<string,WorldClient> previousClients=WorldServer.Clients;
    private static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds","Hardline Dreams MxO server.csproj")))p=Path.GetDirectoryName(p)!;return p;}}
    public MissionProxyIntroductionTests()
    {
        SectorProperties.Load(Path.Combine(Repo,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data","navmesh"));
        Store.world=(WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));Store.world.objMan=new ObjectManager();WorldServer.Clients=new Dictionary<string,WorldClient>();
    }
    public void Dispose(){Store.world=previous;Store.currentClient=previousClient;WorldServer.Clients=previousClients;}
    private static readonly double[] Root={23739.223227027636,895,13996.116135138183};
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();
        c.messageQueue=new MessageQueue();c.viewMan=new ViewManager();c.Alive=true;c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerData.missionInstanceCounter=47;c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(Root[0],Root[1],Root[2]));
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());return c;
    }
    private static MissionProgress Stage(WorldClient c,bool ground=false)
    {
        var xml=new XmlDocument();xml.LoadXml("<mission id='901' title='Introduction' description='D' district='1'><phase site='Normal'><object id='1' kind='Box'/><objective goal='1' type='Use' object='1'/></phase></mission>");
        var m=MissionXml.Parse(xml.DocumentElement!);var p=new MissionProgress(m);c.playerData.activeMission=m;c.playerData.missionProgress=p;
        StaticWorldObject Door(double y,uint id)=>new(){metrId=1,sectorID=209,mxoStaticId=id,pos_x=23700,pos_y=y,pos_z=13800,type=BitConverter.GetBytes((ushort)418),quat="0000000000000000000000000000803f"};
        p.site=new MissionSite{metrId=1,style="SlumApt",floorY=945,minX=21700,maxX=26100,minZ=13650,maxZ=15950,elevatorDoor=Door(945,0x0d1004a4),lobbyDoor=Door(ground?945:545,0x0d100401)};
        p.site.npcSpots.Add(Root);Assert.True(MissionInteriorSites.TryRegion(p.site,out _,out string why),why);
        var source=new StaticWorldObject{metrId=1,sectorID=209,mxoStaticId=0x0d100397,pos_x=24070,pos_y=988,pos_z=15112.5,type=BitConverter.GetBytes((ushort)1412),quat="00000000f20435bf00000000f304353f"};
        var pose=new MissionOpenFloorPlacement(4,1,901,0,1,source.mxoStaticId,1412,548,MissionOpenFloorPlacement.RawHash(source),new string('B',64),"00000000F304353F00000000F204353F",
            new[]{23814.611328125,840,14241.5712890625},new[]{23814.611328125,895,14143.16796875},Root.Concat(new[]{23814.611328125,895,14143.16796875}).ToArray(),new ushort[]{209,83,5},new ushort[]{209,83,3},new ushort[]{209,83,3});
        p.objects[1]=new MissionObjectState{def=m.phases[0].objects[0],spot=source,goid=548,entity=MissionStaging.EntityBase+47000UL+1,OpenFloor=pose};return p;
    }
    private static void DispatchAndAck(WorldClient c,bool dispatch=true)
    {
        var packet=new WorldPacket(c.playerData);foreach(SequencedMessage message in c.messageQueue.ObjectMessagesQueue)packet.addObjectContent(message);
        packet.getFinalData(c.playerData);if(dispatch)packet.RecordDispatchedObjectReceipts(c.messageQueue);
        c.playerData.setPss(0);c.DeletedAckedPackets(packet.neededAckSSeq);
    }
    private static void TickIntroduction(WorldClient c,MissionProgress p)
    {
        using var output=MissionOutput.Begin(c.messageQueue);MissionStaging.IntroduceDeferredObjects(c,p);output.Commit();
    }
    private static void Arrival(WorldClient c)
    {using(var output=MissionOutput.Begin(c.messageQueue)){MissionProxyIntroduction.Arrival(c,Root[0],Root[1],Root[2]);output.Commit();}}
    [Fact] public void StationaryCommittedReceiptIntroducesOnceWithoutAnyClientPositionAndPreservesState()
    {
        var c=Client();Store.currentClient=c;var p=Stage(c);var state=p.objects[1];state.lootOpen=true;state.putOpen=true;state.unlockedOnce=true;
        string before=JsonSerializer.Serialize(state.spot,new JsonSerializerOptions{IncludeFields=true});
        TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot());Arrival(c);
        DispatchAndAck(c,false);TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot());
        DispatchAndAck(c);Assert.Empty(c.viewMan.Snapshot()); // ACK observer never dispatches mission creation
        TickIntroduction(c,p);var view=Assert.Single(c.viewMan.Snapshot());Assert.Equal(state.entity,view.entityId);
        Assert.True(view.viewCreated);Assert.True(state.lootOpen);Assert.True(state.putOpen);Assert.True(state.unlockedOnce);
        Assert.Equal(before,JsonSerializer.Serialize(state.spot,new JsonSerializerOptions{IncludeFields=true}));
        int count=c.messageQueue.ObjectMessagesQueue.Count;TickIntroduction(c,p);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);Assert.Single(c.viewMan.Snapshot());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void GroundWalkAndAlreadyInsideAcceptanceUseOnlyActualAcceptedCurrentClientPosition(bool ground)
    {
        var c=Client();Store.currentClient=c;
        MissionProxyIntroduction.AcceptedPosition(c,Root[0],Root[1],Root[2]);var p=Stage(c,ground);TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot());
        using(MissionProxyIntroduction.Incoming(c,true))MissionProxyIntroduction.AcceptedPosition(c,Root[0],Root[1],Root[2]);
        TickIntroduction(c,p);Assert.Single(c.viewMan.Snapshot());
    }
    [Fact] public void ActualOwnViewParserAdmitsWalkPositionOnlyInItsCapturedIncomingLifecycle()
    {
        var c=Client();Store.currentClient=c;var p=Stage(c,true);
        byte[] Position()=>new byte[]{1,8}.Concat(BitConverter.GetBytes((float)Root[0])).Concat(BitConverter.GetBytes((float)Root[1])).Concat(BitConverter.GetBytes((float)Root[2])).ToArray();
        var bytes=Position();Assert.Equal(bytes.Length,c.playerInstance.parseAutoView(ref bytes,0));
        TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot()); // no current encrypted incoming owner
        using(MissionProxyIntroduction.Incoming(c,true))
        {bytes=Position();Assert.Equal(bytes.Length,c.playerInstance.parseAutoView(ref bytes,0));}
        TickIntroduction(c,p);Assert.Single(c.viewMan.Snapshot());
    }
    [Fact] public void RolledBackArrivalPreservesPriorReceivedCurrentInterestAndNeverArmsTheFailedMessage()
    {
        var c=Client();Store.currentClient=c;Arrival(c);DispatchAndAck(c);
        using(MissionOutput.Begin(c.messageQueue))MissionProxyIntroduction.Arrival(c,17000,495,2300);
        var p=Stage(c);TickIntroduction(c,p);Assert.Single(c.viewMan.Snapshot());
    }
    [Theory]
    [InlineData("world")][InlineData("life")][InlineData("epoch")][InlineData("dead")][InlineData("district")][InlineData("data")][InlineData("queue")][InlineData("player")]
    [InlineData("mission")][InlineData("phase")][InlineData("state")][InlineData("entity")][InlineData("server-outside")][InlineData("failed")]
    public void StaleArrivalCannotPublishIntoAChangedOwnerOrMission(string change)
    {
        var c=Client();Store.currentClient=c;var p=Stage(c);Arrival(c);DispatchAndAck(c);
        switch(change)
        {
            case "world":c.playerData.worldGeneration++;break;case "life":c.playerData.lifeGeneration++;break;
            case "epoch":c.TransportFence.PublishIncoming(true,()=>{});break;case "dead":c.playerData.isDead=true;break;
            case "district":c.playerData.setDistrictId(2);break;case "data":c.playerData=new ClientData();break;
            case "queue":c.messageQueue=new MessageQueue();break;case "player":c.playerInstance=new PlayerCharacter();break;
            case "mission":c.playerData.missionProgress=null;break;case "phase":p.phaseIndex++;break;
            case "state":p.objects.Clear();break;case "entity":p.objects[1].entity++;break;
            case "server-outside":c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000,495,2300));break;
            case "failed":p.failed=true;break;
        }
        TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void IncomingResetBatchOrEpochChangeCannotAdmitItsOwnPosition(bool reset)
    {
        var c=Client();Store.currentClient=c;var p=Stage(c);
        using(MissionProxyIntroduction.Incoming(c,!reset))
        {if(!reset)c.TransportFence.PublishIncoming(true,()=>{});MissionProxyIntroduction.AcceptedPosition(c,Root[0],Root[1],Root[2]);}
        if(!reset)c.TransportFence.Resume(c.TransportFence.Capture());
        TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot());
    }
    [Fact] public void BoundAndLegacyObjectsAreNotDeferredAndColdRestoreKeepsAnAbsentViewPending()
    {
        var c=Client();Store.currentClient=c;var p=Stage(c);var state=p.objects[1];Assert.True(MissionProxyIntroduction.Deferred(state));
        MissionStaging.RestoreObjects(c,p);Assert.Empty(c.viewMan.Snapshot());Arrival(c);DispatchAndAck(c);TickIntroduction(c,p);Assert.Single(c.viewMan.Snapshot());
        state.spot.type=BitConverter.GetBytes((ushort)548);Assert.False(MissionProxyIntroduction.Deferred(state));state.OpenFloor=null;Assert.False(MissionProxyIntroduction.Deferred(state));
    }
    private static readonly double[] MeasuredRoot={28413.453786483045,1695,14219.695388506345};
    private const string MeasuredFingerprint="FE1A4A0FAD5BF68CC4633B517E0E855108E109A0FAB99E2D3B5C9BC31ABCAADA";
    private static readonly Lazy<MissionSite> MeasuredSite=new(()=>
    {
        var rows=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Where(l=>l.StartsWith("1,209,",StringComparison.Ordinal)))
        {
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],System.Globalization.CultureInfo.InvariantCulture);
            rows.Add(new StaticWorldObject{metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),
                staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(rows,1).Single(site=>site.elevatorDoor.mxoStaticId==0x0d10001c&&MissionOpenFloorSite.CatalogFingerprint(site)==MeasuredFingerprint);
    });
    private static MissionProgress StageMeasured(WorldClient c,bool floatPosition=false)
    {
        var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data","missions","zion_1_2_16_fly_in_the_soup.xml"));
        var mission=MissionXml.Parse(xml.DocumentElement!);var p=new MissionProgress(mission){phaseIndex=3};
        c.playerData.missionInstanceCounter=50;c.playerData.activeMission=mission;c.playerData.missionProgress=p;
        p.site=MeasuredSite.Value;p.sites.Add(p.site);p.openFloorResolvedPhase=3;
        var source=p.site.objectSpots.Single(o=>o.mxoStaticId==0x0d1000d0);
        Assert.Equal("2E90568E2604A4E33749454BC400F37C0F1B938F05E188575711DB03002ACF94",MissionOpenFloorPlacement.RawHash(source));
        var choices=new[]{new MissionOpenFloorChoice(1,548,JsonSerializer.Serialize(source,MissionProgress.SaveJson))};
        Assert.True(MissionElevatorPoses.TryCreate(p.site,choices,out var witness,out string why),why);Assert.NotNull(witness);
        Assert.Equal(new[]{28500d,1745,14400},witness.Floor);Assert.Equal((ushort)46,witness.Region);
        p.elevatorWitnesses.Add(MeasuredFingerprint,witness);
        var pose=new MissionOpenFloorPlacement(4,1,101216,3,1,0x0d1000d0,1412,548,MissionOpenFloorPlacement.RawHash(source),
            "A0FCD2C5D0AEEDA311010754F59CAFC424173F0751AC77C982BE9F7AF881A9F7","00000000F304353300000000FFFF7F3F",
            new[]{28641.595703125,1640,14214.611328125},new[]{28543.166015625,1695,14214.611328125},
            MeasuredRoot.Concat(new[]{28543.166015625,1695,14214.611328125}).ToArray(),new ushort[]{209,46,111},new ushort[]{209,46,111},new ushort[]{209,46,111});
        p.objects[1]=new MissionObjectState{def=mission.phases[3].objects.Single(o=>o.id==1),spot=source,goid=548,entity=MissionStaging.EntityBase+50000UL+151UL,OpenFloor=pose};
        var root=MeasuredRoot.Select(x=>floatPosition?(double)(float)x:x).ToArray();
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(root[0],root[1],root[2]));return p;
    }
    private static void MeasuredArrival(WorldClient c)
    {using var output=MissionOutput.Begin(c.messageQueue);MissionProxyIntroduction.Arrival(c,MeasuredRoot[0],MeasuredRoot[1],MeasuredRoot[2]);output.Commit();}
    [Theory][InlineData(false)][InlineData(true)]
    public void ExactFailedZionFourthAreaUsesMeasuredRoomForBothServerAndReceivedArrival(bool floatPosition)
    {
        var c=Client();Store.currentClient=c;var p=StageMeasured(c,floatPosition);var state=p.objects[1];
        var root=MeasuredRoot.Select(x=>floatPosition?(double)(float)x:x).ToArray();var point=new NavPoint{x=root[0],y=root[1],z=root[2]};
        Assert.False(MissionInteriorSites.Contains(p.site,point));Assert.False(MissionInteriorSites.Contains(p.site,point,null));
        Assert.True(MissionInteriorSites.Contains(p.site,point,p.ElevatorFor(p.site)));
        var legacy=new NavPoint{x=27996.574437386866,y=1695,z=13736.857707010036};
        Assert.True(MissionInteriorSites.Contains(p.site,legacy)); // unchanged two-argument legacy component
        string before=JsonSerializer.Serialize(new{state.spot,state.OpenFloor},MissionProgress.SaveJson);
        MeasuredArrival(c);TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot()); // no body until dispatched exact receipt
        DispatchAndAck(c,false);TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot());
        DispatchAndAck(c);Assert.Empty(c.viewMan.Snapshot());TickIntroduction(c,p);
        var introduced=Assert.Single(c.viewMan.Snapshot());Assert.Equal(state.entity,introduced.entityId);Assert.True(introduced.viewCreated);
        Assert.Equal(before,JsonSerializer.Serialize(new{state.spot,state.OpenFloor},MissionProgress.SaveJson));
        int count=c.messageQueue.ObjectMessagesQueue.Count;TickIntroduction(c,p);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);
    }
    [Theory][InlineData("missing-witness")][InlineData("other-site-witness")][InlineData("owner")][InlineData("world")][InlineData("life")][InlineData("transport")]
    public void MeasuredRoomDoesNotBorrowMissingHistoricalWitnessOrStaleArrivalOwner(string change)
    {
        var c=Client();Store.currentClient=c;var p=StageMeasured(c);MeasuredArrival(c);DispatchAndAck(c);
        switch(change)
        {
            case "missing-witness":p.elevatorWitnesses.Clear();break;
            case "other-site-witness":var w=p.elevatorWitnesses[MeasuredFingerprint];p.elevatorWitnesses.Clear();p.elevatorWitnesses.Add("another site",w);break;
            case "owner":c.playerData=new ClientData();break;
            case "world":c.playerData.worldGeneration++;break;
            case "life":c.playerData.lifeGeneration++;break;
            case "transport":c.TransportFence.PublishIncoming(true,()=>{});break;
        }
        TickIntroduction(c,p);Assert.Empty(c.viewMan.Snapshot());
    }

}
