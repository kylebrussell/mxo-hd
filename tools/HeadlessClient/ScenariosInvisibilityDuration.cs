using System.Diagnostics;
using hds;
using System.Security.Cryptography;
using System.Text.Json;
using MxoHd.PacketResearch;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task InvisibilityDuration(ScenarioContext context)
    {
        string sourceHash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly)));
        InvisibilityDurationFixture.RequirePin(System.Environment.GetEnvironmentVariable(InvisibilityDurationFixture.SourcePin),sourceHash);
        var actor=await context.AddPlayerAsync(InvisibilityDurationFixture.Seed("DurationCaster"));
        var observer=await context.AddPlayerAsync(InvisibilityDurationFixture.Seed("DurationObserver") with {X=17200});
        string stage="entry";Exception? failure=null;var operations=new List<object>();
        try
        {
            await observer.EnterWorldAsync();await actor.EnterWorldAsync();
            Check.That(!InvisibilityDurationFixture.Mods(actor).Any(m=>m.Parent==1117&&!m.Off),"Shadow buff is not preactivated/injected");
            await Natural(130,"baseline");
            stage="ordinary-shadow-action";int cursor=actor.World.RpcSnapshot().Count,viewCursor=actor.World.ViewSnapshot().Count;
            ushort before=RefusalOwnValue(actor,"InnerStrengthAvailable","02 80 80 80 10");
            var self=actor.World.CreationSnapshot().Last(v=>v.ViewId==2);
            var request=new AbilityUseRecord(1117,DuelPackets.Handle(self.ViewId,self.SpawnId),0,0,0,0,0x6b,0x6b,0,0).Encode();actor.SendRpc(request);
            await actor.WaitUntilAsync(()=>InvisibilityDurationFixture.Mods(actor,cursor).Any(m=>m.Parent==1117&&m.Target==644&&!m.Off&&m.Value==3),"ordinary Shadow class buff644+3",TimeSpan.FromSeconds(10));
            var updates=ViewUpdateValues(actor,2,"02 80 80 80 10",viewCursor).ToArray();var history=new[]{before}.Concat(updates).ToArray();
            Check.That(history.Zip(history.Skip(1)).Any(v=>v.First-v.Second==20),"Actual normal1117 twentyIS debit");
            operations.Add(new{Stage=stage,Request=Convert.ToHexString(request),Cursor=cursor,BeforeIS=before,IsUpdates=updates,Modifiers=InvisibilityDurationFixture.Mods(actor,cursor).ToArray(),NativeActionsVisibilityProven=false});
            await Natural(133,"shadow-duration");
            Check.Equal(0,actor.World.Problems.Count+observer.World.Problems.Count,"Normal-wire parser healthy");stage="passed";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-invisibility-duration-"+context.Database.Name);
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Stage=stage,Failure=failure?.ToString(),SourceAssembly=context.Server.SourceAssembly,SourceSha256=sourceHash,context.Database.Name,context.Server.ProcessId,
                Operations=operations,Actors=new[]{actor,observer}.Select(p=>new{p.CharId,p.Handle,Margin=p.CharacterData.ToDictionary(k=>k.Key,v=>Convert.ToHexString(v.Value)),Rpc=p.World.RpcSnapshot(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Creations=p.World.CreationSnapshot(),Problems=p.World.Problems}),
                Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),NoRngOrStateInjection=true,NativeActionsVisibilityProven=false,NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("INVISIBILITY DURATION EVIDENCE "+stem+".json");
        }
        async Task Natural(float expected,string label)
        {
            stage=label;int cursor=actor.World.RpcSnapshot().Count;int debugCursor=context.Server.DebugLog().Length;var clock=Stopwatch.StartNew();
            var grant=await GhostStrikeInvisibilityTool.ActivateAsync(actor);
            await actor.WaitUntilAsync(()=>InvisibilityDurationFixture.Mods(actor,cursor).Count(m=>m.Parent==1116&&!m.Off&&new ushort[]{115,881}.Contains(m.Target))==2,"both exact invisibility state/modifier records",TimeSpan.FromSeconds(5));
            var records=InvisibilityDurationFixture.Mods(actor,cursor).Where(m=>m.Parent==1116&&!m.Off&&new ushort[]{115,881}.Contains(m.Target)).ToArray();
            Check.Equal(expected,grant.Duration,"Independent primary-script duration");foreach(var record in records)Check.Equal(expected,record.Seconds,"Same captured duration on stat/state records");
            Check.That(actor.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length==18&&r.Bytes[0]==0x80&&r.Bytes[1]==0xac&&BitConverter.ToUInt16(r.Bytes,2)==1116&&BitConverter.ToSingle(r.Bytes,14)==3f),"Actual stored3s cast bar");
            double onSeconds=clock.Elapsed.TotalSeconds;int offCursor=actor.World.RpcSnapshot().Count;
            await actor.WaitUntilAsync(()=>records.All(r=>InvisibilityDurationFixture.Off(actor,r,offCursor)),"natural exact-identity invisibility OFF "+expected+"s",TimeSpan.FromSeconds(expected+15));
            Check.That(context.Server.DebugLog()[debugCursor..].Contains(actor.Handle+" 1116 off (expired)"),"Natural expiry, not a toggle/action break");
            var all=InvisibilityDurationFixture.Mods(actor,cursor).ToArray();foreach(var record in records)Check.Equal(1,all.Count(m=>m.Off&&m.Parent==record.Parent&&m.Target==record.Target&&m.Record==record.Record),"Each exact invisibility record OFF once");
            operations.Add(new{Stage=label,Cursor=cursor,Grant=grant,ExpectedSeconds=expected,OnSeconds=onSeconds,NaturalOffSeconds=clock.Elapsed.TotalSeconds,Records=records,CurrentModifiers=all,Debug=context.Server.DebugLog()[debugCursor..],ObserverRpc=observer.World.RpcSnapshot(),ObserverViews=observer.World.ViewSnapshot().Select(Convert.ToHexString)});
        }
    }
}
