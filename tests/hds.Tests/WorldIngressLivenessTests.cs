using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using hds.databases;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class WorldIngressLivenessTests:IDisposable
{
    private readonly Dictionary<string,WorldClient> oldClients=WorldServer.Clients;
    private readonly DatabaseManager oldDatabase=Store.dbManager;
    public WorldIngressLivenessTests(){WorldServer.Clients=new();Store.dbManager=null!;}
    public void Dispose(){WorldServer.Clients=oldClients;Store.dbManager=oldDatabase;}
    private static void Set(WorldClient c,string field,object value)=>typeof(WorldClient).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,value);
    private static bool Running(WorldClient c)=>(bool)typeof(WorldClient).GetField("packetWorkerRunning",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!;
    private static WorldClient Client(Func<uint> clock,bool held=false)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.LivenessClock=clock;c.playerData=new();c.Alive=true;
        Set(c,"lastUsedTime",clock());Set(c,"processLock",new object());Set(c,"receivedPackets",new Queue<byte[]>());Set(c,"packetWorkerRunning",held);
        return c;
    }
    [Fact]public void AConfirmedQuitIsReapableAtOnceWhileAnIdleClientStillWaitsThirtySeconds()
    {
        uint now=100;var quit=Client(()=>now);var idle=Client(()=>now);WorldServer.Clients["quit"]=quit;WorldServer.Clients["idle"]=idle;
        TeleportHandler.ReleaseAfterQuit(quit);
        // Same second as the last datagram: the quit is already dead for the view thread, and its claim succeeds.
        Assert.False(quit.Alive);Assert.True(idle.Alive);
        Assert.True(WorldThreads.TryClaimInactiveClient(quit,out string key));Assert.Equal("quit",key);
        Assert.False(WorldThreads.TryClaimInactiveClient(idle,out _));
        // Late ingress from the quitting endpoint can neither revive it nor queue work.
        quit.QueuePacket(new byte[]{0xff});quit.Alive=true;Assert.False(quit.Alive);Assert.Equal(0,quit.PendingPacketCount);
        now=130;Assert.True(idle.Alive);now=131;Assert.False(idle.Alive); // real disconnects keep the idle timeout
        Assert.True(WorldThreads.RemoveClaimedClient(key,quit));Assert.False(WorldServer.Clients.ContainsKey("quit"));
    }
    [Fact]public void ExactExistingThirtySecondLimitAndBotPolicyRemain()
    {
        uint now=100;var c=Client(()=>now);now=130;Assert.True(c.Alive);now=131;Assert.False(c.Alive);
        Set(c,"<IsBot>k__BackingField",true);Assert.True(c.Alive);c.Alive=false;c.Alive=true;Assert.False(c.Alive);
    }
    [Fact]public void ActualQueuedIngressWinsOverAStaleDeadSnapshotBeforeAnyCleanupSideEffect()
    {
        uint now=100;var c=Client(()=>now,true);WorldServer.Clients["endpoint"]=c;now=131;Assert.False(c.Alive);
        var stale=new List<WorldClient>{c};c.QueuePacket(Array.Empty<byte>());Assert.True(c.Alive);
        // Actual cleanup entry must skip before touching the deliberately absent database/owner services.
        typeof(WorldThreads).GetMethod("CleanDeadPlayers",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{stale,stale});
        Assert.Same(c,WorldServer.Clients["endpoint"]);Assert.False(c.IngressSnapshot.Claimed);Assert.Equal(1,c.PendingPacketCount);
    }
    [Fact]public void ReplacedEndpointOwnerAndDuplicateClaimCannotCleanOrRemoveTheNewOwner()
    {
        uint now=1;var old=Client(()=>now);WorldServer.Clients["endpoint"]=old;now=32;
        var current=Client(()=>now);WorldServer.Clients["endpoint"]=current;
        Assert.False(WorldThreads.TryClaimInactiveClient(old,out _));Assert.False(WorldThreads.RemoveClaimedClient("endpoint",old));
        Assert.Same(current,WorldServer.Clients["endpoint"]);current.Alive=false;
        Assert.True(WorldThreads.TryClaimInactiveClient(current,out string key));Assert.Equal("endpoint",key);
        Assert.False(WorldThreads.TryClaimInactiveClient(current,out _));current.QueuePacket(Array.Empty<byte>());current.Alive=true;
        Assert.False(current.Alive);Assert.Equal(0,current.PendingPacketCount);
        current.ReleaseFailedReap();Assert.True(WorldThreads.TryClaimInactiveClient(current,out _)); // existing failed-cleanup retry, still terminal
        Assert.False(current.Alive);Assert.True(WorldThreads.RemoveClaimedClient(key,current));
    }
    [Fact]public void RealSerializedWorkerDoesNotRefreshOldQueuedWorkAndTerminalClaimDiscardsIt()
    {
        uint now=100;var c=Client(()=>Volatile.Read(ref now));var gate=typeof(WorldClient).GetField("processLock",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!;
        Monitor.Enter(gate);
        try
        {
            c.QueuePacket(Array.Empty<byte>());
            Assert.True(SpinWait.SpinUntil(()=>c.PendingPacketCount==0,TimeSpan.FromSeconds(30))); // actual worker is now blocked on processLock
            Volatile.Write(ref now,131);c.QueuePacket(Array.Empty<byte>());Assert.True(c.Alive);Assert.Equal(1,c.PendingPacketCount);
            Volatile.Write(ref now,162);
        }
        finally{Monitor.Exit(gate);}
        Assert.True(SpinWait.SpinUntil(()=>!Running(c),TimeSpan.FromSeconds(30)));
        Assert.Equal(131u,c.IngressSnapshot.Last);Assert.False(c.Alive); // dequeuing at162 did not extend inactivity
        WorldServer.Clients["endpoint"]=c;Assert.True(WorldThreads.TryClaimInactiveClient(c,out _));
        c.QueuePacket(new byte[]{0xff});Assert.Equal(0,c.PendingPacketCount);Assert.False(c.Alive);
    }
    [Fact]public void ExplicitStopWinsOverLateIngressAndAClaimCannotRunQueuedHandlers()
    {
        uint now=10;var c=Client(()=>now,true);WorldServer.Clients["endpoint"]=c;c.QueuePacket(new byte[]{0xff});
        c.Alive=false;now=11;c.QueuePacket(new byte[]{0xff});Assert.Equal(1,c.PendingPacketCount);c.Alive=true;Assert.False(c.Alive);
        Assert.True(WorldThreads.TryClaimInactiveClient(c,out _));
        typeof(WorldClient).GetMethod("ProcessQueuedPackets",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(c,null);
        Assert.Equal(0,c.PendingPacketCount);Assert.False(Running(c));Assert.Equal(0,c.playerData.getRPCCounter());
    }
    [Fact]public void ActualSendFailureStopsAndDiscardsAlreadyQueuedHandlers()
    {
        var c=Client(()=>10,true);c.QueuePacket(new byte[]{0xff});
        // The missing socket exercises the real sendPacket exception path, not a simulated setter.
        Assert.False((bool)typeof(WorldClient).GetMethod("sendPacket",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(c,new object[]{Array.Empty<byte>()})!);
        c.Alive=true;c.QueuePacket(new byte[]{0xff});Assert.False(c.Alive);Assert.Equal(1,c.PendingPacketCount);
        typeof(WorldClient).GetMethod("ProcessQueuedPackets",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(c,null);
        Assert.Equal(0,c.PendingPacketCount);Assert.False(Running(c));Assert.Equal(0,c.playerData.getRPCCounter());
    }
    [Fact]public void SourcePinIsMandatoryAndExact()
    {
        HeadlessClient.IngressLivenessFixture.RequirePin(new string('a',64),new string('A',64));
        foreach(string? pin in new[]{null,"",new string('a',63),new string('z',64),new string('b',64)})
            Assert.Throws<InvalidDataException>(()=>HeadlessClient.IngressLivenessFixture.RequirePin(pin,new string('a',64)));
    }
    [Theory][InlineData("0","mxotest_owned","1",110,false)][InlineData("1","reality_hd","1",110,false)]
    [InlineData("1","mxotest_owned","0",110,false)][InlineData("1","mxotest_owned","1",111,false)]
    [InlineData("1","mxotest_owned","1",110,true)]
    public void WorkerPauseRequiresEveryActualDisposableGate(string dev,string db,string feature,uint character,bool dead)=>
        Assert.False(IngressLivenessQa.Allowed(dev,db,feature,character,true,dead));
    [Fact]public void ActualControlArmIsOneUseAndDiscoveredModeDoesNotChangeTimersOrRng()
    {
        var previousConfig=Store.config;var previousCurrent=Store.currentClient;
        string[] keys={"MXO_DEV_COMMANDS","MXO_INGRESS_LIVENESS_QA"};var previous=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_ingress_unit"});
            Environment.SetEnvironmentVariable(keys[0],"1");Environment.SetEnvironmentVariable(keys[1],"1");
            var c=Client(()=>10);c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerData.setUDPSessionEstablished(true);c.messageQueue=new();
            WorldServer.Clients["endpoint"]=c;Store.currentClient=c;Assert.True(IngressLivenessQa.Arm(c));Assert.False(IngressLivenessQa.Arm(c));
            var mode=NativeQa.NativeQaModes.Find("world-ingress-liveness");Assert.True(mode.Plan.DevCommands);Assert.False(mode.Plan.FixedCallbackRolls);Assert.False(mode.Plan.GmCharacter110);
            Assert.Equal("1",NativeQa.NativeQaFixturePlan.EnvironmentFor(mode.Name)[keys[1]]);Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")[keys[1]]);
            Assert.Single(HeadlessClient.Scenarios.All,x=>x.Name=="world-ingress-liveness");
        }
        finally{Store.config=previousConfig;Store.currentClient=previousCurrent;for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],previous[i]);}
    }
}
