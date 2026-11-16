using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;
public sealed partial class MissionStreetAccessTests
{
    [Fact]public void ShippedHardTimeFurnitureAndZeroActorBound326UseOrdinaryCarrierRatherThanNpcOrFreeProxy()
    {
        var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data","missions","machines_1_2_3_hard_time.xml"));var actual=MissionXml.Parse(xml.DocumentElement!);actual.district=1;
        Assert.Equal("Furniture",Assert.Single(actual.phases[0].objects).kind);Assert.False(MissionOpenFloorAdmission.Requires(actual.phases[0]));
        Assert.Equal("Objects",MissionStreetCheckpoint.KindFor(actual,0));
        var zero=new Mission{id=201203,district=1};zero.phases.Add(new MissionPhase{site="Normal"});zero.phases[0].objects.Add(new MissionObjectDef{id=1,kind="Furniture"});
        var p=new MissionProgress(zero){streetPolicy=true};Assert.Empty(zero.npcs);Assert.Equal(0,MissionStaging.SpotsForWaves(zero.phases[0]));Assert.True(MissionStreetAdmission.Candidate(p,0));
        var source=new StaticWorldObject{metrId=1,sectorID=209,mxoStaticId=0x0d1001a2,staticId=418,type=BitConverter.GetBytes((uint)326),pos_x=30765,pos_y=1347,pos_z=14160,rot=90,quat="0000000000000000000000000000803f"};
        Assert.Equal((ushort)326,MissionStaging.ExpectedConcrete(zero.phases[0].objects[0],source));Assert.False(MissionOpenFloorAdmission.Target(326,source));
        zero.phases[0].objects.Clear();Assert.Null(MissionStreetCheckpoint.KindFor(zero,0));Assert.False(MissionStreetAdmission.Candidate(p,0)); // Empty Upload/waypoint has no interior authority.
    }
    [Fact]public void LegacyPendingAndSavedStateOmitAllNewStreetFields()
    {
        var pending=new MissionOpenFloorPendingAdmission(1,1,"old",1,123,null,Array.Empty<MissionOpenFloorChoice>(),"NoSite");
        using var old=JsonDocument.Parse(JsonSerializer.Serialize(pending,MissionProgress.SaveJson));Assert.False(old.RootElement.TryGetProperty("Street",out _));
        var native=new MissionNpcInteriorPending(2,"NpcInterior",null!,null!,"old-refusal");using var npc=JsonDocument.Parse(JsonSerializer.Serialize(native,MissionProgress.SaveJson));Assert.False(npc.RootElement.TryGetProperty("Street",out _));
        using var state=JsonDocument.Parse(JsonSerializer.Serialize(new MissionProgress.SavedState(),MissionProgress.SaveJson));Assert.False(state.RootElement.TryGetProperty("StreetAccess",out _));
    }
}
