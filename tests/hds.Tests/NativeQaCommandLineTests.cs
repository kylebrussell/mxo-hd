using System.Diagnostics;
using NativeQa;
namespace hds.Tests;
public class NativeQaCommandLineTests
{
    [Theory][InlineData("--plan")][InlineData("--list")][InlineData("--fixture-plann")]
    public async Task UnknownLeadingOptionFailsBeforeAnyFixtureSetup(string option)
    {
        var result=await Run(option);
        Assert.Equal(2,result.Exit);Assert.Empty(result.Output);
        Assert.Contains("Unknown NativeQa option: "+option,result.Error);
    }
    [Theory][InlineData("--help")][InlineData("-h")]
    public async Task HelpPrintsSupportedCommandsWithoutSetup(string option)
    {
        var result=await Run(option);
        Assert.Equal(0,result.Exit);Assert.Empty(result.Error);
        Assert.Contains("--fixture-plan",result.Output);
        Assert.Contains("--bootstrap-checks",result.Output);
    }
    [Fact]
    public async Task InvalidRepositoryFailsBeforeCreatingTheRequestedFixtureDirectory()
    {
        var result=await Run("missing-repository","must-not-be-created","local");
        Assert.Equal(2,result.Exit);Assert.Empty(result.Output);
        Assert.Contains("repository must contain SQL/reality_hd.sql",result.Error);
    }
    static async Task<(int Exit,string Output,string Error)> Run(params string[] args)
    {
        string empty=Path.Combine(Path.GetTempPath(),"mxo-nativeqa-cli-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var start=new ProcessStartInfo("dotnet"){WorkingDirectory=empty,RedirectStandardOutput=true,
                RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            foreach(string arg in new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"hds.Tests.runtimeconfig.json"),
                "--depsfile",Path.Combine(AppContext.BaseDirectory,"hds.Tests.deps.json"),typeof(NativeQaFixturePlan).Assembly.Location}.Concat(args))start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;
            var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(true);throw;}
            Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
            return(process.ExitCode,await output,await error);
        }
        finally{if(!Directory.EnumerateFileSystemEntries(empty).Any())Directory.Delete(empty);}
    }
}
