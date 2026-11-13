using System.Security.Cryptography;
using System.Text.Json;
using hds;
using hds.shared;
using MySqlConnector;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task AbilityTrainingAuthority(ScenarioContext context)
    {
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal)&&context.Database.Name.Skip(8).All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"Owned disposable database only");
        HeadlessPlayer? p=null,race=null;Exception? failure=null;string step="prepare";var sessions=new List<object>();var databaseProof=new List<object>();
        void Record(HeadlessPlayer player)
        {
            sessions.Add(new{player.Handle,player.CharId,Rpc=player.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=player.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=player.World.Problems.ToArray()});
            Check.Equal(0,player.World.Problems.Count,"Training protocol parser clean");
        }
        async Task<byte[]> Upgrade(ushort ability,byte count,bool success,ushort expectedLevel)
        {
            int cursor=p!.World.RpcSnapshot().Count;
            p.SendRpc($"80 b7 {Le(ability)} {Le(AbilityTrainingFixture.Slot(ability))} {count:x2}");
            bool Reply(byte[] b)=>b.Length==10&&b[0]==0x80&&b[1]==0xb8&&BitConverter.ToUInt16(b,2)==ability;
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>Reply(r.Bytes)),"Current training response",TimeSpan.FromSeconds(10));
            var reply=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Bytes).Last(Reply);
            Check.Equal(expectedLevel,BitConverter.ToUInt16(reply,4),"Returned durable training level");
            Check.Equal(success?0u:0x1800000cu,BitConverter.ToUInt32(reply,6),"Success only after commit");return reply;
        }
        try
        {
            p=await context.AddPlayerAsync(new CharacterSeed("TrainingAuthority"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Info=20000000});
            await context.Database.ExecuteAsync(AbilityTrainingFixture.Seed(p.CharId));
            await context.Database.ExecuteAsync($"UPDATE char_abilities SET ability_id=-2147329024 WHERE char_id={p.CharId} AND slot={AbilityTrainingFixture.Slot(151)}");
            step="failure-trigger-preflight";await context.Database.InstallTrainingChargeFailureAsync(p.CharId);await context.Database.RemoveTrainingChargeFailureAsync();
            await p.EnterWorldAsync();await p.WaitForRpcAsync(PassiveRecord(52,643,196));
            long info=20000000;
            foreach(var vector in new (ushort Ability,ushort Next,uint Cost)[]{(151,26,67600),(1112,6,3600),(37,15,22500),(186,2,260900),(621,31,199400)})
            {
                step="normal-"+vector.Ability;await Upgrade(vector.Ability,1,true,vector.Next);info-=vector.Cost;
                Check.Equal(info,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Literal native quote charged once");
                Check.Equal((long)vector.Next,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={p.CharId} AND slot={AbilityTrainingFixture.Slot(vector.Ability)}"),"Owned row trained");
                databaseProof.Add(new{Step=step,vector.Ability,vector.Next,vector.Cost,Info=info});
            }
            Check.Equal(-2147329024L,await context.Database.LongAsync($"SELECT ability_id FROM char_abilities WHERE char_id={p.CharId} AND slot={AbilityTrainingFixture.Slot(151)}"),"Signed descriptor preserved");
            Check.Equal(2148622336L,await context.Database.LongAsync($"SELECT ability_id FROM char_abilities WHERE char_id={p.CharId} AND slot={AbilityTrainingFixture.Slot(1112)}"),"Unsigned descriptor preserved");
            step="ability-cap-refusal";await Upgrade(37,1,false,15);await Upgrade(44,1,false,10);
            Check.Equal(info,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Cap refusal charges nothing");
            step="late-sql-rollback";await context.Database.InstallTrainingChargeFailureAsync(p.CharId);
            try
            {
                int cursor=p.World.RpcSnapshot().Count;await Upgrade(151,1,false,0);
                Check.Equal(26L,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={p.CharId} AND slot={AbilityTrainingFixture.Slot(151)}"),"Earlier level UPDATE rolled back after charge failure");
                Check.Equal(info,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Failed transaction retained Info");
                var window=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Bytes).ToArray();
                Check.That(!window.Any(b=>b.Length==10&&b[0]==0x80&&b[1]==0xb8&&BitConverter.ToUInt32(b,6)==0),"No optimistic success after SQL failure");
                Check.That(!window.Any(b=>b.Length>1&&b[0]==0x80&&(b[1]==0xe7||b[1]==0xe4)),"No optimistic Info delta/current");
                databaseProof.Add(new{Step=step,Info=info,Level=26,SuccessPublished=false});
            }
            finally{await context.Database.RemoveTrainingChargeFailureAsync();}
            step="loaded-passive";int passiveCursor=p.World.RpcSnapshot().Count;await Upgrade(52,1,true,50);info-=500000;
            var passiveWindow=p.World.RpcSnapshot().Skip(passiveCursor).Select(r=>Convert.ToHexString(r.Bytes)).ToArray();
            Check.That(passiveWindow.Any(b=>BytePattern.Parse(PassiveRecord(52,643,196,true)).Occurs(Convert.FromHexString(b))),"Old loaded Hacker196 removed");
            Check.That(passiveWindow.Any(b=>BytePattern.Parse(PassiveRecord(52,643,200)).Occurs(Convert.FromHexString(b))),"New loaded Hacker200 presented");
            Check.That(!p.World.RpcSnapshot().Skip(passiveCursor).Any(r=>r.Bytes.Length>=13&&r.Bytes[0]==0x80&&r.Bytes[1]==0xbc&&r.Bytes[2]==0x45&&BitConverter.ToUInt32(r.Bytes,7)!=52),"No unrelated loaded passive record removed/reapplied");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={p.CharId} AND slot={AbilityTrainingFixture.Slot(45)}"),"Other owned loaded row remains loaded");
            step="earned-relog";uint id=p.CharId;p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");Record(p);p.Dispose();p=null;
            await Task.Delay(TimeSpan.FromSeconds(33));p=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+id,"TrainingAuthority");await p.EnterWorldAsync();await p.WaitForRpcAsync(PassiveRecord(52,643,200));
            Check.Equal(info,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),"Durable Info after relog");
            Check.Equal(50L,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={id} AND slot={AbilityTrainingFixture.Slot(52)}"),"Durable loaded training level");
            //Real owned-database transactions; no queue/test callback runs inside the production transaction.
            race=await context.AddPlayerAsync(new CharacterSeed("TrainingRace"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Info=800});
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={race.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({race.CharId},300,2147485696,1,0)");
            var config=new ServerConfig(Path.Combine(context.RepositoryRoot,"hds","bin","Debug","net8.0","Config.xml.dist"));config.LoadDbParams();config.dbParams.DatabaseName=context.Database.Name;
            var previousConfig=Store.config;Store.config=config;
            try
            {
                var db=new hds.databases.MyWorldDbAccess();var req=new AbilityProgression.UpgradeRequest{Ability=2,Slot=300,Levels=1};
                step="two-training-connections";using var barrier=new Barrier(2);
                Task<AbilityTraining.Result> Train()=>Task.Run(()=>{Check.That(barrier.SignalAndWait(TimeSpan.FromSeconds(10)),"Training race barrier");return db.TrainOwnedAbility(race.CharId,req);});
                var results=await Task.WhenAll(Train(),Train());Check.Equal(1,results.Count(r=>r.Committed),"Only one participating training transaction can spend800");
                Check.Equal(0L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={race.CharId}"),"Race debits once");
                Check.Equal(2L,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={race.CharId} AND slot=300"),"Race trains once");
                databaseProof.Add(new{Step=step,Accepted=results.Count(r=>r.Committed),Info=0,Level=2});
                step="duplicate-slot";await context.Database.ExecuteAsync($"UPDATE characters SET cash=800 WHERE charId={race.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({race.CharId},300,2147521536,1,0)");
                Check.That(!db.TrainOwnedAbility(race.CharId,req).Committed,"Ambiguous slot refuses both rows");
                Check.Equal(800L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={race.CharId}"),"Ambiguity charges nothing");
                step="canonical-duplicate-refusal";
                await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={race.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({race.CharId},300,2147638272,1,0),({race.CharId},301,2147638273,1,0); UPDATE characters SET cash=800 WHERE charId={race.CharId}");
                req.Ability=151;Check.That(!db.TrainOwnedAbility(race.CharId,req).Committed,"ID-only native response requires unique canonical ownership across slots");
                Check.Equal(800L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={race.CharId}"),"Duplicate canonical refusal charges nothing");
                databaseProof.Add(new{Step=step,Committed=false,Cash=800});
                step="physical-row-replacement";await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={race.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({race.CharId},300,2147638272,1,0)");
                long oldIdentity=await context.Database.LongAsync($"SELECT id FROM char_abilities WHERE char_id={race.CharId} AND slot=300");
                var builder=new MySqlConnectionStringBuilder{Server=config.dbParams.Host,Port=(uint)config.dbParams.Port,Database=context.Database.Name,UserID=config.dbParams.Username,Password=config.dbParams.Password};
                using var replacement=new MySqlConnection(builder.ConnectionString);await replacement.OpenAsync();using var transaction=await replacement.BeginTransactionAsync();using var cmd=replacement.CreateCommand();cmd.Transaction=transaction;
                cmd.CommandText=$"SELECT id FROM char_abilities WHERE char_id={race.CharId} AND slot=300 FOR UPDATE";await cmd.ExecuteScalarAsync();
                cmd.CommandText=$"DELETE FROM char_abilities WHERE id={oldIdentity}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({race.CharId},300,2147521536,1,0)";await cmd.ExecuteNonQueryAsync();
                req.Ability=151;using var started=new ManualResetEventSlim();var waiting=Task.Run(()=>{started.Set();return db.TrainOwnedAbility(race.CharId,req);});Check.That(started.Wait(TimeSpan.FromSeconds(5)),"Replacement transaction race started");
                await Task.Delay(250);Check.That(!waiting.IsCompleted,"Training waits for locked owned archive rows");await transaction.CommitAsync();
                Check.That(!(await waiting).Committed,"Locked reread rejects replacement descriptor rather than training every row at the slot");
                long newIdentity=await context.Database.LongAsync($"SELECT id FROM char_abilities WHERE char_id={race.CharId} AND slot=300");Check.That(newIdentity!=oldIdentity,"Actual physical row replaced");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE id={newIdentity}"),"Foreign replacement is not trained");
                Check.Equal(800L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={race.CharId}"),"Replacement refusal charges nothing");
                databaseProof.Add(new{Step=step,OldIdentity=oldIdentity,NewIdentity=newIdentity,ReplacementLevel=1,Committed=false});
                step="persisted-exp-authority";await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={race.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({race.CharId},300,2147485696,1,0); UPDATE characters SET level=50,exp=0,cash=800 WHERE charId={race.CharId}");req.Ability=2;
                Check.That(!db.TrainOwnedAbility(race.CharId,req).Committed,"Cached/stored Level50 cannot replace EXP1 authority");
            }
            finally{Store.config=previousConfig;}
            Check.Equal(info,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Other actor training tests do not alter the main actor");
            step="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            Exception? proofFailure=null;
            try{if(p!=null)Record(p);if(race!=null)Record(race);}catch(Exception e){proofFailure=e;}
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-ability-training-"+context.Database.Name);string dll=context.Server.SourceAssembly;
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=failure?.ToString()??proofFailure?.ToString(),Database=context.Database.Name,DatabaseProof=databaseProof,Sessions=sessions,
                    SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),NativeUiProven=false,GlobalLegacyWalletSerializationClaimed=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());Console.WriteLine("  TRAINING evidence: "+stem+" step="+step);
            }
            catch(Exception e){proofFailure??=e;Console.Error.WriteLine(e);}
            finally{p?.Dispose();race?.Dispose();}
            if(failure==null&&proofFailure!=null)throw new ScenarioFailure("Required training evidence failed: "+proofFailure);
        }
    }
}
