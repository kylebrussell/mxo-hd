using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task InvisibilityToolRegistration(ScenarioContext context)
    {
        InvisibilityToolRegistrationFixture.RequireSource(Environment.GetEnvironmentVariable(InvisibilityToolRegistrationFixture.ExpectedSource),context.Server.SourceAssemblySha256);
        var p=await context.AddPlayerAsync(InvisibilityToolRegistrationFixture.Seed("InvisibilityTool"));
        const string added="80BC45035C0400AF0000005C04000000000000000000";
        const string removed="80BC45035C0400AF0000005C04000000000100000000";
        var sessions=new List<object>();string stage="initial-entry";Exception? failure=null;bool passed=false;
        int Count(string hex)=>p.World.RpcSnapshot().Count(r=>Convert.ToHexString(r.Bytes)==hex);
        void Record()
        {
            sessions.Add(new{p.CharId,p.Handle,Margin=p.CharacterData.ToDictionary(k=>k.Key,v=>Convert.ToHexString(v.Value)),
                Rpc=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()});
            Check.Equal(0,p.World.Problems.Count,"Every retained session parser clean");
        }
        async Task Ready()
        {
            Check.That(p.World.CreationSnapshot().Any(v=>v.ViewId==2),"Actual self precedes readiness");
            Check.Equal(0,Count(added),"No pre-avatar membership");
            Check.Equal(0,p.Received("80 b2 5c 04").Count,"Managed hidden base waits for live avatar too");
            int cursor=p.World.RpcSnapshot().Count;p.SendRpc("0c 01");
            await p.WaitUntilAsync(()=>Count(added)==1,"175 live native membership after readiness",TimeSpan.FromSeconds(10));
            var rpcs=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToList();
            int b=rpcs.IndexOf("80B25C0400000802"),a=rpcs.IndexOf(added);
            Check.That(b>=cursor&&a>b,"Base then native grant registration after actual avatar");
            foreach(byte op in new byte[]{6,7})
            {
                var data=p.CharacterData[op];Check.That(data.Length%6==0,"Exact Margin ability rows");
                var ids=Enumerable.Range(0,data.Length/6).Select(i=>hds.AbilityCatalog.IdOf(BitConverter.ToUInt32(data,i*6+2))).ToArray();
                Check.That(ids.Contains((ushort)175)&&!ids.Contains((ushort)1116),"Only parent is archived/loaded");
            }
            int features=p.Received("81 49").Count;p.SendRpc("0c 01");p.SendRpc("81 48");
            await p.WaitUntilAsync(()=>p.Received("81 49").Count>features,"Fresh feature reply fences duplicate readiness",TimeSpan.FromSeconds(10));
            Check.Equal(1,Count(added),"Duplicate readiness has no duplicate membership");
        }
        async Task Activate()
        {
            byte[] prefix=WireFormat.FromHex("02 00 02 80 80 80 10");
            ushort[] Strengths(int start)=>p.World.ViewSnapshot().Skip(start).SelectMany(section=>
            {
                var values=new List<ushort>();
                for(int at=BytePattern.IndexOf(section,prefix,0);at>=0;at=BytePattern.IndexOf(section,prefix,at+1))
                    if(at+prefix.Length+2<=section.Length)values.Add(BitConverter.ToUInt16(section,at+prefix.Length));
                return values;
            }).ToArray();
            var prior=Strengths(0);ushort before=prior.Length>0?prior[^1]:BitConverter.ToUInt16(p.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes["InnerStrengthAvailable"]);
            int viewCursor=p.World.ViewSnapshot().Count;
            int cursor=p.World.RpcSnapshot().Count;
            p.SendRpc(WireFormat.ToHex(InvisibilityToolRegistrationFixture.ToolRequest()));
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>BytePattern.Parse(ModPattern(1116,115)).IsMatch(r.Bytes)),"Ordinary tool activates1116",TimeSpan.FromSeconds(10));
            var on=p.World.RpcSnapshot().Skip(cursor).First(r=>BytePattern.Parse(ModPattern(1116,115)).IsMatch(r.Bytes));
            Check.Equal(130f,BitConverter.ToSingle(on.Bytes,18),"Normal duration unchanged");
            int previous=before,debits=0;
            foreach(ushort value in Strengths(viewCursor)){if(previous>value){Check.Equal(50,previous-value,"Normal tool50IS debit");debits++;}previous=value;}
            Check.Equal(1,debits,"One normal activation debit");
            cursor=p.World.RpcSnapshot().Count;p.SendRpc(WireFormat.ToHex(InvisibilityToolRegistrationFixture.ToolRequest()));
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>BytePattern.Parse(ModPattern(1116,115,true)).IsMatch(r.Bytes)),"Ordinary tool toggles off",TimeSpan.FromSeconds(10));
        }
        try
        {
            await p.EnterWorldAsync();await Ready();stage="ordinary-tool";await Activate();
            stage="normal-relog";uint id=p.CharId;p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");Record();p.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(33));p=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+id,"InvisibilityTool");
            await p.EnterWorldAsync();await Ready();await Activate();
            stage="normal-parent-unload";ushort slot=(ushort)(400+Array.IndexOf(InvisibilityToolRegistrationFixture.Abilities,(ushort)175));
            SetAbilityLoaded(p,slot,175,50,false);
            await p.WaitUntilAsync(()=>Count(removed)==1,"Normal parent unload removes native membership",TimeSpan.FromSeconds(10));
            SetAbilityLoaded(p,slot,175,50,true);
            await p.WaitUntilAsync(()=>Count(added)==2,"Normal parent reload restores one native membership",TimeSpan.FromSeconds(10));
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND ((ability_id & 4193280)>>10)=1116"),"No hidden child archived by lifecycle");
            Check.Equal(0,p.World.Problems.Count,"Current parser clean");stage="complete";passed=true;
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            Exception? evidenceFailure=null;
            try
            {
                Record();Check.That(sessions.Count==2||failure!=null,"Both sessions retained");
                string path=Path.Combine(Path.GetTempPath(),"mxo-invisibility-tool-registration-"+context.Database.Name);
                await File.WriteAllTextAsync(path+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());
                string packetPath=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
                using(var stream=new FileStream(packetPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                using(var reader=new StreamReader(stream))await File.WriteAllTextAsync(path+"-packets.log",await reader.ReadToEndAsync());
                await File.WriteAllTextAsync(path+".json",JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Database=context.Database.Name,
                    Source=context.Server.SourceAssembly,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
                    ServerPid=context.Server.ProcessId,DriverPid=Environment.ProcessId,WorkingDirectory=Environment.CurrentDirectory,Sessions=sessions,NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Invisibility registration proof: "+path);
            }
            catch(Exception e){evidenceFailure=e;Console.Error.WriteLine("Required registration evidence failed: "+e);}
            finally{p.Dispose();}
            if(failure==null&&evidenceFailure!=null)throw new ScenarioFailure("Required registration evidence failed: "+evidenceFailure);
        }
    }
}
