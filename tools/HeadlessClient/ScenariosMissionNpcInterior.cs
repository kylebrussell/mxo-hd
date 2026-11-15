using System.Diagnostics;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task<HeadlessPlayer> MissionNpcInteriorContinuation(ScenarioContext context,ServerInstance server,HeadlessPlayer p,Action<HeadlessPlayer> replace,
        Func<Task<MissionProgress.SavedState>> saved,Func<bool,MissionProgress.SavedState?,Task> ride,List<object> proof,long baselineXp,long baselineInfo,int preOffer)
    {
        var fields=new JsonSerializerOptions{IncludeFields=true};var before=await saved();var plan=MissionNpcInteriorProof.Require(before);uint id=p.CharId;string handle=p.Handle;
        Check.That(before.Done.Length==0&&before.Items.TryGetValue(6783,out _),"Actual earned disk and pending current Ophiuchi6 Give");ushort slot=before.Items[6783];
        async Task<(string Raw,MissionBoundCabinetFixture.InventoryProof Value)> Inventory()
        {string raw=await context.Database.ExecuteAsync($"SELECT charId,goid,slot,`count` FROM inventory WHERE charId={id} AND goid=6783 ORDER BY slot");return(raw,MissionBoundCabinetFixture.RequireMissionInventory(raw,id,6783,slot));}
        var diskBefore=await Inventory();string rawBefore=await context.Database.ScalarAsync($"SELECT HEX(payload) FROM char_active_missions WHERE charId={id}");
        proof.Add(new{Step="npc-v2-before-carry",RawCheckpointHex=rawBefore,Plan=plan,Inventory=diskBefore.Raw,InventoryProof=diskBefore.Value});
        p.SkipReceived();p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");
        var prior=p.World.RpcSnapshot().Skip(preOffer).Select(r=>r.Bytes).ToArray();
        proof.Add(new{Step="npc-v2-old-session",Rpc=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),OwnLife=p.World.CurrentOwnViewLife(),Problems=p.World.Problems.ToArray()});Check.Equal(0,p.World.Problems.Count,"Original session parser healthy");
        p.Dispose();var wait=Stopwatch.StartNew();await Task.Delay(TimeSpan.FromSeconds(33));wait.Stop();
        p=await HeadlessPlayer.LoginAsync(server.Address,"user"+id,handle);replace(p);await p.EnterWorldAsync();await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");
        await p.WaitUntilAsync(()=>new byte[]{0xe5,0xe4}.All(op=>p.World.RpcSnapshot().Any(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==op&&BitConverter.ToUInt32(r.Bytes,6)==0)),"Actual restored wallet baselines",TimeSpan.FromSeconds(10));
        var restoredBaselineIndices=new HashSet<int>(new byte[]{0xe5,0xe4}.Select(op=>prior.Length+p.World.RpcSnapshot().FindIndex(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==op&&BitConverter.ToUInt32(r.Bytes,6)==0)));
        var restored=await saved();var afterPlan=MissionNpcInteriorProof.Require(restored);var diskAfter=await Inventory();
        Check.Equal(plan.Raw,afterPlan.Raw,"Exact actual NPC witness survives ordinary carry relog without migration");Check.Equal(before.Identity,restored.Identity,"Accepted mission owner retained");Check.Equal(before.Started,restored.Started,"Original acceptance time retained");Check.Equal(before.Instance,restored.Instance,"Mission instance retained");
        Check.Equal(JsonSerializer.Serialize(before.OpenFloorSites,fields),JsonSerializer.Serialize(restored.OpenFloorSites,fields),"Exact raw site history retained");Check.Equal(JsonSerializer.Serialize(before.Items,fields),JsonSerializer.Serialize(restored.Items,fields),"Owned item slots retained");Check.Equal(diskBefore.Value,diskAfter.Value,"Actual inventory owner/item/slot/rawcount retained");
        proof.Add(new{Step="npc-v2-carry-restored",WaitSeconds=wait.Elapsed.TotalSeconds,RawCheckpointHex=await context.Database.ScalarAsync($"SELECT HEX(payload) FROM char_active_missions WHERE charId={id}"),Inventory=diskAfter.Raw,InventoryProof=diskAfter.Value,Plan=afterPlan});
        await ride(true,restored);var entered=await saved();Check.That(entered.Flags.GetValueOrDefault("entered"),"Current NPC interior entry recorded");Check.Equal(plan.Raw,MissionNpcInteriorProof.Require(entered).Raw,"Ride retains immutable actor/door/source proof");
        var route=MissionNpcInteriorProof.Route(context.RepositoryRoot,plan);var doors=MissionNpcInteriorProof.RequiredDoors(plan);var opened=new HashSet<uint>();
        foreach(var step in route.Skip(1))
        {
            foreach(uint key in step.Doors)
            {
                if(!opened.Add(key))continue;var door=doors.Single(d=>d.Key==key);int cursor=p.World.RpcSnapshot().Count;string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SkipReceived();p.SendRpc(request);
                ViewCreation? view=null;await p.WaitUntilAsync(()=>
                {
                    view=p.World.CreationSnapshot().GroupBy(v=>v.ViewId).Select(g=>g.Last()).SingleOrDefault(v=>v.RelatedStaticObjectId==key&&v.Goid==door.Goid&&v.Attributes.TryGetValue("Position",out var at)&&at.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(at,i*8)==door.Position[i]));
                    if(view==null)return false;
                    try{return NpcInteriorComputerState.Capture(p.World.CurrentOwnViewLife(),view).State==0x834;}catch(InvalidDataException){return false;}
                },"actual certified own-GO wooden door Open",TimeSpan.FromSeconds(15));
                Check.Equal(door.Quaternion.ToUpperInvariant(),Convert.ToHexString(view!.Attributes["Orientation"]),"Wooden door keeps own-GO native orientation");
                proof.Add(new{Step="npc-v2-required-portal-open",Door=door,Request=request,RpcCursor=cursor,View=view,TopologyOnly=true,NativePassageProven=false});
            }
            await MoveForMissionTest(p,step.Standing[0],step.Standing[1],step.Standing[2]);
        }
        var scope=NpcInteriorCurrentNpc.Capture(await saved(),6);NpcInteriorCurrentNpc.CurrentView? npc=null;
        await p.WaitUntilAsync(()=>{var current=saved().GetAwaiter().GetResult();Check.Equal(plan.Raw,MissionNpcInteriorProof.Require(current).Raw,"Current role6 witness remains exact while approaching");try{npc=NpcInteriorCurrentNpc.Require(p.World.CurrentOwnViewLife(),scope,current);return true;}catch(InvalidDataException){return false;}},"actual current Ophiuchi6 own-life/UUID/fullXYZ",TimeSpan.FromSeconds(15));
        Check.That(npc!.View.Attributes.TryGetValue("TakerActiveTracker",out var tracker)&&tracker.Any(x=>x!=0),"Actual current Ophiuchi6 offers Give");
        int giveCursor=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(npc.View)} 0e 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(1,p.World.RpcSnapshot().Skip(giveCursor).Count(r=>BytePattern.Parse("80 a0 01 06 00 01").IsMatch(r.Bytes)),"One actual Give goal acknowledgement");
        var given=await saved();Check.That(given.Done.Contains(1)&&given.Flags.GetValueOrDefault("waitingForExit"),"Give persists Done and waits for ordinary departure");Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid=6783"),"Normal Give consumed actual earned disk");
        proof.Add(new{Step="npc-v2-give",Current=npc,GiveCursor=giveCursor,RawCheckpointHex=await context.Database.ScalarAsync($"SELECT HEX(payload) FROM char_active_missions WHERE charId={id}")});
        await ride(false,given);await p.WaitForRpcAsync("80 a2 00",TimeSpan.FromSeconds(20));
        await p.WaitUntilAsync(()=>new byte[]{0xe6,0xe5,0xe4}.All(op=>p.World.RpcSnapshot().Any(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==op&&BitConverter.ToUInt32(r.Bytes,6)==2)),"Actual complete mission wallet records",TimeSpan.FromSeconds(10));
        var records=prior.Concat(p.World.RpcSnapshot().Select(r=>r.Bytes)).ToArray();MissionNpcInteriorProof.Awards(records,baselineXp,baselineInfo,restoredBaselineIndices);
        string payload=await context.Database.ScalarAsync($"SELECT IFNULL(HEX(payload),'NULL') FROM char_active_missions WHERE charId={id}"),receiptHex=await context.Database.ScalarAsync($"SELECT HEX(receipt) FROM char_active_missions WHERE charId={id}");
        var receipt=JsonSerializer.Deserialize<MissionProgress.SavedState>(MissionBoundCabinetFixture.DecodeCheckpointHex(receiptHex),fields)!;
        Check.Equal("NULL",payload,"Retained CAS row payload is NULL after actual completion");Check.Equal(plan.Identity,receipt.Identity,"Terminal receipt has exact accepted mission identity");Check.Equal(plan.Started,receipt.Started,"Terminal receipt retains actual acceptance");Check.Equal(201201,receipt.Mission,"Terminal receipt selected mission");Check.Equal(2,receipt.Phase,"Terminal receipt actual final stage");
        long completions=await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={id} AND missionId=201201"),xp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={id}"),info=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}");
        Check.Equal(1L,completions,"One selected earned receipt");Check.Equal(baselineXp+MissionNpcInteriorProof.Experience,xp,"Literal independent201201 XP quote");Check.Equal(baselineInfo+MissionNpcInteriorProof.Information,info,"Literal independent201201 information quote");
        int next=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc("80 94 05 00 02");await p.WaitForRpcAsync("80 97 05 00");int nextIndex=MissionContactChoice.CriticalIndex(p.World.RpcSnapshot().Skip(next).Select(r=>r.Bytes),"Poster Hunt");
        Check.Equal(0,p.World.Problems.Count,"Final own session parser healthy");proof.Add(new{Step="npc-v2-complete",PayloadHex=payload,ReceiptHex=receiptHex,Receipt=receipt,Completions=completions,ActualExperience=xp,ActualInformation=info,NextMission=201202,NextIndex=nextIndex,NextOffer=p.World.RpcSnapshot().Skip(next).Select(r=>r.Hex).ToArray(),ScopedMissionRpc=records.Select(Convert.ToHexString).ToArray(),Route=route,CurrentWitness=plan,NativeHoverProven=false,NativeWalkingProven=false});return p;
    }
}
