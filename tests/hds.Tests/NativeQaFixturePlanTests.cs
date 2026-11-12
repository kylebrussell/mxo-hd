using System.Diagnostics;
using System.Text.Json;
using NativeQa;

namespace hds.Tests;

public class NativeQaFixturePlanTests
{
    [Theory]
    [InlineData("remaining", true)]
    [InlineData("health-callbacks", true)]
    [InlineData("compile-items", false)]
    [InlineData("actions", false)]
    [InlineData("write-code", false)]
    public void CompilerFixturesKeepTheirActionTools(string mode, bool disguise)
    {
        Assert.Equal(disguise, NativeQaFixturePlan.SeedDisguiseTool(mode));
        Assert.Equal(2399u, hds.CraftingRequests.SourceItem(NativeQaFixturePlan.CompileArchiveValue | 1));
        Assert.False(hds.CodeStorage.IsCode(NativeQaFixturePlan.CompileArchiveValue));
    }

    public static IEnumerable<object[]> LegacyModes()
    {
        using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"NativeQaLegacyPlans.json")));
        return baseline.RootElement.EnumerateObject().Select(p=>new object[]{p.Name}).ToArray();
    }

    [Theory]
    [MemberData(nameof(LegacyModes))]
    public async Task ReadOnlyCliReportsActualServerEnvironmentWithoutSetup(string mode)
    {
        string empty = Path.Combine(Path.GetTempPath(), "mxo-fixture-plan-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory=empty, RedirectStandardOutput=true,
                RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
            foreach (string argument in new[] { "exec", "--runtimeconfig", Path.Combine(AppContext.BaseDirectory,"hds.Tests.runtimeconfig.json"),
                "--depsfile", Path.Combine(AppContext.BaseDirectory,"hds.Tests.deps.json"), typeof(NativeQaFixturePlan).Assembly.Location,
                "--fixture-plan", mode }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync(), errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(true); throw; }
            Assert.True(process.ExitCode==0,await errors);
            using var json = JsonDocument.Parse(await output);
            using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"NativeQaLegacyPlans.json")));
            var expected=baseline.RootElement.GetProperty(mode);
            Compare(expected.GetProperty("Environment"),json.RootElement.GetProperty("Environment"));
            Compare(expected.GetProperty("Plan"),json.RootElement.GetProperty("Plan"));
            Compare(expected.GetProperty("SeedDisguiseTool"),json.RootElement.GetProperty("SeedDisguiseTool"));
            Compare(expected.GetProperty("CompileArchiveValue"),json.RootElement.GetProperty("CompileArchiveValue"));
            Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
        }
        finally { Directory.Delete(empty); }
    }

    private static void Compare(JsonElement expected,JsonElement actual)
    {
        if(expected.ValueKind==JsonValueKind.Object)
            foreach(var p in expected.EnumerateObject()) Compare(p.Value,actual.GetProperty(p.Name));
        else if(expected.ValueKind==JsonValueKind.Array)
        {
            Assert.Equal(expected.GetArrayLength(),actual.GetArrayLength());
            for(int i=0;i<expected.GetArrayLength();i++) Compare(expected[i],actual[i]);
        }
        // C# multiline SQL literals use the checkout's line endings on Windows/Linux.
        else Assert.Equal(expected.ToString().Replace("\r\n","\n"),actual.ToString().Replace("\r\n","\n"));
    }

    [Fact]
    public void DiscoveredModesKeepPrivilegesScopedAndPlansImmutable()
    {
        // Future mode files need no shared registration or baseline-test list edit.
        foreach(string mode in LegacyModes().Select(row=>(string)row[0]).Where(name=>name!="unknown-mode"))
        {
            Assert.Contains(mode,NativeQaModes.Names);
            var plan=NativeQaModes.Find(mode).Plan;
            Assert.Equal(mode=="ignored-actions",plan.GmCharacter110);
            Assert.Equal(mode=="desperation"?"1":"",NativeQaFixturePlan.EnvironmentFor(mode)["MXO_DESPERATION_QA_HOLD_HEALTH"]);
            Assert.Throws<NotSupportedException>(()=>((IDictionary<string,string>)plan.EnvironmentOverrides).Add("qa","1"));
            Assert.Equal(mode=="health-callbacks",plan.FixedCallbackRolls);
            Assert.Equal(mode=="stealth-shields",plan.Bootstrap=="stealth-shield-217");
            Assert.Throws<NotSupportedException>(()=>((IList<ushort>)plan.Abilities)[0]=999);
        }
        Assert.Same(NativeQaModes.Find("").Plan,NativeQaModes.Find("unknown-mode").Plan);
    }
}
