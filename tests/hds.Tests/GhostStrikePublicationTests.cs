using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;

// This existing collection disables parallelization, including tests in other collections.
[Collection("ConcentrationEnvironment")]
public class GhostStrikePublicationTests:IDisposable
{
    private readonly ServerConfig oldConfig=Store.config;
    private readonly WorldServer oldWorld=Store.world;
    private readonly Dictionary<string,WorldClient> oldClients=WorldServer.Clients;
    private readonly string[] keys={"MXO_DEV_COMMANDS","MXO_GHOST_STRIKE_QA","MXO_GHOST_STRIKE_QA_ROLL"};
    private readonly string?[] oldEnvironment;
    public GhostStrikePublicationTests()
    {
        oldEnvironment=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        Environment.SetEnvironmentVariable(keys[0],"1");Environment.SetEnvironmentVariable(keys[1],"1");Environment.SetEnvironmentVariable(keys[2],null);
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_ghost_gate"});
        WorldServer.Clients=new();Store.world=(WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
    }
    public void Dispose()
    {
        Store.config=oldConfig;Store.world=oldWorld;WorldServer.Clients=oldClients;
        for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],oldEnvironment[i]);
    }
    [Fact]public void EvenACapturedCurrentEpochCannotAuthorizeCallbackWhileResetIsPending()
    {
        var c=GhostStrikeCallbackTests.Client();var ticket=GhostStrikeCallbackTests.Ticket(c,GhostStrikeCallbackTests.Client());
        c.TransportFence.PublishIncoming(true,()=>{});
        // Model a capture after header publication but before the pending reset applies to the owner.
        ticket.SourceCurrent=GhostStrikeCallbacks.Lifetime(c);int draws=0;
        Assert.False(GhostStrikeCallbacks.Accepted(ticket,()=>{draws++;return 8;}));Assert.Equal(0,draws);Assert.Null(Stealth.Current(c));
    }
    [Fact]public async Task AcceptedCallbackAndActualScopedDrawPredicateDoNotReenterPublicationGate()
    {
        var c=GhostStrikeCallbackTests.Client();c.playerData.setCharID(110);
        var ticket=GhostStrikeCallbackTests.Ticket(c,GhostStrikeCallbackTests.Client());
        Assert.Contains("callback roll=8",GhostStrikeCallbacks.Dev(c,new[]{"8"}));
        long epoch=c.TransportFence.Capture();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var publisher=Task.Factory.StartNew(()=>c.TransportFence.TryRun(epoch,()=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(30)));}),TaskCreationOptions.LongRunning);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(30)));
            // Normal cast completion owns craftingSync. The callback must observe transport without taking its gate.
            var callback=Task.Factory.StartNew(()=>{lock(c.playerData.craftingSync)return GhostStrikeCallbacks.Accepted(ticket);},TaskCreationOptions.LongRunning);
            Assert.True(await callback.WaitAsync(TimeSpan.FromSeconds(30)));Assert.Equal((ushort)1116,Stealth.Current(c).Def.Ability);
        }
        finally{release.Set();await publisher;Stealth.Cleared(c);}
    }
}
