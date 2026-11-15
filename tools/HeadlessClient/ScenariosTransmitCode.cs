using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task TransmitCodeWire(ScenarioContext context)
    {
        string pin=Environment.GetEnvironmentVariable("MXO_TRANSMIT_CODE_SOURCE_SHA256")??throw new InvalidOperationException("Exact Transmit Code source pin required");
        Check.Equal(pin,context.Server.SourceAssemblySha256,"Actual launched source before authentication");
        var caster=await context.AddPlayerAsync(TransmitCodeFixture.Seed("TransmitCaster",true));
        var target=await context.AddPlayerAsync(TransmitCodeFixture.Seed("TransmitTarget",false));
        var measurements=new List<object>();string stage="entry";bool passed=false;Exception? failure=null;
        try
        {
            await PvpOpponentsFixture.EnterAsync(caster);await PvpOpponentsFixture.EnterAsync(target);
            var view=await caster.WaitForViewNamedAsync(target.Handle);
            string parents=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={target.CharId} AND ability_id<>2148090880 ORDER BY slot,id");
            async Task Loaded(bool on)
            {
                target.SkipReceived();SetAbilityLoaded(target,TransmitCodeFixture.Slot,593,50,on);
                await target.WaitForRpcAsync(on?"80 b2 51 02":"80 b3 51 02");
                foreach(ushort stat in new ushort[]{1146,1148})await target.WaitForRpcAsync(PassiveRecord(593,stat,10,!on));
                Check.Equal(on?1L:0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={target.CharId} AND slot={TransmitCodeFixture.Slot} AND ability_id=2148090880"),"Normal owned593 loaded flag");
                Check.Equal(parents,await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={target.CharId} AND ability_id<>2148090880 ORDER BY slot,id"),"Prerequisite rows remain byte-identical before/after593");
            }
            async Task Style(ushort id,ushort stat,int expected)
            {
                Cast(caster,id,2);var any=BytePattern.Parse($"80 bc ?? ?? ?? ?? 00 {Le((uint)id)} {Le(stat)} ?? ?? ?? ?? ??");
                await caster.WaitUntilAsync(()=>
                {
                    var latest=caster.World.RpcSnapshot().LastOrDefault(r=>any.IsMatch(r.Bytes));
                    return latest!=null&&latest.Bytes[17]==0&&BitConverter.ToInt32(latest.Bytes,13)==expected;
                },"Actual current selected style influence",TimeSpan.FromSeconds(5));
            }
            async Task Hit(ushort ability,int raw,int expected,int total)
            {
                await Style(ability==1135?(ushort)1112:(ushort)1006,ability==1135?(ushort)1034:(ushort)172,ability==1135?29:27);
                caster.SkipReceived();target.SkipReceived();int cursor=target.World.RpcSnapshot().Count,logStart=context.Server.DebugLog().Length;
                Cast(caster,ability,view.ViewId);
                var damage=await target.WaitForRpcAsync(r=>TransmitCodeFixture.Damage(r.Bytes,ability,caster.Handle,target.Handle,out int done,out int resisted)&&done==expected&&resisted==raw-expected,
                    "Independent received typed damage/absorbed numbers",TimeSpan.FromSeconds(10));
                string log=context.Server.DebugLog()[logStart..];
                Check.That(log.Contains($"lands on {target.Handle} for {expected}"),"Normal landed health damage");
                if(total!=0)Check.That(log.Contains($"[TYPED-RESISTANCE OURS] {ability} type{(ability==1135?7:2)} total{total} raw{raw} final{expected}"),"Exactly one bonus conversion in the existing raw consumer");
                measurements.Add(new{Stage=stage,Ability=ability,Raw=raw,Final=expected,Total=total,RpcCursor=cursor,Received=Convert.ToHexString(damage.Bytes),Log=log});
                //Observe the real own timer's removal, not a sleep/reset or an assumed generic cast shape.
                var started=await caster.WaitForRpcAsync(r=>r.Bytes.Length==11&&r.Bytes[0]==0x80&&r.Bytes[1]==0xbd&&r.Bytes[2]==1&&BitConverter.ToUInt16(r.Bytes,3)==ability&&r.Bytes[5]==4&&r.Bytes[10]==1,"Normal own reuse timer",TimeSpan.FromSeconds(5));
                var ended=started.Bytes.ToArray();ended[10]=0;
                await caster.WaitForRpcAsync(r=>r.Bytes.SequenceEqual(ended),"Natural own reuse expiry",TimeSpan.FromSeconds(30));
            }
            stage="normal593-unload";await Loaded(false);
            stage="baseline-viral";await Hit(330,424,424,0);
            stage="baseline-thrown";await Hit(1135,83,83,0);
            stage="normal593-load";await Loaded(true);
            stage="bonus-viral";await Hit(330,424,401,22);
            stage="bonus-thrown";await Hit(1135,83,78,22);
            stage="bonus-dot";await Style(1006,172,27);target.SkipReceived();int dotStart=context.Server.DebugLog().Length;
            Cast(caster,474,view.ViewId);await target.WaitForRpcAsync(ModPattern(474,1157),TimeSpan.FromSeconds(10));
            var tick=await target.WaitForRpcAsync(r=>TransmitCodeFixture.Damage(r.Bytes,474,caster.Handle,target.Handle,out int done,out int resisted)&&done==256&&resisted==15,
                "Independent first DOT tick271->256 with593",TimeSpan.FromSeconds(10));
            Check.That(context.Server.DebugLog()[dotStart..].Contains("[TYPED-RESISTANCE OURS] 474 type2 total22 raw271 final256"),"DOT enters the same resistance consumer once");
            measurements.Add(new{Stage=stage,Raw=271,Final=256,Received=Convert.ToHexString(tick.Bytes),Log=context.Server.DebugLog()[dotStart..]});
            Check.Equal(0,caster.World.Problems.Count+target.World.Problems.Count,"Both session parsers clean");passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                string file=Path.Combine(Path.GetTempPath(),"mxo-transmit-code-"+context.Database.Name+".json");
                await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),ExpectedSource=pin,ActualSource=context.Server.SourceAssemblySha256,
                    Source=context.Server.SourceAssembly,SourceFileSha=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Workdir=context.Server.WorkingDirectory,
                    Measurements=measurements,Rows=await context.Database.ExecuteAsync($"SELECT char_id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id IN({caster.CharId},{target.CharId}) ORDER BY char_id,slot"),
                    Sessions=new[]{caster,target}.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                    Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),Policy="OURS4*level+20 percentage conversion, once through existing typed curve",ScopedControls=TransmitCodeFixture.Environment,NativeProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Transmit Code proof: "+file);
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
