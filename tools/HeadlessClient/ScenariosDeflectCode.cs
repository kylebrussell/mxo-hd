using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task DeflectCodeWire(ScenarioContext context)
    {
        string pin=Environment.GetEnvironmentVariable("MXO_DEFLECT_CODE_SOURCE_SHA256")??throw new InvalidOperationException("Deflect Code source pin required");
        Check.Equal(pin,context.Server.SourceAssemblySha256,"Actual source before authentication");
        var caster=await context.AddPlayerAsync(DeflectCodeFixture.Seed("DeflectCaster",true));
        var target=await context.AddPlayerAsync(DeflectCodeFixture.Seed("DeflectTarget",false));
        var measurements=new List<object>();string stage="entry";bool passed=false;Exception? failure=null;
        try
        {
            await PvpOpponentsFixture.EnterAsync(caster);await PvpOpponentsFixture.EnterAsync(target);
            var view=await caster.WaitForViewNamedAsync(target.Handle);
            string parents=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={target.CharId} AND ability_id<>2147507200 ORDER BY slot,id");
            Cast(caster,1006,2);await caster.WaitForRpcAsync(ModPattern(1006,172));
            async Task Loaded(bool on)
            {
                target.SkipReceived();SetAbilityLoaded(target,DeflectCodeFixture.Slot,23,50,on);
                await target.WaitForRpcAsync(on?"80 b2 17 00":"80 b3 17 00");
                await target.WaitForRpcAsync(PassiveRecord(23,1047,10,!on));
                Check.Equal(on?1L:0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={target.CharId} AND slot={DeflectCodeFixture.Slot} AND ability_id=2147507200"),"Normal owned23 loaded state");
                Check.Equal(parents,await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={target.CharId} AND ability_id<>2147507200 ORDER BY slot,id"),"All parent rows unchanged");
            }
            async Task Stun(float expected)
            {
                target.SkipReceived();caster.SkipReceived();int cursor=target.World.RpcSnapshot().Count;
                Cast(caster,315,view.ViewId);
                var state=await target.WaitForRpcAsync(r=>DeflectCodeFixture.State(r.Bytes,315,102,false,out _),"Current normal315 stun",TimeSpan.FromSeconds(10));
                DeflectCodeFixture.State(state.Bytes,315,102,false,out float seconds);Check.That(Math.Abs(seconds-expected)<.0001f,"Independent expected stun duration");
                await target.WaitUntilAsync(()=>DeflectCodeFixture.FreshState(target.World.RpcSnapshot().Select(r=>r.Bytes),cursor,734,681)!=null,
                    "Current-cast immunity (producer emits it before the Stun)",TimeSpan.FromSeconds(5));
                var immunity=DeflectCodeFixture.FreshState(target.World.RpcSnapshot().Select(r=>r.Bytes),cursor,734,681)??throw new InvalidOperationException("Current immunity disappeared");
                DeflectCodeFixture.State(immunity,734,681,false,out float immunitySeconds);Check.Equal(14f,immunitySeconds,"Unmitigated734 timer");
                var reuse=await caster.WaitForRpcAsync(r=>r.Bytes.Length==11&&r.Bytes[0]==0x80&&r.Bytes[1]==0xbd&&r.Bytes[2]==1&&BitConverter.ToUInt16(r.Bytes,3)==315&&r.Bytes[5]==4&&r.Bytes[10]==1,"Normal315 own reuse timer");
                var reuseOff=reuse.Bytes.ToArray();reuseOff[10]=0;
                var stateOff=state.Bytes.ToArray();stateOff[17]=1;var immunityOff=immunity.ToArray();immunityOff[17]=1;
                await target.WaitForRpcAsync(r=>r.Bytes.SequenceEqual(stateOff),"Natural exact-record Stun expiry",TimeSpan.FromSeconds(12));
                await target.WaitForRpcAsync(r=>r.Bytes.SequenceEqual(immunityOff),"Real exact-record immunity expiry, never removed by a command",TimeSpan.FromSeconds(16));
                await caster.WaitForRpcAsync(r=>r.Bytes.SequenceEqual(reuseOff),"Real normal315 reuse expiry",TimeSpan.FromSeconds(10));
                measurements.Add(new{Stage=stage,Duration=seconds,Immunity=immunitySeconds,Cursor=cursor,State=Convert.ToHexString(state.Bytes),ImmunityRecord=Convert.ToHexString(immunity)});
            }
            stage="normal23-off";await Loaded(false);await Stun(7);
            stage="normal23-on";await Loaded(true);await Stun(6.3f);
            stage="normal23-unload";await Loaded(false);await Stun(7);
            Check.Equal(0,caster.World.Problems.Count+target.World.Problems.Count,"Both session parsers clean");passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                string file=Path.Combine(Path.GetTempPath(),"mxo-deflect-code-"+context.Database.Name+".json");
                await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),ExpectedSource=pin,ActualSource=context.Server.SourceAssemblySha256,
                    Source=context.Server.SourceAssembly,SourceFileSha=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Workdir=context.Server.WorkingDirectory,
                    Measurements=measurements,Sessions=new[]{caster,target}.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                    Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),Policy="OURS positive1047 percent shortening, raw575 first, total33% floor",ScopedControls=DeflectCodeFixture.Environment,NativeProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Deflect Code proof: "+file);
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
