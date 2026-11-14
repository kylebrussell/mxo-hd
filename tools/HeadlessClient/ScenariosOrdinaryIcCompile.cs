using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using hds;

namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task OrdinaryIcCompile(ScenarioContext context)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { Phase="source",context.Server.SourceAssembly,
            Configuration=new DirectoryInfo(Path.GetDirectoryName(context.Server.SourceAssembly)!).Parent!.Name,
            Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))) }));
        var p=await context.AddPlayerAsync(new CharacterSeed("OrdinaryCompiler") { Level=50,Info=200000,Items=new[]{(1,1271u,1)} });
        const uint code=0x40000000u | 39413u<<7; // retained opaque upper bit, excluded from decodedGOID
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES"+
            $"({p.CharId},29,{AbilityGoid(2)},50,1),({p.CharId},30,{AbilityGoid(277)},50,1),({p.CharId},31,{AbilityGoid(24)},1,0),({p.CharId},32,{AbilityGoid(37)},1,0),({p.CharId},33,{AbilityGoid(1008)},1,0),({p.CharId},34,{AbilityGoid(36)},1,0),({p.CharId},400,{code},2,0),({p.CharId},401,{2399u<<7},1,0)");
        // Preflight the real CHECK before timed work. Only the later167120-9 debit is rejected.
        await context.Database.ExecuteAsync("ALTER TABLE characters ADD CONSTRAINT ordinary_ic_refuse_debit CHECK(handle<>'OrdinaryCompiler' OR cash<>167111)");
        await p.EnterWorldAsync();
        await p.WaitForRpcAsync(PassiveRecord(277,706,50));
        var actual706=p.Received(PassiveRecord(277,706,50)).Select(r=>WireFormat.ToHex(r.Bytes)).ToArray();
        // Same prerequisites as the native fixture, loaded through ordinary80ae, never SQL flags.
        foreach(var prerequisite in new[]{(Id:(ushort)37,Slot:(ushort)32),(Id:(ushort)1008,Slot:(ushort)33),(Id:(ushort)36,Slot:(ushort)34)})
        {
            p.SendChat("?qaload "+prerequisite.Id);
            await p.WaitForRpcAsync($"80 b2 {Le(prerequisite.Id)} 01 00 {Le(prerequisite.Slot)}");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={p.CharId} AND slot={prerequisite.Slot}"),"Normal prerequisite load "+prerequisite.Id);
        }
        Check.Equal(0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={p.CharId} AND slot=31"),"Baseline Minor Repairs remains off");
        string request=$"60 {Le(39413u)} 90 01 00 01 01";
        p.SkipReceived(); p.SendRpc(request); await p.WaitForRpcAsync("61 01");
        CompilerTool(p,1,true); await p.WaitForRpcAsync(ModPattern(969,968));
        p.SkipReceived(); var watch=Stopwatch.StartNew(); p.SendRpc(request);
        await Task.Delay(800);
        Check.Equal(2L,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={p.CharId} AND slot=400"),"No early charge");
        Check.Equal(200000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"No early debit");
        p.SendRpc(request); await p.WaitForRpcAsync("61 01"); // duplicate admission refused, first still owns completion
        await p.WaitForRpcAsync("61 00",TimeSpan.FromSeconds(22));
        double baselineElapsed=watch.Elapsed.TotalSeconds;
        Check.That(watch.Elapsed.TotalSeconds>=17.8 && watch.Elapsed.TotalSeconds<22,"Actual70650 baseline timer18s");
        Check.That(p.World.RpcSnapshot().Any(r=>r.Bytes.SequenceEqual(OrdinaryItemCode.Update(400,code,1))),"Exact ordinary-charge update precedes61 success");
        Console.WriteLine($"ORDINARY_IC baseline passed elapsed={baselineElapsed:F3}s");
        Check.Equal(183560L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Source float32 Info cost16440");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={p.CharId} AND goid=39413"),"Duplicate request cannot duplicate output");
        CompilerTool(p,1,false); await p.WaitForRpcAsync(ModPattern(969,968,true));
        p.SendChat("?qaload 24"); await p.WaitForRpcAsync(PassiveRecord(24,955,33));
        var actual955=p.Received(PassiveRecord(24,955,33)).Select(r=>WireFormat.ToHex(r.Bytes)).ToArray();
        CompilerTool(p,1,true); await p.WaitForRpcAsync(ModPattern(969,968));
        p.SkipReceived(); watch.Restart(); p.SendRpc(request);
        await p.WaitForRpcAsync("61 00",TimeSpan.FromSeconds(16));
        double reducedElapsed=watch.Elapsed.TotalSeconds;
        Check.That(watch.Elapsed.TotalSeconds>=11.9 && watch.Elapsed.TotalSeconds<16,"Same706/954 with95533 timer12.06s");
        Check.Equal(1,p.Received("5f 90 01 00").Count,"Final charge removal precedes61 success");
        Console.WriteLine($"ORDINARY_IC reduced passed elapsed={reducedElapsed:F3}s");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND slot=400"),"Last direct charge removed");
        Check.Equal(167120L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Only two successful debits");
        Check.Equal(2L,await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={p.CharId} AND goid=39413"),"Two single-instance outputs");
        // Native2399 low-complexity route clamps3s. Closing the tool cancels without consumption.
        p.SkipReceived(); p.SendRpc($"60 {Le(2399u)} 91 01 00 01 01");
        await Task.Delay(400); CompilerTool(p,1,false);
        await p.WaitForRpcAsync("61 01",TimeSpan.FromSeconds(5));
        await Task.Delay(3100); // No stale delayed completion after the original deadline.
        Check.Equal(1L,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={p.CharId} AND slot=401"),"Cancelled source intact");
        Check.Equal(167120L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Cancelled cash intact");
        Check.Equal(0,p.World.Problems.Count,"All actual result/archive/inventory packets decode");
        File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-ordinary-ic-main-wire.json"),JsonSerializer.Serialize(new {
            Phase="timing-pair-and-cancel",context.Server.SourceAssembly,
            Rpc=p.World.RpcSnapshot().Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),baselineElapsed,reducedElapsed,
            Cash=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}") },new JsonSerializerOptions{WriteIndented=true}));
        // Test the actual transaction concurrently against one exact source snapshot in this disposable DB.
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned disposable atomic fixture");
        var config=new ServerConfig(Path.Combine(Path.GetDirectoryName(context.Server.SourceAssembly)!,"Config.xml.dist")); config.LoadDbParams(); config.dbParams.DatabaseName=context.Database.Name;
        var previous=hds.shared.Store.config; hds.shared.Store.config=config;
        try
        {
            // Offline fixture110 avoids changing the live actor's cached Info through the repository test.
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id=110; DELETE FROM inventory WHERE charId=110; UPDATE characters SET cash=18 WHERE charId=110;"+
                $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES(110,30,{AbilityGoid(277)},50,1),(110,401,{2399u<<7},1,0); INSERT INTO inventory(charId,slot,goid,count) VALUES(110,1,1271,1)");
            var db=new hds.databases.MyWorldDbAccess(); var source=db.GetOrdinaryItemCode(110,401);
            var results=await Task.WhenAll(Task.Run(()=>db.MaterializeOrdinaryItemCode(110,source!,1,9)),Task.Run(()=>db.MaterializeOrdinaryItemCode(110,source!,1,9)));
            Check.Equal(1,results.Count(r=>r.Result==0),"One exact-charge transaction wins concurrent race");
            Check.Equal(1L,await context.Database.LongAsync("SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId=110 AND goid=2399"),"Atomic race one output");
            Check.Equal(9L,await context.Database.LongAsync("SELECT cash FROM characters WHERE charId=110"),"Same-source race one debit");
            await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId=110 AND goid=2399; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES(110,401,{2399u<<7},1,0),(110,402,{2399u<<7},1,0)");
            var first=db.GetOrdinaryItemCode(110,401); var second=db.GetOrdinaryItemCode(110,402);
            results=await Task.WhenAll(Task.Run(()=>db.MaterializeOrdinaryItemCode(110,first!,1,9)),Task.Run(()=>db.MaterializeOrdinaryItemCode(110,second!,1,9)));
            Check.Equal(1,results.Count(r=>r.Result==0),"Different-source cash race one winner");
            Check.Equal(1,results.Count(r=>r.Result==4),"Cash race refuses overdraw");
            Check.Equal(0L,await context.Database.LongAsync("SELECT cash FROM characters WHERE charId=110"),"Cash race one final debit");
            Check.Equal(1L,await context.Database.LongAsync("SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND slot IN (401,402)"),"Losing source remains owned");
        }
        finally { hds.shared.Store.config=previous; }
        Console.WriteLine("ORDINARY_IC same-charge and shared-cash atomic races passed");
        // Native 00:29 close-during-compile observation sent only target-clear8151, not a
        // crafting close. That packet cannot authorize cancellation of an unrelated action.
        var noCancel=await context.AddPlayerAsync(new CharacterSeed("CompilerNoCancel") { Level=50,Info=9,Items=new[]{(1,1271u,1)} });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={noCancel.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES"+
            $"({noCancel.CharId},30,{AbilityGoid(277)},50,1),({noCancel.CharId},401,{2399u<<7},1,0)");
        await noCancel.EnterWorldAsync();
        CompilerTool(noCancel,1,true); await noCancel.WaitForRpcAsync(ModPattern(969,968));
        noCancel.SkipReceived(); noCancel.SendRpc($"60 {Le(2399u)} 91 01 00 01 01");
        await Task.Delay(400); noCancel.SendRpc("81 51 00 00 00 00");
        await noCancel.WaitForRpcAsync("61 00",TimeSpan.FromSeconds(5));
        Check.Equal(0L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={noCancel.CharId}"),"No crafting close: exactly9 charged");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={noCancel.CharId} AND slot=401"),"No crafting close: source consumed");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={noCancel.CharId} AND goid=2399"),"Target-clear leaves pending compile intact");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={noCancel.CharId} AND goid=1271"),"Compiler retained");
        // Force a real late cash-write exception, after tentative inventory/code writes, in this DB only.
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({p.CharId},402,{2399u<<7},1,0)");
        CompilerTool(p,1,true); await p.WaitForRpcAsync(ModPattern(969,968));
        p.SkipReceived(); watch.Restart(); p.SendRpc($"60 {Le(2399u)} 92 01 00 01 01");
        await p.WaitForRpcAsync("61 01",TimeSpan.FromSeconds(5));
        Check.That(watch.Elapsed.TotalSeconds>=2.9,"Late transaction error reaches actual delayed completion");
        Check.Equal(167120L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Exception rolls back cash");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={p.CharId} AND slot=402"),"Exception rolls back code charge");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=2399"),"Exception rolls back tentative output");
        Check.That(context.Server.DebugLog().Contains("[COMPILE60] Completion failed"),"Actual transaction exception is logged");
        await context.Database.ExecuteAsync("ALTER TABLE characters DROP CONSTRAINT ordinary_ic_refuse_debit");
        Console.WriteLine("ORDINARY_IC actual late transaction exception rolled back and61 refusal received");
        // Every possible ordinary draw fails C23/S1; exercise failure without any QA/global RNG override.
        var failed=await context.AddPlayerAsync(new CharacterSeed("CompilerFailure") { Level=50,Info=200000,Items=new[]{(1,1271u,1)} });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={failed.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES"+
            $"({failed.CharId},30,{AbilityGoid(277)},1,1),({failed.CharId},31,{AbilityGoid(24)},1,1),({failed.CharId},400,{39412u<<7},1,0)");
        await failed.EnterWorldAsync();
        await failed.WaitUntilAsync(()=>failed.Received(PassiveRecord(277,706,1)).Any() && failed.Received(PassiveRecord(24,955,33)).Any(),
            "actual failure profile7061/95533 (passive source order independent)",TimeSpan.FromSeconds(10));
        CompilerTool(failed,1,true); await failed.WaitForRpcAsync(ModPattern(969,968));
        failed.SkipReceived(); watch.Restart(); failed.SendRpc($"60 {Le(39412u)} 90 01 00 01 01");
        await failed.WaitForRpcAsync("61 01",TimeSpan.FromSeconds(36));
        Check.That(watch.Elapsed.TotalSeconds>=31.3,"Failure follows actual31.49s execution, not admission refusal");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={failed.CharId} AND slot=400"),"Failed roll leaves direct code charge intact");
        Check.Equal(200000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={failed.CharId}"),"Failed roll leaves cash intact");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={failed.CharId} AND goid=39412"),"Failed roll produces no item");
        // Exercise the discovered mode's exact SQL grammar only in this owned throwaway database.
        string configuration=new DirectoryInfo(Path.GetDirectoryName(context.Server.SourceAssembly)!).Parent!.Name;
        var start=new ProcessStartInfo("dotnet") { RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false };
        start.ArgumentList.Add(Path.Combine(context.RepositoryRoot,"tools","NativeQa","bin",configuration,"net8.0","NativeQa.dll"));
        start.ArgumentList.Add("--fixture-plan"); start.ArgumentList.Add("ordinary-ic-compile");
        using(var process=Process.Start(start)!)
        {
            string json=await process.StandardOutput.ReadToEndAsync(); await process.WaitForExitAsync();
            Check.Equal(0,process.ExitCode,"Read-only mode inspection exits without setup");
            using var manifest=JsonDocument.Parse(json);
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id=110; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES(110,30,{AbilityGoid(277)},50,1)");
            foreach(string stage in new[]{"AfterLoadout","AfterHelpers"})
                foreach(var sql in manifest.RootElement.GetProperty("Plan").GetProperty(stage).EnumerateArray()) await context.Database.ExecuteAsync(sql.GetString()!);
            Check.Equal(2L,await context.Database.LongAsync("SELECT level FROM char_abilities WHERE char_id=110 AND slot=400"),"Exact native mode two-charge SQL parses");
            Check.Equal(50L,await context.Database.LongAsync("SELECT level FROM characters WHERE charId=110"),"Exact final level50");
            Check.Equal(0L,await context.Database.LongAsync("SELECT is_loaded FROM char_abilities WHERE char_id=110 AND slot=300"),"Minor Repairs starts owned/unloaded");
        }
        string destination=Path.Combine(Path.GetTempPath(),"mxo-ordinary-ic-success-debug.log");
        File.WriteAllText(destination,context.Server.DebugLog());
        string wirePath=Path.Combine(Path.GetTempPath(),"mxo-ordinary-ic-actual-wire.json");
        File.WriteAllText(wirePath,JsonSerializer.Serialize(new {
            Actor=p.World.RpcSnapshot().Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),
            FailureActor=failed.World.RpcSnapshot().Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),
            NoCancelActor=noCancel.World.RpcSnapshot().Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),
            FinalCash=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),
            FinalBag=await context.Database.ScalarAsync($"SELECT GROUP_CONCAT(CONCAT(slot,':',goid,':',count) ORDER BY slot) FROM inventory WHERE charId={p.CharId}"),
            FinalCodes=await context.Database.ScalarAsync($"SELECT GROUP_CONCAT(CONCAT(slot,':',ability_id,':',level) ORDER BY slot) FROM char_abilities WHERE char_id={p.CharId} AND slot>=400"),
            FailureCash=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={failed.CharId}"),
            baselineElapsed,reducedElapsed },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new { Case="ordinary-ic-compile",context.Server.SourceAssembly,
            Configuration=configuration,
            DllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))),RawLog=destination,ActualWire=wirePath,
            BaselineSeconds=18,ReducedSeconds=12.0600004196167,baselineElapsed,reducedElapsed,SourceCode=code,SourceStat706=50,SourceStat955=33,
            ActualStat706=actual706,ActualStat955=actual955 }));
    }
}
