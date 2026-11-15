using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using NativeQa;
using NativeQa.Modes;
using hds;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    private static MissionProgress.SavedState QaState()
    {
        var p=new MissionProgress(Definition()){phaseIndex=1,persistenceId="11111111111111111111111111111111",startedAt=123};var site=Site();var c=Client(p);var provider=new Provider();
        Assert.True(MissionNpcInteriorPlanning.Prepare(c,p,2,site,provider,out _,out var pending,out var why),why);p.NextPhase();p.site=site;p.sites.Add(site);p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=pending.Witness;p.entered=true;p.itemSlots[6783]=6;
        var floor=pending.Witness.Floor.Position;p.waypoints=new(){new MissionWaypoint{x=floor[0],y=floor[1],z=floor[2],doorHex=Convert.ToHexString(BitConverter.GetBytes(site.elevatorDoor.mxoStaticId))}};p.exits.Add(pending.Witness.Lobby.Position);
        var s=p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{site});var actor=pending.Selection.Actors[0].Standing;
        s.Mobs.Add(new MissionProgress.SavedMob{Identity="22222222222222222222222222222222",Npc=6,Phase=0,Name="Ophiuchi",Friendly=true,Health=500,MaxHealth=500,Level=50,Rsi="58010058",X=actor[0],Y=actor[1],Z=actor[2]});return s;
    }
    [Fact] public void QaUsesActualPersistedKeysStandingRawRoleAndIndependentOriginalTopology()
    {
        var s=QaState();var plan=MissionNpcInteriorProof.Require(s);Assert.Equal(0x22602817u,plan.Floor.Key);Assert.Equal(0x22600f04u,plan.Lobby.Key);Assert.NotEqual(s.SiteDoors[0].pos_x,plan.Floor.Position[0]);
        var route=MissionNpcInteriorProof.Route(Repo,plan);Assert.Equal(plan.Floor.Standing,route[0].Standing);Assert.Equal(new[]{s.Mobs[0].X,s.Mobs[0].Y,s.Mobs[0].Z},route[^1].Standing);Assert.Equal(125,route[0].Region);Assert.Equal(43,route[^1].Region);
        Assert.Contains(route,step=>step.Doors.Contains(0x2260056bu));Assert.Equal((ushort)346,Assert.Single(MissionNpcInteriorProof.RequiredDoors(plan)).Goid);Assert.All(route,step=>Assert.Equal(6095,step.Standing[1]));
    }
    [Theory] [InlineData("absent")] [InlineData("null")] [InlineData("owner")] [InlineData("emptyActors")] [InlineData("wirePhase")] [InlineData("waypoint")]
    public void QaRejectsUnadoptedMalformedOwnerAndStoryPhaseAsWireByte(string change)
    {
        var s=QaState();var node=JsonNode.Parse(s.NpcInterior.GetRawText())!;
        if(change=="absent")s.NpcInterior=default;
        else if(change=="null")s.NpcInterior=JsonSerializer.Deserialize<JsonElement>("null");
        else if(change=="owner")s.Identity="33333333333333333333333333333333";
        else if(change=="waypoint")s.Waypoints[0].x++;
        else {if(change=="emptyActors")node["Witnesses"]![0]!["Selection"]!["Actors"]=new JsonArray();else node["Witnesses"]![0]!["Selection"]!["Actors"]![0]!["WirePhase"]=2;s.NpcInterior=JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());}
        Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorProof.Require(s));
    }
    [Theory] [InlineData("otherFloor")] [InlineData("wrongYaw")]
    public void QaDoesNotBorrowOtherFloorOrPairedDoorYaw(string change)
    {
        var plan=MissionNpcInteriorProof.Require(QaState());var node=JsonNode.Parse(plan.Witness.GetRawText())!;
        if(change=="otherFloor")node["Actors"]![0]!["Nodes"]![1]!["Region"]=57;
        else node["Doors"]![0]!["Quaternion"]="0000000000000000000000000000803f";
        var changed=plan with{Witness=JsonSerializer.Deserialize<JsonElement>(node.ToJsonString())};Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorProof.Route(Repo,changed));
    }
    [Fact] public void ActualRegisteredQaProfileRetainsEarlierReceiptsDefaultOffAndExistingSixMinuteOuterBudget()
    {
        var scenario=Assert.Single(Scenarios.All,s=>s.Name==MissionNpcInteriorProof.Name);Assert.Equal(TimeSpan.FromMinutes(6),scenario.Timeout);Assert.Equal("201101",scenario.Environment!["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal("1",scenario.Environment["MXO_PACKET_LOG"]);
        Assert.Equal(20,InteriorAdmissionFixture.NpcInteriorRoute.Prerequisites.Length);Assert.DoesNotContain(201201,InteriorAdmissionFixture.NpcInteriorRoute.Prerequisites);
        var method=typeof(Scenarios).GetMethod("InteriorAdmissionProof",BindingFlags.Static|BindingFlags.NonPublic)!;Assert.Equal(false,method.GetParameters().Single(p=>p.Name=="verifyNpcInterior").DefaultValue);
        var mode=Assert.IsType<MissionNpcInteriorRouteMode>(NativeQaModes.Find(MissionNpcInteriorProof.Name));Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);Assert.Contains("STOP/HOLD",string.Join(" ",mode.Plan.ReadyMessages));
        Assert.Equal(InteriorAdmissionFixture.Environment(InteriorAdmissionFixture.NpcInteriorRoute),mode.Plan.EnvironmentOverrides);Assert.Equal(InteriorAdmissionFixture.Seed(InteriorAdmissionFixture.NpcInteriorRoute,110),mode.Plan.AfterHelpers[0]);
    }
    static byte[] Wallet(byte op,uint amount,uint type=2)=>new byte[]{0x80,op}.Concat(BitConverter.GetBytes(amount)).Concat(BitConverter.GetBytes(type)).ToArray();
    static List<byte[]> LiteralAwards()
    {
        var summary=new byte[26];summary[0]=0x80;summary[1]=0xa2;BitConverter.GetBytes(133350u).CopyTo(summary,11);BitConverter.GetBytes(168042u).CopyTo(summary,15);
        return new(){summary,Wallet(0xe6,168042),Wallet(0xe5,2168042),Wallet(0xe4,134350)};
    }
    [Fact] public void LiteralRewardDecoderRejectsDuplicateSummaryWrongQuoteAndUnbracketedBaseline()
    {
        var rows=LiteralAwards();MissionNpcInteriorProof.Awards(rows,2000000,1000);rows.Add(rows[0]);Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorProof.Awards(rows,2000000,1000));
        rows=LiteralAwards();rows[1]=Wallet(0xe6,168043);Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorProof.Awards(rows,2000000,1000));
        rows=LiteralAwards();rows.Insert(0,Wallet(0xe5,2000000,0));Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorProof.Awards(rows,2000000,1000));MissionNpcInteriorProof.Awards(rows,2000000,1000,new HashSet<int>{0});
    }
}
