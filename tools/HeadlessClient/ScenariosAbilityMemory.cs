using System.Security.Cryptography;
using System.Text.Json;
using hds;
using hds.databases;
using hds.shared;
using MySqlConnector;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task AbilityMemoryAdmission(ScenarioContext context)
    {
        var player=await context.AddPlayerAsync(new CharacterSeed("MemoryCap"){Level=50,Experience=0});
        int Goid(ushort id)=>unchecked((int)(0x80000000u|((uint)id<<10)));
        string filter=$"char_id={player.CharId}";
        var observations=new List<object>();var sessions=new List<object>();
        string step="seed";Exception? failure=null;bool passed=false;HeadlessPlayer? relogged=null;
        object Session(HeadlessPlayer actor,string label)=>new{Label=label,actor.Handle,actor.CharId,
            AllRpc=actor.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),
            ReceivedViewLists=actor.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=actor.World.Problems.ToArray()};
        try
        {
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE {filter}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                $"({player.CharId},500,{Goid(23)},50,1),({player.CharId},501,{Goid(356)},50,1),({player.CharId},502,{Goid(314)},50,0),({player.CharId},503,{Goid(75)},50,0),({player.CharId},504,{Goid(2)},50,1)");
            step="initial-world";await player.EnterWorldAsync();
            step="exact-cap";SetAbilityLoaded(player,502,314,50,true);await player.WaitForRpcAsync("80 b2 3a 01");
            player.SendRpc("81 48");await player.WaitForRpcAsync("81 49");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE {filter} AND slot=502"),"Exact16 memory admits314");
            step="one-over-refusal";int beforeDenied=player.Received("80 b2 4b 00").Count;
            player.SkipReceived();SetAbilityLoaded(player,503,75,50,true);await player.WaitForTextAsync("exceed your memory capacity");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE {filter} AND slot=503"),"One-over batch leaves DB unchanged");
            Check.Equal(beforeDenied,player.Received("80 b2 4b 00").Count,"No success for refused75");
            step="unload-frees-memory";SetAbilityLoaded(player,501,356,50,false);await player.WaitForRpcAsync("80 b3 64 01");
            SetAbilityLoaded(player,503,75,50,true);await player.WaitForRpcAsync("80 b2 4b 00");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE {filter} AND slot=503"),"Committed unload frees memory for ordinary load");
            SetAbilityLoaded(player,502,314,50,false);await player.WaitForRpcAsync("80 b3 3a 01");
            SetAbilityLoaded(player,503,75,50,false);await player.WaitForRpcAsync("80 b3 4b 00");
            step="handler-rollback";await context.Database.InstallAbilityRollbackTriggerAsync();
            player.SkipReceived();
            int beforeRollbackLoads=player.Received("80 b2").Count,beforeRollbackRecords=player.Received("80 bc").Count;
            string batch=$"80 ae 00 00 00 00 00 00 0a 00 02 00 00 {Le((ushort)501)} {Le((uint)356)} {Le((ushort)50)} 00 {Le((ushort)502)} {Le((uint)314)} {Le((ushort)50)}";
            try
            {
                player.SendRpc(batch);await player.WaitForTextAsync("loadout could not be saved");
                Check.Equal(0L,await context.Database.LongAsync($"SELECT SUM(is_loaded) FROM char_abilities WHERE {filter} AND slot IN (501,502)"),"Real handler SQL failure rolls back both additions");
                Check.Equal(beforeRollbackLoads,player.Received("80 b2").Count,"Rollback publishes no load acknowledgement");
                Check.Equal(beforeRollbackRecords,player.Received("80 bc").Count,"Rollback publishes no passive/grant records");
            }
            finally{await context.Database.RemoveAbilityRollbackTriggerAsync();}
            step="handler-retry";player.SendRpc(batch);await player.WaitForRpcAsync("80 b2 64 01");await player.WaitForRpcAsync("80 b2 3a 01");
            SetAbilityLoaded(player,501,356,50,false);await player.WaitForRpcAsync("80 b3 64 01");
            SetAbilityLoaded(player,503,75,50,true);await player.WaitForRpcAsync("80 b2 4b 00");
            string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={player.CharId}");
            step="ordinary-logout";player.SendRpc("80 fe 00");await player.WaitForRpcAsync("80 fd");
            sessions.Add(Session(player,"initial-before-dispose"));player.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(33)); // ordinary UDP inactivity releases the one-live-session reservation
            step="ordinary-relog";
            using(var restored=relogged=await HeadlessPlayer.LoginAsync(context.Server.Address,username,player.Handle))
            {
                await restored.EnterWorldAsync();
                byte[] loaded=restored.CharacterData[6];
                Check.Equal(0,loaded.Length%6,"Margin loaded-ability six-byte rows");
                var ids=Enumerable.Range(0,loaded.Length/6).Select(i=>(BitConverter.ToUInt32(loaded,i*6+2)>>10)&0xfff).ToHashSet();
                Check.That(ids.Contains(75u),"Margin relog restores admitted75");
                Check.That(!ids.Contains(356u),"Margin relog keeps unloaded356 off");
                observations.Add(new{RelogLoadedMargin=Convert.ToHexString(loaded)});
                Check.Equal(0,restored.World.Problems.Count,"Relog protocol framing");
                sessions.Add(Session(restored,"relog-before-dispose"));
            }
            step="connection-race";
            // Two different connections enter the REAL service concurrently, each addition fits16 alone, together20 does not.
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE {filter}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({player.CharId},501,{Goid(23)},50,0),({player.CharId},502,{Goid(71)},50,0)");
            var config=new ServerConfig(Path.Combine(Path.GetDirectoryName(context.Server.SourceAssembly)!,"Config.xml.dist"));config.LoadDbParams();
            Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned transaction fixture");config.dbParams.DatabaseName=context.Database.Name;
            var old=Store.config;Store.config=config;
            try
            {
                var a=new MyWorldDbAccess();var b=new MyWorldDbAccess();using var barrier=new Barrier(2);
                AbilityProgression.LoadoutEntry E(ushort slot,ushort id)=>new(){Slot=slot,Ability=id,Level=50};
                var results=await Task.WhenAll(Task.Run(()=>{barrier.SignalAndWait();return a.CommitAbilityLoadout(player.CharId,new[]{E(501,23)},false,50);}),
                    Task.Run(()=>{barrier.SignalAndWait();return b.CommitAbilityLoadout(player.CharId,new[]{E(502,71)},false,50);}));
                Check.Equal(1,results.Count(r=>r.Refusal==null),"Two connections cannot jointly admit20 into16");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT SUM(is_loaded) FROM char_abilities WHERE {filter}"),"Only one racing durable load");
                // Real provider-backed values, not ExecuteNonQuery's row count. SHOW supports either server variable name.
                var db=config.dbParams;
                using(var diagnostic=new MySqlConnection(new MySqlConnectionStringBuilder{Server=db.Host,Port=(uint)db.Port,Database=context.Database.Name,UserID=db.Username,Password=db.Password,Pooling=true}.ConnectionString))
                {
                    diagnostic.Open();using var tx=diagnostic.BeginTransaction();using var query=diagnostic.CreateCommand();query.Transaction=tx;
                    query.CommandText="SELECT TABLE_NAME,ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','char_abilities') ORDER BY TABLE_NAME";
                    var engines=new List<string>();using(var reader=query.ExecuteReader())while(reader.Read())engines.Add(reader.GetString(0)+":"+reader.GetString(1));
                    Check.That(engines.Count==2 && engines.Contains("char_abilities:InnoDB") && engines.Contains("characters:InnoDB"),"Actual required transactional engines");
                    query.CommandText="SHOW SESSION VARIABLES WHERE Variable_name IN ('transaction_isolation','tx_isolation')";
                    var isolation=new List<string>();using(var reader=query.ExecuteReader())while(reader.Read())isolation.Add(reader.GetString(0)+":"+reader.GetString(1));
                    Check.That(isolation.Count>0,"Server exposes its actual isolation variable name/value");
                    observations.Add(new{RaceSuccesses=results.Count(r=>r.Refusal==null),ProviderTransactionIsolation=tx.IsolationLevel.ToString(),IsolationVariables=isolation,Engines=engines});tx.Rollback();
                }
                await context.Database.ExecuteAsync($"UPDATE char_abilities SET is_loaded=0 WHERE {filter}; UPDATE char_abilities SET ability_id={Goid(314)} WHERE {filter} AND slot=501; UPDATE char_abilities SET ability_id={Goid(75)} WHERE {filter} AND slot=502");
                step="direct-transaction-rollback";await context.Database.InstallAbilityRollbackTriggerAsync();
                bool failed=false;
                try{a.CommitAbilityLoadout(player.CharId,new[]{E(501,314),E(502,75)},false,50);}catch(Exception ex) when(ex.Message.Contains("deliberate ability rollback")){failed=true;}
                finally{await context.Database.RemoveAbilityRollbackTriggerAsync();}
                Check.That(failed,"Real SQL failure after first mutation exercised");
                Check.Equal(0L,await context.Database.LongAsync($"SELECT SUM(is_loaded) FROM char_abilities WHERE {filter}"),"Whole load batch rolls back");
                step="direct-transaction-retry";var retry=a.CommitAbilityLoadout(player.CharId,new[]{E(501,314),E(502,75)},false,50);
                Check.Equal(2,retry.Accepted.Length,"Retry after rollback commits both exact primary IDs");
                observations.Add(new{Rollback=failed,RetryCount=retry.Accepted.Length});
            }
            finally{Store.config=old;}
            Check.Equal(0,player.World.Problems.Count,"Initial protocol framing");step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                // Snapshots survive expected client disposal; retain latest failure context for both lifetimes too.
                sessions.Add(Session(player,"initial-final"));if(relogged!=null)sessions.Add(Session(relogged,"relog-final"));
                var stem=Path.Combine(Path.GetTempPath(),$"mxo-memory-{context.Database.Name}");
                await File.WriteAllTextAsync(stem+"-stdout.log",context.Server.FullOutput());
                await File.WriteAllTextAsync(stem+"-debug.log",context.Server.DebugLog());
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Database=context.Database.Name,SourceAssembly=context.Server.SourceAssembly,
                    SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
                    Observations=observations,Sessions=sessions,NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Memory proof before teardown: "+stem+".json");
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Required memory proof also failed; original failure retained: "+proofError);}
            finally{relogged?.Dispose();}
        }
    }
}
