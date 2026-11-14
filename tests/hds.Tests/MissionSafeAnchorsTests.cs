using hds;
using System.Text.Json;
using System.Security.Cryptography;
namespace hds.Tests;
public class MissionSafeAnchorsTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static string Row => File.ReadLines(Path.Combine(Root,"data","mission_safe_anchors.csv")).Skip(1).Single(l=>l.StartsWith("1,209,62,"));
    private static MissionSafeAnchors Read(string? row=null)=>MissionSafeAnchors.Read(new StringReader(MissionSafeAnchors.Header+"\n"+(row??Row)+"\n"));
    private static StaticWorldObject Spot()=>new(){metrId=1,sectorID=209,staticId=62,mxoStaticId=0x0d10003e,type=new byte[]{0x82,5},pos_x=27300,pos_y=2146,pos_z=13100,quat="00000000f204353f00000000f304353f"};
    [Fact] public void Normal2201SourceResolvesImmutable552PoseAndCheckpoint()
    {
        var source=Spot();var options=new JsonSerializerOptions{IncludeFields=true};string before=JsonSerializer.Serialize(source,options);
        var restored=JsonSerializer.Deserialize<StaticWorldObject>(before,options)!;
        Assert.True(Read().TryResolve(source,552,out var a));Assert.True(Read().TryResolve(restored,552,out var b));
        Assert.Equal((29100d,2001d,14900d),(a.X,a.Y,a.Z));Assert.Equal(a.Orientation,b.Orientation);Assert.Equal(before,JsonSerializer.Serialize(source,options));
        byte[] q=a.Orientation;q[0]^=255;Assert.NotEqual(q,a.Orientation);
        var state=new MissionObjectState{goid=552,spot=source};Assert.Equal(2146,MissionObjectPoses.For(state).Y); // No unsafe native-anchor fallback without admitted metadata.
        state.goid=1416;Assert.Equal(2146,MissionObjectPoses.For(state).Y); // Old saved wrong prototype is not migrated.
    }
    [Fact] public void LiteralQuarterTurnAndPivotSampleIsIndependentOfGeneratedExpectedValues()
    {
        // Counted SIDI135, GO1410/static4460: local1060/0/50, parent pivot120/420/620, sector corner90000/0/8000.
        var source=new StaticWorldObject{metrId=1,sectorID=347,staticId=4460,mxoStaticId=0x15b0116c,type=new byte[]{0x82,5},
            pos_x=92260,pos_y=4345,pos_z=14150,quat="0000000000000000000000000000803f"};
        using var file=File.OpenText(Path.Combine(Root,"data","mission_safe_anchors.csv"));var index=MissionSafeAnchors.Read(file);
        Assert.True(index.TryResolve(source,552,out var pose));
        Assert.Equal(90000+120*10+50,pose.X);Assert.Equal(420*10,pose.Y);Assert.Equal(8000+620*10-1060,pose.Z);
        Assert.Equal("00000000F304353F00000000F304353F",Convert.ToHexString(pose.Orientation));
    }
    [Fact] public void BoundOtherWorldAliasesAndMovedCustomSpotsStayUnchanged()
    {
        var index=Read();var s=Spot();foreach(ushort g in new ushort[]{1416,1410,3,0})Assert.False(index.TryResolve(s,g,out _));
        s.metrId=2;Assert.False(index.TryResolve(s,552,out _));s=Spot();s.exterior=true;Assert.False(index.TryResolve(s,552,out _));
        s=Spot();s.mxoStaticId++;Assert.False(index.TryResolve(s,552,out _));s=Spot();s.type=new byte[]{0x82,5,1,0};Assert.False(index.TryResolve(s,552,out _));
        s=Spot();s.quat="0000000000000000000000000000803f";Assert.False(index.TryResolve(s,552,out _));
        s=Spot();s.pos_x=29100;s.pos_y=2146;s.pos_z=14900;s.quat=Row.Split(',')[8];Assert.True(index.TryResolve(s,552,out var p));Assert.Equal(2001,p.Y);
        s.pos_y=2001;Assert.False(index.TryResolve(s,552,out _));
    }
    [Theory][InlineData(0,"2")][InlineData(1,"2048")][InlineData(2,"1048576")][InlineData(3,"0")][InlineData(4,"1415")][InlineData(5,"NaN")][InlineData(8,"oops")][InlineData(13,"0")][InlineData(14,"-1")][InlineData(15,"-1")]
    public void BadOrUnprovenRowsRefuseWholeArtifact(int field,string value)
    {var f=Row.Split(',');f[field]=value;Assert.ThrowsAny<Exception>(()=>Read(string.Join(',',f)));}
    [Fact] public void ArtifactCountsHashAndOmissionsAreExact()
    {
        string path=Path.Combine(Root,"data","mission_safe_anchors.csv");using var reader=File.OpenText(path);Assert.Equal(4650,MissionSafeAnchors.Read(reader).Count);
        Assert.Throws<InvalidDataException>(()=>Read(Row+"\n"+Row));Assert.Throws<InvalidDataException>(()=>Read("1,209"));
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","mission-safe-anchors-manifest.json")));
        Assert.Equal(doc.RootElement.GetProperty("csvSha256").GetString(),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        Assert.False(doc.RootElement.TryGetProperty("readerSha256",out _));
        Assert.Equal("efcc4d721c2c41ef29f2dc7afb5a8ff2d1aee5fd7e2e8e4fbb4e077834d2dac4",doc.RootElement.GetProperty("normalizedReaderPrefixSha256").GetString());
        Assert.Contains("unused numeric anchors",doc.RootElement.GetProperty("readerFingerprintScope").GetString());
        Assert.DoesNotContain(File.ReadLines(path).Skip(1),l=>l.Split(',')[1] is "556" or "1139");
    }
    [Fact] public void ExistingClass46OpenAndMissionContainerContractNeedsNoNewExecutor()
    {
        Assert.True(MissionStaging.IsClickable(552));Assert.False(MissionStaging.IsComputer(552));ObjectStates.Load(Path.Combine(Root,"data"));
        Assert.Equal(0x835u,ObjectStates.DefaultState(552));Assert.Equal((byte)3,ObjectStates.UseNumber(552,"Open"));Assert.Contains((byte)3,ObjectStates.Offered(552,0x835));
        Assert.Equal(0x834u,ObjectStates.StateAfterUse(552,0x835,3));Assert.Equal(100,ObjectStates.FindUsable(552,3).maxDistance);
    }
    [Fact] public void NormalSafeApproachIsDirectlyOnSourceFloorWithoutPoseOrReachAdjustment()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        Assert.True(NavMesh.Locate(1,29100,2095,14780,out var nav));Assert.Equal(2000,nav.floorY,2);
        Assert.True(NavMesh.Project(1,29100,2095,14780,out var p,reachableFrom:new[]{29100d,2095d,14780d},requireDirect:true));
        Assert.Equal(29100,p.x,2);Assert.Equal(2095,p.y,2);Assert.Equal(14780,p.z,2);
    }
    [Fact] public void NativeModeOnlySeedsLegitimatePrerequisitesAndLayout()
    {
        var mode=NativeQa.NativeQaModes.Find("mission-safe-anchors");Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Equal("220110",mode.Plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.DoesNotContain(mode.Plan.AfterHelpers,x=>x.Contains("2201,"));
        Assert.NotNull(HeadlessClient.Scenarios.All.Single(s=>s.Name==mode.Name));
    }
}
