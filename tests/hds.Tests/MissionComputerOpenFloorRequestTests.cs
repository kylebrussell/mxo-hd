using hds;
using System.Text.Json;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionComputerOpenFloorRequestTests
{
    private static StaticWorldObject Source(bool modeled=true)=>new(){metrId=1,sectorID=(ushort)(modeled?127:209),staticId=(uint)(modeled?1443:988),
        mxoStaticId=(uint)(modeled?0x07f005a3:0x0d1003dc),type=modeled?new byte[]{0x7f,5}:new byte[]{0x87,5},
        pos_x=modeled?40931:23940,pos_y=modeled?3430:2245,pos_z=modeled?-88280:15050,quat="00000000f204353f00000000f304353f"};
    [Theory][InlineData(true)][InlineData(false)]
    public void SharedSourceOnlyIndexPreservesRawComputerEvidenceAndActualModelPresence(bool modeled)
    {
        var source=Source(modeled);var options=new JsonSerializerOptions{IncludeFields=true};string before=JsonSerializer.Serialize(source,options);
        Assert.True(MissionOpenFloorAnchors.TryCurrent(source,out var anchor,out string evidence));Assert.False(string.IsNullOrWhiteSpace(evidence));
        Assert.Equal(before,JsonSerializer.Serialize(source,options));Assert.Equal(modeled?3285:2100,anchor.Y);
        Assert.True(MissionOpenFloorSourceModels.TryGet(1,MissionSites.Goid(source),out var presence,out _));
        Assert.Equal(modeled?G.SourceModelPresence.AuthoredStaticBounds:G.SourceModelPresence.Model6Absent,presence);
        Assert.True(MissionOpenFloorProfiles.TryModel(3,out var model));Assert.Equal(-25.00377655029297,model.SupportMinY);
        Assert.Equal(1225.00377655029297,model.CentreY(1200));
        Assert.True(MissionOpenFloorProfiles.TryUsable(3,out var usable));Assert.Equal(400,usable.LowerBound);Assert.True(usable.AssumesInitialClientConfiguration);
        Assert.Equal((43d,25d,40d),(usable.HalfExtents.X,usable.HalfExtents.Y,usable.HalfExtents.Z));
    }
    [Fact]
    public void SharedIndexRefusesMovedAliasedMalformedAndWrongWorldRawSources()
    {
        var source=Source();source.pos_x+=1;Assert.False(MissionOpenFloorAnchors.TryCurrent(source,out _,out _));
        source=Source();source.mxoStaticId++;Assert.False(MissionOpenFloorAnchors.TryCurrent(source,out _,out _));
        source=Source();source.metrId=2;Assert.False(MissionOpenFloorAnchors.TryCurrent(source,out _,out _));
        foreach(var type in new byte[][]{null!,Array.Empty<byte>(),new byte[]{0x7f},new byte[]{0x7f,5,1,0},new byte[]{0x7f,5,0}})
        {source=Source();source.type=type;Assert.False(MissionOpenFloorAnchors.TryCurrent(source,out _,out _));}
    }
    [Fact]
    public void ProductionHasOneSharedRequestFactoryAndOneEffectivePoseFacade()
    {
        var assembly=typeof(MissionStaging).Assembly;Assert.Null(assembly.GetType("hds.MissionComputerOpenFloorRequest"));
        Assert.Single(assembly.GetTypes().Where(t=>t.FullName=="hds.MissionObjectPoses"));Assert.NotNull(assembly.GetType("hds.MissionOpenFloorRequests"));
    }
}
