using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;
public sealed class NativeBoundFurnitureMode:NativeQaMode
{
 public override string Name=>NativeBoundFurnitureFixture.Name;
 private NativeQaModePlan? plan;
 public override NativeQaModePlan Plan=>plan??=Create();
 static NativeQaModePlan Create()
 {
  var p=NativeBoundFurnitureFixture.Default;var start=p.Public.Outside[0];
  return new(NativeBoundFurnitureFixture.Abilities,(int)start[0],(int)start[2],Array.Empty<string>(),new[]{NativeBoundFurnitureFixture.SeedSql(110,p)},Array.Empty<string>(),
   DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
   environmentOverrides:new(){["MXO_PACKET_LOG"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"},
   extraQaMetadata:new(){["CapturedPhaseFixture"]=true,["Mission"]=301210,["Phase"]=1,["NewEarnedChain"]=false,["CapturedItem"]=6854,["CapturedItemSlot"]=1,["SeededEarlierReceipts"]=29,
    ["SourceKey"]="0x3d9004b8",["BoundGoid"]=1450,["NewStreetSelectionVersion"]=3,["NewRouteKind"]="BoundFurnitureRoutes",["CapturedSha256"]=NativeBoundFurnitureFixture.CapturedSha256,
    ["ProfileManifestSha256"]=NativeBoundFurnitureFixture.ManifestSha256,["QaBaseMaster"]="500dc117afedb2d95cf1229558564d67982f79c1",["UnmergedDependency"]="PR241/fa06152b generic runtime only"},
   readyMessages:new[]{"CAPTURED301210 phase1 fixture, NOT an earned story chain. Earlier29 receipts and HitList6854 bag1 are seeded; current301210 receipt and AccessCode6782 are NOT. The real fresh admission builder creates a NEW separate V3 furniture route before seeding; runtime restore never upgrades old V1/V2. CSR110 only. Start outside, WALK through exact public Door/Open to the saved lobby; ordinary car Up. Follow saved original nodes through OWN6958 key1032848039 (never6959 alias). Clear avatar/camera and inspect native1450 Dresser centre41445/1049.98193359375/-27900; walk to saved grounded Standing, native Open/Search, Take earned optional AccessCode6782. Mandatory Annita Get stays unfinished, no payout/completion. Stop on no hover/blocked passage/source mismatch; no missionfloor/TP/source reroll. Headless movement aids are separate, not native walking proof. See docs/NATIVE-BOUND-FURNITURE.md."});
 }
 public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
 {
  var prepared=NativeBoundFurnitureFixture.Default;bool checkedOwned=false;
  if(context.OwnedDatabase!=null)
  {
   NativeBoundFurnitureFixture.RequireOwned(context.OwnedDatabase.Name);
   string hex=await context.OwnedDatabase.ScalarAsync("SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId=110");
   if(!Convert.FromHexString(hex.Trim()).SequenceEqual(prepared.Payload))throw new InvalidDataException("Actual captured furniture seed differs");checkedOwned=true;
  }
  return JsonSerializer.SerializeToUtf8Bytes(new{Mode=Name,ActualOwnedSeedChecked=checkedOwned,PreparedCaptureSha256=NativeBoundFurnitureFixture.CapturedSha256,
   ProfileManifest=NativeBoundFurnitureFixture.ManifestSha256,RouteKind="BoundFurnitureRoutes",GameplayOrNativeOpenProven=false,Scope="Captured seed/auth/preparation only; normal wire case is separate"});
 }
}
