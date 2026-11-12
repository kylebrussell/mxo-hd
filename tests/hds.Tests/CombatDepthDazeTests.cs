using System.Diagnostics;
using System.Text.Json;
using hds;
using hds.shared;
using NativeQa;
namespace hds.Tests;

[Collection("DelayedActions")]
public class CombatDepthDazeTests
{
    [Theory]
    [InlineData("1","mxotest_native_123",110u,true,false,"CSR_Tester",true)]
    [InlineData("true","mxotest_native_123",110u,true,false,"CSR_Tester",false)]
    [InlineData("1","reality_hd",110u,true,false,"CSR_Tester",false)]
    [InlineData("1","mxotest_",110u,true,false,"CSR_Tester",false)]
    [InlineData("1","mxotest_bad-name",110u,true,false,"CSR_Tester",false)]
    [InlineData("1","mxotest_native",2u,true,false,"other",false)]
    [InlineData("1","mxotest_native",0u,true,false,"other",false)]
    [InlineData("1","mxotest_native",110u,false,false,"other",false)]
    [InlineData("1","mxotest_native",110u,true,true,"other",false)]
    [InlineData("1","mxotest_native",110u,true,false,"TheNeo",false)]
    public void ScopeRequiresDisposableLiveNonprotectedIdentity(string flag,string db,uint id,bool world,bool dead,string name,bool expected) =>
        Assert.Equal(expected,CombatDepthDaze.Allowed(flag,db,id,world,dead,name));

    [Theory]
    [InlineData("daze",true,30)] [InlineData("daze 1",true,1)] [InlineData("daze 60",true,60)]
    [InlineData("daze 1.5",true,1.5)] [InlineData("daze NaN",false,0)] [InlineData("daze Infinity",false,0)]
    [InlineData("daze 0",false,0)] [InlineData("daze 61",false,0)] [InlineData("daze -1",false,0)]
    [InlineData("daze 30 6111",false,0)] [InlineData("daze 1e1",false,0)]
    public void OnlyOptionalFiniteBoundedDurationIsAccepted(string command,bool valid,float duration)
    {
        Assert.Equal(valid,CombatDepthDaze.Parse(command.Split(' '),out float seconds));
        if(valid) Assert.Equal(duration,seconds);
    }

    private sealed class Subject : TimedEffects.Subject
    {
        internal override WorldClient Context => null!;
        internal override bool Alive => true;
        internal override void Applied(TempMods.Effect e,List<TimedEffects.Active> a,TimedEffects.Active i,List<TimedEffects.Active> r) { }
        internal override bool Ticked(TimedEffects.Active m,int damage) => true;
        internal override void Ended(List<TimedEffects.Active> ended) { }
    }

    [Fact]
    public void ActualDazeStatePreservesExistingImmunityAndOtherEffectsAndItsSourceTemplate()
    {
        Assert.True(TempMods.TryGet(884,out var original));
        var source=Assert.Single(original.Mods);Assert.Equal(199,source.Target);Assert.Equal(30,source.Seconds);
        var subject=new Subject();double now=TimeUtils.GetSimTimeSeconds();
        var immunity=new TimedEffects.Active {Kind=TempMods.KindPlain,Proc=TempMods.Proc.State,Target=681,Seconds=30};
        subject.Effects.Apply(734,new[]{immunity},now);
        subject.Effects.Apply(1102,new[]{new TimedEffects.Active {Kind=TempMods.KindStat,Proc=TempMods.Proc.Stat,Target=575,Value=100,Seconds=30}},now);
        CombatDepthDaze.Apply(subject,1.5f);
        Assert.Same(immunity,Assert.Single(subject.Effects.Snapshot(),m=>m.Parent==734));
        var daze=Assert.Single(subject.Effects.Snapshot(),m=>m.Parent==884);
        Assert.Equal(199,daze.Target);Assert.Equal(TempMods.Proc.State,daze.Proc);Assert.Equal(1.5f,daze.Seconds);
        Assert.Equal(100,subject.Effects.Total(575));Assert.Equal(30,source.Seconds);
        TimedEffects.RunDue(daze.Start+1.6);
        Assert.False(subject.Effects.HasParent(884));Assert.True(subject.Effects.HasParent(734));
        TimedEffects.Clear(subject);
    }

    [Fact]
    public async Task FocusedFixtureReadOnlyCliOwnsThrowsAndSpyWithoutOtherPrivileges()
    {
        string empty=Path.Combine(Path.GetTempPath(),"mxo-conditional-cli-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(empty);
        try
        {
            var start=new ProcessStartInfo("dotnet") {WorkingDirectory=empty,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            foreach(string arg in new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"hds.Tests.runtimeconfig.json"),
                "--depsfile",Path.Combine(AppContext.BaseDirectory,"hds.Tests.deps.json"),typeof(NativeQaFixturePlan).Assembly.Location,"--fixture-plan","conditional-controls"}) start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();Assert.True(process.ExitCode==0,await errors);
            using var json=JsonDocument.Parse(await output);var plan=json.RootElement.GetProperty("Plan");var env=json.RootElement.GetProperty("Environment");
            Assert.Equal(new ushort[]{2,17,347,1135,1112},plan.GetProperty("Abilities").EnumerateArray().Select(e=>e.GetUInt16()));
            Assert.Equal("1",env.GetProperty("MXO_DEV_COMMANDS").GetString());
            foreach(string key in new[]{"MXO_GM_CHARACTER_IDS","MXO_CALLBACK_QA_ROLL15","MXO_CALLBACK_QA_ROLL100","MXO_DESPERATION_QA_HOLD_HEALTH"})
                if(env.TryGetProperty(key,out var value)) Assert.Equal("",value.GetString());
            foreach(string stage in new[]{"BeforeLoadout","AfterLoadout","AfterHelpers"})Assert.Equal(0,plan.GetProperty(stage).GetArrayLength());
            Assert.Equal("",plan.GetProperty("Bootstrap").GetString());Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
        }
        finally {Directory.Delete(empty);}
    }
}
