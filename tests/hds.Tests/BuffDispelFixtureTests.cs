using System.Diagnostics;
using System.Text.Json;
using System.Runtime.CompilerServices;
using hds;
using NativeQa;
using HeadlessClient;
namespace hds.Tests;

[CollectionDefinition("BuffDispelQaEnvironment",DisableParallelization=true)]
public class BuffDispelQaEnvironmentCollection {}
[Collection("BuffDispelQaEnvironment")]
public class BuffDispelFixtureTests
{
    private static void WithEnvironment(string? dev,string? database,string? roll,Action test)
    {
        string[] names={"MXO_DEV_COMMANDS","MXO_DB_NAME","MXO_CALLBACK_QA_ROLL100"};
        var old=names.Select(Environment.GetEnvironmentVariable).ToArray();
        try {Environment.SetEnvironmentVariable(names[0],dev);Environment.SetEnvironmentVariable(names[1],database);Environment.SetEnvironmentVariable(names[2],roll);test();}
        finally {for(int i=0;i<names.Length;i++)Environment.SetEnvironmentVariable(names[i],old[i]);}
    }
    [Theory]
    [InlineData("0","mxotest_dispel","0")]
    [InlineData("true","mxotest_dispel","0")]
    [InlineData(null,"mxotest_dispel","0")]
    [InlineData("1","reality_hd","0")]
    [InlineData("1",null,"0")]
    [InlineData("1","MXOTEST_dispel","0")]
    [InlineData("1","mxotest_dispel","-1")]
    [InlineData("1","mxotest_dispel","100")]
    [InlineData("1","mxotest_dispel","bad")]
    [InlineData("1","mxotest_dispel",null)]
    public void QaRandomIsAbsentForEveryNonDisposableOrNonExactDevGuard(string? dev,string? database,string? roll) =>
        WithEnvironment(dev,database,roll,()=>Assert.Null(AbilityHealthCallbacks.TermRandom()));

    [Theory]
    [InlineData("0",0.0)]
    [InlineData("20",0.2)]
    [InlineData("99",0.99)]
    public void ExistingGuardedFixtureRandomProvidesOnlyRequestedPercent(string roll,double expected) =>
        WithEnvironment("1","mxotest_dispel",roll,()=>Assert.Equal(expected,AbilityHealthCallbacks.TermRandom()!.NextDouble()));

    private sealed class MissRandom:Random {public override double NextDouble()=>0.99;}
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)10000);c.playerInstance.MaxHealth.setValue((ushort)10000);
        c.playerInstance.InnerStrengthMax.setValue((ushort)2000);c.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }
    [Fact]
    public void ExplicitCallerRandomStaysAuthoritativeAndOnlyNullUsesQaFixture()
    {
        WithEnvironment("1","mxotest_dispel","0",()=>
        {
            var caster=Client();var target=Client();
            Assert.True(AbilityBuffs.Apply(target,49));Assert.True(AbilityBuffs.Apply(target,327));
            AbilityHostile.LandedOnPlayer(caster,target,261,new MissRandom());
            Assert.True(AbilityBuffs.IsActive(target,49));
            AbilityHostile.LandedOnPlayer(caster,target,261);
            Assert.False(AbilityBuffs.IsActive(target,49));Assert.True(AbilityBuffs.IsActive(target,327));
        });
    }

    [Theory]
    [InlineData("buff-dispel",0)]
    [InlineData("buff-dispel-miss",99)]
    public async Task DiscoveredProfileReportsActualActorHelperLoadoutAndScopedRoll(string mode,int roll)
    {
        var profile=NativeQaModes.Find(mode).Plan;
        Assert.Equal(BuffDispelFixture.Abilities,profile.Abilities);
        Assert.Equal(133,AbilityCatalog.IdOf((uint)AbilityCatalog.Find(261)!.Number("Ability/CastRequiredCombatStance")));
        foreach(ushort id in new ushort[]{133,261,49,327,42,420})Assert.Contains(id,profile.Abilities);
        Assert.Contains("110,6111,6112,6113,6114",Assert.Single(profile.AfterHelpers));
        Assert.Contains("pvpflag=1",Assert.Single(profile.AfterHelpers));
        Assert.Contains("WHEN charId=6114 THEN 2 ELSE 1",Assert.Single(profile.AfterHelpers));
        Assert.False(profile.GmCharacter110);Assert.False(profile.HealthCallbackQa);Assert.Equal("buff-dispel-records",profile.Bootstrap);
        string empty=Path.Combine(Path.GetTempPath(),"mxo-dispel-readonly-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(empty);
        try
        {
            var start=new ProcessStartInfo("dotnet"){WorkingDirectory=empty,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            foreach(string arg in new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"hds.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"hds.Tests.deps.json"),typeof(NativeQaFixturePlan).Assembly.Location,"--fixture-plan",mode})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));await process.WaitForExitAsync(timeout.Token);Assert.True(process.ExitCode==0,await errors);
            using var json=JsonDocument.Parse(await output);var env=json.RootElement.GetProperty("Environment");
            Assert.Equal("1",env.GetProperty("MXO_DEV_COMMANDS").GetString());Assert.Equal(roll.ToString(),env.GetProperty("MXO_CALLBACK_QA_ROLL100").GetString());
            Assert.Equal("",env.GetProperty("MXO_GM_CHARACTER_IDS").GetString());Assert.Equal("",env.GetProperty("MXO_DESPERATION_QA_HOLD_HEALTH").GetString());
            Assert.Equal(5,json.RootElement.GetProperty("Plan").GetProperty("ExtraQaMetadata").GetProperty("BuffDispelOnRecords").GetInt32());
            Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
        }
        finally {Directory.Delete(empty);}
    }
}
