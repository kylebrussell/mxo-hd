using System.Text.Json;
using System.Xml;
using hds;

namespace hds.Tests;

public class MissionMidphaseElevatorTests
{
    static string Root()
    {
        var at=new DirectoryInfo(AppContext.BaseDirectory);
        while(at!=null&&!File.Exists(Path.Combine(at.FullName,"tests/hds.Tests/Fixtures/MissionMidphase101208.json")))at=at.Parent;
        return at?.FullName??throw new DirectoryNotFoundException();
    }
    static (MissionSite Site,MissionNpcInteriorWitness Npc) Captured()
    {
        // This retained MySQL batch dump doubled the embedded JSON quote escapes.
        // Decode that capture framing only; production checkpoint parsing is unchanged.
        string batch=File.ReadAllText(Path.Combine(Root(),"tests/hds.Tests/Fixtures/MissionMidphase101208.json"));
        using var json=JsonDocument.Parse(batch.Replace(@"\\u0022",@"\u0022"));
        var saved=json.RootElement;
        Assert.Equal(101208,saved.GetProperty("Mission").GetInt32());
        Assert.False(saved.GetProperty("Flags").GetProperty("waitingForExit").GetBoolean());
        Assert.Equal(6,saved.GetProperty("Items").GetProperty("6831").GetInt32());
        var npc=JsonSerializer.Deserialize<MissionNpcInteriorWitness>(saved.GetProperty("NpcInterior").GetProperty("Witnesses")[0].GetRawText(),MissionProgress.SaveJson)!;
        var site=new MissionSite {metrId=1,elevatorDoor=MissionNpcInteriorPlanning.ReadRaw(npc.Floor.RawDoorJson),lobbyDoor=MissionNpcInteriorPlanning.ReadRaw(npc.Lobby.RawDoorJson)};
        site.shaft.Add(site.elevatorDoor);site.shaft.Add(site.lobbyDoor);
        return(site,npc);
    }
    [Fact]
    public void CapturedUnfinishedUploadCanRideDownAndReturnWithoutCompletingTheMission()
    {
        var (site,npc)=Captured();
        Assert.Equal(270532686u,site.elevatorDoor.mxoStaticId);Assert.Equal(270532869u,site.lobbyDoor.mxoStaticId);
        Assert.Equal(MissionElevatorDirection.Down,MissionElevatorTrips.Choose(site,site.elevatorDoor,1295,true,false,null,npc));
        Assert.Equal(MissionElevatorDirection.Up,MissionElevatorTrips.Choose(site,site.lobbyDoor,95,true,false,null,npc));
        var xml=new XmlDocument();xml.Load(Path.Combine(Root(),"data/missions/zion_1_2_8_hardlined_learning.xml"));
        var p=new MissionProgress(MissionXml.Parse(xml.DocumentElement!)){site=site,entered=true};p.sites.Add(site);
        foreach(int id in new[]{1,3,2})p.Complete(p.Phase.missionObjectives.Single(o=>o.goal==id));
        Assert.False(p.AllDone);Assert.False(p.waitingForExit);var open=p.OpenObjectives().Select(o=>o.goal).ToArray();
        Assert.Equal(new[]{4},open);
        p.CommitGeneratedFloor(0,site,site.lobbyDoor);
        Assert.True(p.HasGeneratedDeparture);Assert.Equal(0,p.phaseIndex);Assert.False(p.waitingForExit);Assert.Equal(open,p.OpenObjectives().Select(o=>o.goal));
        p.CommitGeneratedFloor(0,site,site.elevatorDoor);
        Assert.False(p.HasGeneratedDeparture);Assert.Equal(0,p.phaseIndex);Assert.False(p.waitingForExit);Assert.Equal(open,p.OpenObjectives().Select(o=>o.goal));
    }
    [Fact]
    public void ACarDoorOnAnotherFloorIsNotAnUpRideEvenWithinTheShaftRadius()
    {
        var (site,npc)=Captured();
        var other=new StaticWorldObject {metrId=1,mxoStaticId=0x10200001,pos_x=3789,pos_y=545,pos_z=13559};
        Assert.True(Math.Pow(other.pos_x-site.lobbyDoor.pos_x,2)+Math.Pow(other.pos_z-site.lobbyDoor.pos_z,2)<900*900);
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(site,other,495,true,false,null,npc));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(site,site.lobbyDoor,495,true,false,null,npc));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(site,site.elevatorDoor,495,true,false,null,npc));
    }
    [Fact]
    public void HistoricalAndCompletedSitesDoNotOfferAnotherUpRide()
    {
        var (site,npc)=Captured();
        foreach(var state in new[]{(Current:false,Waiting:false),(Current:true,Waiting:true)})
        {
            Assert.Equal(MissionElevatorDirection.Down,MissionElevatorTrips.Choose(site,site.elevatorDoor,1295,state.Current,state.Waiting,null,npc));
            Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(site,site.lobbyDoor,95,state.Current,state.Waiting,null,npc));
        }
        var foreign=new StaticWorldObject {metrId=2,mxoStaticId=site.elevatorDoor.mxoStaticId};
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(site,foreign,1295,true,false,null,npc));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(site,site.elevatorDoor,double.NaN,true,false,null,npc));
    }
    [Fact]
    public void LegacyAndFreePropWitnessesUseTheirOwnFloorKeysAndHeights()
    {
        var (site,_)=Captured();
        Assert.Equal(MissionElevatorDirection.Down,MissionElevatorTrips.Choose(site,site.elevatorDoor,1295,true,false,null,null));
        Assert.Equal(MissionElevatorDirection.Up,MissionElevatorTrips.Choose(site,site.lobbyDoor,95,true,false,null,null));
        var witness=new MissionElevatorWitness(1,"fixture",14,new[]{3700d,1745,13300},new[]{3700d,545,13300},"","","",Array.Empty<string>(),site.elevatorDoor.mxoStaticId,site.lobbyDoor.mxoStaticId,26);
        Assert.Equal(MissionElevatorDirection.Down,MissionElevatorTrips.Choose(site,site.elevatorDoor,1695,true,false,witness,null));
        Assert.Equal(MissionElevatorDirection.Up,MissionElevatorTrips.Choose(site,site.lobbyDoor,495,true,false,witness,null));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(site,site.elevatorDoor,1295,true,false,witness,null));
        site.elevatorDoor.pos_y=site.lobbyDoor.pos_y;
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(site,site.lobbyDoor,95,true,false,null,null));
    }
}
