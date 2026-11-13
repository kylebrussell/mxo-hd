using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task DebuffRadiusNativeFixture(ScenarioContext context)
    {
        var actor=await context.AddPlayerAsync(new CharacterSeed("RadiusActor"){X=17000,Y=495,Z=2300,Level=50,Organization=2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={actor.CharId}");await LoadAbilitiesAsync(context,actor,DebuffRadiusFixture.Abilities);
        var startup=new List<object>{await DebuffRadiusFixture.EnterFlaggedAsync(actor)};
        var near=await context.AddPlayerAsync(new CharacterSeed("QAFriend"){X=17880,Y=495,Z=2300,Level=50,Organization=1,Health=5000,MaxHealth=5000});
        var edge=await context.AddPlayerAsync(new CharacterSeed("QAFar"){X=17940,Y=495,Z=2300,Level=50,Organization=1,Health=5000,MaxHealth=5000});
        var far=await context.AddPlayerAsync(new CharacterSeed("QAOther"){X=17500,Y=495,Z=2799,Level=50,Organization=1,Health=5000,MaxHealth=5000});
        var primary=await context.AddPlayerAsync(new CharacterSeed("QAEnemy"){X=17500,Y=495,Z=2300,Level=50,Organization=1,Health=5000,MaxHealth=5000});
        var all=new[]{actor,near,edge,far,primary};
        try
        {
            foreach(var p in all.Skip(1))startup.Add(await DebuffRadiusFixture.EnterFlaggedAsync(p));
            int loaded=actor.Received("80 b2 4d 01").Count;actor.SendChat("?qaload 333");
            await actor.WaitUntilAsync(()=>actor.Received("80 b2 4d 01").Count>loaded,"Owned333 refreshed through ordinary80ae",TimeSpan.FromSeconds(5));
            var target=await actor.WaitForViewNamedAsync("QAEnemy");await actor.WaitForViewNamedAsync("QAFriend");await actor.WaitForViewNamedAsync("QAFar");await actor.WaitForViewNamedAsync("QAOther");
            async Task CastArea(bool expanded)
            {
                int n=near.Received(ModPattern(333,102)).Count,e=edge.Received(ModPattern(333,102)).Count,f=far.Received(ModPattern(333,102)).Count,start=context.Server.DebugLog().Length;
                Cast(actor,333,target.ViewId);
                await near.WaitUntilAsync(()=>near.Received(ModPattern(333,102)).Count>n,"Normal same-world hostile3.8m recipient",TimeSpan.FromSeconds(12));
                if(expanded)await edge.WaitUntilAsync(()=>edge.Received(ModPattern(333,102)).Count>e,"Normal4.4m recipient after captured345+21",TimeSpan.FromSeconds(10));
                Check.Equal(f,far.Received(ModPattern(333,102)).Count,"4.99m remains outside");
                if(!expanded)Check.Equal(e,edge.Received(ModPattern(333,102)).Count,"4.4m outside base4m");
                string trace=context.Server.DebugLog()[start..];
                var contests=trace.Split('\n').Where(l=>l.Contains("[FREECAST-VIRAL] ability=333 source=RadiusActor ")).ToArray();
                foreach(string name in expanded?new[]{"QAEnemy","QAFriend","QAFar"}:new[]{"QAEnemy","QAFriend"})
                {
                    var rows=contests.Where(l=>l.Contains("target="+name+" ")).ToArray();Check.Equal(1,rows.Length,"Exactly one333 contest for "+name);
                    Check.That(rows[0].Contains("hit=True qa=True"),"Actual owned/runtime-cache scoped333 draw accepted for "+name);
                }
                Check.That(!contests.Any(l=>l.Contains("target=QAOther ")),"No contest outside radius");
                if(!expanded)Check.That(!contests.Any(l=>l.Contains("target=QAFar ")),"No contest for baseline excluded edge");

                Check.That(trace.Contains("candidate=QAFriend")&&trace.Contains("reason=included"),"Actual recipient decisions logged");
                Check.That(trace.Split('\n').Any(l=>l.Contains("candidate=QAFar")&&l.Contains(expanded?"reason=included":"reason=radius")),"Exact edge include/exclude reason");
                Check.That(trace.Split('\n').Any(l=>l.Contains("candidate=QAOther")&&l.Contains("reason=radius")),"Exact outside reason");
                Check.That(trace.Contains("org/alignment=1 flag=True")&&trace.Contains("org/alignment=2 flag=True"),"Actual loaded organizations and automatic flags");
            }
            await CastArea(false);await Task.Delay(35000);Cast(actor,345);await actor.WaitForRpcAsync(ModPattern(345,177));await CastArea(true);
            foreach(var p in all)Check.Equal(0,p.World.Problems.Count,"Native fixture protocol errors");
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-debuff-radius-native-"+context.Database.Name+".json"),JsonSerializer.Serialize(new{Startup=startup,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceAssemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Raw=all.Select(p=>new{p.Handle,Rpcs=p.World.RpcSnapshot().Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(WireFormat.ToHex).ToArray()}).ToArray(),Diagnostics=context.Server.DebugLog(),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
