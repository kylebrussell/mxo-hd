using System.Text.Json;
using System.Security.Cryptography;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task SeverArteryStates(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(NearNpc("SeverCaster") with {Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var victim=await context.AddPlayerAsync(NearNpc("SeverVictim",100) with {Level=50,Organization=2,Health=5000,MaxHealth=5000});
        string stage="entry";Exception? failure=null;var proof=new List<object>();
        async Task Control(int roll,int selector){caster.SendChat($"?severstate {roll} {selector}");await caster.WaitForTextAsync($"callback roll={roll} selector={selector}");}
        try
        {
            await LoadAbilitiesAsync(context,caster,2,17,339,341,1112);await caster.EnterWorldAsync();await victim.EnterWorldAsync();
            foreach(var player in new[]{caster,victim}){player.SendChat("?pvpflag on");await player.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
            var view=await caster.WaitForViewNamedAsync(victim.Handle);SelectStyle(caster,1112);await caster.WaitForRpcAsync(ModPattern(1112,1032));
            stage="negative99-normal-cast";await Control(99,0);int cursor=victim.World.RpcSnapshot().Count;
            Cast(caster,341,view.ViewId);await caster.WaitForRpcAsync("80 ac 55 01");await victim.WaitForRpcAsync(ModPattern(341,1157),TimeSpan.FromSeconds(10));
            await caster.WaitForRpcAsync("80 bd 01 55 01 04 00 00 70 41 00",TimeSpan.FromSeconds(20));
            Check.That(!victim.World.RpcSnapshot().Skip(cursor).Any(r=>new ushort[]{199,200,201}.Any(s=>BytePattern.Parse(ModPattern(341,s)).IsMatch(r.Bytes))),"Negative99 adds no341 random state on init/updates");
            proof.Add(new{Stage=stage,RecipientRpcCursor=cursor});
            stage="positive-init-daze";await Control(0,0);cursor=victim.World.RpcSnapshot().Count;
            Cast(caster,341,view.ViewId);await caster.WaitForRpcAsync("80 ac 55 01");
            foreach(var pair in new[]{(Selector:0,State:(ushort)199),(Selector:1,State:(ushort)200),(Selector:2,State:(ushort)201)})
            {
                if(pair.Selector>0){stage="positive-update-"+pair.State;await Control(0,pair.Selector);}
                var record=await victim.WaitForRpcAsync(ModPattern(341,pair.State),TimeSpan.FromSeconds(10));
                Check.Equal(10f,BitConverter.ToSingle(record.Bytes,18),"Original script exact10s state duration");
                proof.Add(new{Stage=stage,pair.Selector,pair.State,Record=Convert.ToHexString(record.Bytes),RecipientRpcCursor=cursor});
                await Control(99,pair.Selector);await victim.WaitForRpcAsync(ModPattern(341,pair.State,true),TimeSpan.FromSeconds(13));
            }
            stage="expired-parent-no-late-callback";int log=context.Server.DebugLog().Length;await Control(0,0);await Task.Delay(3000);
            Check.That(!context.Server.DebugLog()[log..].Contains("[SEVER-STATE]"),"Expired parent cannot create new states");
            Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count,"Clean callback wire parsers");stage="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-sever-states-"+context.Database.Name);string dll=context.Server.SourceAssembly;
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Stage=stage,Failure=failure?.ToString(),Database=context.Database.Name,Proof=proof,Source=dll,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),ServerPid=context.Server.ProcessId,Actors=new[]{caster,victim}.Select(p=>new{p.CharId,p.Handle,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),p.World.Problems}),NativePresentationProven=false,CallbackOnlyQa=true,GlobalCastCombatRngOverride=false},new JsonSerializerOptions{WriteIndented=true}));
            await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
        }
    }
}
