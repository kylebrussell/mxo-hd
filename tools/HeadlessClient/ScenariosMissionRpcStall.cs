using System.Security.Cryptography;
using System.Text.Json;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task MissionRpcStallRecovery(ScenarioContext context)
    {
        string source=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly)));
        InteriorAdmissionFixture.RequireSource(Environment.GetEnvironmentVariable(InteriorAdmissionFixture.ExpectedSourceEnvironment),source);
        var c=InteriorAdmissionFixture.Zion;
        var p=await context.AddPlayerAsync(new CharacterSeed("SubwayZion") {Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),
            Organization=1,X=c.X,Y=c.Y,Z=c.Z,CompletedMissions=c.Prerequisites});
        Exception? failure=null;string? checkpoint=null;var requests=new List<string>();long baselineXp=0,baselineInfo=0;
        void Send(string hex){requests.Add(hex);p.SendRpc(hex);}
        IReadOnlyList<SubwayStateWire.State> States()
        {
            var ids=p.World.CreationSnapshot().Where(v=>v.Goid==6568).Select(v=>v.ViewId).ToHashSet();
            var states=new List<SubwayStateWire.State>();
            foreach(byte[] section in p.World.ViewSnapshot().Where(s=>s.Length>=4&&ids.Contains(BitConverter.ToUInt16(s))&&s[2]==1&&s[3]==2))
            {
                var decoded=SubwayStateWire.DecodeComplete(section.Concat(new byte[]{0,0}).ToArray());
                Check.That(decoded.All(s=>ids.Contains(s.View)),"Scoped subway list contains only known car views");states.AddRange(decoded);
            }
            return states;
        }
        try
        {
            await p.EnterWorldAsync();
            baselineXp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}");
            baselineInfo=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
            await p.WaitUntilAsync(()=>p.ViewsOfGoid(6568).Count>0,"native-origin subway view",TimeSpan.FromSeconds(15));
            int offers=p.World.RpcSnapshot().Count;Send("80 94 01 00 01");await p.WaitForRpcAsync("80 97 01 00");
            int index=MissionContactChoice.CriticalIndex(p.World.RpcSnapshot().Skip(offers).Select(r=>r.Bytes),c.Title);
            Send($"80 98 02 00 {Le((ushort)index)} 01 01 00");
            // Same bounded count as native run2: identical RPC counter/body, new UDP envelope each time.
            // No artificial server stall, source override, acknowledgement manipulation or global load hook.
            for(int i=0;i<28;i++)p.World.ResendLastRpcs();
            await p.WaitForRpcAsync("80 99",TimeSpan.FromSeconds(30));
            Send("80 9b 03 00 01");await p.WaitForRpcAsync("80 9e 00",TimeSpan.FromSeconds(30));
            checkpoint=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={p.CharId}");
            var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(checkpoint,new JsonSerializerOptions{IncludeFields=true})!;
            Check.Equal(101209,state.Mission,"Normal Hardline Hunter acceptance");Check.Equal(0,state.Phase,"No mission goals injected");
            // Observe the existing natural countdown, including the departure that terminated both native streams.
            await p.WaitUntilAsync(()=>States().Any(s=>s.Value==0xbbe),"complete natural subway departure list",TimeSpan.FromSeconds(90));
            await p.WaitUntilAsync(()=>States().Any(s=>s.Value==0xbb9),"subway continues to limbo",TimeSpan.FromSeconds(15));
            Check.That(States().Where(s=>s.Value==0xbbe).GroupBy(s=>s.View).All(g=>g.Count()==1),"One departure publication per observed car");
            p.SkipReceived();Send("80 94 04 00 01");await p.WaitForRpcAsync("80 97 04 00");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId}"),"One accepted checkpoint");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId=101209"),"No completion award claimed");
            Check.Equal(baselineXp,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"No duplicate or unearned experience");
            Check.Equal(baselineInfo,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"No duplicate or unearned info");
            Check.Equal(0,p.World.Problems.Count,"Healthy RPC/parser session");
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-mission-rpc-stall-"+context.Database.Name+".json"),JsonSerializer.Serialize(new{
                SourceAssembly=context.Server.SourceAssembly,SourceSha256=source,Origin=new[]{c.X,c.Y,c.Z},c.LayoutSeed,c.Prerequisites,
                DuplicateInfoRequests=28,Requests=requests,Checkpoint=checkpoint,BaselineExperience=baselineXp,BaselineInfo=baselineInfo,Creations=p.World.CreationSnapshot(),
                RawViewSections=p.World.ViewSnapshot().Select(Convert.ToHexString),Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex),
                ProtocolProblems=p.World.Problems,Failure=failure?.ToString(),NativeUiProven=false,
                Scope="Normal contact offer/info/accept plus natural subway cycle and post-departure list. No mission completion/interior proof."},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
