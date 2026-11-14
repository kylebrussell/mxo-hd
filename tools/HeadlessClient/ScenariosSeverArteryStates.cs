using System.Text.Json;
using System.Security.Cryptography;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task SeverArteryStates(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(SeverArteryFixture.Actor(true));
        var victim=await context.AddPlayerAsync(SeverArteryFixture.Actor(false));
        string stage="entry";Exception? failure=null;var proof=new List<object>();
        async Task Control(int roll,int selector){caster.SkipReceived();caster.SendChat($"?severstate {roll} {selector}");await caster.WaitForTextAsync($"callback roll={roll} selector={selector}");}
        async Task<(RpcRecord Frame,int Index)> Frame(int cursor,string pattern,TimeSpan? timeout=null,ushort? recordId=null)
        {
            var bytes=BytePattern.Parse(pattern);RpcRecord? found=null;int index=-1;
            await victim.WaitUntilAsync(()=>{var rows=victim.World.RpcSnapshot();for(int i=cursor;i<rows.Count;i++)if(bytes.IsMatch(rows[i].Bytes)&&(!recordId.HasValue||BitConverter.ToUInt16(rows[i].Bytes,4)==recordId.Value)){found=rows[i];index=i;return true;}return false;},"current generation "+pattern,timeout??TimeSpan.FromSeconds(12));
            return(found!,index);
        }
        try
        {
            await caster.EnterWorldAsync();await victim.EnterWorldAsync();
            foreach(var player in new[]{caster,victim}){player.SendChat("?pvpflag on");await player.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
            var view=await caster.WaitForViewNamedAsync(victim.Handle);SelectStyle(caster,1112);await caster.WaitForRpcAsync(ModPattern(1112,1032));
            stage="negative99-normal-cast";await Control(99,0);int cursor=victim.World.RpcSnapshot().Count;
            Cast(caster,341,view.ViewId);await caster.WaitForRpcAsync("80 ac 55 01");await Frame(cursor,ModPattern(341,1157));
            var negativeParent=await Frame(cursor,ModPattern(341,710));ushort negativeId=BitConverter.ToUInt16(negativeParent.Frame.Bytes,4);
            await caster.WaitForRpcAsync("80 bd 01 55 01 04 00 00 70 41 00",TimeSpan.FromSeconds(20));
            var negativeRemoved=await Frame(negativeParent.Index+1,ModPattern(341,710,true),TimeSpan.FromSeconds(35),negativeId);
            Check.That(!victim.World.RpcSnapshot().Skip(cursor).Any(r=>new ushort[]{199,200,201}.Any(s=>BytePattern.Parse(ModPattern(341,s)).IsMatch(r.Bytes))),"Negative99 adds no341 random state on init/updates");
            proof.Add(new{Stage=stage,RecipientRpcCursor=cursor,Parent=Convert.ToHexString(negativeParent.Frame.Bytes),Removed=Convert.ToHexString(negativeRemoved.Frame.Bytes),RemovedFrame=negativeRemoved.Index});
            stage="positive-init-daze";await Control(0,0);cursor=victim.World.RpcSnapshot().Count;
            caster.SkipReceived();Cast(caster,341,view.ViewId);await caster.WaitForRpcAsync("80 ac 55 01");
            var positiveParent=await Frame(cursor,ModPattern(341,710));ushort positiveId=BitConverter.ToUInt16(positiveParent.Frame.Bytes,4);
            Check.That(positiveId!=negativeId,"Positive cast has a fresh callback record after actual old-parent removal");
            foreach(var pair in new[]{(Selector:0,State:(ushort)199),(Selector:1,State:(ushort)200),(Selector:2,State:(ushort)201)})
            {
                if(pair.Selector>0){stage="positive-update-"+pair.State;cursor=victim.World.RpcSnapshot().Count;await Control(0,pair.Selector);}
                var stateFrame=await Frame(cursor,ModPattern(341,pair.State));var record=stateFrame.Frame;
                Check.That(stateFrame.Index>positiveParent.Index,"State belongs to current positive parent generation");
                Check.Equal(10f,BitConverter.ToSingle(record.Bytes,18),"Original script exact10s state duration");
                proof.Add(new{Stage=stage,pair.Selector,pair.State,Record=Convert.ToHexString(record.Bytes),RecipientRpcCursor=cursor,StateFrame=stateFrame.Index,ParentRecord=positiveId});
                await Control(99,pair.Selector);await Frame(stateFrame.Index+1,ModPattern(341,pair.State,true),TimeSpan.FromSeconds(13),BitConverter.ToUInt16(record.Bytes,4));
            }
            await Frame(positiveParent.Index+1,ModPattern(341,710,true),TimeSpan.FromSeconds(12),positiveId);
            stage="expired-parent-no-late-callback";int log=context.Server.DebugLog().Length;await Control(0,0);await Task.Delay(3000);
            Check.That(!context.Server.DebugLog()[log..].Contains("[SEVER-STATE]"),"Expired parent cannot create new states");
            Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count,"Clean callback wire parsers");stage="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-sever-states-"+context.Database.Name);string dll=context.Server.SourceAssembly;
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Stage=stage,Failure=failure?.ToString(),Database=context.Database.Name,Proof=proof,Source=dll,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),ServerPid=context.Server.ProcessId,Actors=new[]{caster,victim}.Select(p=>new{p.CharId,p.Handle,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),p.World.Problems}),NativePresentationProven=false,CallbackOnlyQa=true,GlobalCastCombatRngOverride=false},new JsonSerializerOptions{WriteIndented=true}));
            await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
        }
    }
}
