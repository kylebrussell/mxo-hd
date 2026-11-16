using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using HeadlessClient;
namespace hds.Tests;
public class ZionAfter1212StreetEntryTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static MissionProgress.SavedState State()
    {
        uint floor=(209u<<20)|100u,lobby=(209u<<20)|101u;
        var raw=new StaticWorldObject{metrId=1,sectorID=209,staticId=100,mxoStaticId=floor,type=BitConverter.GetBytes((ushort)417),pos_x=1,pos_y=2,pos_z=3};
        var other=new StaticWorldObject{metrId=1,sectorID=209,staticId=101,mxoStaticId=lobby,type=BitConverter.GetBytes((ushort)417),pos_x=1,pos_y=1,pos_z=3};
        var site=new MissionOpenFloorSite(1,floor,1,2,3,"site");
        object Entry(StaticWorldObject r,double y)=>new{RawDoorJson=JsonSerializer.Serialize(r,Fields),EvidenceDigest=new string('A',64),Quaternion="0000000000000000000000000000803F",Node=new{Region=47,Polygon=1},Position=new[]{10d,y,30d},Standing=new[]{10d,y-50,30d}};
        var state=new MissionProgress.SavedState{Version=2,Identity="11111111111111111111111111111111",Started=123,Mission=301203,Phase=2,SiteDoors=new[]{raw},OpenFloorSites=new[]{site},OpenFloorCurrentSiteIndex=0,Waypoints=new[]{new MissionWaypoint{doorHex=Convert.ToHexString(BitConverter.GetBytes(floor)),x=10,y=2145,z=30}},Exits=new[]{new[]{10d,145,30d}}};
        state.StreetAccess=JsonSerializer.SerializeToElement(new{Version=1,Kind="StreetAccess",Enabled=true,Witnesses=new[]{new{Version=1,Mission=state.Mission,Identity=state.Identity,Started=state.Started,Phase=2,Site=site,Kind="Objects",Refusal=(string?)null,Context=new{Kind="Objects",Refusal=(string?)null,Floor=Entry(raw,2145),Lobby=Entry(other,145)}}}});
        return state;
    }
    [Fact] public void BoundOnlySavedStreetEntryUsesExactStandingAndKeyRatherThanCsv()
    {
        var s=State();string before=s.StreetAccess.GetRawText();var p=ZionAfter1212StreetEntry.ForSite(s)!;
        Assert.Equal(new[]{10d,2095,30d},p.Floor.Standing);Assert.Equal(new[]{10d,95,30d},p.Lobby.Standing);
        var ride=ZionAfter1212RidePlan.Select(s,true,Array.Empty<string>());Assert.NotNull(ride.Street);Assert.True(ride.Measured);Assert.Equal(p.Lobby.Key,ride.Key);Assert.Equal(p.Lobby.Standing,ride.Approach);
        Assert.Equal(before,s.StreetAccess.GetRawText());
    }
    [Theory] [InlineData("owner")] [InlineData("future")] [InlineData("world")] [InlineData("pose")] [InlineData("site")] [InlineData("quaternion")]
    public void InvalidSavedContextNeverFallsBackToLegacy(string fault)
    {
        var s=State();var n=JsonNode.Parse(s.StreetAccess.GetRawText())!;var w=n["Witnesses"]![0]!;
        if(fault=="owner")w["Identity"]="foreign";
        if(fault=="future")w["Phase"]=3;
        if(fault=="world")w["Site"]!["District"]=2;
        if(fault=="site")w["Site"]!["DoorX"]=999;
        if(fault=="pose")w["Context"]!["Floor"]!["Position"]![0]=999;
        if(fault=="quaternion")w["Context"]!["Floor"]!["Quaternion"]="00";
        using var d=JsonDocument.Parse(n.ToJsonString());s.StreetAccess=d.RootElement.Clone();Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.Select(s,true,Array.Empty<string>()));
    }
    [Fact] public void OldUnenrolledStateHasNoSyntheticStreetContext(){var s=State();s.StreetAccess=default;Assert.Null(ZionAfter1212StreetEntry.ForSite(s));}
}
