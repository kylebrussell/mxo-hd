using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task NativeQaDuelHelper(ScenarioContext context)
    {
        var actor=await context.AddPlayerAsync(new CharacterSeed("CSR_Tester"){Level=50,Organization=1,Health=5000,MaxHealth=5000,OwnedLoadedAbilities=new (ushort,ushort)[]{(2,50),(17,50)}});
        var helper=await context.AddPlayerAsync(new CharacterSeed("QAEnemy"){X=17120,Level=50,Organization=2,Health=5000,MaxHealth=5000,OwnedLoadedAbilities=new (ushort,ushort)[]{(2,50),(17,50)}});
        string step="entry";Exception? failure=null;bool passed=false;HelperDuelAcceptance.Evidence? accepted=null;HelperDuelAcceptance.Armed? armedState=null;
        try
        {
            await actor.EnterWorldAsync();await helper.EnterWorldAsync();
            var target=await actor.WaitForViewNamedAsync(helper.Handle);await helper.WaitForViewNamedAsync(actor.Handle);
            actor.SendRpc($"40 {HandleOf(target)} 01 6b");await actor.WaitForRpcAsync($"4d 18 00 00 00 {HandleOf(target)}");
            step="armed-challenge";var armed=new TaskCompletionSource<HelperDuelAcceptance.Armed>(TaskCreationOptions.RunContinuationsAsynchronously);
            var accepting=HelperDuelAcceptance.AcceptNextAsync(helper,actor.Handle,onArmed:state=>{armed.SetResult(state);return Task.CompletedTask;});
            armedState=await armed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Check.That(helper.World.RpcSnapshot().Count>=armedState.RpcCursor,"Challenge starts only after actual helper arm cursor");
            actor.SendRpc($"50 {HandleOf(target)}");accepted=await accepting;
            await actor.WaitForRpcAsync($"55 {HandleOf(target)}");
            Check.That(hds.PvpRules.DuelOpponents(accepted.SelfDuelId,accepted.ChallengerDuelId),"Actual new paired IDs make only these actors opponents");
            step="legal-fight";actor.SendRpc($"40 {HandleOf(target)} 01 6b");await actor.WaitForRpcAsync($"46 {HandleOf(target)} 01");
            await WaitForCombatView(actor);await WaitForCombatView(helper);
            await actor.WaitUntilAsync(()=>OwnHealth(actor)<5000||OwnHealth(helper)<5000,"Ordinary legal duel exchanges actual health damage",TimeSpan.FromSeconds(45));
            step="normal-stop";actor.SendRpc("50 00 00 00 00");await actor.WaitForRpcAsync("56");await helper.WaitForRpcAsync("56");
            int begins=helper.Received("55").Count;step="stale-record-refusal";bool timeout=false;
            try{await HelperDuelAcceptance.AcceptNextAsync(helper,actor.Handle,TimeSpan.FromSeconds(1));}catch(TimeoutException){timeout=true;}
            Check.That(timeout,"A historical51 cannot be reused by a new armed action");Check.Equal(begins,helper.Received("55").Count,"No invented begin from stale challenge history");
            Check.Equal(0,actor.World.Problems.Count+helper.World.Problems.Count,"Duel helper wire framing");step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-nativeqa-duel-"+context.Database.Name);
                await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Database=context.Database.Name,Accepted=accepted,Armed=armedState,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Actors=new[]{actor,helper}.Select(p=>new{p.Handle,p.CharId,AllRpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()}),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Required duel evidence also failed; retaining original: "+proofError);}
        }
    }
}
