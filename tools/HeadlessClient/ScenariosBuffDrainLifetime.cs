using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task BuffDrainLifetimeWire(ScenarioContext context)
    {
        string source=context.Server.SourceAssembly;
        string sha=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source)));
        Check.Equal(Environment.GetEnvironmentVariable("MXO_BUFF_DRAIN_SOURCE_SHA256"),sha,"Required actual source pin before authentication");
        var sessions=new List<HeadlessPlayer>();var measurements=new List<object>();bool passed=false;string stage="first-login";Exception? failure=null;
        HeadlessPlayer? actor=null;
        try
        {
            actor=await context.AddPlayerAsync(new CharacterSeed("DrainLifetime")
            {X=24800,Y=495,Z=-1498,Level=50,Health=20000,MaxHealth=20000,InnerStrength=2000,MaxInnerStrength=2000,
             OwnedLoadedAbilities=BuffDrainLifetimeFixture.Abilities.Select(a=>(a,(ushort)50)).ToArray()});
            sessions.Add(actor);await actor.EnterWorldAsync();uint character=actor.CharId;
            async Task On(HeadlessPlayer p)
            {
                int cursor=p.World.ViewSnapshot().Count,log=context.Server.DebugLog().Length;
                p.SkipReceived();Cast(p,16,2);
                await p.WaitForRpcAsync(r=>BytePattern.Parse(ModPattern(16,212)).IsMatch(r.Bytes)&&r.Bytes[17]==0,"Normal16 activation",TimeSpan.FromSeconds(8));
                await p.WaitUntilAsync(()=>BuffDrainLifetimeFixture.Drains(context.Server.DebugLog()[log..],character).Length>=2,"Two actual ordinary1IS drain ticks",TimeSpan.FromSeconds(6));
                var ticks=BuffDrainLifetimeFixture.Drains(context.Server.DebugLog()[log..],character);
                foreach(var t in ticks)Check.Equal(1,t.Amount,"Unchanged sourced1IS cost");
                var wire=p.World.ViewSnapshot().Skip(cursor).Where(b=>BuffDrainLifetimeFixture.TryInnerStrength(b,out _)).Select(b=>BitConverter.ToUInt16(b,7)).ToArray();
                Check.That(ticks.Any(t=>wire.Contains((ushort)t.Remaining)),"At least one actual drain total independently received as exact own9-byte IS update");
                measurements.Add(new{Stage=stage,Ticks=ticks.Select(t=>new{t.Generation,t.Amount,t.Remaining}),StandaloneOwnIs=wire});
            }
            stage="session1-on";await On(actor);
            stage="normal-quit";int quitLog=context.Server.DebugLog().Length;
            actor.SendRpc("80 fe 00");await actor.WaitForRpcAsync("80 fd");actor.Dispose();actor=null;
            await Task.Delay(TimeSpan.FromSeconds(33)); // Existing world reservation, no forced cleanup/relogin bypass.
            Check.Equal(0,BuffDrainLifetimeFixture.Drains(context.Server.DebugLog()[quitLog..],character).Length,"Confirmed quit ends old chain during normal reservation");
            stage="same-identity-relog";actor=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+character,"DrainLifetime");sessions.Add(actor);
            await actor.EnterWorldAsync();Check.Equal(character,actor.CharId,"Same character relog");
            stage="session2-on";await On(actor);
            stage="session2-off";actor.SkipReceived();Cast(actor,16,2);
            await actor.WaitForRpcAsync(r=>BytePattern.Parse(ModPattern(16,212,true)).IsMatch(r.Bytes),"One normal off removes16",TimeSpan.FromSeconds(8));
            int offLog=context.Server.DebugLog().Length,offViews=actor.World.ViewSnapshot().Count;
            await Task.Delay(TimeSpan.FromSeconds(3.2)); // Observe more than three unchanged one-second drain intervals.
            Check.Equal(0,BuffDrainLifetimeFixture.Drains(context.Server.DebugLog()[offLog..],character).Length,"No activation drains after off");
            var after=actor.World.ViewSnapshot().Skip(offViews).Where(b=>BuffDrainLifetimeFixture.TryInnerStrength(b,out _)).Select(b=>BitConverter.ToUInt16(b,7)).ToArray();
            for(int i=1;i<after.Length;i++)Check.That(after[i]>=after[i-1],"Post-off exact IS records never decrease; ordinary regeneration is allowed");
            measurements.Add(new{Stage=stage,StandaloneOwnIs=after,DrainCount=0,ObservationSeconds=3.2});
            Check.Equal(0,sessions.Sum(p=>p.World.Problems.Count),"Both session parsers clean");passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string path=Path.Combine(Path.GetTempPath(),"mxo-buff-drain-"+context.Database.Name+".json");
            await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Source=source,SourceSha256=sha,
                ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Measurements=measurements,Sessions=sessions.Select(p=>new{p.Handle,p.CharId,
                Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),
                Limitations="Normal same-identity reconnect uses a new UDP endpoint; same-client numeric-generation alias and rollback are scheduler/unit controls. IS reader asserts only complete standalone9-byte updates; full raw sections retained."},new JsonSerializerOptions{WriteIndented=true}));
            actor?.Dispose();Console.WriteLine("Buff drain proof: "+path);
        }
    }
}
