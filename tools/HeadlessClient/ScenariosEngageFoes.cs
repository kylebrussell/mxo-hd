using hds;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using MxoHd.PacketResearch;

namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task EngageFoesTaunt(ScenarioContext context)
    {
        string sourceHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly)));
        EngageFoesFixture.RequireSource(System.Environment.GetEnvironmentVariable(EngageFoesFixture.ExpectedSourceEnvironment),sourceHash);
        var a=await context.AddPlayerAsync(EngageFoesFixture.Seed("EngageBlocker",true));
        var b=await context.AddPlayerAsync(EngageFoesFixture.Seed("EngageCaster"));
        Exception? failure=null;int fullHp=0;ViewCreation? target=null;string baseline="";byte[]? request=null;int initialIs=0;ushort[] spent=Array.Empty<ushort>();
        try
        {
            await a.EnterWorldAsync();await b.EnterWorldAsync();
            await EngageFoesFixture.BlockNpcAsync(a);
            await b.WaitUntilAsync(()=>EngageFoesFixture.CurrentNpc(b.World.CreationSnapshot(),"WinterFrost")!=null,"caster's actual NPC view",TimeSpan.FromSeconds(15));
            target=EngageFoesFixture.CurrentNpc(b.World.CreationSnapshot(),"WinterFrost")!;
            fullHp=await EngageFoesFixture.RequireFullHealthAsync(b,target);baseline=context.Server.DebugLog();
            var below=EngageFoesFixture.CurrentNpc(b.World.CreationSnapshot(),"WinterHelper");
            initialIs=BitConverter.ToUInt16(b.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes["InnerStrengthAvailable"]);
            b.SendRpc("42 03 00 00 00 00 00");int cursor=b.World.RpcSnapshot().Count,viewCursor=b.World.ViewSnapshot().Count;b.SkipReceived();
            request=new AbilityUseRecord(610,DuelPackets.Handle(target.ViewId,target.SpawnId),0,0,0,0,0x6b,0x6b,0,0).Encode();
            Check.Equal(43,request.Length,"Full native-layout80b9 request");b.SendRpc(request);
            await b.WaitUntilAsync(()=>context.Server.DebugLog()[baseline.Length..].Split('\n').Any(line=>line.Contains($"[ENGAGE-FOES] source={b.CharId} ") && line.Contains($" view={target.ViewId} ")),"610 actual successful per-recipient initializer",TimeSpan.FromSeconds(15));
            await b.WaitUntilAsync(()=>context.Server.DebugLog()[baseline.Length..].Contains($" evaluated={b.CharId} owner={b.CharId} diverted=False"),"NPC round consumer attributes an attack attempt to new most-hated owner",TimeSpan.FromSeconds(15));
            await b.WaitUntilAsync(()=>b.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length>0&&r.Bytes[0]==0x45),"new owner's ordinary combat round",TimeSpan.FromSeconds(15));
            string after=context.Server.DebugLog()[baseline.Length..];
            spent=ViewUpdateValues(b,2,"02 80 80 80 10",viewCursor).ToArray();Check.That(spent.Length>0,"Actual IS update after normal use");
            Check.Equal(initialIs-50,(int)spent[0],"Independent stored50IS cost, before later ordinary regen/round changes");
            Check.That(after.Contains("[EFFECTS] 610 on WinterFrost: State 93 0 6 s"),"Existing NPC Enraged93 six-second policy retained on actual landing");
            var applied=Regex.Matches(after,$@"\[ENGAGE-FOES\] source={b.CharId} entity=(\d+) view=(\d+) spawn=(\d+) death=(\d+) most-hated applied");
            Check.That(applied.Count>=2,"Existing target-centred area reaches multiple normal nearby NPCs");
            Check.That(applied.GroupBy(m=>m.Groups[1].Value).All(g=>g.Count()==1),"Exactly one initializer per actual NPC recipient");
            if(below!=null)Check.That(!applied.Any(m=>ushort.Parse(m.Groups[2].Value)==below.ViewId),"Different-floor NPC receives no initializer");
            Check.That(!after.Contains($"[ENGAGE-FOES] source={a.CharId} "),"Blocker never casts or receives an invented taunt");
            Check.Equal(0,a.World.Problems.Count+b.World.Problems.Count,"Normal-wire parser and counter health");
            b.SendRpc("44 00");a.SendRpc("44 00");
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-engage-foes-"+context.Database.Name+".json"),JsonSerializer.Serialize(new{
                SourceAssembly=context.Server.SourceAssembly,SourceSha256=sourceHash,SentAbilityRpc=request==null?null:Convert.ToHexString(request),
                FullHpBeforeCast=fullHp,Target=target,ActorA=a.CharId,ActorB=b.CharId,BaselineLogLength=baseline.Length,InitialInnerStrength=initialIs,CurrentOperationIsUpdates=spent,
                Requests="A ordinary Block then40; B ordinary610/80b9; read-only combatdepth status; ordinary44 leave",NoRandomControls=true,
                Players=new[]{a,b}.Select(p=>new{p.CharId,p.Handle,Rpcs=p.World.RpcSnapshot(),RawViews=p.World.ViewSnapshot().Select(Convert.ToHexString),Creations=p.World.CreationSnapshot(),Problems=p.World.Problems}),
                Failure=failure?.ToString(),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
