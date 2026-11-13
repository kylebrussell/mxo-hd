using hds;
using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static bool AcquisitionHeader(RpcRecord rpc,byte first,byte second)=>rpc.Bytes.Length>=2&&rpc.Bytes[0]==first&&rpc.Bytes[1]==second;
    private static async Task AbilityAcquisitionCase(ScenarioContext context)
    {
        const uint code=0x80001400,other=0x80000400;const uint vendor=277874480;
        var sessions=new List<HeadlessPlayer>();var operations=new List<object>();string step="entry";Exception? failure=null;bool passed=false;
        HeadlessPlayer? p=null;ServerInstance? reloaded=null;
        try
        {
            p=(await context.AddPlayersInWorldAsync(new CharacterSeed("Acquire"){X=17000,Y=495,Z=2300,Info=200000,Level=50,Experience=checked((int)PlayerProgression.ExperienceForLevel(50)),
                OwnedLoadedAbilities=new[]{((ushort)2,(ushort)1),((ushort)17,(ushort)1)},Items=new[]{(11,7332u,2)}}))[0];sessions.Add(p);
            Task<long> Cash()=>context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
            Task<long> Count(uint id)=>context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid={id}");
            async Task Barrier(){p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
            async Task Buy(uint id,bool accept)
            {
                int cursor=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc("81 0e "+Le(id)+" 00");
                if(accept)await p.WaitForRpcAsync("81 0f");else{await p.WaitForTextAsync("purchase could not be completed");await Barrier();}
                var raw=p.World.RpcSnapshot().Skip(cursor).ToArray();operations.Add(new{Step=step,Cursor=cursor,Records=raw.Select(r=>r.Hex).ToArray()});
                if(accept)
                {
                    int add=Array.FindIndex(raw,r=>r.Bytes[0]==0x5e),delta=Array.FindIndex(raw,r=>AcquisitionHeader(r,0x80,0xe7)),current=Array.FindIndex(raw,r=>AcquisitionHeader(r,0x80,0xe4)),ack=Array.FindIndex(raw,r=>AcquisitionHeader(r,0x81,0x0f));
                    Check.That(add>=0&&delta>add&&current>delta&&ack>current,"Committed normal purchase preserves retail packet order");
                    uint price=VendorPricing.GetBuyPrice(id);Check.That(raw.Any(r=>r.Bytes.SequenceEqual(ServerPackets.BuildAbilityCodeBuyAck(id,price))),"Literal ordinary ability buy ack");
                }
                else Check.Equal(0,raw.Count(r=>r.Bytes[0]==0x5e||AcquisitionHeader(r,0x80,0xe7)||AcquisitionHeader(r,0x80,0xe4)||AcquisitionHeader(r,0x81,0x0f)),"No success publication after rollback");
            }
            step="failure-trigger-preflight";
            await context.Database.InstallAcquisitionRollbackTriggerAsync(p.CharId,unchecked((int)code));
            await context.Database.RemoveAcquisitionRollbackTriggerAsync();
            p.SendRpc("80 c8 "+Le(vendor)+" 02 00");await p.WaitForRpcAsync("81 0d");
            step="buy-second-write-rollback";
            await context.Database.ExecuteAsync($"ALTER TABLE characters ADD CONSTRAINT acquisition_wallet_failure CHECK(handle<>'Acquire' OR cash<>160000)");
            await Buy(code,false);Check.Equal(200000L,await Cash(),"Cash rolled back");Check.Equal(0L,await Count(code),"Inserted bag item rolled back with wallet");
            await context.Database.ExecuteAsync("ALTER TABLE characters DROP CONSTRAINT acquisition_wallet_failure");
            step="repeat-real-purchases";await Buy(code,true);await Buy(code,true);Check.Equal(120000L,await Cash(),"Repeated BUY charges per item");Check.Equal(2L,await Count(code),"Repeated BUY creates two separate codes");
            step="buyback-not-normal-purchase";p.SkipReceived();p.SendRpc("81 0e "+Le(code)+" 01");await p.WaitForTextAsync("not supported");Check.Equal(120000L,await Cash(),"Unsupported buyback cannot buy");
            step="learn-second-write-rollback";
            int before=p.World.RpcSnapshot().Count;
            await context.Database.InstallAcquisitionRollbackTriggerAsync(p.CharId,unchecked((int)code));
            await ZionUpload(p);Check.Equal(2L,await Count(code),"Failed archive insert restores bag code");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND ability_id={unchecked((int)code)}"),"No failed archive row");
            var failed=p.World.RpcSnapshot().Skip(before).ToArray();operations.Add(new{Step=step,Cursor=before,Records=failed.Select(r=>r.Hex).ToArray()});
            Check.Equal(0,failed.Count(r=>r.Bytes.SequenceEqual(WireFormat.FromHex("5f 01 00 01"))||r.Bytes.SequenceEqual(WireFormat.FromHex("5f 02 00 01"))||
                (r.Bytes.Length==12&&r.Bytes[0]==0x5e&&BitConverter.ToUInt32(r.Bytes,7)==(code|1u))),"Failed ability learning publishes neither its bag delete nor archive add; independent Code Bits remain unchanged in scope");
            await context.Database.RemoveAcquisitionRollbackTriggerAsync();
            step="hardline-learn-and-canonical-duplicate";p.SkipReceived();await ZionUpload(p);
            Check.Equal(1L,await Count(code),"One code learned; duplicate retained");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND ability_id={unchecked((int)code)} AND level=1 AND is_loaded=0 AND slot<520"),"One genuine archive entry, not loaded/granted");
            p.SkipReceived();await ZionUpload(p);Check.Equal(1L,await Count(code),"Repeated upload keeps duplicate code");
            step="la-learning-same-transaction";
            p.SendRpc("80 c8 "+Le(vendor)+" 02 00");await p.WaitForRpcAsync("81 0d");await Buy(other,true);
            ushort bag=checked((ushort)await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={p.CharId} AND goid={other}"));
            await GoToLoadingAreaAsync(p);p.SkipReceived();int laCursor=p.World.RpcSnapshot().Count;p.SendRpc("62 "+Le(bag)+" 6b 00 01");await p.WaitForRpcAsync("81 18 01 01 00 00 00 00");
            operations.Add(new{Step=step,Cursor=laCursor,Records=p.World.RpcSnapshot().Skip(laCursor).Select(r=>r.Hex).ToArray()});Check.Equal(0L,await Count(other),"LA upload consumes code");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND ability_id={unchecked((int)other)} AND level=1 AND is_loaded=0"),"LA same genuine archive entry");
            step="two-connection-race-and-capacity";
            operations.Add(new{Isolation=await context.Database.ScalarAsync("SELECT @@session.transaction_isolation"),Engines=await context.Database.ExecuteAsync("SELECT TABLE_NAME,ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','inventory','char_abilities')")});
            var config=new ServerConfig(Path.Combine(context.RepositoryRoot,"hds","bin","Debug","net8.0","Config.xml.dist"));config.LoadDbParams();Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned race DB");config.dbParams.DatabaseName=context.Database.Name;
            var old=hds.shared.Store.config;hds.shared.Store.config=config;
            try
            {
                await context.Database.ExecuteAsync($"UPDATE characters SET cash=40000 WHERE charId={p.CharId}");
                var db=new hds.databases.MyWorldDbAccess();
                var race=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>{try{db.AcquireAbility(p.CharId,code,40000);return true;}catch(AbilityAcquisition.Refusal){return false;}})));
                Check.Equal(1,race.Count(x=>x),"Independent SQL connections cannot overspend");Check.Equal(0L,await Cash(),"Exactly one debit");
                Check.Equal(2L,await Count(code),"Exactly one race item joins retained duplicate");
                operations.Add(new{Step=step,Accepted=race});
                step="canonical-alias-and-family";
                await context.Database.ExecuteAsync($"UPDATE char_abilities SET ability_id={unchecked((int)(code|0x00400000u))} WHERE char_id={p.CharId} AND ability_id={unchecked((int)code)}");
                ushort duplicateSlot=checked((ushort)await context.Database.LongAsync($"SELECT MIN(slot) FROM inventory WHERE charId={p.CharId} AND goid={code}"));
                bool refused=false;try{db.AcquireAbility(p.CharId,code,0,duplicateSlot);}catch(AbilityAcquisition.Refusal){refused=true;}Check.That(refused,"Canonical alias duplicate remains owned and bag code stays");Check.Equal(2L,await Count(code),"Alias refusal keeps both codes");
                await context.Database.ExecuteAsync($"UPDATE char_abilities SET ability_id={unchecked((int)code)} WHERE char_id={p.CharId} AND ability_id={unchecked((int)(code|0x00400000u))}");
                refused=false;try{db.AcquireAbility(p.CharId,0x8404c180,0);}catch(AbilityAcquisition.Refusal){refused=true;}Check.That(refused,"Known-ID code-family alias cannot be purchased as an ability");
                step="last-slot-and-archive-full";
                string occupied=await context.Database.ScalarAsync($"SELECT GROUP_CONCAT(slot) FROM char_abilities WHERE char_id={p.CharId} AND slot<520");var used=occupied.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
                var filler=Enumerable.Range(0,519).Where(i=>!used.Contains(i)).ToArray();
                await context.Database.ExecuteAsync("INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES"+string.Join(",",filler.Select(i=>$"({p.CharId},{i},2214904192,0,0,'acquisition filler')")));
                await context.Database.ExecuteAsync($"INSERT INTO inventory(charId,slot,goid,count) VALUES({p.CharId},20,{unchecked((int)0x80001800u)},1),({p.CharId},21,2147491840,1)");
                long corruptArchive=(long)0x80001800u+(1L<<32),corruptBag=(long)0x80002400u+(1L<<32);
                await context.Database.ExecuteAsync($"UPDATE char_abilities SET ability_id={corruptArchive} WHERE char_id={p.CharId} AND ability_name='acquisition filler' AND slot={filler[0]}; INSERT INTO inventory(charId,slot,goid,count) VALUES({p.CharId},22,{corruptBag},1)");
                bool rawRejected=false;try{db.AcquireAbility(p.CharId,0x80002400,0,22);}catch(AbilityAcquisition.Refusal){rawRejected=true;}Check.That(rawRejected,"Corrupt64 bag value cannot alias a32-bit ability");
                Check.Equal(corruptBag,await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={p.CharId} AND slot=22"),"Corrupt64 source remains exact");
                await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot=22");
                var last=db.AcquireAbility(p.CharId,0x80001800,0,20);Check.Equal((ushort)519,last.ArchiveSlot,"Last real archive slot is usable");
                refused=false;try{db.AcquireAbility(p.CharId,0x80002000,0,21);}catch(AbilityAcquisition.Refusal){refused=true;}Check.That(refused,"Archive full retains the unowned code");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND slot=21 AND goid=2147491840"),"Full archive does not delete source");
                await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId} AND ability_name='acquisition filler'");
                step="two-connection-learning-race";
                var learns=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>{try{db.AcquireAbility(p.CharId,0x80002000,0,21);return true;}catch(AbilityAcquisition.Refusal){return false;}})));
                Check.Equal(1,learns.Count(x=>x),"Independent learning connections consume one exact source only");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND ability_id={unchecked((int)0x80002000u)}"),"Learning race adds exactly one archive row");
                operations.Add(new{Step=step,Accepted=learns,LastSlot=last.ArchiveSlot,SignedRawSource=unchecked((int)0x80001800u),CorruptArchive=corruptArchive,CorruptBag=corruptBag});
            }
            finally{hds.shared.Store.config=old;}
            step="cold-relog-known-archive";string user=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");p.Dispose();context.Server.Dispose();
            using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);reloaded=server;using var again=await HeadlessPlayer.LoginAsync(server.Address,user,p.Handle);sessions.Add(again);
            byte[] known=again.CharacterData[0x07];Check.That(BytePattern.IndexOf(known,BitConverter.GetBytes(code|1u),0)>=0,"Calm Mind known archive survives cold login");Check.That(BytePattern.IndexOf(known,BitConverter.GetBytes(other|1u),0)>=0,"LA learned archive survives cold login");
            await again.EnterWorldAsync();foreach(var session in sessions)Check.Equal(0,session.World.Problems.Count,"Acquisition parser clean");step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            bool wasSuccess=failure==null;Exception? cleanupFailure=null;
            try{await context.Database.RemoveAcquisitionRollbackTriggerAsync();}
            catch(Exception error){cleanupFailure=error;if(wasSuccess){failure=error;passed=false;}Console.Error.WriteLine("Acquisition trigger cleanup: "+error);}
            string proof=Path.Combine(Path.GetTempPath(),"mxo-ability-acquisition-"+context.Database.Name+".json");
            try{string dll=context.Server.SourceAssembly;await File.WriteAllTextAsync(proof,JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),Operations=operations,Sessions=sessions.Select(p=>new{p.Handle,p.CharId,CharacterData=p.CharacterData.ToDictionary(k=>k.Key,v=>Convert.ToHexString(v.Value)),Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems.ToArray()}),Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),RelogDebug=reloaded?.DebugLog(),RelogOutput=reloaded?.FullOutput(),NativePickingProven=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Acquisition proof before teardown: "+proof);}
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
            if(wasSuccess&&cleanupFailure!=null)throw cleanupFailure;
        }
    }
}
