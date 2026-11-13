using System.Security.Cryptography;
using System.Text.Json;
using hds;
using hds.shared;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task CharacterCreationPersonality(ScenarioContext context)
    {
        var margins=new List<MarginSession>();var players=new List<HeadlessPlayer>();var evidence=new List<object>();
        var assets=CreationAssets.Load(context.RepositoryRoot);
        Exception? failure=null;string step="start";var address=context.Server.Address;
        async Task<(string User,uint Account,MarginSession Margin,uint Character)> Reserve(string handle)
        {
            string user=await context.AddCreationAccountAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var ticket=await AuthLogin.LoginAsync(address.Host,address.AuthPort,user,"x",timeout.Token);Check.Equal(0,ticket.Characters.Count,"No seeded character");
            var margin=new MarginSession();margins.Add(margin);await margin.ConnectAsync(address.Host,address.MarginPort,ticket,timeout.Token);
            uint id=await margin.ReserveCharacterAsync(handle,timeout.Token);
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM rsivalues WHERE charid={id}"),"Normal reservation creates its own RSI shell");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId={id} AND userId={ticket.UserId} AND trait_id IS NULL AND profession=2"),"Owned unassigned reservation, profession unchanged");
            return(user,ticket.UserId,margin,id);
        }
        async Task<string> Snapshot(uint id)=>await context.Database.ScalarAsync($"SELECT userId,firstName,lastName,background,trait_id,profession,healthC,healthM,innerStrC,innerStrM,cash FROM characters WHERE charId={id}")+"\nRSI:"+await context.Database.ScalarAsync($"SELECT * FROM rsivalues WHERE charid={id}")+"\nARCHIVE:"+await context.Database.ScalarAsync($"SELECT id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={id} ORDER BY id")+"\nITEMS:"+await context.Database.ScalarAsync($"SELECT invId,goid,slot,count,purity FROM inventory WHERE charId={id} ORDER BY invId");
        async Task Create(MarginSession margin,uint id,uint trait)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await margin.CreateCharacterAsync(id,CharacterCreationWire.Create(trait,"First","Last","Personality creation proof",appearance:trait==304?new uint[]{0,102,100,0,0,0,0,0,737,671,842,726,5621,748,743,6734}:null),timeout.Token);
        }
        async Task Refused(MarginSession margin,uint id,byte[] body)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));bool refused=false;
            try{await margin.CreateCharacterAsync(id,body,timeout.Token);}catch(Exception e)when(e is EndOfStreamException or IOException or InvalidDataException){refused=true;}
            Check.That(refused,"Invalid/replayed creation closes without invented numeric response or successful character data");
        }
        void Record(HeadlessPlayer player)
        {
            evidence.Add(new{Step=step,player.CharId,Margin=player.CharacterData.ToDictionary(k=>k.Key.ToString("x2"),v=>Convert.ToHexString(v.Value)),Rpc=player.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=player.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=player.World.Problems.ToArray()});
            Check.Equal(0,player.World.Problems.Count,"Actual session protocol parser clean");
        }
        try
        {
            Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned database");
            foreach(uint trait in new uint[]{308,304})
            {
                step="normal-create-"+trait;var a=await Reserve("Personality"+trait);await Create(a.Margin,a.Character,trait);
                Check.Equal(trait,BitConverter.ToUInt32(a.Margin.CharacterData[2]),"New02 trait is FIRSTu32, without return header");
                Check.Equal((long)trait,await context.Database.LongAsync($"SELECT trait_id FROM characters WHERE charId={a.Character}"),"Actual selected trait persisted");
                Check.Equal(trait==308?"0\t0\t0\t0\t0\t0\t5\t4\t1\t3\t5\t1\t1\t12\t3\t9\t0\t0":"2\t1\t0\t0\t0\t0\t7\t3\t5\t4\t11\t1\t4\t5\t2\t3\t1\t2",await context.Database.ScalarAsync($"SELECT body,sex,face,hair,haircolor,tattoo,hat,glasses,shirt,gloves,coat,pants,shoes,shirtcolor,coatcolor,pantscolor,shoecolor,leggins FROM rsivalues WHERE charid={a.Character}"),"Actual request appearance and source clothing values, no template RSI");
                foreach(byte attribute in CharacterAttributes.All)Check.Equal((long)CharacterAttributes.BaseValue(attribute,trait),await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={a.Character} AND slot={CharacterAttributes.SlotOf(attribute)}"),"Allfive coherent selected bases");
                Check.Equal(19L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={a.Character}"),"Exactly unchanged14starting grants plusfiveattributes");
                Check.Equal(trait==308?7L:8L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={a.Character}"),"Exactly actual selected creation clothes, with optional female tights");
                string before=await Snapshot(a.Character);evidence.Add(new{Step=step,a.Character,Snapshot=before,New02=Convert.ToHexString(a.Margin.CharacterData[2])});
                var created=await HeadlessPlayer.AttachCreatedAsync(address,"Personality"+trait,a.Character,a.Margin);players.Add(created);await created.EnterWorldAsync();
                created.SendRpc("80fe00");await created.WaitForRpcAsync("80fd");Record(created);created.Dispose();
                // Progression ONLY for one ordinary80b5 witness. No RSI/trait/attribute row is seeded around creation.
                await context.Database.ExecuteAsync($"UPDATE characters SET level=2,exp={PlayerProgression.ExperienceForLevel(2)} WHERE charId={a.Character}");
                step="normal-attribute-"+trait;var player=await HeadlessPlayer.LoginAsync(address,a.User,"Personality"+trait);players.Add(player);
                Check.Equal(trait,BitConverter.ToUInt32(player.CharacterData[2],32),"Returning02 trait follows EXACT32header");await player.EnterWorldAsync();
                foreach(byte attribute in CharacterAttributes.All){ushort value=CharacterAttributes.BaseValue(attribute,trait);await player.WaitUntilAsync(()=>player.World.RpcSnapshot().Any(r=>r.Bytes.SequenceEqual(new byte[]{0x80,0xb2,attribute,(byte)(value>>8),(byte)value,0,8,2})),"Actual initial attribute",TimeSpan.FromSeconds(10));}
                byte raised=trait==308?(byte)78:(byte)82;int cursor=player.World.RpcSnapshot().Count;
                player.SendRpc(new byte[]{0x80,0xb5,4,0,1,0,0,raised,0,0,0,1});
                await player.WaitUntilAsync(()=>player.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.SequenceEqual(new byte[]{0x80,0xb6,8,0,0,0,0,0,1,0,0,raised,0,0,0,9})),"Normal success normalized9, own signed contribution",TimeSpan.FromSeconds(10));
                Check.Equal((long)CharacterAttributes.BaseValue(raised,trait)+1,await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={a.Character} AND slot={CharacterAttributes.SlotOf(raised)}"),"Onepoint persisted");
                Check.Equal(1000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={a.Character}"),"Attribute raise spends noInfo");
                player.SendRpc("80fe00");await player.WaitForRpcAsync("80fd");Record(player);player.Dispose();
                var cold=await HeadlessPlayer.LoginAsync(address,a.User,"Personality"+trait);players.Add(cold);Check.Equal(trait,BitConverter.ToUInt32(cold.CharacterData[2],32),"Coldreturn trait");
                Check.That(cold.CharacterData[2].AsSpan().EndsWith(new byte[]{1,raised,0,0,0,1,0}),"Coldreturn onepoint metadata uses assignedtrait");await cold.EnterWorldAsync();Record(cold);cold.SendRpc("80fe00");await cold.WaitForRpcAsync("80fd");cold.Dispose();
            }
            foreach(uint bad in new uint[]{0,9,4096,0x1000134})
            {
                step="invalid-trait-"+bad;var a=await Reserve("BadTrait"+bad);string before=await Snapshot(a.Character);
                await Refused(a.Margin,a.Character,CharacterCreationWire.Create(bad,"First","Last","Invalid"));Check.Equal(before,await Snapshot(a.Character),"Invalidtrait reservation/RSI shell unchanged");evidence.Add(new{Step=step,a.Character,Before=before,After=await Snapshot(a.Character)});a.Margin.Dispose();
            }
            step="actual-0c-replay";var replay=await Reserve("CreationReplay");await Create(replay.Margin,replay.Character,308);string replayBefore=await Snapshot(replay.Character);
            await Refused(replay.Margin,replay.Character,CharacterCreationWire.Create(304,"Other","Name","Replay"));Check.Equal(replayBefore,await Snapshot(replay.Character),"Replay cannot rewrite trait/RSI/rows or regrant items");evidence.Add(new{Step=step,replay.Character,Before=replayBefore,After=await Snapshot(replay.Character)});replay.Margin.Dispose();
            step="malformed-offset";var malformed=await Reserve("BadOffset");string malformedBefore=await Snapshot(malformed.Character);byte[] broken=CharacterCreationWire.Create(308,"First","Last","");broken[69]--;
            await Refused(malformed.Margin,malformed.Character,broken);Check.Equal(malformedBefore,await Snapshot(malformed.Character),"Malformedfullrequest nomutation");malformed.Margin.Dispose();
            step="actual-late-SQL-rollback";var rollback=await Reserve("CreationRollback");string rollbackBefore=await Snapshot(rollback.Character);
            await context.Database.InstallCreationFailureAsync(rollback.Character);
            try{await Refused(rollback.Margin,rollback.Character,CharacterCreationWire.Create(308,"First","Last","Rollback"));}
            finally{await context.Database.RemoveCreationFailureAsync();}
            Check.Equal(rollbackBefore,await Snapshot(rollback.Character),"Finalattribute insertion failure rollsbacktrait/RSI/clothes/allgrants/allfive rows");evidence.Add(new{Step=step,rollback.Character,Before=rollbackBefore,After=await Snapshot(rollback.Character)});rollback.Margin.Dispose();
            step="owner-replay-race";var race=await Reserve("CreationRace");
            Check.That(CharacterCreation.TryParse(CharacterCreationWire.Create(304,"First","Last","Race"),out var request)&&assets.TryResolve(request,out var state),"Actualknownappearance resolver");
            var config=new ServerConfig(Path.Combine(Path.GetDirectoryName(context.Server.SourceAssembly)!,"Config.xml.dist"));config.LoadDbParams();config.dbParams.DatabaseName=context.Database.Name;var prior=Store.config;Store.config=config;
            try
            {
                var db=new hds.databases.MyMarginDBAccess();string before=await Snapshot(race.Character);
                assets.TryResolve(request,out var initial);
                Check.That(!db.CompleteCharacterCreation(race.Character,race.Account+1,initial),"Wrongauthenticatedaccount cannotconsume ownedreservation");Check.Equal(before,await Snapshot(race.Character),"Foreignowner refusal unchanged");
                using var barrier=new Barrier(2);
                Task<bool> Compete()=>Task.Run(()=>{Check.That(barrier.SignalAndWait(TimeSpan.FromSeconds(10)),"Twoactualconnections barrier");return new hds.databases.MyMarginDBAccess().CompleteCharacterCreation(race.Character,race.Account,initial);});
                bool[] results=await Task.WhenAll(Compete(),Compete());Check.Equal(1,results.Count(v=>v),"Exactlyone actualconnection consumesNULLtraitreservation");
                Check.Equal(19L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={race.Character}"),"No racing duplicategrants");evidence.Add(new{Step=step,race.Character,Committed=results,Snapshot=await Snapshot(race.Character)});
            }
            finally{Store.config=prior;race.Margin.Dispose();}
            step="legacy-null-no-migration";
            await context.Database.ExecuteAsync($"UPDATE characters SET trait_id=NULL,level=50,exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId={race.Character}; DELETE FROM char_abilities WHERE char_id={race.Character} AND slot IN (598,599,601,602,604)");
            var legacy=await HeadlessPlayer.LoginAsync(address,race.User,"CreationRace");players.Add(legacy);
            Check.Equal(299u,BitConverter.ToUInt32(legacy.CharacterData[2],32),"Legacy NULL sends old299 selector");
            Check.That(legacy.CharacterData[2].AsSpan().EndsWith(Convert.FromHexString("03510000000400520000000B00540000000100")),"Legacy fixed16 metadata remains qualified");
            await legacy.EnterWorldAsync();
            foreach(byte attribute in CharacterAttributes.All){ushort value=CharacterAttributes.LegacyValue(attribute);await legacy.WaitUntilAsync(()=>legacy.World.RpcSnapshot().Any(r=>r.Bytes.SequenceEqual(new byte[]{0x80,0xb2,attribute,(byte)(value>>8),(byte)value,0,8,2})),"Legacy unchanged actual attribute",TimeSpan.FromSeconds(10));}
            int legacyCursor=legacy.World.RpcSnapshot().Count;legacy.SendRpc("80b504000100004e00000001");
            await legacy.WaitUntilAsync(()=>legacy.World.RpcSnapshot().Skip(legacyCursor).Any(r=>r.Bytes.SequenceEqual(Convert.FromHexString("80b608000c0000180000"))),"Legacy90spent refuses without invented migration",TimeSpan.FromSeconds(10));
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={race.Character} AND slot IN (598,599,601,602,604)"),"Legacy refusal creates no rows");
            Check.Equal(1000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={race.Character}"),"Legacy refusal no Info change");Record(legacy);legacy.Dispose();
            step="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            Exception? proofFailure=null;
            foreach(var p in players){try{Record(p);}catch(Exception e){proofFailure??=e;}}
            string engines="unavailable";
            try{engines=await context.Database.ScalarAsync("SELECT TABLE_NAME,ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','rsivalues','char_abilities','inventory') ORDER BY TABLE_NAME");}catch(Exception e){proofFailure??=e;}
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-character-personality-"+context.Database.Name),dll=context.Server.SourceAssembly;
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=failure?.ToString()??proofFailure?.ToString(),Database=context.Database.Name,Engines=engines,Evidence=evidence,Margin=margins.Select((m,i)=>new{Session=i,Transcript=m.CreationTranscript.Select(r=>new{r.Direction,Body=Convert.ToHexString(r.Body)})}),SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),ProcessId=context.Server.ProcessId,NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n---Debug---\n"+context.Server.DebugLog());Console.WriteLine("  PERSONALITY evidence: "+stem+" step="+step);
            }
            catch(Exception e){proofFailure=e;Console.Error.WriteLine(e);}
            finally{foreach(var p in players)p.Dispose();foreach(var m in margins)m.Dispose();}
            if(failure==null&&proofFailure!=null)throw new ScenarioFailure("Requiredcreationproof: "+proofFailure);
        }
    }
}
