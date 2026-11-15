using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ReverseBulldogControls(ScenarioContext context)
    {
        string sourceHash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly)));
        ReverseBulldogFixture.RequirePin(System.Environment.GetEnvironmentVariable(ReverseBulldogFixture.SourcePin),sourceHash);
        var a=await context.AddPlayerAsync(ReverseBulldogFixture.Seed("BulldogPrimary"));var b=await context.AddPlayerAsync(ReverseBulldogFixture.Seed("BulldogBlocked"));
        var target=await context.AddPlayerAsync(ReverseBulldogFixture.Seed("BulldogTarget",true));
        string stage="entry";Exception? failure=null;var operations=new List<object>();ReverseBulldogFixture.Modifier? stun=null,timer=null;
        try
        {
            await target.EnterWorldAsync();await a.EnterWorldAsync();await b.EnterWorldAsync();
            await a.WaitUntilAsync(()=>ReverseBulldogFixture.Current(a.World.CreationSnapshot(),target.Handle)!=null,"actual primary target view",TimeSpan.FromSeconds(10));
            await b.WaitUntilAsync(()=>ReverseBulldogFixture.Current(b.World.CreationSnapshot(),target.Handle)!=null,"actual negative target view",TimeSpan.FromSeconds(10));
            await ReverseBulldogFixture.SneakAsync(a);await ReverseBulldogFixture.SneakAsync(b);
            stage="positive";int cursor=target.World.RpcSnapshot().Count,sourceViews=a.World.ViewSnapshot().Count;ushort before=RefusalOwnValue(a,"InnerStrengthAvailable","02 80 80 80 10");
            ushort targetHealthBefore=RefusalOwnValue(target,"Health","02 80 80 80 40");Check.Equal((ushort)10000,targetHealthBefore,"Fresh target health before normal388");int targetViews=target.World.ViewSnapshot().Count;
            var av=ReverseBulldogFixture.Current(a.World.CreationSnapshot(),target.Handle)!;byte[] request=ReverseBulldogFixture.Cast(a,388,av);
            await target.WaitUntilAsync(()=>ReverseBulldogFixture.Mods(target,cursor).Any(m=>!m.Off&&m.Parent==388&&m.Target==102&&m.Kind==0x11),"actual388 Stun102 callback",TimeSpan.FromSeconds(10));
            stun=ReverseBulldogFixture.Mods(target,cursor).Single(m=>!m.Off&&m.Parent==388&&m.Target==102&&m.Kind==0x11);Check.Equal(8f,stun.Seconds,"Independent stored8 input with neutral duration fixture");
            await target.WaitUntilAsync(()=>ReverseBulldogFixture.Mods(target,cursor).Any(m=>!m.Off&&m.Parent==734&&m.Target==681),"separate16second immunity callback",TimeSpan.FromSeconds(5));
            timer=ReverseBulldogFixture.Mods(target,cursor).Single(m=>!m.Off&&m.Parent==734&&m.Target==681);Check.Equal(16f,timer.Seconds,"Independent explicit16timer");
            var isUpdates=ViewUpdateValues(a,2,"02 80 80 80 10",sourceViews).ToArray();var isHistory=new[]{before}.Concat(isUpdates).ToArray();
            Check.That(isHistory.Zip(isHistory.Skip(1)).Any(pair=>pair.First-pair.Second==60),"Actual adjacent60IS debit after normalSneak, independent of regen");
            var healthUpdates=ViewUpdateValues(target,2,"02 80 80 80 40",targetViews).ToArray();Check.That(healthUpdates.Any(h=>h>0&&h<targetHealthBefore),"Actual current-view normal388 damage with living target");
            Check.That(a.World.RpcSnapshot().Any(r=>Convert.ToHexString(r.Bytes).StartsWith("80BD018401040000704201",StringComparison.Ordinal)),"Normal60second reuse timer");
            operations.Add(new{Stage=stage,Cursor=cursor,Target=av,Sent=Convert.ToHexString(request),BeforeIS=before,IS=isUpdates,TargetHealthBefore=targetHealthBefore,TargetHealthUpdates=healthUpdates,Stun=stun,Timer=timer});
            stage="existing-immunity";cursor=target.World.RpcSnapshot().Count;var bv=ReverseBulldogFixture.Current(b.World.CreationSnapshot(),target.Handle)!;request=ReverseBulldogFixture.Cast(b,388,bv);
            await target.WaitUntilAsync(()=>ReverseBulldogFixture.Mods(target,cursor).Any(m=>m.Off&&m.Parent==388&&m.Target==710&&m.Kind==9),"second normal cast's terminal marker expired",TimeSpan.FromSeconds(8));
            Check.That(!ReverseBulldogFixture.Mods(target,cursor).Any(m=>!m.Off&&(m.Parent==388&&m.Target==102||m.Parent==734&&m.Target==681)),"Existing734/681 blocks both new callbacks");
            Check.That(!ReverseBulldogFixture.Off(target,stun)&&!ReverseBulldogFixture.Off(target,timer),"Immune reapplication keeps exact existing state/timer identities");
            operations.Add(new{Stage=stage,Cursor=cursor,Target=bv,Sent=Convert.ToHexString(request),NewModifiers=ReverseBulldogFixture.Mods(target,cursor).ToArray()});
            stage="natural-expiry";await target.WaitUntilAsync(()=>ReverseBulldogFixture.Off(target,stun),"natural8second Stun OFF",TimeSpan.FromSeconds(12));
            await target.WaitUntilAsync(()=>ReverseBulldogFixture.Off(target,timer),"natural16second timer OFF",TimeSpan.FromSeconds(20));
            Check.Equal(0,a.World.Problems.Count+b.World.Problems.Count+target.World.Problems.Count,"Normal-wire parser/counters");stage="passed";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-reverse-bulldog-"+context.Database.Name);
            await File.WriteAllTextAsync(stem+"-debug.log",context.Server.DebugLog());
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Stage=stage,Failure=failure?.ToString(),SourceAssembly=context.Server.SourceAssembly,SourceSha256=sourceHash,
                Operations=operations,Stun=stun,Timer=timer,NoRandomOrStateInjection=true,Actors=new[]{a,b,target}.Select(p=>new{p.CharId,p.Handle,AllRpc=p.World.RpcSnapshot(),RawViews=p.World.ViewSnapshot().Select(Convert.ToHexString),Creations=p.World.CreationSnapshot(),Problems=p.World.Problems}),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
