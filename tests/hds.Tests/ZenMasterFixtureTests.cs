using System.Diagnostics;
using System.Text.Json;
using NativeQa;
namespace hds.Tests;

public class ZenMasterFixtureTests
{
    [Fact]
    public async Task ReadOnlyZenProfileMatchesNativePassedLegacyFixtureWithoutSetup()
    {
        using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ZenMasterLegacyPlan.json")));
        string empty=Path.Combine(Path.GetTempPath(),"mxo-zen-plan-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var start=new ProcessStartInfo("dotnet") {WorkingDirectory=empty,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            foreach(string arg in new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"hds.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"hds.Tests.deps.json"),typeof(NativeQaFixturePlan).Assembly.Location,"--fixture-plan","zen-master"}) start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;
            Task<string> output=process.StandardOutput.ReadToEndAsync(),error=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.ExitCode==0,await error);
            using var actual=JsonDocument.Parse(await output);
            var env=actual.RootElement.GetProperty("Environment");
            foreach(var field in baseline.RootElement.GetProperty("Environment").EnumerateObject())
                Assert.Equal(field.Value.ToString(),env.GetProperty(field.Name).ToString());
            Assert.Equal("",env.GetProperty("MXO_DESPERATION_QA_HOLD_HEALTH").GetString());
            var plan=actual.RootElement.GetProperty("Plan");
            foreach(var field in baseline.RootElement.GetProperty("Plan").EnumerateObject())
            {
                if(field.Name=="ZenMasterForcedLevel") continue;
                Compare(field.Value,plan.GetProperty(field.Name));
            }
            Assert.Equal(99,plan.GetProperty("ExtraQaMetadata").GetProperty("ZenMasterForcedLevel").GetInt32());
            var state=NativeQaModes.Find("zen-master").Plan.StateWithMetadata(new {CharId=110,SourceAssemblySha256="source"});
            Assert.Equal(99,state["ZenMasterForcedLevel"]!.GetValue<int>());
            Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
        }
        finally {Directory.Delete(empty);}
    }

    private static void Compare(JsonElement expected,JsonElement actual)
    {
        if(expected.ValueKind==JsonValueKind.Array)
        {
            Assert.Equal(expected.GetArrayLength(),actual.GetArrayLength());
            for(int i=0;i<expected.GetArrayLength();i++) Compare(expected[i],actual[i]);
        }
        else Assert.Equal(expected.ToString().Replace("\r\n","\n"),actual.ToString().Replace("\r\n","\n"));
    }
}
