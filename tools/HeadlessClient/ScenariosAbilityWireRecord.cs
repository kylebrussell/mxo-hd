using System.Security.Cryptography;
using System.Text.Json;
using hds;
using MxoHd.PacketResearch;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task AbilityWireRecord(ScenarioContext context)
    {
        var actor=await context.AddPlayerAsync(new CharacterSeed("WireHealer"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Health=100,MaxHealth=1000,InnerStrength=1000,MaxInnerStrength=1000,OwnedLoadedAbilities=new (ushort,ushort)[]{(2,1),(17,1),(45,1),(1006,1),(77,1)}});
        bool passed=false;string step="entry";Exception? failure=null;byte[]? sent=null;ViewCreation? self=null;int healthViewCursor=-1;
        try
        {
            Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned health-preparation fixture");
            await actor.EnterWorldAsync();self=actor.World.CreationSnapshot().Last(v=>v.ViewId==2);actor.SendRpc("0c 01");
            step="ordinary-health-preparation";actor.SendChat("?health 100");await actor.WaitUntilAsync(()=>OwnHealth(actor)==100,"Actual own health preparation",TimeSpan.FromSeconds(10));
            step="normal77-full-record";
            var record=new AbilityUseRecord(77,DuelPackets.Handle(self.ViewId,self.SpawnId),0,0,0,0,0x6b,0x6b,0,0);sent=record.Encode();
            Check.That(AbilityUseRecord.TryDecode(sent,out var decoded)&&decoded==record,"Exact full43-byte request roundtrip");
            actor.SkipReceived();healthViewCursor=actor.World.ViewSnapshot().Count;actor.SendRpc(sent);await actor.WaitForRpcAsync("80 ac 4d 00");
            await actor.WaitForRpcAsync("80 bd 01 4d 00");
            await actor.WaitUntilAsync(()=>context.Server.DebugLog().Contains("Heal 77 -> WireHealer for "),"Existing ordinary heal executor witnessed",TimeSpan.FromSeconds(10));
            Check.That(ViewUpdateValues(actor,2,"02 80 80 80 40",healthViewCursor).Any(v=>v>=109),"Actual own health reply after full request");
            Check.Equal(0,actor.World.Problems.Count,"Full record canary framing");step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-ability-wire-"+context.Database.Name);
                await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Database=context.Database.Name,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),SentRpc=sent==null?null:Convert.ToHexString(sent),SelfView=self?.ViewId,SelfSpawn=self?.SpawnId,HealthViewCursor=healthViewCursor,CurrentOperationHealthValues=healthViewCursor<0?Array.Empty<ushort>():ViewUpdateValues(actor,2,"02 80 80 80 40",healthViewCursor).ToArray(),AllRpc=actor.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),ReceivedViewLists=actor.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=actor.World.Problems.ToArray(),NativeUiProven=false,AllFieldsConsumedClaim=false},new JsonSerializerOptions{WriteIndented=true}));
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Required wire-record proof also failed: "+proofError);}
        }
    }
}
