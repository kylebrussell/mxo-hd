using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task CharacterCreationBaseline(ScenarioContext context)
    {
        using var margin=new MarginSession(); uint character=0; Exception? failure=null; string step="account";
        var observations=new List<object>();
        try
        {
            Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned disposable DB");
            string username=await context.AddCreationAccountAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var address=context.Server.Address;
            step="auth-no-character";
            var ticket=await AuthLogin.LoginAsync(address.Host,address.AuthPort,username,"x",timeout.Token);
            Check.Equal(0,ticket.Characters.Count,"Normal creation starts with no seeded character");
            await margin.ConnectAsync(address.Host,address.MarginPort,ticket,timeout.Token);
            step="normal-reservation";character=await margin.ReserveCharacterAsync("PersonalityBaseline",timeout.Token);
            observations.Add(new{Step=step,Character=character,Rows=await context.Database.ScalarAsync($"SELECT charId,userId,handle,profession FROM characters WHERE charId={character}"),RsiRows=await context.Database.LongAsync($"SELECT COUNT(*) FROM rsivalues WHERE charid={character}")});
            step="normal-creation";
            Exception? creationFailure=null;
            try{await margin.CreateCharacterAsync(character,CharacterCreationWire.Create(308,"First","Last","Creation personality baseline"),timeout.Token);}
            catch(Exception e){creationFailure=e;}
            long count=await context.Database.LongAsync($"SELECT COUNT(*) FROM rsivalues WHERE charid={character}");
            observations.Add(new{Step=step,RsiRows=count,CreationFailure=creationFailure?.ToString(),Character=await context.Database.ScalarAsync($"SELECT charId,userId,firstName,lastName,profession FROM characters WHERE charId={character}"),AttributeRows=await context.Database.ScalarAsync($"SELECT id,slot,ability_id,level FROM char_abilities WHERE char_id={character} AND slot IN (598,599,601,602,604) ORDER BY slot,id")});
            Check.Equal(1L,count,"Normal 0a/0c must create its own RSI row without a template clone");
            if(creationFailure!=null)throw creationFailure;
            step="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            Exception? proofFailure=null;
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-character-creation-baseline-"+context.Database.Name),dll=context.Server.SourceAssembly;
                string engines=await context.Database.ScalarAsync("SELECT TABLE_NAME,ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','rsivalues','char_abilities','inventory') ORDER BY TABLE_NAME");
                string rsi=character==0?"":await context.Database.ScalarAsync($"SELECT * FROM rsivalues WHERE charid={character}");
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=failure?.ToString(),Database=context.Database.Name,Character=character,Engines=engines,Rsi=rsi,Observations=observations,Margin=margin.CreationTranscript.Select(r=>new{r.Direction,Body=Convert.ToHexString(r.Body)}),SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),ProcessId=context.Server.ProcessId,NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n---Debug---\n"+context.Server.DebugLog());
                Console.WriteLine("  CREATION BASELINE evidence: "+stem+" step="+step);
            }
            catch(Exception e){proofFailure=e;Console.Error.WriteLine(e);}
            if(failure==null&&proofFailure!=null)throw new ScenarioFailure("Required creation proof failed: "+proofFailure);
        }
    }
}
