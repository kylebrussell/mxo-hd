using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using hds;
using hds.shared;

namespace hds.Tests;

[CollectionDefinition("MissionTickQaObservation",DisableParallelization=true)]
public class MissionTickQaObservationCollection { }

[Collection("MissionTickQaObservation")]
public class MissionTickQaDiagnosticsTests : IDisposable
{
    readonly ServerConfig? priorConfig=Store.config;
    readonly Action<string> priorSink=MissionTickQaDiagnostics.Sink;
    readonly string? priorFlag=Environment.GetEnvironmentVariable("MXO_MISSION_TICK_QA");
    readonly string? priorDev=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS");
    readonly string? priorEnvDb=Environment.GetEnvironmentVariable("MXO_DB_NAME");
    readonly ConcurrentQueue<string> lines=new();
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic;
    public MissionTickQaDiagnosticsTests()
    {
        Environment.SetEnvironmentVariable("MXO_MISSION_TICK_QA","1");
        Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","1");
        Store.config=new ServerConfig("unused-no-database-connection");
        typeof(ServerConfig).GetField("_dbParams",Instance)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_tick_observation"});
        MissionTickQaDiagnostics.Sink=lines.Enqueue;
    }
    public void Dispose()
    {
        Store.config=priorConfig;MissionTickQaDiagnostics.Sink=priorSink;
        Environment.SetEnvironmentVariable("MXO_MISSION_TICK_QA",priorFlag);
        Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",priorDev);
        Environment.SetEnvironmentVariable("MXO_DB_NAME",priorEnvDb);
    }
    static MissionProgress Progress()
    {
        var doc=new XmlDocument();doc.LoadXml("<mission id='101209' title='Observed'><phase><objective goal='1' type='Use' object='1' text='Open'/></phase><phase><objective goal='2' type='Use' object='1' text='Open'/></phase></mission>");
        var p=new MissionProgress(MissionXml.Parse(doc.DocumentElement!));
        p.site=new MissionSite{elevatorDoor=new StaticWorldObject{metrId=1,sectorID=555,mxoStaticId=581959699,pos_x=30300,pos_y=545,pos_z=-6800},
            lobbyDoor=new StaticWorldObject{metrId=1,sectorID=555,mxoStaticId=581959700,pos_x=30300,pos_y=145,pos_z=-6800}};
        p.waitingForExit=true;p.CommitGeneratedFloor(p.phaseIndex,p.site,p.site.lobbyDoor);return p;
    }
    static WorldClient Client(MissionProgress p)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerData.setUniqueKey("127.0.0.1:32123");c.playerData.missionProgress=p;c.playerData.activeMission=p.mission;
        c.playerInstance=new PlayerCharacter();c.playerInstance.CharacterName.setValue(Encoding.UTF8.GetBytes("CSR110\0"));
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",Instance)!.SetValue(c,new WorldSendFence());
        c.Alive=true;typeof(WorldClient).GetField("lastUsedTime",Instance)!.SetValue(c,TimeUtils.getUnixTimeUint32());return c;
    }
    static string Stage(string line){using var j=JsonDocument.Parse(line["[MISSION-TICK-QA] ".Length..]);return j.RootElement.GetProperty("stage").GetString()!;}
    [Theory]
    [InlineData("0","1","mxotest_tick",110,"CSR110",false)]
    [InlineData("1","0","mxotest_tick",110,"CSR110",false)]
    [InlineData("1","1","mxotest_",110,"CSR110",false)]
    [InlineData("1","1","main",110,"CSR110",false)]
    [InlineData("1","1","mxotest_bad-name",110,"CSR110",false)]
    [InlineData("1","1","mxotest_tick",2,"CSR110",false)]
    [InlineData("1","1","mxotest_tick",0,"CSR110",false)]
    [InlineData("1","1","mxotest_tick",110,"TheNeo",false)]
    [InlineData("1","1","mxotest_tick",110,"",false)]
    [InlineData("1","1","mxotest_tick",110,"CSR110",true)]
    public void ExactExplicitGate(string flag,string dev,string database,uint character,string name,bool expected)
        =>Assert.Equal(expected,MissionTickQaDiagnostics.Allowed(flag,dev,database,character,name));
    [Fact] public void EnvironmentDatabaseCannotSubstituteForActualConfigurationAndMissingFieldsRefuse()
    {
        var c=Client(Progress());Environment.SetEnvironmentVariable("MXO_DB_NAME","mxotest_spoof");Store.config=null;
        MissionTickQaDiagnostics.Arm(c,c.playerData.missionProgress);Assert.Empty(lines);
        Store.config=priorConfig;c.playerInstance=null;MissionTickQaDiagnostics.Arm(c,c.playerData.missionProgress);Assert.Empty(lines);
    }
    [Fact] public void MustHaveCommittedDepartureAndWaitingBeforeArm()
    {
        var p=Progress();var c=Client(p);p.CommitGeneratedFloor(p.phaseIndex,p.site,p.site.elevatorDoor);
        MissionTickQaDiagnostics.Arm(c,p);Assert.Empty(lines);
        p.CommitGeneratedFloor(p.phaseIndex,p.site,p.site.lobbyDoor);p.waitingForExit=false;
        MissionTickQaDiagnostics.Arm(c,p);Assert.Empty(lines);
        p.waitingForExit=true;MissionTickQaDiagnostics.Arm(c,p);Assert.Single(lines);
    }
    [Fact] public void BoundedPerProgressPhaseAndDoesNotChangeProgressOrQueues()
    {
        var p=Progress();var c=Client(p);var site=p.site;MissionTickQaDiagnostics.Arm(c,p);
        for(int i=0;i<1000;i++)MissionTickQaDiagnostics.Record(c,"bounded");
        Assert.Equal(512,lines.Count);Assert.Same(p,c.playerData.missionProgress);Assert.Same(site,p.site);Assert.True(p.HasGeneratedDeparture);
        Assert.Null(c.messageQueue);Assert.Equal(0,p.phaseIndex);
        p.phaseIndex=1;p.CommitGeneratedFloor(1,p.site,p.site.lobbyDoor);MissionTickQaDiagnostics.Arm(c,p);
        Assert.Equal(513,lines.Count);
        var next=Progress();c.playerData.missionProgress=next;MissionTickQaDiagnostics.Record(c,"unarmed-new-progress");Assert.Equal(513,lines.Count);
        MissionTickQaDiagnostics.Arm(c,next);Assert.Equal(514,lines.Count);
    }
    [Fact] public void SnapshotDistinguishesExactEndpointOwnerFromAliasAndPredicateIsIdentity()
    {
        var p=Progress();var c=Client(p);MissionTickQaDiagnostics.Arm(c,p);
        MissionTickQaDiagnostics.Active("snapshot",new Dictionary<string,WorldClient>{{"127.0.0.1:32123",Client(Progress())}});
        using(var last=JsonDocument.Parse(lines.Last()["[MISSION-TICK-QA] ".Length..]))Assert.False(last.RootElement.GetProperty("registered").GetBoolean());
        MissionTickQaDiagnostics.Active("snapshot",new Dictionary<string,WorldClient>{{"127.0.0.1:32123",c}});
        using(var last=JsonDocument.Parse(lines.Last()["[MISSION-TICK-QA] ".Length..]))Assert.True(last.RootElement.GetProperty("registered").GetBoolean());
        Assert.True(MissionTickQaDiagnostics.Gate(c,"control",true));Assert.False(MissionTickQaDiagnostics.Gate(c,"control",false));
    }
    [Fact] public void ActualTickEmitsBeforeBlockedMissionLockThenPreservesRejectedGateShortCircuit()
    {
        var p=Progress();var c=Client(p);MissionTickQaDiagnostics.Arm(c,p);c.Alive=false;
        using var before=new ManualResetEventSlim();using var inside=new ManualResetEventSlim();
        MissionTickQaDiagnostics.Sink=line=>{lines.Enqueue(line);if(Stage(line)=="tick-before-mission-lock")before.Set();if(Stage(line)=="tick-inside-mission-lock")inside.Set();};
        Task task;
        lock(c.playerData.missionSync)
        {
            task=Task.Factory.StartNew(()=>typeof(MissionRuntime).GetMethod("Tick",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{c}),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            Assert.True(before.Wait(TimeSpan.FromSeconds(30)));Assert.False(inside.IsSet);
        }
        Assert.True(task.Wait(TimeSpan.FromSeconds(30)));Assert.True(inside.IsSet);
        Assert.Contains(lines,x=>Stage(x)=="tick-gate-rejected");
        Assert.DoesNotContain(lines,x=>x.Contains("onWorld=True"));Assert.DoesNotContain(lines,x=>Stage(x)=="mutate-before-mission-lock");
    }
    [Fact] public void QuarantineIsObservedBeforeActionAndNoDatabaseWork()
    {
        var p=Progress();var c=Client(p);MissionTickQaDiagnostics.Arm(c,p);p.restoreRefusal="retained-test-refusal";int calls=0;
        Assert.False(MissionPersistence.Mutate(c,()=>{calls++;return true;}));Assert.Equal(0,calls);
        Assert.Contains(lines,x=>Stage(x)=="mutate-quarantine-return");
    }
    [Fact] public void RecordObservesTransportWithoutAcquiringPublicationGate()
    {
        var p=Progress();var c=Client(p);MissionTickQaDiagnostics.Arm(c,p);
        var sync=typeof(WorldSendFence).GetField("sync",Instance)!.GetValue(c.TransportFence)!;
        using var observed=new ManualResetEventSlim();Task task;
        lock(sync)
        {
            task=Task.Factory.StartNew(()=>{MissionTickQaDiagnostics.Record(c,"publication-held");observed.Set();},CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            Assert.True(observed.Wait(TimeSpan.FromSeconds(30)));
        }
        Assert.True(task.Wait(TimeSpan.FromSeconds(30)));Assert.Contains(lines,x=>Stage(x)=="publication-held");
        c.TransportFence.PublishIncoming(true,()=>{});var snapshot=c.TransportFence.DiagnosticObservation;
        Assert.Equal(1,snapshot.Epoch);Assert.True(snapshot.ResetPending);Assert.False(c.TransportFence.IsCurrent(0));
    }
}
