using MxoHd.PacketResearch;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task StatProducers(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("StatProducers"){Level=50,InnerStrength=1000,MaxInnerStrength=1000,
            OwnedLoadedAbilities=new (ushort,ushort)[]{(32,50),(75,50),(356,50),(314,50),(2,50),(17,50)}});
        // Ownership and loaded flags were seeded BEFORE Login, so initial setup is the actual tested path.
        string ownedRows=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={p.CharId} AND slot IN (400,401,402,403) ORDER BY slot");
        Check.Equal(4L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND slot IN (400,401,402,403) AND level=50 AND is_loaded=1"),"Exact owned fixture rows before world admission");
        int readyRpcIndex=-1; bool selfBeforeReady=false; bool initialProven=false; bool relogProven=false; HeadlessPlayer? relogged=null; string[] firstSessionRpc=Array.Empty<string>(); string[] firstSessionViews=Array.Empty<string>(); Exception? scenarioFailure=null;
        try
        {
        await p.EnterWorldAsync();
        // WaitForRpcAsync advances a cursor: startup32 precedes75.
        // Snapshot predicates preserve the exact contracts without depending on receipt/wait order.
        await p.WaitUntilAsync(()=>p.Received(PassiveRecord(32,1147,10)).Count>0 &&
            p.Received(PassiveRecord(75,1055,25)).Count>0,
            "Exact startup32/75 records",TimeSpan.FromSeconds(10));
        Check.Equal(0,p.Received("80 b2 3f 04").Count,"Reviewed child is deferred until native loading-complete");
        readyRpcIndex=p.World.RpcSnapshot().Count; selfBeforeReady=p.World.CreationSnapshot().Any(c=>c.ViewId==2);
        Check.That(selfBeforeReady,"Actual self creation was received before readiness");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND ability_id={0x80000000L|((long)1087<<10)}"),"Child registration never adds direct archive ownership");
        p.SendRpc("0c 01");
        await p.WaitForRpcAsync("80 b2 3f 04 00 00 08 02");
        await p.WaitForRpcAsync("80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 00 00 00 00 00");
        var grantAdd=BytePattern.Parse("80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 00 00 00 00 00");
        var grantOff=BytePattern.Parse("80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 01 00 00 00 00");
        var initial=p.World.RpcSnapshot().Select(r=>r.Bytes).ToList();
        int childBase=initial.FindIndex(b=>BytePattern.Parse("80 b2 3f 04 00 00 08 02").IsMatch(b));
        int childGrant=initial.FindIndex(b=>grantAdd.IsMatch(b));
        Check.That(childBase>=readyRpcIndex && childGrant>childBase,"Initial actual self -> ready -> base -> grant order");
        initialProven=true;
        int firstAdds=p.World.RpcSnapshot().Count(r=>grantAdd.IsMatch(r.Bytes));
        p.SendRpc("0c 01");p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
        Check.Equal(firstAdds,p.World.RpcSnapshot().Count(r=>grantAdd.IsMatch(r.Bytes)),"Duplicate loading-complete does not register again");
        int unloadStart=p.World.RpcSnapshot().Count;
        SetAbilityLoaded(p,402,356,50,false);await p.WaitForRpcAsync("80 b3 3f 04");
        await p.WaitForRpcAsync("80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 01 00 00 00 00");
        var unloaded=p.World.RpcSnapshot().Skip(unloadStart).Select(r=>r.Bytes).ToList();
        Check.That(unloaded.FindIndex(b=>BytePattern.Parse("80 b3 3f 04").IsMatch(b))<unloaded.FindIndex(b=>grantOff.IsMatch(b)),"Child unload precedes captured grant removal");
        SetAbilityLoaded(p,402,356,50,true);await p.WaitForRpcAsync("80 b2 64 01 32 00 92 01");
        await p.WaitForRpcAsync("80 b2 3f 04 00 00 08 02");
        await p.WaitForRpcAsync("80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 00 00 00 00 00");
        SetAbilityLoaded(p,401,75,50,false);await p.WaitForRpcAsync(PassiveRecord(75,1055,25,true));
        SetAbilityLoaded(p,401,75,50,true);await p.WaitForRpcAsync("80 b2 4b 00 32 00 91 01");
        Cast(p,1087);await p.WaitForRpcAsync(ModPattern(1087,177));
        Cast(p,314);await p.WaitForRpcAsync(ModPattern(314,342));await p.WaitForRpcAsync(ModPattern(314,342,true),TimeSpan.FromSeconds(30));
        var wire=p.World.RpcSnapshot().Where(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&(r.Bytes[1]==0xb2||r.Bytes[1]==0xbc)).ToArray();
        var bases=wire.Where(r=>r.Bytes[1]==0xb2).Select(r=>BaseAttributeRecord.TryDecode(r.Bytes,out var x)?x:null).ToArray();
        var mods=wire.Where(r=>r.Bytes[1]==0xbc).Select(r=>ManageBonusRecord.TryDecode(r.Bytes,out var x)?x:null).ToArray();
        Check.That(bases.All(r=>r!=null)&&mods.All(r=>r!=null),"Every received producer RPC has its exact supported width");
        Check.That(bases.Any(r=>r!.Id==1087&&r.Value==0&&r.CsSlot==520),"Initial loading-complete supplies captured zero-level hidden grant");
        Check.That(bases.Any(r=>r!.Id==75&&r.Value==50&&r.CsSlot==401),"Base value and archive slot independently decoded");
        Check.That(mods.Any(r=>r!.SourceId==75&&r.TargetId==1055&&r.SignedValue==25&&r.IndividualOperation==IndividualBonusOperation.Add),"Raw passive75 owns1055, not177");
        var how=mods.Single(r=>r!.SourceId==1087&&r.TargetId==177&&r.AddRemoveType==0)!;
        Check.Equal(17,how.SignedValue,"Independent captured Howitzer direct177");
        Check.That(mods.Any(r=>r!.SourceId==1087&&r.TargetId==342&&r.SignedValue==13&&r.AddRemoveType==0),"Howitzer separately supplies captured direct342");
        Check.That(mods.Any(r=>r!.SourceId==75&&r.TargetId==1055&&r.AddRemoveType==1),"Ordinary passive unload supplies actual Remove");
        var timed=mods.Single(r=>r!.SourceId==314&&r.TargetId==342&&r.AddRemoveType==0)!;
        Check.Equal(108,timed.SignedValue,"Captured314 direct342");Check.Equal(20f,timed.Duration,"Real20s expiry");
        Check.That(mods.Any(r=>r!.SourceId==314&&r.TargetId==342&&r.AddRemoveType==1&&r.Locator==timed.Locator),"Expiry removes same timed locator");
        Check.Equal(0,p.World.Problems.Count,"Producer protocol errors");
        p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");
        firstSessionRpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray();
        firstSessionViews=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray();
        uint actorId=p.CharId;p.Dispose();
        // Ordinary logout reservation cleanup, not a production timer or registration workaround.
        await Task.Delay(TimeSpan.FromSeconds(33));
        p=relogged=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+actorId,"StatProducers");
        await p.EnterWorldAsync();
        Check.That(p.World.CreationSnapshot().Any(c=>c.ViewId==2),"Relog receives actual self before readiness");
        Check.Equal(0,p.Received("80 b2 3f 04").Count,"Relog child remains deferred before readiness");
        int relogReadyIndex=p.World.RpcSnapshot().Count;
        p.SendRpc("0c 01");await p.WaitForRpcAsync("80 b2 3f 04 00 00 08 02");
        await p.WaitForRpcAsync("80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 00 00 00 00 00");
        var relogRpc=p.World.RpcSnapshot().Select(r=>r.Bytes).ToList();
        int relogBase=relogRpc.FindIndex(b=>BytePattern.Parse("80 b2 3f 04 00 00 08 02").IsMatch(b));
        int relogGrant=relogRpc.FindIndex(b=>grantAdd.IsMatch(b));
        Check.That(relogBase>=relogReadyIndex&&relogGrant>relogBase,"Relog self -> ready -> base -> grant without parent reload");
        p.SendRpc("0c 01");p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
        Check.Equal(1,p.World.RpcSnapshot().Count(r=>grantAdd.IsMatch(r.Bytes)),"Relog readiness remains idempotent");
        Check.Equal(0,p.World.Problems.Count,"Relog protocol errors");relogProven=true;
        }
        catch(Exception error){scenarioFailure=error;throw;}
        finally
        {
          try
          {
            File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-stat-producers-wire-"+context.Database.Name+"-debug.log"),context.Server.DebugLog());
            var wire=p.World.RpcSnapshot().Where(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&(r.Bytes[1]==0xb2||r.Bytes[1]==0xbc)).ToArray();
            var bases=wire.Where(r=>r.Bytes[1]==0xb2).Select(r=>BaseAttributeRecord.TryDecode(r.Bytes,out var x)?x:null).ToArray();
            var mods=wire.Where(r=>r.Bytes[1]==0xbc).Select(r=>ManageBonusRecord.TryDecode(r.Bytes,out var x)?x:null).ToArray();
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-stat-producers-wire-"+context.Database.Name+".json"),JsonSerializer.Serialize(new{OwnedRowsBeforeWorld=ownedRows,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceAssemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Bases=bases,Modifiers=mods,Raw=wire.Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),AllRpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),ProtocolProblems=p.World.Problems.ToArray(),InitialReadyRpcIndex=readyRpcIndex,SelfBeforeReady=selfBeforeReady,GrantRegistrationInitial=initialProven,GrantRegistrationRelog=relogProven,FirstSessionRpc=firstSessionRpc,FirstSessionViewLists=firstSessionViews,ScenarioFailure=scenarioFailure?.ToString(),ManualPostEntryReloadWorkaround=false,CausalPassiveConversionProven=false},new JsonSerializerOptions{WriteIndented=true}));
          }
          catch(Exception proofError)
          {
            if(scenarioFailure==null)throw;
            Console.Error.WriteLine("Required grant evidence also failed; preserving original failure: "+proofError);
          }
          finally { relogged?.Dispose(); }
        }
    }
}
