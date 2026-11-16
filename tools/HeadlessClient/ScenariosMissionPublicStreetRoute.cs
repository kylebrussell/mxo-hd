using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    public static Dictionary<string,string> MissionPublicStreetEnvironment()=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="101213",["MXO_PACKET_LOG"]="1"};
    private static async Task MissionPublicStreetRoute(ScenarioContext context)
    {
        InteriorAdmissionFixture.RequireSource(Environment.GetEnvironmentVariable(InteriorAdmissionFixture.ExpectedSourceEnvironment),context.Server.SourceAssemblySha256);
        int[] priors={1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207,101208,101209,101210,101211,101212};
        var proof=new List<object>();HeadlessPlayer? p=null;bool passed=false;Exception? failure=null;string step="seed";string checkpoint="unavailable";
        var options=new JsonSerializerOptions{IncludeFields=true};
        async Task<MissionProgress.SavedState> Saved(){string hex=await context.Database.ScalarAsync($"SELECT HEX(payload) FROM char_active_missions WHERE charId={p!.CharId}");checkpoint=MissionBoundCabinetFixture.DecodeCheckpointHex(hex);return JsonSerializer.Deserialize<MissionProgress.SavedState>(checkpoint,options)!;}
        async Task Barrier(){p!.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
        try
        {
            p=await context.AddPlayerAsync(new CharacterSeed("PublicStreet"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=1,X=17000,Y=495,Z=2300,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,CompletedMissions=priors,OwnedLoadedAbilities=new[]{((ushort)2,(ushort)50),((ushort)17,(ushort)50),((ushort)1006,(ushort)50),((ushort)77,(ushort)50)}});
            await p.EnterWorldAsync();step="ordinary-offer";int offer=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc("80 94 05 00 01");await p.WaitForRpcAsync("80 97 05 00");int index=MissionContactChoice.CriticalIndex(p.World.RpcSnapshot().Skip(offer).Select(r=>r.Bytes),"It's Hard To Find Good Help");p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            var saved=await Saved();Check.Equal(101213,saved.Mission,"Actual normal selected mission");Check.Equal(0,saved.Phase,"Actual first phase");
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data/navmesh"));var plan=MissionPublicStreetProof.Read(context.RepositoryRoot,saved);var car=MissionNpcInteriorRide.ForSite(saved)??throw new InvalidDataException("Current NPC car witness is missing");
            string account=await context.Database.ExecuteAsync($"SELECT exp,cash FROM characters WHERE charId={p.CharId}");string items=await context.Database.ExecuteAsync($"SELECT slot,goid,count FROM inventory WHERE charId={p.CharId} ORDER BY slot");string goals=JsonSerializer.Serialize(new{saved.Done,saved.EverDone},options);
            async Task Same(){var current=await Saved();Check.Equal(plan.Raw,MissionPublicStreetProof.Select(current).GetRawText(),"Same owned phase/site/street witness before wire");Check.Equal(goals,JsonSerializer.Serialize(new{current.Done,current.EverDone},options),"Public approach cannot complete goals");}
            async Task Move(double[] at){await Same();await MoveForMissionTest(p,at[0],at[1],at[2]);}
            async Task<NpcInteriorComputerState.Observation> Open(MissionNpcInteriorProof.Door expected)
            {
                await Same();int sections=p.World.ViewSnapshot().Count;var life=p.World.CurrentOwnViewLife();p.SendRpc($"80 c8 {Le(expected.Key)} 03 00");await Barrier();NpcInteriorComputerState.Observation? observed=null;
                await p.WaitUntilAsync(()=>{foreach(var v in p.World.CreationSnapshot().Where(v=>v.RelatedStaticObjectId==expected.Key&&v.Goid==expected.Goid).Reverse())try{var o=NpcInteriorComputerState.Capture(p.World.CurrentOwnViewLife(),v);if(!v.Attributes.TryGetValue("Position",out var pos)||pos.Length!=24||!Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(pos,i*8)==expected.Position[i])||!v.Attributes.TryGetValue("Orientation",out var q)||!q.SequenceEqual(Convert.FromHexString(expected.Quaternion)))continue;observed=o;return true;}catch(InvalidDataException){}return false;},"Current exact public/portal own-GO overlay",TimeSpan.FromSeconds(15));
                await p.WaitUntilAsync(()=>{try{return NpcInteriorComputerState.Read(p.World.CurrentOwnViewLife(),observed.Capability)==0x834;}catch(InvalidDataException){return false;}},"Actual static80c8 Open0834 on current native door",TimeSpan.FromSeconds(15));await Barrier();
                Check.Equal(0,MissionPublicStreetWire.Teleports(p.World.CurrentOwnViewLife(),life,sections).Count,"Ordinary public/portal Open never rides to mission floor");proof.Add(new{Step=step,Door=expected,Current=NpcInteriorComputerState.CurrentDoor(p.World.CurrentOwnViewLife(),expected),NoRide=true});return observed;
            }
            bool Waypoint(uint key,double[] at)=>p.World.RpcSnapshot().Where(r=>r.Bytes.Length==35&&r.Bytes[0]==0x80&&r.Bytes[1]==0x9e&&r.Bytes[2]==0).LastOrDefault() is var latest&&latest!=null&&BitConverter.ToUInt32(latest.Bytes,31)==key&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(latest.Bytes,3+i*8)==at[i]);
            Check.That(Waypoint(plan.Door.Key,plan.Door.Position),"Actual initial809e guides to public door, not interior car");
            step="declared-outside";foreach(var at in plan.Outside)await Move(at);await Same();
            Check.That(NavMesh.Locate(1,plan.Outside[^1][0],plan.Outside[^1][1],plan.Outside[^1][2],out var outside)&&!outside.indoors,"Prescribed final outside point is actually outdoor");
            step="ordinary-public-open";await Open(plan.Door);step="declared-inside-to-lobby";foreach(var at in plan.Inside)await Move(at);Check.That(NavMesh.Locate(1,plan.Lobby[0],plan.Lobby[1],plan.Lobby[2],out var lobby)&&lobby.indoors,"Actual measured lobby Standing is indoor");
            await p.WaitUntilAsync(()=>Waypoint(car.Floor.Key,car.Floor.Position),"Actual lobby809e switches to owned car",TimeSpan.FromSeconds(15));
            step="ordinary-car";await Same();int cursor=p.World.ViewSnapshot().Count;var rideLife=p.World.CurrentOwnViewLife();int packetLine=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt")).Count(c=>c=='\n');p.SendRpc($"80 c8 {Le(car.Lobby.Key)} 03 00");NpcInteriorComputerState.Teleport? arrival=null;
            await p.WaitUntilAsync(()=>{try{arrival=NpcInteriorComputerState.FreshTeleport(p.World.CurrentOwnViewLife(),rideLife,cursor,plan.Floor);return true;}catch(InvalidDataException){return false;}},"Fresh exact ordinary car arrival",TimeSpan.FromSeconds(15));await Barrier();
            MissionPublicStreetWire.Acknowledgment? receipt=null;await p.WaitUntilAsync(()=>{try{receipt=MissionPublicStreetWire.Ack(InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt")),p.CharId,packetLine,arrival!);return true;}catch(InvalidDataException){return false;}},"Actual exact client ACK of ordinary arrival",TimeSpan.FromSeconds(10));proof.Add(new{Step=step,Car=car,Arrival=arrival,Receipt=receipt,ReceiptIsNotRoomReady=true});
            step="same-owned-floor-actors";var afterRide=await Saved();
            Check.Equal(JsonSerializer.Serialize(saved.Mobs,options),JsonSerializer.Serialize(afterRide.Mobs,options),"Public approach/car does not replace accepted NPC identities or poses");
            proof.Add(new{Step=step,SavedActors=afterRide.Mobs,CurrentNpcVisibilityProven=false,Qualification="Stops at ordinary floor arrival/ACK; no hardcoded201201 actor route and no entered narrative flag used as arrival authority."});
            await Same();Check.Equal(account,await context.Database.ExecuteAsync($"SELECT exp,cash FROM characters WHERE charId={p.CharId}"),"No reward/cash during access proof");Check.Equal(items,await context.Database.ExecuteAsync($"SELECT slot,goid,count FROM inventory WHERE charId={p.CharId} ORDER BY slot"),"No items injected/earned");Check.Equal(0,p.World.Problems.Count,"Actual framing");passed=true;step="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            if(p!=null)try{await Saved();}catch(Exception error){checkpoint+="\nFINAL READ ERROR:"+error;}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-public-street-route-"+context.Database.Name),packet=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt"));await File.WriteAllTextAsync(stem+"-PacketLog.txt",packet);await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),context.Database.Name,context.Server.ProcessId,Source=context.Server.SourceAssembly,SourceSha256=context.Server.SourceAssemblySha256,Checkpoint=checkpoint,Proof=proof,PacketSha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(packet))),Rpcs=p?.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)),Views=p?.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p?.World.Problems,Qualification="Direct NAV waypoint TP aids; ordinary public Open and car RPC; no native walking/collision/render claim, no forced missionfloor/source/site/seed. No Talk/completion/award."},new JsonSerializerOptions{WriteIndented=true}));p?.Dispose();Console.WriteLine("PUBLIC STREET EVIDENCE="+stem);
        }
    }
}
