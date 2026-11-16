using System.Text.Json;
using hds;
using HeadlessClient;
namespace hds.Tests;
public class MissionElevatorStreetTripTests
{
    static StaticWorldObject Raw(uint key,double y,ushort world=1)=>new StaticWorldObject {metrId=world,mxoStaticId=key,pos_y=y};
    static MissionNpcInteriorEntry Entry(uint key,double y,ushort world=1)=>new MissionNpcInteriorEntry(
        JsonSerializer.Serialize(Raw(key,y,world),MissionProgress.SaveJson),"trusted-saved-fixture","",new MissionNpcInteriorNode(1,1),new[]{0d,y,0d},new[]{0d,y-50,0d});
    static MissionSite Site()=>new MissionSite {metrId=1,elevatorDoor=Raw(10,1345),lobbyDoor=Raw(20,145)};
    [Fact] public void SavedStreetHeightOverridesRawAndFreePropHeight()
    {
        var s=Site();var free=new MissionElevatorWitness(1,"fixture",14,new[]{0d,1745,0d},new[]{0d,545,0d},"","","",Array.Empty<string>(),10,20,26);
        Assert.Equal(MissionElevatorDirection.Down,MissionElevatorTrips.Choose(s,s.elevatorDoor,2295,true,false,free,null,Entry(10,2345),Entry(20,945)));
        Assert.Equal(MissionElevatorDirection.Up,MissionElevatorTrips.Choose(s,s.lobbyDoor,895,true,false,free,null,Entry(10,2345),Entry(20,945)));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,s.elevatorDoor,1695,true,false,free,null,Entry(10,2345),Entry(20,945)));
    }
    [Fact] public void SavedStreetPairOverridesCapturedNpcPairWithoutChangingIt()
    {
        using var json=JsonDocument.Parse(MissionMidphaseReturnFixture.Payload);
        string raw=json.RootElement.GetProperty("NpcInterior").GetProperty("Witnesses")[0].GetRawText();
        var npc=JsonSerializer.Deserialize<MissionNpcInteriorWitness>(raw,MissionProgress.SaveJson)!;
        var s=Site();string before=JsonSerializer.Serialize(npc,MissionProgress.SaveJson);
        Assert.Equal(MissionElevatorDirection.Down,MissionElevatorTrips.Choose(s,Raw(10,0),2295,true,false,null,npc,Entry(10,2345),Entry(20,945)));
        Assert.Equal(MissionElevatorDirection.Up,MissionElevatorTrips.Choose(s,Raw(20,0),895,true,false,null,npc,Entry(10,2345),Entry(20,945)));
        Assert.Equal(before,JsonSerializer.Serialize(npc,MissionProgress.SaveJson));
    }
    [Fact] public void SavedStreetKeysAreUsedAndForeignWorldOrOtherFloorIsRejected()
    {
        var s=Site();var f=Entry(30,2345);var l=Entry(40,945);
        Assert.Equal(MissionElevatorDirection.Down,MissionElevatorTrips.Choose(s,Raw(30,0),2295,true,false,null,null,f,l));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,s.elevatorDoor,2295,true,false,null,null,f,l));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,Raw(30,0,2),2295,true,false,null,null,f,l));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,Raw(30,0),2295,true,false,null,null,Entry(30,2345,2),l));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,Raw(30,0),2545,true,false,null,null,f,l));
    }
    [Fact] public void GroundFloorAndPartialPairDoNotInventARide()
    {
        var s=Site();
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,s.lobbyDoor,95,true,false,null,null,Entry(10,145),Entry(20,145)));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,s.elevatorDoor,1295,true,false,null,null,Entry(10,1345),null));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,s.lobbyDoor,895,false,false,null,null,Entry(10,2345),Entry(20,945)));
        Assert.Equal(MissionElevatorDirection.None,MissionElevatorTrips.Choose(s,s.lobbyDoor,895,true,true,null,null,Entry(10,2345),Entry(20,945)));
    }
}
