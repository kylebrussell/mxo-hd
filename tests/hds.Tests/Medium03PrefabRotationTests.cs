using System.Diagnostics;
using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
public class Medium03PrefabRotationTests
{
    private static string Root { get {var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact] public void RestoredDoorHasExactIndependentPrimaryPoseAndFullQuaternion()
    {
        var f=NativeWorldDoorFixtures.Medium03Prefab;
        Assert.Equal(580,f.Goid);Assert.Equal(33,f.Sector);Assert.Equal(1380,f.LocalId);
        Assert.Equal("00000000f30435b300000000ffff7f3f",f.ExpectedQuaternion);
        var row=Assert.Single(File.ReadLines(Path.Combine(Root,"data/staticObjects_medium03.csv")).Skip(1).Select(l=>l.Split(',')),r=>r[2]=="64051002");
        Assert.Equal(new[]{"25300.0","2545.0","43200.0"},row[6..9]);Assert.Equal(f.ExpectedQuaternion,row[10]);
        Assert.Equal(typeof(AttributeClass364),typeof(GameObjectDefinitions).GetField("Object580")!.FieldType);
        ObjectStates.Load(Path.Combine(Root,"data"));Assert.Equal(4004u,ObjectStates.FindUsable(580,3).executeRule);
        Assert.Equal(0x835u,ObjectStates.DefaultState(580));Assert.Equal(20u,ObjectStates.Find(580,0x834).seconds);
        NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));Assert.True(NavMesh.Locate(16,f.X,f.Y,f.Z,out var nav));
        Assert.Equal(33,nav.sector);Assert.True(nav.indoors);Assert.Equal(2400,nav.floorY);
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/medium03-prefab-door.json")));
        Assert.Equal("0x6800001a",source.RootElement.GetProperty("modelDescriptionId").GetString());
        Assert.EndsWith("sl_push_knob_fadedgreen02.moa",source.RootElement.GetProperty("modelPath").GetString());
        Assert.True(source.RootElement.GetProperty("modelBytes").GetInt32()>0);
        Assert.True(source.RootElement.GetProperty("nativeRenderingPending").GetBoolean());
    }
    [Fact] public void RolloutRetainsEveryHistoricalRawReferenceAndAmbiguousKeyVariant()
    {
        using var audit=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/medium03-static-audit.json")));
        var a=audit.RootElement;Assert.Equal(4491,a.GetProperty("exportedKeys").GetInt32());
        Assert.Equal(134,a.GetProperty("excludedKeys").GetArrayLength());Assert.Equal(4762,a.GetProperty("raw").GetArrayLength());
        var historical=a.GetProperty("historicallyUnprovenRotationKeys").EnumerateArray().Select(r=>(r[0].GetInt32(),r[1].GetInt32())).ToHashSet();
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(r=>(r[0].GetInt32(),r[1].GetInt32())).ToHashSet();
        Assert.Contains((33,1380),historical);Assert.DoesNotContain((33,1380),excluded);
        var rows=File.ReadLines(Path.Combine(Root,"data/staticObjects_medium03.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        var keys=rows.Select(r=>(int.Parse(r[1]),(int)BitConverter.ToUInt32(Convert.FromHexString(r[3])))).ToHashSet();
        Assert.Equal(3209,keys.Count(historical.Contains));Assert.Empty(keys.Intersect(excluded));
        Assert.All(a.GetProperty("nativePrefabRotations").GetProperty("placements").EnumerateArray(),p=>
        {Assert.Equal("direct-sector",p.GetProperty("parent").GetString());Assert.InRange(p.GetProperty("rotation").GetInt32(),0,3);Assert.Equal(64,p.GetProperty("resourceSha256").GetString()!.Length);});
    }
    [Fact] public void NativeModeAddsNoAuthorityAndPreservesExistingDoorFixtures()
    {
        var f=NativeWorldDoorFixtures.Medium03Prefab;var p=NativeQa.NativeQaModes.Find(f.Mode).Plan;
        Assert.Equal(f.X,p.X);Assert.Equal(f.Z,p.Z);Assert.Contains("y=2495,rotation=0",Assert.Single(p.AfterLoadout));
        Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Null(NativeWorldDoorFixtures.Large01.ExpectedQuaternion);Assert.Null(NativeWorldDoorFixtures.Large02.ExpectedQuaternion);
    }
    [Fact] public void PythonGoldenTestsExerciseActualNativeMathWithoutClientAssets()
    {
        var start=new ProcessStartInfo(OperatingSystem.IsWindows()?"python":"python3")
        {WorkingDirectory=Root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        start.ArgumentList.Add("research/world-data/test_native_prefab_transform.py");
        using var process=Process.Start(start)!;string output=process.StandardOutput.ReadToEnd()+process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(15000),"Native transform Python tests timed out");Assert.True(process.ExitCode==0,output);
    }
}
