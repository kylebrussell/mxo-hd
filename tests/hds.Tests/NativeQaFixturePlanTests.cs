using System.Diagnostics;
using System.Text.Json;
using NativeQa;

namespace hds.Tests;

public class NativeQaFixturePlanTests
{
    [Theory]
    [InlineData("ignored-actions", "1", "110")]
    [InlineData("remaining", "1", "")]
    [InlineData("stealth-shields", "1", "")]
    [InlineData("coder-pets", "1", "")]
    [InlineData("styles", "1", "")]
    [InlineData("data-mining", "1", "")]
    [InlineData("death-presentation", "1", "")]
    [InlineData("actions", "1", "")]
    [InlineData("health-callbacks", "1", "")]
    [InlineData("desperation", "1", "")]
    [InlineData("", "0", "")]
    [InlineData("unknown-mode", "0", "")]
    public async Task ReadOnlyCliReportsActualServerEnvironmentWithoutSetup(string mode, string dev, string gm)
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
            var environment = json.RootElement.GetProperty("Environment");
            Assert.Equal(dev, environment.GetProperty("MXO_DEV_COMMANDS").GetString());
            Assert.Equal(mode=="desperation"?"1":"",environment.GetProperty("MXO_DESPERATION_QA_HOLD_HEALTH").GetString());
            Assert.Equal(gm, environment.GetProperty("MXO_GM_CHARACTER_IDS").GetString());
            Assert.Equal("11000", environment.GetProperty("MXO_AUTH_PORT").GetString());
            Assert.Equal(mode == "health-callbacks" ? "0" : "", environment.GetProperty("MXO_CALLBACK_QA_ROLL15").GetString());
            Assert.Equal(mode == "health-callbacks" ? "0" : "", environment.GetProperty("MXO_CALLBACK_QA_ROLL100").GetString());
            Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
        }
        finally { Directory.Delete(empty); }
    }
}
