using System.Text;
using System.Text.Json;
using hds;
namespace hds.Tests;
public class MissionOpenFloorSceneTests
{
    private static string Json(object[][]? rows=null,bool complete=true,bool all=true,object[]? unsupported=null)=>JsonSerializer.Serialize(new{format=1,world=1,sector=127,coverage=new[]{0,200,0,200},complete,allHeights=all,unsupportedLayers=unsupported??Array.Empty<object>(),profiles=new{go="g1",osid="o1",sid8="s1",epi="e1",ipi="i1"},rows=rows??new[]{new object[]{"go:a",(uint)1,(ushort)1407,10d,-50d,10d,20d,100d,20d,new string('a',64)}}});
    private static MissionOpenFloorResolver.StaticScene Read(string text,ushort sector=127){using var stream=new MemoryStream(Encoding.UTF8.GetBytes(text));return MissionOpenFloorScenes.Read(stream,1,sector);}
    [Fact]
    public void CompactRowsRemainImmutableSortedAllHeightAndDomainScoped()
    {
        var scene=Read(Json());Assert.True(scene.Complete);Assert.Equal(-50,scene.Obstacles[0].MinY);Assert.Equal((ushort)1407,scene.Obstacles[0].Goid!.Value);Assert.Equal((uint)1,scene.Obstacles[0].Key!.Value);
        Assert.Equal(scene.Fingerprint,Read(Json()).Fingerprint);Assert.Throws<System.IO.InvalidDataException>(()=>Read(Json(),209));
    }
    [Theory][InlineData(false,true)][InlineData(true,false)]
    public void MissingCoverageCannotBecomeAnEmptyClearScene(bool complete,bool all)=>Assert.Throws<System.IO.InvalidDataException>(()=>Read(Json(complete:complete,all:all)));
    [Fact]
    public void DeclaredMissingLayerAndDuplicateVariantIdentityRefuse()
    {
        Assert.Throws<System.IO.InvalidDataException>(()=>Read(Json(unsupported:new object[]{"556:SIDI"})));
        var row=new object[]{"x",null!,null!,10d,0d,10d,20d,0d,20d,new string('b',64)};
        Assert.Throws<System.IO.InvalidDataException>(()=>Read(Json(new[]{row,row})));
    }
    [Fact]
    public void ZeroThicknessAxesAndZeroHeightAreClosedObstaclesWithOnlyNumericalGuard()
    {
        var row=new object[]{"sid8:plane",null!,null!,32768d,0d,-32768d,32768d,0d,-32768d,new string('c',64)};
        var obstacle=Assert.Single(Read(Json(new[]{row})).Obstacles);Assert.Null(obstacle.Key);Assert.Null(obstacle.Goid);Assert.Equal(0,obstacle.MinY);Assert.Equal(0,obstacle.MaxY);
        Assert.True(obstacle.Footprint.Min(p=>p.X)<32768&&obstacle.Footprint.Max(p=>p.X)>32768);Assert.True(obstacle.Footprint.Min(p=>p.Z)<-32768&&obstacle.Footprint.Max(p=>p.Z)>-32768);
        Assert.InRange(obstacle.Footprint.Max(p=>p.X)-32768,0.0078125,0.0078125);Assert.Contains("zero-XZ-native-ULP-v1",obstacle.Evidence);
    }
}
