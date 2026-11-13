using System.Security.Cryptography;
using System.Text.Json;
using hds;
using hds.shared;
using MySqlConnector;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task AttributeAllocationAuthority(ScenarioContext context)
    {
        HeadlessPlayer? main=null,partial=null,legacy=null,aux=null;Exception? failure=null;string step="prepare";
        var sessions=new List<object>();var witnesses=new List<object>();
        var bases=CharacterAttributes.All.ToDictionary(a=>a,a=>CharacterAttributes.BaseValue(a));
        string slots="598,599,601,602,604";
        async Task<string> Rows(uint id)=>await context.Database.ScalarAsync($"SELECT id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={id} AND slot IN ({slots}) ORDER BY slot,id");
        void Record(HeadlessPlayer p)
        {
            sessions.Add(new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Margin=p.CharacterData.ToDictionary(v=>v.Key.ToString("x2"),v=>Convert.ToHexString(v.Value)),Problems=p.World.Problems.ToArray()});
            Check.Equal(0,p.World.Problems.Count,"Attribute session protocol parser clean");
        }
        async Task Allocate(HeadlessPlayer p,(byte Attribute,byte Points)[] requested,bool success,Dictionary<byte,ushort> expected)
        {
            int cursor=p.World.RpcSnapshot().Count;
            string body="0400 "+Le((ushort)requested.Length)+" "+string.Join(" ",requested.Select(r=>$"00 {r.Attribute:x2}000000 {r.Points:x2}"));
            p.SendRpc("80b5 "+body);
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length>=10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xb6),"Current attribute response",TimeSpan.FromSeconds(10));
            var replies=p.World.RpcSnapshot().Skip(cursor).Where(r=>r.Bytes.Length>=10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xb6).ToArray();
            Check.Equal(1,replies.Length,"Exactly one whole-batch response");byte[] reply=replies[0].Bytes;
            string literal=success?"80b6080000000000"+Le((ushort)requested.Length).Replace(" ","")+string.Join("",requested.Select(r=>"00"+r.Attribute.ToString("x2")+"000000"+(8+Math.Max(0,expected[r.Attribute]-CharacterAttributes.BaseValue(r.Attribute))).ToString("x2"))):"80b608000c0000180000";
            Check.Equal(literal,Convert.ToHexString(reply).ToLowerInvariant(),"Literal normalized native batch/status");
            Check.That(!p.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length>1&&r.Bytes[0]==0x80&&(r.Bytes[1]==0xe4||r.Bytes[1]==0xe7)),"Attribute points never spend Info");
        }
        async Task Initial(HeadlessPlayer p,Dictionary<byte,ushort> expected)
        {
            await p.EnterWorldAsync();
            foreach(var v in expected)await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Any(r=>r.Bytes.SequenceEqual(new byte[]{0x80,0xb2,v.Key,(byte)(v.Value>>8),(byte)v.Value,0,8,2})),"Initial actual attribute "+v.Key,TimeSpan.FromSeconds(10));
        }
        async Task Logout(HeadlessPlayer p){p.SendRpc("80fe00");await p.WaitForRpcAsync("80fd");Record(p);p.Dispose();}
        try
        {
            Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned disposable database only");
            main=await context.AddPlayerAsync(new CharacterSeed("AttributeAuthority"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Info=1000});
            await context.Database.ExecuteAsync(AttributeAllocationFixture.Seed(main.CharId)+$"; UPDATE char_abilities SET ability_id=2147566592 WHERE char_id={main.CharId} AND slot=601; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({main.CharId},854,-2147403776,30,0)");
            step="failure-trigger-preflight";await context.Database.InstallAttributeFailureAsync(main.CharId);await context.Database.RemoveAttributeFailureAsync();
            await Initial(main,bases);string original=await Rows(main.CharId);
            string identities=await context.Database.ScalarAsync($"SELECT id,slot,ability_id,is_loaded FROM char_abilities WHERE char_id={main.CharId} AND slot IN ({slots}) ORDER BY slot,id");
            step="normal-two-attribute-batch";var expected=new Dictionary<byte,ushort>(bases){[78]=9,[81]=9};
            await Allocate(main,new[]{((byte)78,(byte)1),((byte)81,(byte)1)},true,expected);
            string committed=await Rows(main.CharId);Check.Equal(1000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={main.CharId}"),"Normal allocation leaves Info unchanged");
            Check.Equal(2147566592L,await context.Database.LongAsync($"SELECT ability_id FROM char_abilities WHERE char_id={main.CharId} AND slot=601"),"Unsigned raw attribute descriptor preserved");
            step="cap-refusal";await Allocate(main,new[]{((byte)78,(byte)22)},false,expected);Check.Equal(committed,await Rows(main.CharId),"Cap refusal preserves every physical row");
            step="late-second-write-rollback";await context.Database.InstallAttributeFailureAsync(main.CharId);
            try{await Allocate(main,new[]{((byte)78,(byte)1),((byte)81,(byte)1)},false,expected);Check.Equal(committed,await Rows(main.CharId),"Second-row failure rolls back first-row update and preserves PKs");}
            finally{await context.Database.RemoveAttributeFailureAsync();}
            step="normal-retry";expected[78]=10;expected[81]=10;await Allocate(main,new[]{((byte)78,(byte)1),((byte)81,(byte)1)},true,expected);
            Check.Equal(identities,await context.Database.ScalarAsync($"SELECT id,slot,ability_id,is_loaded FROM char_abilities WHERE char_id={main.CharId} AND slot IN ({slots}) ORDER BY slot,id"),"Normal allocation preserves every existing PK/raw descriptor/load flag");
            witnesses.Add(new{Step=step,Before=original,After=await Rows(main.CharId),Info=1000});
            uint mainId=main.CharId;await Logout(main);main=null;
            step="eligible-partial-first-raise";partial=await context.AddPlayerAsync(new CharacterSeed("AttributePartial"){Level=18,Experience=(int)PlayerProgression.ExperienceForLevel(18),Info=1000});
            await context.Database.ExecuteAsync(AttributeAllocationFixture.Seed(partial.CharId)+$"; DELETE FROM char_abilities WHERE char_id={partial.CharId} AND slot=598");
            var partialBefore=new Dictionary<byte,ushort>(bases){[78]=24};await Initial(partial,partialBefore);
            var partialAfter=new Dictionary<byte,ushort>(partialBefore){[81]=9};await Allocate(partial,new[]{((byte)81,(byte)1)},true,partialAfter);
            Check.Equal(5L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={partial.CharId} AND slot IN ({slots})"),"Accepted partial snapshot lazily materializes all five values");
            witnesses.Add(new{Step=step,Values=partialAfter,Rows=await Rows(partial.CharId),Spent=17,Remaining=0});uint partialId=partial.CharId;await Logout(partial);partial=null;
            step="no-row-legacy-refusal";legacy=await context.AddPlayerAsync(new CharacterSeed("AttributeLegacy"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Info=1000});
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={legacy.CharId} AND slot IN ({slots})");
            var legacyValues=CharacterAttributes.All.ToDictionary(a=>a,a=>CharacterAttributes.LegacyValue(a));await Initial(legacy,legacyValues);await Allocate(legacy,new[]{((byte)78,(byte)1)},false,legacyValues);
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={legacy.CharId} AND slot IN ({slots})"),"No-row refusal creates no fallback rows");uint legacyId=legacy.CharId;await Logout(legacy);legacy=null;
            step="earned-relog";await Task.Delay(TimeSpan.FromSeconds(33));
            main=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+mainId,"AttributeAuthority");await Initial(main,expected);
            partial=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+partialId,"AttributePartial");await Initial(partial,partialAfter);
            Check.That(partial.CharacterData.Values.Any(b=>b.AsSpan().EndsWith(CharacterAttributes.BuildSpentPoints(partialAfter))),"Relog margin reports all preserved logical partial attributes and17 spent points");
            legacy=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+legacyId,"AttributeLegacy");await Initial(legacy,legacyValues);
            Check.That(legacy.CharacterData.Values.Any(b=>b.AsSpan().EndsWith(CharacterAttributes.BuildSpentPoints(new()))),"Legacy no-row fixed16 metadata preserved explicitly");
            foreach(uint id in new[]{mainId,partialId,legacyId})Check.Equal(1000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),"Info unchanged after relog");
            aux=await context.AddPlayerAsync(new CharacterSeed("AttributeRace"){Level=2,Experience=(int)PlayerProgression.ExperienceForLevel(2),Info=1000});await context.Database.ExecuteAsync(AttributeAllocationFixture.Seed(aux.CharId));
            var config=new ServerConfig(Path.Combine(Path.GetDirectoryName(context.Server.SourceAssembly)!,"Config.xml.dist"));config.LoadDbParams();config.dbParams.DatabaseName=context.Database.Name;var previous=Store.config;Store.config=config;
            try
            {
                var db=new hds.databases.MyWorldDbAccess();CharacterAttributes.Raise[] Request(byte a)=>new[]{new CharacterAttributes.Raise{Attribute=a,Points=1}};
                step="two-connections-different-attributes-one-point";using var barrier=new Barrier(2);
                Task<AttributeAllocation.Result> Race(byte a)=>Task.Run(()=>{Check.That(barrier.SignalAndWait(TimeSpan.FromSeconds(10)),"Attribute race barrier");return db.AllocateAttributes(aux.CharId,Request(a));});
                var results=await Task.WhenAll(Race(78),Race(81));Check.Equal(1,results.Count(r=>r.Committed),"Only one DIFFERENT-attribute allocation can consume the shared final point");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT SUM(level-8) FROM char_abilities WHERE char_id={aux.CharId} AND slot IN (598,601)"),"One total point persisted");witnesses.Add(new{Step=step,Winners=1,Rows=await Rows(aux.CharId)});
                step="lazy-materialization-rollback";await context.Database.ExecuteAsync(AttributeAllocationFixture.Seed(aux.CharId)+$"; DELETE FROM char_abilities WHERE char_id={aux.CharId} AND slot=598; UPDATE characters SET level=18,exp={PlayerProgression.ExperienceForLevel(18)} WHERE charId={aux.CharId}");
                string partialRows=await Rows(aux.CharId);await context.Database.InstallAttributeFailureAsync(aux.CharId);bool threw=false;
                try{db.AllocateAttributes(aux.CharId,Request(81));}catch(MySqlException){threw=true;}finally{await context.Database.RemoveAttributeFailureAsync();}
                Check.That(threw,"Actual late failure after inserted fallback row");Check.Equal(partialRows,await Rows(aux.CharId),"Failed second write rolls back new fallback row too");
                step="physical-row-replacement";await context.Database.ExecuteAsync(AttributeAllocationFixture.Seed(aux.CharId)+$"; UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId={aux.CharId}");
                long oldId=await context.Database.LongAsync($"SELECT id FROM char_abilities WHERE char_id={aux.CharId} AND slot=598");
                var builder=new MySqlConnectionStringBuilder{Server=config.dbParams.Host,Port=(uint)config.dbParams.Port,Database=context.Database.Name,UserID=config.dbParams.Username,Password=config.dbParams.Password};
                using var replacement=new MySqlConnection(builder.ConnectionString);await replacement.OpenAsync();using var tx=await replacement.BeginTransactionAsync();using var command=replacement.CreateCommand();command.Transaction=tx;
                command.CommandText=$"SELECT id FROM char_abilities WHERE id={oldId} FOR UPDATE";await command.ExecuteScalarAsync();command.CommandText=$"DELETE FROM char_abilities WHERE id={oldId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({aux.CharId},598,2214672384,8,0)";await command.ExecuteNonQueryAsync();
                using var started=new ManualResetEventSlim();var pending=Task.Run(()=>{started.Set();return db.AllocateAttributes(aux.CharId,Request(78));});Check.That(started.Wait(TimeSpan.FromSeconds(5)),"Physical replacement allocation started");await Task.Delay(250);Check.That(!pending.IsCompleted,"Allocation waits for actual physical row lock");await tx.CommitAsync();Check.That(!(await pending).Committed,"Wrong typed replacement refuses without overwriting it");
                long newId=await context.Database.LongAsync($"SELECT id FROM char_abilities WHERE char_id={aux.CharId} AND slot=598");Check.That(newId!=oldId,"Different physical row identity retained");witnesses.Add(new{Step=step,OldIdentity=oldId,NewIdentity=newId,Rows=await Rows(aux.CharId)});
                step="persisted-exp-authority";await context.Database.ExecuteAsync(AttributeAllocationFixture.Seed(aux.CharId)+$"; UPDATE characters SET level=50,exp=0 WHERE charId={aux.CharId}");Check.That(!db.AllocateAttributes(aux.CharId,Request(78)).Committed,"PersistedEXP1 refuses despite cached/storedLevel50");
                step="duplicate-row-refusal";await context.Database.ExecuteAsync($"UPDATE characters SET exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId={aux.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({aux.CharId},598,-2147403776,8,0)");string duplicate=await Rows(aux.CharId);Check.That(!db.AllocateAttributes(aux.CharId,Request(81)).Committed,"Ambiguous reserved slot refuses all mutation");Check.Equal(duplicate,await Rows(aux.CharId),"Duplicate snapshot unchanged");
            }finally{Store.config=previous;}
            Check.Equal(1000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={aux.CharId}"),"Transactional tests never spend Info");step="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            Exception? proofFailure=null;try{foreach(var p in new[]{main,partial,legacy,aux})if(p!=null)Record(p);}catch(Exception e){proofFailure=e;}
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-attribute-allocation-"+context.Database.Name),dll=context.Server.SourceAssembly;
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=failure?.ToString()??proofFailure?.ToString(),Database=context.Database.Name,Witnesses=witnesses,Sessions=sessions,SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n---Debug---\n"+context.Server.DebugLog());Console.WriteLine("  ATTRIBUTE evidence: "+stem+" step="+step);
            }catch(Exception e){proofFailure??=e;Console.Error.WriteLine(e);}finally{main?.Dispose();partial?.Dispose();legacy?.Dispose();aux?.Dispose();}
            if(failure==null&&proofFailure!=null)throw new ScenarioFailure("Required attribute proof failed: "+proofFailure);
        }
    }
}
