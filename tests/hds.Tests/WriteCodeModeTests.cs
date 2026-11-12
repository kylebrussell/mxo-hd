using System.Diagnostics;
using System.Text.Json;
using NativeQa;
namespace hds.Tests;
public class WriteCodeModeTests
{
    [Fact]
    public async Task ReadOnlyWriterPlanMatchesCapturedLegacySelectionAndSql()
    {
        string empty=Path.Combine(Path.GetTempPath(),"mxo-writer-plan-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var start=new ProcessStartInfo("dotnet") {WorkingDirectory=empty,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            foreach(string argument in new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"hds.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"hds.Tests.deps.json"),typeof(NativeQaFixturePlan).Assembly.Location,"--fixture-plan","write-code"}) start.ArgumentList.Add(argument);
            using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(process.ExitCode==0,await errors);
            using var actual=JsonDocument.Parse(await output);
            using var expected=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"NativeQaWriteCodeLegacyPlan.json")));
            Compare(expected.RootElement,actual.RootElement);
            Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
        }
        finally {Directory.Delete(empty);}
    }
    private static void Compare(JsonElement expected,JsonElement actual)
    {
        if(expected.ValueKind==JsonValueKind.Object)
            foreach(var property in expected.EnumerateObject())
                Compare(property.Value,property.Name=="WriteCodeQa" ? actual.GetProperty("ExtraQaMetadata").GetProperty(property.Name) : actual.GetProperty(property.Name));
        else if(expected.ValueKind==JsonValueKind.Array)
        {
            Assert.Equal(expected.GetArrayLength(),actual.GetArrayLength());
            for(int i=0;i<expected.GetArrayLength();i++) Compare(expected[i],actual[i]);
        }
        else Assert.Equal(expected.ToString().Replace("\r\n","\n"),actual.ToString().Replace("\r\n","\n"));
    }
    [Fact]
    public void WriterMetadataAndPermissionsStayScopedToItsDiscoveredMode()
    {
        foreach(string mode in NativeQaModes.Names.Concat(new[]{"unknown-mode"}))
        {
            var plan=NativeQaModes.Find(mode).Plan;
            Assert.Equal(mode=="write-code",plan.ExtraQaMetadata.TryGetValue("WriteCodeQa",out var writer)&&writer.GetBoolean());
            Assert.Equal(mode=="ignored-actions",plan.GmCharacter110);
            Assert.Equal(mode is "health-callbacks" or "zen-master",plan.FixedCallbackRolls);
            Assert.Equal(mode=="desperation"?"1":"",NativeQaFixturePlan.EnvironmentFor(mode)["MXO_DESPERATION_QA_HOLD_HEALTH"]);
        }
        var writerPlan=NativeQaModes.Find("write-code").Plan;
        Assert.True(writerPlan.DevCommands);Assert.False(writerPlan.SeedDisguiseTool);Assert.False(writerPlan.CompileItemsQa);
        Assert.True(writerPlan.StateWithMetadata(new { BuildCommit="test", SourceAssemblySha256="source", SnapshotAssemblySha256="source" })["WriteCodeQa"]!.GetValue<bool>());
        Assert.Throws<NotSupportedException>(()=>((IDictionary<string,JsonElement>)writerPlan.ExtraQaMetadata).Add("Other",JsonSerializer.SerializeToElement(true)));
    }
}
