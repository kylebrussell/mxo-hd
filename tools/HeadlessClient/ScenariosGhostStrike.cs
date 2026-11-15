using System.Text.Json;
using System.Security.Cryptography;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static ushort GhostWireVital(HeadlessPlayer player,string attribute,string update)
    {
        var values=ViewUpdateValues(player,2,update);
        return values.Count>0?values.Last():BitConverter.ToUInt16(player.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes[attribute]);
    }
    private static int GhostDebits(ushort before,IReadOnlyList<ushort> values,int? exact=null)
    {
        int count=0,previous=before;foreach(ushort value in values){if(previous>value&&(!exact.HasValue||previous-value==exact.Value))count++;previous=value;}return count;
    }
    private static async Task GhostStrikeCallback(ScenarioContext context)
    {
        using var caster=await GhostStrikeFixture.CsrAsync(context);
        var victim=await context.AddPlayerAsync(GhostStrikeFixture.Seed("GhostEnemy",true));
        var observer=await context.AddPlayerAsync(GhostStrikeFixture.Seed("GhostFriend") with{Z=GhostStrikeFixture.FriendZ});
        var proof=new List<object>();bool passed=false;string stage="entry";string? failure=null;
        string profiles=await context.Database.ExecuteAsync($"SELECT charId,level,alignment,x,y,z,healthC,innerStrC FROM characters WHERE charId IN (110,{victim.CharId},{observer.CharId}) ORDER BY charId");
        string owned=await context.Database.ExecuteAsync("SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id=110 ORDER BY slot,id");
        try
        {
            await caster.EnterWorldAsync();await victim.EnterWorldAsync();await observer.EnterWorldAsync();
            var target=await caster.WaitForViewNamedAsync(victim.Handle);var observed=await observer.WaitForViewNamedAsync(caster.Handle);
            int observerFlagCursor=observer.World.ViewSnapshot().Count;
            foreach(var p in new[]{caster,victim}){p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
            GhostStrikeObserverFlags.Sample? initialObserverFlags=null;
            await observer.WaitUntilAsync(()=>{initialObserverFlags=GhostStrikeObserverFlags.Latest(observer.World.ViewSnapshot(),observed.ViewId,observerFlagCursor);
                return initialObserverFlags!=null&&(initialObserverFlags.Flags&PvpFlag.PvpBit)!=0;},"actual observer flag update after normal pvpflag-on command",TimeSpan.FromSeconds(5));
            proof.Add(new{Stage="observer-pvp-baseline",Observed=initialObserverFlags});
            Cast(caster,1112);await caster.WaitForRpcAsync(ModPattern(1112,1034));
            stage="no-invisibility-current-base-policy";int noInvisibility=context.Server.DebugLog().Length;
            Cast(caster,365,target.ViewId);await caster.WaitUntilAsync(()=>context.Server.DebugLog()[noInvisibility..].Contains(" lands on GhostEnemy for "),"ordinary hit without supported invisibility",TimeSpan.FromSeconds(6));
            Check.That(!context.Server.DebugLog()[noInvisibility..].Contains("[GHOST-STRIKE] accepted callback"),"No unsupported Test/refund callback is fabricated");
            proof.Add(new{stage,Log=context.Server.DebugLog()[noInvisibility..]});await Task.Delay(11000);
            foreach(int roll in new[]{9,8})
            {
                stage="roll-"+roll;caster.SkipReceived();caster.SendChat("?ghoststrike "+roll);await caster.WaitForTextAsync("callback roll="+roll);
                var observerBaseline=GhostStrikeObserverFlags.Latest(observer.World.ViewSnapshot(),observed.ViewId)
                    ??throw new ScenarioFailure("No actual current observer flag baseline");
                Check.That((observerBaseline.Flags&PvpFlag.PvpBit)!=0,"Current observer baseline retains normal PvP flag");
                uint observerOn=GhostStrikeObserverFlags.Expected(observerBaseline.Flags,true),observerOff=GhostStrikeObserverFlags.Expected(observerBaseline.Flags,false);
                proof.Add(new{Stage=stage+"-observer-baseline",Observed=observerBaseline,ExpectedOn=observerOn,ExpectedOff=observerOff});
                observer.SkipReceived();var toolGrant=await GhostStrikeInvisibilityTool.ActivateAsync(caster);var initial=Convert.FromHexString(toolGrant.Modifier);
                await observer.WaitForViewUpdateAsync(observed.ViewId,WireFormat.ToHex(Stealth.BuildOther(Stealth.Invisible,observerOn)));
                double originalDuration=toolGrant.Duration;int cursor=caster.World.RpcSnapshot().Count;
                int log=context.Server.DebugLog().Length,sourceViews=caster.World.ViewSnapshot().Count,targetViews=victim.World.ViewSnapshot().Count;
                var before=new{SourceHealth=GhostWireVital(caster,"Health","02 80 80 80 40"),SourceIS=GhostWireVital(caster,"InnerStrengthAvailable","02 80 80 80 10"),TargetHealth=GhostWireVital(victim,"Health","02 80 80 80 40")};
                DateTime start=DateTime.UtcNow;Cast(caster,365,target.ViewId);
                await caster.WaitForRpcAsync(ModPattern(1116,115,true)); // this cast keeps the ordinary break
                await observer.WaitForViewUpdateAsync(observed.ViewId,WireFormat.ToHex(Stealth.BuildOther(0,observerOff)));
                await caster.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains(" lands on GhostEnemy for "),"accepted ordinary365 hit",TimeSpan.FromSeconds(6));
                await victim.WaitUntilAsync(()=>ViewUpdateValues(victim,2,"02 80 80 80 40",targetViews).Count>0,"fresh target own03 health update",TimeSpan.FromSeconds(3));
                await Task.Delay(500);
                var sourceIsUpdates=ViewUpdateValues(caster,2,"02 80 80 80 10",sourceViews);
                var targetHealthUpdates=ViewUpdateValues(victim,2,"02 80 80 80 40",targetViews);
                Check.Equal(1,GhostDebits(before.SourceIS,sourceIsUpdates,60),"Exactly one actual wire60IS cast debit, separate from positive regen updates");
                Check.Equal(1,GhostDebits(before.TargetHealth,targetHealthUpdates),"One ordinary health-loss update; the callback adds no second damage write");
                var restorations=caster.World.RpcSnapshot().Skip(cursor).Where(r=>BytePattern.Parse(ModPattern(1116,115)).IsMatch(r.Bytes)).ToArray();
                Check.Equal(roll==8?1:0,restorations.Length,"Only successful callback creates one normal1116 modifier");
                if(roll==8)
                {
                    float remaining=BitConverter.ToSingle(restorations.Single().Bytes,18);
                    Check.That(remaining>0&&remaining<originalDuration-1,"Restoration uses remaining original duration, never a fresh grant based on a hardcoded duration");
                    Check.Equal(1,System.Text.RegularExpressions.Regex.Matches(context.Server.DebugLog()[log..],@"\[GHOST-STRIKE\] accepted callback").Count,"Once per accepted cast");
                    await observer.WaitForViewUpdateAsync(observed.ViewId,WireFormat.ToHex(Stealth.BuildOther(Stealth.Invisible,observerOn)));
                    // Turning the normal restored grant off uses its own standard activation path.
                    await GhostStrikeInvisibilityTool.DeactivateAsync(caster);
                }
                proof.Add(new{stage,roll,Started=start,ToolGrant=toolGrant,Original=Convert.ToHexString(initial),Restorations=restorations.Select(r=>Convert.ToHexString(r.Bytes)),BeforeWire=before,
                    SourceIsUpdates=sourceIsUpdates,TargetHealthUpdates=targetHealthUpdates,
                    AfterWire=new{SourceHealth=GhostWireVital(caster,"Health","02 80 80 80 40"),SourceIS=GhostWireVital(caster,"InnerStrengthAvailable","02 80 80 80 10"),TargetHealth=GhostWireVital(victim,"Health","02 80 80 80 40")},
                    SourceFreshViews=caster.World.ViewSnapshot().Skip(sourceViews).Select(Convert.ToHexString),TargetFreshViews=victim.World.ViewSnapshot().Skip(targetViews).Select(Convert.ToHexString),Log=context.Server.DebugLog()[log..]});
                await Task.Delay(31000); // normal1116 term cooldown30s and365 reuse10s; no timer injection
            }
            stage="npc-accepted";caster.SkipReceived();caster.SendChat("?ghoststrike 8");await caster.WaitForTextAsync("callback roll=8");
            var npc=await caster.WaitForViewNamedAsync("Trainman");
            var npcGrant=await GhostStrikeInvisibilityTool.ActivateAsync(caster);int npcLog=context.Server.DebugLog().Length;
            Cast(caster,365,npc.ViewId);await caster.WaitForRpcAsync(ModPattern(1116,115,true));await caster.WaitForRpcAsync(ModPattern(1116,115));
            Check.That(context.Server.DebugLog()[npcLog..].Contains("[ABILITY] 365 lands"),"NPC ordinary accepted-hit path invokes source callback");
            proof.Add(new{stage,ToolGrant=npcGrant,NpcView=npc.ViewId,NpcSpawn=npc.SpawnId,Log=context.Server.DebugLog()[npcLog..]});
            await GhostStrikeInvisibilityTool.DeactivateAsync(caster);await Task.Delay(31000);
            stage="actual-world-departure";var departureGrant=await GhostStrikeInvisibilityTool.ActivateAsync(caster);
            int cancelLog=context.Server.DebugLog().Length,cancelRpc=caster.World.RpcSnapshot().Count;
            var clock=System.Diagnostics.Stopwatch.StartNew();Cast(caster,365,target.ViewId);
            var bar=await caster.WaitForRpcAsync("80 ac 6d 01");float seconds=BitConverter.ToSingle(bar.Bytes,bar.Bytes.Length-4);
            Check.Equal(1.5f,seconds,"Stored normal365 cast window");
            caster.SendRpc("80 fc 01");var finish=await caster.WaitForRpcAsync("80 fd");caster.SendRpc("80 fe 01");var reset=await caster.WaitForRpcAsync("81 07");
            double boundaryMs=clock.Elapsed.TotalMilliseconds;Check.That(boundaryMs<seconds*1000,"Actual world reset precedes cast due time");
            Check.That(context.Server.DebugLog()[cancelLog..].Contains("[LA] Loading the Loading Area"),"Real departure changed world lifecycle");
            caster.SendRpc("81 08");await caster.WaitForRpcAsync("81 09");caster.SendRpc("05");
            await caster.WaitUntilAsync(()=>caster.World.RpcSnapshot().Skip(cancelRpc).Any(r=>BytePattern.IndexOf(r.Bytes,System.Text.Encoding.UTF8.GetBytes("la.metr"),0)>=0),"Actual LA world command",TimeSpan.FromSeconds(4));
            await Task.Delay(2000);Check.That(!context.Server.DebugLog()[cancelLog..].Contains("[GHOST-STRIKE] accepted callback"),"Actual departure cancels callback");
            proof.Add(new{stage,ToolGrant=departureGrant,seconds,boundaryMs,CastBar=bar.Hex,Finish=finish.Hex,Reset=reset.Hex,Log=context.Server.DebugLog()[cancelLog..]});
            Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count+observer.World.Problems.Count,"Protocol parsing");passed=true;
        }
        catch(Exception error){failure=error.ToString();throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-ghost-strike-"+context.Database.Name);
            string? finalPersistence=null,persistenceFailure=null;
            try{finalPersistence=await context.Database.ExecuteAsync("SELECT charId,healthC,innerStrC,exp,cash FROM characters WHERE charId=110");}
            catch(Exception error){persistenceFailure=error.ToString();}
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{passed,stage,failure,SeedProfiles=profiles,owned,proof,Database=context.Database.Name,ServerPid=context.Server.ProcessId,
                SourceAssembly=context.Server.SourceAssembly,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))),Environment=GhostStrikeFixture.Environment,
                Actors=new[]{caster,victim,observer}.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems.ToArray()}),FinalPersistence=finalPersistence,PersistenceFailure=persistenceFailure,
                Debug=context.Server.DebugLog()},new JsonSerializerOptions{WriteIndented=true}));
            await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
        }
    }
}
