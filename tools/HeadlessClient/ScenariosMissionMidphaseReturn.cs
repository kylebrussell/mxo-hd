using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MissionMidphaseReturn(ScenarioContext context)
    {
        MissionMidphaseReturnFixture.RequireOwned(context.Database.Name);
        MissionMidphaseReturnFixture.RequireSource(Environment.GetEnvironmentVariable(MissionMidphaseReturnFixture.SourcePin),context.Server.SourceAssemblySha256);
        byte[] fixture=MissionMidphaseReturnFixture.Payload; // Verify both hashes/shape BEFORE any player/DB mutation.
        var sessions=new List<HeadlessPlayer>();var steps=new List<object>();HeadlessPlayer? actor=null;
        bool passed=false;Exception? failure=null;string stage="captured-seed",account="",items="",receipts="";
        string packetPath=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        async Task<string> Account()=>await context.Database.ExecuteAsync("SELECT exp,cash FROM characters WHERE charId=1001");
        async Task<string> Items()=>await context.Database.ExecuteAsync("SELECT goid,slot,count FROM inventory WHERE charId=1001 ORDER BY slot,goid");
        async Task<string> Receipts()=>await context.Database.ExecuteAsync("SELECT missionId,completions FROM char_missions WHERE charId=1001 ORDER BY missionId");
        async Task<MissionProgress.SavedState> Current()
        {
            string hex=await context.Database.ScalarAsync("SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId=1001");
            var current=MissionMidphaseReturnFixture.ReadHex(hex);MissionMidphaseReturnFixture.RequireUnchanged(current);return current;
        }
        async Task Unchanged()
        {
            var saved=await Current();Check.Equal(account,await Account(),"No payout/XP/cash change");Check.Equal(items,await Items(),"No item duplication/removal/rawcount normalization");
            Check.Equal(receipts,await Receipts(),"Only earlier receipts; no101208 receipt");
            Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM char_missions WHERE charId=1001 AND missionId=101208"),"Current mission not completed");
            Check.Equal(1L,await context.Database.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=1001 AND goid=6831 AND slot=6 AND count=0"),"Exact owned CD row");
        }
        async Task Ride(bool up)
        {
            var saved=await Current();var plan=MissionNpcInteriorRide.ForSite(saved)!;var to=up?plan.Floor:plan.Lobby;uint key=up?plan.Lobby.Key:plan.Floor.Key;
            var ownLife=actor!.World.CurrentOwnViewLife();int rpc=actor.World.RpcSnapshot().Count,views=actor.World.ViewSnapshot().Count;
            int line=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c=='\n');actor.SkipReceived();
            string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";
            steps.Add(new{Step="before-ordinary-request",Up=up,Key=key,Request=request,RpcCursor=rpc,ViewCursor=views,PacketLine=line,Checkpoint=saved});
            actor.SendRpc(request);
            await actor.WaitForTextAsync(up?MissionStaging.ElevatorUpLine:MissionStaging.ElevatorDownLine,TimeSpan.FromSeconds(10));
            NpcInteriorComputerState.Teleport? arrival=null;
            await actor.WaitUntilAsync(()=>{try{arrival=NpcInteriorComputerState.FreshTeleport(actor.World.CurrentOwnViewLife(),ownLife,views,to.Standing!);return true;}catch(InvalidDataException){return false;}},"Fresh typed current-own-life ordinary011c elevator destination",TimeSpan.FromSeconds(15));
            MissionMidphaseReturnFixture.Arrival? receipt=null;
            await actor.WaitUntilAsync(()=>{try{receipt=MissionMidphaseReturnFixture.RequireArrival(InteriorAdmissionDeliveryProof.Read(packetPath),actor.CharId,line,arrival!);return true;}catch(InvalidDataException){return false;}},"Independent exact destination transport ACK",TimeSpan.FromSeconds(10));
            Check.That(ownLife.Current&&actor.World.CurrentOwnViewLife().Life==ownLife.Life,"Ride belongs to unchanged current own life");
            await Unchanged();steps.Add(new{Step=up?"ordinary-up-committed":"ordinary-down-committed",TypedArrival=arrival,Receipt=receipt,Checkpoint=await Current(),PhysicalTeleportObserved=true,MovementAid=false});
        }
        try
        {
            actor=await context.AddPlayerAsync(new CharacterSeed("MidphaseReturn"){X=3700,Y=1295,Z=13100,Level=50,Health=5000,MaxHealth=5000,InnerStrength=1000,MaxInnerStrength=1000,Organization=1,
                Items=new[]{(6,6831u,0)},CompletedMissions=MissionMidphaseReturnFixture.EarlierReceipts,OwnedLoadedAbilities=MissionMidphaseReturnFixture.Abilities.Select(a=>(a,(ushort)50)).ToArray()});
            sessions.Add(actor);Check.Equal(1001u,actor.CharId,"Owned scenario character");
            // AddPlayer returns after UDP auth. No RPC05 yet: WorldEntry/restore happens in EnterWorld.
            await context.Database.ExecuteAsync(MissionMidphaseReturnFixture.ActiveSql(actor.CharId));
            Check.Equal(Convert.ToHexString(fixture),await context.Database.ScalarAsync("SELECT HEX(payload) FROM char_active_missions WHERE charId=1001"),"Exact normalized capture installed before RPC05");
            account=await Account();items=await Items();receipts=await Receipts();
            await actor.EnterWorldAsync();await actor.WaitForRpcAsync("80 9c");await Unchanged();
            var own=actor.World.CreationSnapshot().Last(c=>c.ViewId==2);Check.That(own.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(pos,i*8)==new[]{3700d,1295,13100}[i]),"Cold self starts on captured floor");
            stage="unfinished-down";await Ride(false);
            stage="ordinary-quit-on-lobby";actor.SkipReceived();actor.SendRpc("80 fe 00");await actor.WaitForRpcAsync("80 fd");actor.Dispose();actor=null;
            await Task.Delay(TimeSpan.FromSeconds(33)); // Actual unchanged login reservation; no forced reset/cleanup bypass.
            await Unchanged();stage="same-identity-lobby-relog";actor=await HeadlessPlayer.LoginAsync(context.Server.Address,"user1001","MidphaseReturn");sessions.Add(actor);
            Check.Equal(1001u,actor.CharId,"Same owned character relog");await actor.EnterWorldAsync();await actor.WaitForRpcAsync("80 9c");await Unchanged();
            var lobbySelf=actor.World.CreationSnapshot().Last(c=>c.ViewId==2).Attributes["Position"];
            Check.That(Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(lobbySelf,i*8)==new[]{3700d,95,13100}[i]),"Normal Quit saved lobby standing");
            stage="unfinished-return-up";await Ride(true);stage="unfinished-down-again";await Ride(false);
            Check.Equal(0,sessions.Sum(s=>s.World.Problems.Count),"Both session parsers clean");passed=true;stage="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string checkpoint="";try{checkpoint=await context.Database.ScalarAsync("SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId=1001");}catch(Exception e){checkpoint="READ FAILED: "+e.Message;}
            await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-midphase-return-"+context.Database.Name+".json"),JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Source=context.Server.SourceAssembly,
                SourceSha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))),ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Workdir=context.Server.WorkingDirectory,
                RawBatchSha=MissionMidphaseReturnFixture.RawSha,NormalizedSha=MissionMidphaseReturnFixture.NormalizedSha,Steps=steps,FinalCheckpointHex=checkpoint,InitialAccount=account,InitialItems=items,InitialReceipts=receipts,
                Sessions=sessions.Select(p=>new{p.CharId,p.Handle,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),p.World.Problems}),
                PacketLog=File.Exists(packetPath)?InteriorAdmissionDeliveryProof.Read(packetPath):"MISSING",Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),CapturedSeed=true,NewEarnedChain=false,NativeVisibleProof=false,MovementAids=false},new JsonSerializerOptions{WriteIndented=true}));
            actor?.Dispose();
        }
    }
}
