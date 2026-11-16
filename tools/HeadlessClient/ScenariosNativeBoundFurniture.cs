using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
 private static async Task NativeBoundFurniture(ScenarioContext context)
 {
  NativeBoundFurnitureFixture.RequireSource(Environment.GetEnvironmentVariable(NativeBoundFurnitureFixture.SourcePin),context.Server.SourceAssemblySha256);
  NativeBoundFurnitureFixture.RequireOwned(context.Database.Name);var prepared=NativeBoundFurnitureFixture.Prepare(context.RepositoryRoot);var start=prepared.Public.Outside[0];
  HeadlessPlayer? p=null;var proof=new List<object>();bool passed=false;Exception? failure=null;string step="seed",checkpoint="unavailable";var fields=new JsonSerializerOptions{IncludeFields=true};
  async Task<MissionProgress.SavedState> Saved(){checkpoint=MissionBoundCabinetFixture.DecodeCheckpointHex(await context.Database.ScalarAsync($"SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId={p!.CharId}"));return JsonSerializer.Deserialize<MissionProgress.SavedState>(checkpoint,fields)!;}
  async Task Barrier(){p!.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
  try
  {
   p=await context.AddPlayerAsync(new CharacterSeed("FurnitureCore"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=3,X=start[0],Y=start[1],Z=start[2],Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,
    CompletedMissions=NativeBoundFurnitureFixture.EarlierReceipts,Items=new[]{(1,6854u,0)},OwnedLoadedAbilities=NativeBoundFurnitureFixture.Abilities.Select(a=>(a,(ushort)50)).ToArray()});
   Check.Equal(1001u,p.CharId,"Owned disposable fixture identity");
   // PlayerHandler WorldEntry is after RPC05. Authenticated AddPlayer has not called EnterWorld yet.
   await context.Database.ExecuteAsync(NativeBoundFurnitureFixture.SeedSql(p.CharId,prepared));await p.EnterWorldAsync();await Barrier();
   var before=await Saved();var route=NativeBoundFurnitureProof.Read(context.RepositoryRoot,before);var street=BoundFurniturePublicProof.Read(context.RepositoryRoot,before);
   string wallet=await context.Database.ExecuteAsync($"SELECT exp,cash FROM characters WHERE charId={p.CharId}");int summaries=p.World.RpcSnapshot().Count(r=>r.Bytes.AsSpan().StartsWith(new byte[]{0x80,0xa2,0}));
   async Task Same(){NativeBoundFurnitureProof.RequireCurrent(route,await Saved());Check.That(p.World.CurrentOwnViewLife().Current,"Current fixture own life");}
   async Task Move(double[] at){await Same();await MoveForMissionTest(p,at[0],at[1],at[2]);}
   async Task Open(MissionNpcInteriorProof.Door door)
   {
    await Same();var life=p.World.CurrentOwnViewLife();Check.That(life.Current&&life.OwnViewReady,"Current own life immediately before ordinary80c8");int debug=context.Server.DebugLog().Length;int sections=p.World.ViewSnapshot().Count;
    p.SendRpc($"80 c8 {Le(door.Key)} 03 00");await Barrier();
    await p.WaitUntilAsync(()=>{try{var now=p.World.CurrentOwnViewLife();if(!ReferenceEquals(now.Owner,life.Owner)||now.Life!=life.Life||!life.Current)return false;_=NpcInteriorComputerState.CurrentDoor(now,door);return context.Server.DebugLog()[debug..].Contains($"[DOOR] Use of static {door.Key} type {door.Goid}");}catch(InvalidDataException){return false;}},"Actual exact own-GOID door use plus current Open",TimeSpan.FromSeconds(15));
    await Barrier();await Same();Check.Equal(0,MissionPublicStreetWire.Teleports(p.World.CurrentOwnViewLife(),life,sections).Count,"Ordinary noncar Open never performs an elevator ride");_=NpcInteriorComputerState.CurrentDoor(p.World.CurrentOwnViewLife(),door);proof.Add(new{Step=step,Door=door,DebugCursor=debug,Current=NpcInteriorComputerState.CurrentDoor(p.World.CurrentOwnViewLife(),door),NativePassageProven=false});
   }
   async Task Ride(MissionNpcInteriorProof.Door requested,double[] expected)
   {
    await Same();var life=p.World.CurrentOwnViewLife();int cursor=p.World.ViewSnapshot().Count;string path=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");int packet=InteriorAdmissionDeliveryProof.Read(path).Count(c=>c=='\n');int debug=context.Server.DebugLog().Length;
    p.SendRpc($"80 c8 {Le(requested.Key)} 03 00");NpcInteriorComputerState.Teleport? arrival=null;
    await p.WaitUntilAsync(()=>{try{arrival=NpcInteriorComputerState.FreshTeleport(p.World.CurrentOwnViewLife(),life,cursor,expected);return true;}catch(InvalidDataException){return false;}},"Fresh exact ordinary car arrival",TimeSpan.FromSeconds(15));
    MissionPublicStreetWire.Acknowledgment? ack=null;await p.WaitUntilAsync(()=>{try{ack=MissionPublicStreetWire.Ack(InteriorAdmissionDeliveryProof.Read(path),p.CharId,packet,arrival!);return true;}catch(InvalidDataException){return false;}},"Exact current-character receipt after car request",TimeSpan.FromSeconds(10));
    Check.That(context.Server.DebugLog()[debug..].Contains("[MISSION] Teleported to "),"Actual mission physical ride, not an approach/noop");await Same();proof.Add(new{Step=step,Requested=requested,Arrival=arrival,Ack=ack,ReceiptIsNotRendererReady=true});
   }
   step="public-outside";foreach(var at in street.Outside)await Move(at);step="ordinary-public-open";await Open(street.Door);
   step="public-inside";foreach(var at in street.Inside)await Move(at);step="ordinary-car-up";await Ride(route.Lobby,route.Floor.Standing!);
   step="fixed-own-door-route";foreach(var routeStep in NativeBoundFurnitureProof.Steps(context.RepositoryRoot,route))
   {await Same();foreach(uint key in routeStep.Doors)await Open(route.Doors.Single(d=>d.Key==key));await Move(routeStep.Standing);}
   step="current-native1450-body";NpcInteriorComputerState.Observation? observed=null;ViewCreation? body=null;
   await p.WaitUntilAsync(()=>
   {
    foreach(var v in p.World.CreationSnapshot().Where(v=>v.RelatedStaticObjectId==NativeBoundFurnitureFixture.SourceKey&&v.Goid==1450).Reverse())
     try{if(!v.Attributes.TryGetValue("Position",out var bytes)||bytes.Length!=24||!Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(bytes,i*8)==route.Centre[i])||!v.Attributes.TryGetValue("Orientation",out var q)||!q.SequenceEqual(Convert.FromHexString(route.Quaternion)))continue;
      observed=NpcInteriorComputerState.Capture(p.World.CurrentOwnViewLife(),v);body=v;return true;}catch(InvalidDataException){}
    return false;
   },"Exact current own-life native source binding/centre/quaternion",TimeSpan.FromSeconds(15));
   await Same();byte use=ObjectStates.UseNumber(1450,"Open")??throw new InvalidDataException("Original native Open use missing");
   Check.That(ObjectStates.Offered(1450,NpcInteriorComputerState.Read(p.World.CurrentOwnViewLife(),observed!.Capability)).Contains(use),"Current body actually offers Open");
   step="ordinary-body-open";int action=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(body!)} {use:x2} 00");var window=await p.WaitForRpcAsync("81 19");
   Check.That(p.World.RpcSnapshot().Skip(action).Any(r=>r.Bytes.SequenceEqual(window.Bytes)),"Fresh window after actual selected body Open");
   var identities=MissionBoundCabinetFixture.LootIdentities(window.Bytes,false);Check.That(identities.Any(i=>BitConverter.ToUInt32(Convert.FromHexString(i),4)==6782),"Actual optional key in container window");
   step="ordinary-key-take";p.SkipReceived();p.SendRpc("81 1f");var claim=await p.WaitForRpcAsync("81 20");Check.That(identities.SequenceEqual(MissionBoundCabinetFixture.LootIdentities(claim.Bytes,true)),"Unique actual window identities accepted");
   await p.WaitUntilAsync(()=>Saved().GetAwaiter().GetResult().Done.Contains(3),"Current optional Search3 commits",TimeSpan.FromSeconds(15));var after=await Saved();await Same();
   Check.That(after.Done.Contains(1)&&after.Done.Contains(3)&&!after.Done.Contains(2)&&after.Items.ContainsKey(6854)&&after.Items.ContainsKey(6782),"Captured Talk +new Search, mandatory Get remains open");
   string inventory=await context.Database.ExecuteAsync($"SELECT charId,goid,slot,count FROM inventory WHERE charId={p.CharId} AND goid=6782");_=MissionBoundCabinetFixture.RequireMissionInventory(inventory,p.CharId,6782,after.Items[6782]);
   Check.Equal(wallet,await context.Database.ExecuteAsync($"SELECT exp,cash FROM characters WHERE charId={p.CharId}"),"No mission/economic payout");Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId=301210"),"No selected completion receipt");
   Check.Equal(summaries,p.World.RpcSnapshot().Count(r=>r.Bytes.AsSpan().StartsWith(new byte[]{0x80,0xa2,0})),"No completion summary");Check.Equal(0,p.World.Problems.Count,"Wire parser problems");
   proof.Add(new{Step=step,Body=body,Capability=observed,Window=window.Hex,Claim=claim.Hex,Inventory=inventory,after.Done,after.Items,RawSourcesUnchanged=true,NativePickingProven=false});passed=true;step="complete";
  }
  catch(Exception e){failure=e;throw;}
  finally
  {
   string stem=Path.Combine(Path.GetTempPath(),"mxo-native-bound-furniture-"+context.Database.Name);string packet=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt"));
   if(p!=null)try{await Saved();}catch(Exception e){checkpoint+=" FINAL READ ERROR:"+e;}
   await File.WriteAllTextAsync(stem+"-PacketLog.txt",packet);await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),context.Database.Name,context.Server.ProcessId,Source=context.Server.SourceAssembly,SourceSha256=context.Server.SourceAssemblySha256,CapturedSha=NativeBoundFurnitureFixture.CapturedSha256,NewEarnedChain=false,Checkpoint=checkpoint,Proof=proof,PacketSha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(packet))),Rpc=p?.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)),Views=p?.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p?.World.Problems,Qualification="Explicit capturedphase/items/earlier receipts. Actual fresh builder before seed; no runtime upgrade. TP along proved nodes is a headless aid, not native walking/clearance/render proof. Ordinary public/car/internal door/Open/Take RPCs; no forced rolls or reward."},new JsonSerializerOptions{WriteIndented=true}));p?.Dispose();Console.WriteLine("BOUND FURNITURE EVIDENCE="+stem);
  }
 }
}
