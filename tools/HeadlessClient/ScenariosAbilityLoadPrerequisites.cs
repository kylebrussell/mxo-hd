using System.Security.Cryptography;
using System.Text.Json;
using hds;
using hds.databases;
using hds.shared;
using MySqlConnector;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task AbilityLoadPrerequisiteAdmission(ScenarioContext context)
    {
        var seed=await context.AddPlayerAsync(new CharacterSeed("LoadPrerequisite"){Level=50,Experience=0});
        string user=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={seed.CharId}");
        seed.Dispose();context.Server.Dispose();
        ServerInstance? server=null;HeadlessPlayer? actor=null;var sessions=new List<object>();var observations=new List<object>();var requests=new List<object>();
        string step="prelogin-owned-profile";Exception? failure=null;bool passed=false;
        uint character=seed.CharId;string filter=$"char_id={character}";
        uint Descriptor(ushort id)=>0x80000000u|((uint)id<<10);
        void Record(string label)
        {
            if(actor==null)return;
            sessions.Add(new{Label=label,actor.CharId,KnownMargin=Convert.ToHexString(actor.CharacterData[7]),LoadedMargin=Convert.ToHexString(actor.CharacterData[6]),
                Rpcs=actor.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=actor.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=actor.World.Problems.ToArray()});
        }
        string Request(bool load,params (ushort Slot,ushort Ability)[] entries)=>$"80 ae 00 00 00 00 {(load?"00 00 0a 00":"0a 00 00 00")} {Le((ushort)entries.Length)} "+
            string.Join(" ",entries.Select(e=>$"00 {Le(e.Slot)} {Le((uint)e.Ability)} 01 00"));
        void Send(string label,bool load,params (ushort Slot,ushort Ability)[] entries)
        {actor!.SkipReceived();string raw=Request(load,entries);requests.Add(new{Label=label,Raw=raw,RpcStart=actor.World.RpcSnapshot().Count});actor.SendRpc(raw);}
        async Task Barrier(){actor!.SendRpc("81 48");await actor.WaitForRpcAsync("81 49");}
        async Task Deny(string label,string text,params (ushort Slot,ushort Ability)[] entries)
        {
            int start=actor!.World.RpcSnapshot().Count;Send(label,true,entries);await actor.WaitForTextAsync(text);await Barrier();
            Check.Equal(0,actor.World.RpcSnapshot().Skip(start).Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&(r.Bytes[1]==0xb2||r.Bytes[1]==0xb3)),"Denied current operation has no positive load/unload acknowledgement");
            observations.Add(new{Label=label,RpcStart=start,Rows=await context.Database.ExecuteAsync($"SELECT id,slot,ability_id,level,is_loaded FROM char_abilities WHERE {filter} ORDER BY id")});
        }
        async Task Accept(string label,bool load,params (ushort Slot,ushort Ability)[] entries)
        {
            Send(label,load,entries);
            foreach(var e in entries)await actor!.WaitForRpcAsync($"80 {(load?"b2":"b3")} {Le(e.Ability)}");
            await Barrier();
            foreach(var e in entries)Check.Equal(load?1L:0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE {filter} AND slot={e.Slot}"),"Committed exact owned slot flag");
        }
        try
        {
            //Before the measured login: consistent training1/EXP0 graph; storedLevel50 is an explicit authority mismatch witness.
            await context.Database.ExecuteAsync(AbilityMemoryUiFixture.SeedSql(character)+$"UPDATE characters SET level=50 WHERE charId={character}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({character},19,{Descriptor(358)},1,0),({character},20,2214904192,0,1)");
            server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);
            actor=await HeadlessPlayer.LoginAsync(server.Address,user,seed.Handle);await actor.EnterWorldAsync();actor.SendRpc("0c 01");await Barrier();
            step="unload-current-parent-and-child";await Accept(step,false,(3,130));await Accept(step,false,(2,113));
            step="owned-but-unloaded-parent";await Deny(step,"required owned prerequisite",(3,130));
            Check.Equal(0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE {filter} AND slot=3"),"Invalid child-only request remains off");
            step="undertrained-current-parent";await context.Database.ExecuteAsync($"UPDATE char_abilities SET level=0 WHERE {filter} AND slot=0");
            await Deny(step,"required owned prerequisite",(14,217));await context.Database.ExecuteAsync($"UPDATE char_abilities SET level=1 WHERE {filter} AND slot=0");
            step="code-family-parent-cannot-qualify";await context.Database.ExecuteAsync($"UPDATE char_abilities SET ability_id=2214594560 WHERE {filter} AND slot=0");
            await Deny(step,"required owned prerequisite",(14,217));await context.Database.ExecuteAsync($"UPDATE char_abilities SET ability_id={Descriptor(2)} WHERE {filter} AND slot=0");
            step="experience-not-stored-level";await Deny(step,"character level is too low",(19,358));
            step="same-batch-child-before-parent";await Accept(step,true,(3,130),(2,113)); //Reverse of normal UI order, same final legal graph.
            step="exact-memory-cap";await Accept(step,true,(14,217));
            Check.That(server.DebugLog().Contains("memory 16/16"),"Actual dispatcher reports exact capacity");
            step="one-over-memory";await Deny(step,"exceed your memory capacity",(15,628));
            step="unload-frees-memory";await Accept(step,false,(14,217));await Accept(step,true,(15,628));
            //Two legal new additions fit exactly after ordinary unloads; trigger fails the SECOND exact-PK write.
            await Accept("prepare-rollback",false,(15,628));await Accept("prepare-rollback",false,(13,45));
            await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({character},501,{Descriptor(217)},1,0),({character},502,{Descriptor(628)},1,0)");
            step="real-wire-sql-rollback";await context.Database.InstallAbilityRollbackTriggerAsync();
            try{await Deny(step,"could not be saved",(501,217),(502,628));}
            finally{await context.Database.RemoveAbilityRollbackTriggerAsync();}
            Check.Equal(0L,await context.Database.LongAsync($"SELECT SUM(is_loaded) FROM char_abilities WHERE {filter} AND slot IN(501,502)"),"Both SQL mutations rolled back");
            step="wire-retry-after-rollback";await Accept(step,true,(501,217),(502,628));
            await Accept("recover-after-rollback",false,(501,217),(502,628));await Accept("restore-base15",true,(13,45));await Accept("restore-final16",true,(15,628));
            step="ordinary-relog";actor.SendRpc("80 fe 00");await actor.WaitForRpcAsync("80 fd");Record("initial-before-dispose");actor.Dispose();actor=null;
            await Task.Delay(TimeSpan.FromSeconds(33)); //Existing single-live-world reservation expires normally.
            actor=await HeadlessPlayer.LoginAsync(server.Address,user,seed.Handle);await actor.EnterWorldAsync();actor.SendRpc("0c 01");await Barrier();
            var loaded=AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(r=>(r.Descriptor&0xfc000000)==0x80000000).Select(r=>(r.Descriptor>>10)&4095).ToHashSet();
            Check.That(loaded.Contains(628u)&&loaded.Contains(113u)&&loaded.Contains(130u)&&!loaded.Contains(217u),"Relog preserves only admitted final graph");
            Record("relog-before-dispose");Check.Equal(0,actor.World.Problems.Count,"Relog framing");actor.Dispose();actor=null;
            step="two-connection-capacity-race";await context.Database.ExecuteAsync(AbilityMemoryUiFixture.SeedSql(character));
            var config=new ServerConfig(Path.Combine(Path.GetDirectoryName(server.SourceAssembly)!,"Config.xml.dist"));config.LoadDbParams();config.dbParams.DatabaseName=context.Database.Name;
            var old=Store.config;Store.config=config;
            try
            {
                var a=new MyWorldDbAccess();var b=new MyWorldDbAccess();using var barrier=new Barrier(2);
                AbilityProgression.LoadoutEntry E(ushort slot,ushort id)=>new(){Slot=slot,Ability=id,Level=999};
                var race=await Task.WhenAll(Task.Run(()=>{barrier.SignalAndWait();return a.CommitAbilityLoadout(character,new[]{E(14,217)},false,50);}),
                    Task.Run(()=>{barrier.SignalAndWait();return b.CommitAbilityLoadout(character,new[]{E(15,628)},false,50);}));
                Check.Equal(1,race.Count(r=>r.Refusal==null),"Only one additional point fits15/16 across independent connections");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT SUM(is_loaded) FROM char_abilities WHERE {filter} AND slot IN(14,15)"),"Exactly one racing durable admission");
                var db=config.dbParams;
                using var diagnostic=new MySqlConnection(new MySqlConnectionStringBuilder{Server=db.Host,Port=(uint)db.Port,Database=context.Database.Name,UserID=db.Username,Password=db.Password}.ConnectionString);
                diagnostic.Open();using var query=diagnostic.CreateCommand();query.CommandText="SHOW SESSION VARIABLES WHERE Variable_name IN ('transaction_isolation','tx_isolation')";
                var isolation=new List<string>();using(var read=query.ExecuteReader())while(read.Read())isolation.Add(read.GetString(0)+":"+read.GetString(1));
                query.CommandText="SELECT TABLE_NAME,ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','char_abilities') ORDER BY TABLE_NAME";
                var engines=new List<string>();using(var read=query.ExecuteReader())while(read.Read())engines.Add(read.GetString(0)+":"+read.GetString(1));
                Check.That(engines.Contains("characters:InnoDB")&&engines.Contains("char_abilities:InnoDB"),"Real transactional tables");
                observations.Add(new{RaceAccepted=race.Count(r=>r.Refusal==null),Isolation=isolation,Engines=engines});
            }
            finally{Store.config=old;}
            step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                Record("finally-current-session");string stem=Path.Combine(Path.GetTempPath(),"mxo-load-prerequisites-"+context.Database.Name);
                string dll=server?.SourceAssembly??context.Server.SourceAssembly;
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Database=context.Database.Name,
                    SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),
                    Requests=requests,Sessions=sessions,Observations=observations,Debug=server?.DebugLog(),Output=server?.FullOutput(),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Load prerequisite proof before teardown: "+stem+".json");
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Proof failure preserving original: "+proofError);}
            finally{actor?.Dispose();server?.Dispose();}
        }
    }
}
