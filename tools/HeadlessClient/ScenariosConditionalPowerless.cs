using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ConditionalPowerlessWire(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(NearNpc("PowerlessCaster") with{Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var victim=await context.AddPlayerAsync(NearNpc("PowerlessVictim",100) with{Organization=2,Health=5000,MaxHealth=5000});
        bool passed=false;Exception? failure=null;string stage="entry";
        try
        {
            await LoadAbilitiesAsync(context,caster,2,17,340,1112);await LoadAbilitiesAsync(context,victim,2,17,314);
            await caster.EnterWorldAsync();await victim.EnterWorldAsync();
            var playerView=await caster.WaitForViewNamedAsync(victim.Handle);var npc=await caster.WaitForViewNamedAsync("WinterFrost");
            async Task Prepare(ViewCreation target,int seconds)
            {
                caster.SendRpc($"81 51 {Le(target.ViewId)} {target.SpawnId:x2}");caster.SendChat("?combatdepth stagger "+seconds);
                await caster.WaitForTextAsync("Stagger fixture applied 883/201");
            }
            async Task Reuse()
            { await caster.WaitForRpcAsync("80 bd 01 54 01 04 00 00 00 41 00",TimeSpan.FromSeconds(15)); }
            stage="unstaggered-player";Cast(caster,340,playerView.ViewId);await caster.WaitForRpcAsync("80 ac 54 01");
            await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[PVP] PowerlessCaster's"),"Normal340 PvP landing",TimeSpan.FromSeconds(6));
            Check.Equal(0,victim.Received(ModPattern(340,96)).Count,"Unstaggered recipient has no Powerless");await Reuse();
            stage="prepared-player";await Prepare(playerView,30);Cast(caster,340,playerView.ViewId);
            var state=await victim.WaitForRpcAsync(ModPattern(340,96),TimeSpan.FromSeconds(8));
            Check.Equal(6f,BitConverter.ToSingle(state.Bytes,18),"Stored six-second Powerless at unmodified equal-level fixture");
            Check.Equal(0,victim.Received(ModPattern(734,681)).Count,"Conditional state does not invent an immunity timer");
            Cast(victim,314);await victim.WaitForTextAsync("cannot use abilities while powerless");
            await Task.Delay(600);caster.SkipReceived();Cast(caster,340,playerView.ViewId);await caster.WaitForRpcAsync("80 ba 54 01");
            Check.Equal(1,victim.Received(ModPattern(340,96)).Count,"Denied repeated request never reapplies conditional state");
            await victim.WaitForRpcAsync(ModPattern(340,96,true),TimeSpan.FromSeconds(8));
            await Reuse();
            stage="prepared-npc";await Prepare(npc,30);int logStart=context.Server.DebugLog().Length;Cast(caster,340,npc.ViewId);
            await caster.WaitUntilAsync(()=>context.Server.DebugLog()[logStart..].Contains("[EFFECTS] 340 on WinterFrost:")&&context.Server.DebugLog()[logStart..].Contains("State 96 0 6 s"),"Normal340 NPC landing consumes prepared stagger",TimeSpan.FromSeconds(8));
            await caster.WaitUntilAsync(()=>context.Server.DebugLog()[logStart..].Contains("[ABILITY] 340 lands"),"Normal NPC damage path preserved",TimeSpan.FromSeconds(5));
            Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count,"Conditional Powerless wire protocol");passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-conditional-powerless-"+context.Database.Name);
                await File.WriteAllTextAsync(stem+"-debug.log",context.Server.DebugLog());
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Database=context.Database.Name,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceAssemblySha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Actors=new[]{caster,victim}.Select(p=>new{p.Handle,AllRpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()}),PreparationOnly="strict QA883/201 state; not a player ability grant",NativePresentationProven=false},new JsonSerializerOptions{WriteIndented=true}));
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Required conditional proof also failed: "+proofError);}
        }
    }
}
