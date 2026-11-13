using MxoHd.PacketResearch;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task StatProducers(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("StatProducers"){Level=50,InnerStrength=1000,MaxInnerStrength=1000});
        await LoadAbilitiesAsync(context,p,32,75,356,314);
        string ownedRows=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={p.CharId} AND slot IN (400,401,402,403) ORDER BY slot");
        Check.Equal(4L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND slot IN (400,401,402,403) AND level=50 AND is_loaded=1"),"Exact owned fixture rows before world admission");
        try
        {
        await p.EnterWorldAsync();
        // WaitForRpcAsync advances a cursor: startup32 precedes75.
        // Snapshot predicates preserve the exact contracts without depending on receipt/wait order.
        await p.WaitUntilAsync(()=>p.Received(PassiveRecord(32,1147,10)).Count>0 &&
            p.Received(PassiveRecord(75,1055,25)).Count>0,
            "Exact startup32/75 records",TimeSpan.FromSeconds(10));
        // Initial setup ran during Login before these fixture archive rows were inserted.
        // Ordinary owned356 load emits its parent base and normal hidden grant, without injecting1087.
        SetAbilityLoaded(p,402,356,50,true);
        await p.WaitForRpcAsync("80 b2 64 01 32 00 92 01");
        await p.WaitForRpcAsync("80 b2 3f 04 00 00 08 02");
        SetAbilityLoaded(p,401,75,50,false);await p.WaitForRpcAsync(PassiveRecord(75,1055,25,true));
        SetAbilityLoaded(p,401,75,50,true);await p.WaitForRpcAsync("80 b2 4b 00 32 00 91 01");
        Cast(p,1087);await p.WaitForRpcAsync(ModPattern(1087,177));
        Cast(p,314);await p.WaitForRpcAsync(ModPattern(314,342));await p.WaitForRpcAsync(ModPattern(314,342,true),TimeSpan.FromSeconds(30));
        var wire=p.World.RpcSnapshot().Where(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&(r.Bytes[1]==0xb2||r.Bytes[1]==0xbc)).ToArray();
        var bases=wire.Where(r=>r.Bytes[1]==0xb2).Select(r=>BaseAttributeRecord.TryDecode(r.Bytes,out var x)?x:null).ToArray();
        var mods=wire.Where(r=>r.Bytes[1]==0xbc).Select(r=>ManageBonusRecord.TryDecode(r.Bytes,out var x)?x:null).ToArray();
        Check.That(bases.All(r=>r!=null)&&mods.All(r=>r!=null),"Every received producer RPC has its exact supported width");
        Check.That(bases.Any(r=>r!.Id==1087&&r.Value==0&&r.CsSlot==520),"Ordinary parent load supplies hidden grant base record");
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
        }
        finally
        {
            var wire=p.World.RpcSnapshot().Where(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&(r.Bytes[1]==0xb2||r.Bytes[1]==0xbc)).ToArray();
            var bases=wire.Where(r=>r.Bytes[1]==0xb2).Select(r=>BaseAttributeRecord.TryDecode(r.Bytes,out var x)?x:null).ToArray();
            var mods=wire.Where(r=>r.Bytes[1]==0xbc).Select(r=>ManageBonusRecord.TryDecode(r.Bytes,out var x)?x:null).ToArray();
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-stat-producers-wire-"+context.Database.Name+".json"),JsonSerializer.Serialize(new{OwnedRowsBeforeWorld=ownedRows,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceAssemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Bases=bases,Modifiers=mods,Raw=wire.Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),CausalPassiveConversionProven=false},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
