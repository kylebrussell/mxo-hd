using hds;
using System.Text.Json;
using System.Security.Cryptography;

namespace hds.Tests;

public class MissionComputerAnchorsTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private const string Row="1,347,4470,363860342,1415,91865,4300,12660,00000000f304353300000000ffff7f3f,92740,4445,13535,00000000f20435bf00000000f304353f,939577148,135,1362365";
    private static MissionComputerAnchors Read(string body=Row)=>MissionComputerAnchors.Read(new StringReader(MissionComputerAnchors.Header+"\n"+body+"\n"));
    private static StaticWorldObject Spot()=>new(){metrId=1,sectorID=347,staticId=4470,mxoStaticId=363860342,type=new byte[]{0x87,5},pos_x=92740,pos_y=4445,pos_z=13535,quat="00000000f20435bf00000000f304353f"};
    [Fact] public void ExactLegacyAndRestoredCheckpointResolveWithoutMutatingSource()
    {
        var source=Spot();var options=new JsonSerializerOptions{IncludeFields=true};string before=JsonSerializer.Serialize(source,options);
        var restored=JsonSerializer.Deserialize<StaticWorldObject>(before,options)!;
        var index=Read();Assert.True(index.TryResolve(source,3,out var a));Assert.True(index.TryResolve(restored,3,out var b));
        Assert.Equal((91865d,4300d,12660d),(a.X,a.Y,a.Z));Assert.Equal(a.Orientation,b.Orientation);Assert.Equal(before,JsonSerializer.Serialize(source,options));
        byte[] q=a.Orientation;q[0]=99;Assert.Equal(0,a.Orientation[0]);
    }
    [Fact] public void CanonicalInputRetainsPlus145ButResultIsNativeAndDoesNotDoubleOffset()
    {
        var source=Spot();source.pos_x=91865;source.pos_y=4445;source.pos_z=12660;source.quat="00000000f304353300000000ffff7f3f";
        Assert.True(Read().TryResolve(source,3,out var pose));Assert.Equal(4300,pose.Y);
        source.pos_y=4300;Assert.False(Read().TryResolve(source,3,out _));
    }
    [Fact] public void MovedCustomPoseWithRetainedIdentityDoesNotReanchor()
    {
        var source=Spot();source.pos_x+=1;Assert.False(Read().TryResolve(source,3,out _));
        source=Spot();source.pos_y=4325;Assert.False(Read().TryResolve(source,3,out _));
        source=Spot();source.quat="0000000000000000000000000000803f";Assert.False(Read().TryResolve(source,3,out _));
    }
    [Fact] public void BoundOtherWorldTypeZeroAndIdentityAliasesRefuse()
    {
        var index=Read();var s=Spot();Assert.False(index.TryResolve(s,1415,out _));Assert.False(index.TryResolve(s,357,out _));
        s.metrId=2;Assert.False(index.TryResolve(s,3,out _));s=Spot();s.exterior=true;Assert.False(index.TryResolve(s,3,out _));
        s=Spot();s.type=new byte[4];Assert.False(index.TryResolve(s,3,out _));s=Spot();s.mxoStaticId+=1;Assert.False(index.TryResolve(s,3,out _));
        s=Spot();s.staticId+=1;Assert.False(index.TryResolve(s,3,out _));s=Spot();s.type=new byte[]{0x87,5,1,0};Assert.False(index.TryResolve(s,3,out _));
    }
    [Theory]
    [InlineData(0,"2")][InlineData(1,"2048")][InlineData(2,"1048576")][InlineData(3,"4294967295")][InlineData(4,"1407")]
    [InlineData(5,"NaN")][InlineData(8,"0000000000000000000000000000807f")][InlineData(12,"oops")][InlineData(13,"0")][InlineData(14,"-1")][InlineData(15,"-1")]
    public void MalformedOrUnprovenRowsRefuseAllOrNothing(int field,string value)
    {
        string[] f=Row.Split(',');f[field]=value;Assert.ThrowsAny<Exception>(()=>Read(string.Join(',',f)));
    }
    [Fact] public void DuplicateTruncatedAndUnknownFormatRefuse()
    {
        Assert.Throws<InvalidDataException>(()=>Read(Row+"\n"+Row));Assert.Throws<InvalidDataException>(()=>Read("1,347"));
        Assert.Throws<InvalidDataException>(()=>MissionComputerAnchors.Read(new StringReader("unknown\n"+Row)));
    }
    [Fact] public void CompleteCommittedArtifactMatchesManifestAndExcludesUnavailableSector()
    {
        string path=Path.Combine(Root,"data","mission_computer_anchors.csv");using var reader=File.OpenText(path);var index=MissionComputerAnchors.Read(reader);
        Assert.Equal(47365,index.Count);Assert.True(index.TryResolve(Spot(),3,out var pose));Assert.Equal(91865,pose.X);
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","mission-computer-anchors-manifest.json")));
        Assert.Equal(manifest.RootElement.GetProperty("csvSha256").GetString(),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        var rows=File.ReadLines(path).Skip(1).Select(r=>r.Split(',')).ToArray();Assert.DoesNotContain(rows,r=>r[1]=="1139"||r[1]=="556");
    }

    [Theory][InlineData(null)][InlineData("not-a-quaternion")]
    public void PositionOnlyFallbackDoesNotParseCustomQuaternion(string? quaternion)
    {
        var state=new MissionObjectState { goid=3,spot=new StaticWorldObject {pos_x=100,pos_y=200,pos_z=300,type=new byte[4],quat=quaternion!} };
        var pose=MissionComputerAnchors.For(state);Assert.Equal((100d,200d,300d),(pose.X,pose.Y,pose.Z));
        var progress=new MissionProgress(new Mission());progress.objects[1]=state;
        var point=MissionRuntime.ArrivalSpot(null!,progress,new MissionArrival{at="object:1"},0,0,0,0);
        Assert.Equal(200,point[1]);Assert.Equal(100+300*Math.Cos(.6),point[0]);
        var near=typeof(MissionRuntime).GetMethods(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Single(m=>m.Name=="Near"&&m.GetParameters().Length==7&&m.GetParameters()[2].ParameterType==typeof(string));
        Assert.True((bool)near.Invoke(null,new object?[]{null,progress,"object:1",1d,100d,200d,300d})!);
    }
    [Fact] public void DefaultConsumersUseRecoveredPoseAndPreserveSiteFloorAndCheckpoint()
    {
        var spot=Spot();var state=new MissionObjectState{goid=3,spot=spot};var progress=new MissionProgress(new Mission()){site=new MissionSite{floorY=4345}};progress.objects[1]=state;
        var pose=MissionComputerAnchors.For(state);Assert.Equal((91865d,4300d,12660d),(pose.X,pose.Y,pose.Z));
        var arrival=MissionRuntime.ArrivalSpot(null!,progress,new MissionArrival{at="object:1"},0,0,0,0);
        Assert.Equal(91865+300*Math.Cos(.6),arrival[0]);Assert.Equal(4295,arrival[1]);Assert.Equal(12660+300*Math.Sin(.6),arrival[2]);
        Assert.Equal((92740d,4445d,13535d),(spot.pos_x,spot.pos_y,spot.pos_z));
        var near=typeof(MissionRuntime).GetMethods(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Single(m=>m.Name=="Near"&&m.GetParameters().Length==7&&m.GetParameters()[2].ParameterType==typeof(string));
        Assert.True((bool)near.Invoke(null,new object?[]{null,progress,"object:1",100d,91865d,4295d,12740d})!);
        Assert.False((bool)near.Invoke(null,new object?[]{null,progress,"object:1",100d,92740d,4295d,13535d})!);
    }
    [Fact] public void NativeModeUsesDefaultResolverWithoutProbeOrRewardBypass()
    {
        var mode=NativeQa.NativeQaModes.Find("mission-computer-anchors");Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Equal(HeadlessClient.NeighbourhoodMissionFixture.Abilities,mode.Plan.Abilities);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor(mode.Name);Assert.Equal("3072",env["MXO_MISSION_SITE_QA_SEED"]);
        Assert.DoesNotContain(env,p=>p.Key.Contains("POSE",StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrEmpty(p.Value));
        Assert.NotNull(HeadlessClient.Scenarios.All.Single(s=>s.Name==mode.Name));
    }
}
