using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MachinesDisturbingTrends(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(DisturbingTrendsFixture.BeforeMargin(new CharacterSeed("DisturbingTrends"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=2,
            X=17000,Y=495,Z=2300,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,CompletedMissions=DisturbingTrendsFixture.Prerequisites}));
        uint id=p.CharId;string handle=p.Handle,step="world-entry";Exception? primary=null;var checkpoints=new List<object>();var sessions=new List<object>();var npcs=new List<object>();
        var options=new JsonSerializerOptions{IncludeFields=true};
        async Task<MissionProgress.SavedState> Saved(){var s=await MissionCheckpoint(context,p);checkpoints.Add(new{Step=step,State=s});return s;}
        void Record(){sessions.Add(new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()});Check.Equal(0,p.World.Problems.Count,"Every owned session parser clean");}
        async Task Ride(bool enter,MissionProgress.SavedState? original=null)
        {
            var state=original??await Saved();var door=state.Waypoints.Single();var lobby=state.Exits.Single();
            uint key=enter?DisturbingTrendsFixture.DoorAt(new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),lobby):BitConverter.ToUInt32(Convert.FromHexString(door.doorHex),0);
            var pose=enter?lobby:new[]{door.x,door.y,door.z};await MoveForMissionTest(p,pose[0],pose[1]-50,pose[2]);
            var free=enter?state.Objects.Where(o=>o.OpenFloor!=null).ToArray():Array.Empty<MissionProgress.SavedObject>();
            bool Body(MissionProgress.SavedObject o)=>p.World.CreationSnapshot().Any(v=>v.Goid==o.Goid&&v.Attributes.TryGetValue("Position",out var position)&&position.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(position,i*8)==o.OpenFloor!.Centre[i]));
            var pending=free.Where(o=>!Body(o)).ToArray();
            int packetCursor=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt")).Count(ch=>ch==10);
            int cursor=p.World.RpcSnapshot().Count,log=context.Server.DebugLog().Length;p.SkipReceived();
            string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
            if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"ordinary static elevator door ride",TimeSpan.FromSeconds(15));
            string rideLog=context.Server.DebugLog()[log..];
            checkpoints.Add(new{Step=step,Enter=enter,DoorKey=key,Request=request,RpcCursor=cursor,RideLog=rideLog});
            if(enter&&pending.Length>0)
            {
                var arrival=Regex.Matches(rideLog,@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
                bool rode=arrival!=null;
                double[] root=pending[0].OpenFloor!.Route.Take(3).ToArray();
                if(rode)
                {
                    var actual=Enumerable.Range(1,3).Select(i=>double.Parse(arrival!.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                    Check.That(actual.SequenceEqual(root),"Ordinary ride reached the published open-floor root");
                }
                else
                {
                    // Same-floor current interest only; this does not prove a ride receipt or renderer readiness.
                    await MoveForMissionTest(p,root[0],root[1],root[2]);
                    p.World.SendViewList(DisturbingTrendsFixture.ClientPosition(root));
                }
                // No NPC/route movement may substitute for the stationary ride's exact receipt here.
                await p.WaitUntilAsync(()=>pending.All(Body),"First free Computer creation after actual floor arrival",TimeSpan.FromSeconds(15));
                string packets=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt"));
                foreach(var o in pending)
                {
                    object? receipt=rode?InteriorAdmissionDeliveryProof.Require(packets,p.CharId,packetCursor,root,o.Goid,o.OpenFloor!.Centre):null;
                    checkpoints.Add(new{Step=step,Operation="first-free-body",o.Id,RawSource=o.Spot,FixedPlacement=o.OpenFloor,Stationary=rode,TransportReceipt=receipt,ClientPositionInterestOnly=!rode,RendererReadyProven=false});
                }
            }
            else if(enter&&free.Length>0)checkpoints.Add(new{Step=step,Operation="already-current-free-body",Count=free.Length,RendererReadyProven=false});
        }
        async Task<ViewCreation> Ophiuchi(int number)
        {
            var state=await Saved();var mob=state.Mobs.Single(m=>m.Npc==number&&m.Phase==state.Phase&&!m.Dead);
            await MoveForMissionTest(p,mob.X+100,mob.Y,mob.Z);
            ViewCreation? Current()=>DisturbingTrendsFixture.CurrentNpc(p.World.CreationSnapshot(),0,mob,state.Phase,state.Instance);
            await p.WaitUntilAsync(()=>Current()!=null,"Exact current Ophiuchi pose/side/phase",TimeSpan.FromSeconds(15));
            var v=Current()!;npcs.Add(new{Definition=number,Step=step,Source=mob,v.ViewId,v.SpawnId,v.Name,Attributes=v.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))});return v;
        }
        try
        {
            await p.EnterWorldAsync();step="normal-offer";int cursor=p.World.RpcSnapshot().Count;p.SendRpc("80 94 05 00 02");await p.WaitForRpcAsync("80 97 05 00");
            var entries=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
            int selected=MissionContactChoice.CriticalIndex(entries,"Disturbing Trends");p.SendRpc($"80 98 06 00 {Le((ushort)selected)} 01 02 00");await p.WaitForRpcAsync("80 99");p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            Check.Equal(201201,(await Saved()).Mission,"Normal accepted definition, never granted");
            step="phase1-talk";await Ride(true);var first=await Ophiuchi(1);p.SendRpc($"80 c7 {MissionNpcId(first)} 09 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");
            step="phase2-admission";int creationStart=p.World.CreationSnapshot().Count;
            await Ride(false);await p.WaitForRpcAsync("80 9e 00",TimeSpan.FromSeconds(30));
            var before=await Saved();Check.Equal(1,before.Phase,"Operations post phase");
            var source=before.Objects.Single(o=>o.Id==1);Check.Equal((ushort)3,source.Goid,"Actual normal GO3");
            Check.That(source.OpenFloor!=null,"Current master must admit an open-floor Computer before the phase advances");
            var witness=MissionOpenFloorWitness.Read(JsonSerializer.SerializeToElement(source.OpenFloor,options));
            witness.Require(201201,1,1,source.Spot.mxoStaticId,NumericalUtils.ByteArrayToUint16(source.Spot.type,1),3);
            string fixedSource=JsonSerializer.Serialize(source,options);
            step="phase2-ride";
            int rideCursor=context.Server.DebugLog().Length;await Ride(true,before);
            var ride=Regex.Matches(context.Server.DebugLog()[rideCursor..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
            Check.That(Enumerable.Range(1,3).Select(n=>double.Parse(ride.Groups[n].Value,CultureInfo.InvariantCulture)).SequenceEqual(witness.Route.Take(3)),"Actual ride starts at published route root");
            step="phase2-computer";
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            Check.That(NavMesh.Locate(1,witness.Standing[0],witness.Standing[1],witness.Standing[2],out var standingNav),"Published standing point is on the actual NAV floor");
            Check.Equal(standingNav.floorY+95,witness.Standing[1],"Actual standing height");
            Check.That(NavMesh.Locate(1,witness.Route[0],witness.Route[1],witness.Route[2],out var rootNav)&&rootNav.indoors,"Ride root is inside actual NAV");
            foreach(var mob in before.Mobs.Where(m=>m.Phase==1&&!m.Dead))
            {
                Check.That(NavMesh.Locate(1,mob.X,mob.Y,mob.Z,out var nav),"Admitted NPC actual NAV: "+mob.Name);
                Check.Equal(nav.floorY+95,mob.Y,"NPC actual standing height");
                Check.That(nav.indoors&&nav.sector==rootNav.sector&&nav.region==rootNav.region,"NPC shares actual root interior: "+mob.Name);
                await MoveForMissionTest(p,mob.X+100,mob.Y,mob.Z);
                ViewCreation? Current()=>DisturbingTrendsFixture.CurrentNpc(p.World.CreationSnapshot(),creationStart,mob,before.Phase,before.Instance);
                await p.WaitUntilAsync(()=>Current()!=null,"Actual admitted NPC creation: "+mob.Name,TimeSpan.FromSeconds(15));
                var view=Current()!;
                npcs.Add(new{Step=step,Source=mob,Nav=nav,view.ViewId,view.SpawnId,Attributes=view.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))});
            }
            // Headless movement assistance follows the persisted route; native walking remains Claude's gate.
            for(int i=0;i<witness.Route.Count;i+=3)await MoveForMissionTest(p,witness.Route[i],witness.Route[i+1],witness.Route[i+2]);
            bool AtPlan(ViewCreation v)=>v.Goid==3&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(pos,i*8)==witness.Centre[i]);
            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(creationStart).Any(AtPlan),"Actual free Computer at the persisted centre",TimeSpan.FromSeconds(10));
            var computer=p.World.CreationSnapshot().Skip(creationStart).Last(AtPlan);
            Check.Equal(witness.Quaternion,Convert.ToHexString(computer.Attributes["Orientation"]),"Actual creation quaternion matches persisted plan");
            byte take=ObjectStates.UseNumber(computer.Goid,"Take")??throw new ScenarioFailure("No native Take definition on actual Computer");
            await p.WaitUntilAsync(()=>{var updates=ViewUpdateValues(p,computer.ViewId,"01 08");uint state=updates.Count>0?updates[^1]:BitConverter.ToUInt32(computer.Attributes["CurrentState"]);return ObjectStates.Offered(computer.Goid,state).Contains(take);},"Actual Computer state offers Take",TimeSpan.FromSeconds(10));
            Check.Equal(fixedSource,JsonSerializer.Serialize((await Saved()).Objects.Single(o=>o.Id==1),options),"Raw source and fixed DTO remain unchanged through ride and route movement");
            p.SendRpc($"80 c7 {MissionNpcId(computer)} {take:x2} 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");step="earned-disk";var taken=await Saved();
            Check.That(taken.Items.ContainsKey(6783),"Normal Take earned tracked Map Disk6783");ushort slot=taken.Items[6783];
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid=6783"),"Exactly one earned disk");
            // Near-door auto-exit is a separately tracked runtime issue. Stabilize at the delivery
            // phase through the ordinary exit before logout; never pretend its fresh Give goal is
            // the prior phase's completed Take goal, or claim active phase2 proxy replay here.
            if(taken.Phase==1)
            {
                Check.That(taken.Done.Contains(1),"Take completed its current goal before ordinary exit");
                await Ride(false,taken);await p.WaitForRpcAsync("80 9e 00",TimeSpan.FromSeconds(30));
            }
            else Check.Equal(2,taken.Phase,"Only the normal next delivery phase may already have advanced");
            step="carry-before-relog";var carried=await Saved();Check.Equal(2,carried.Phase,"Stable delivery phase before carry logout");
            Check.That(carried.Done.Length==0,"Delivery Give is still pending before carry logout");Check.Equal(slot,carried.Items[6783],"Normal exit retains earned disk slot");
            string savedSites=JsonSerializer.Serialize(carried.OpenFloorSites,options);
            p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");Record();p.Dispose();Console.WriteLine("  DISTURBING normal carry logout; waiting33s reservation cleanup");await Task.Delay(TimeSpan.FromSeconds(33));
            step="carry-relog";p=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+id,handle);await p.EnterWorldAsync();await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");var restored=await Saved();
            Check.Equal(carried.Identity,restored.Identity,"Same active mission identity");Check.Equal(carried.Started,restored.Started,"Original acceptance time survives carry relog");
            Check.Equal(2,restored.Phase,"Same pending delivery phase survives relog");Check.That(restored.Done.Length==0,"Delivery Give remains pending after relog");
            Check.Equal(slot,restored.Items[6783],"Exact earned disk slot survives relog");Check.That(carried.EverDone.SequenceEqual(restored.EverDone),"Earned goal history survives relog");
            Check.Equal(savedSites,JsonSerializer.Serialize(restored.OpenFloorSites,options),"Original phase sites and source descriptors survive carry relog unchanged");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid=6783"),"Relog does not duplicate disk");
            step="phase3-give";await Ride(true);var delivery=await Saved();Check.Equal(2,delivery.Phase,"New phase3 Ophiuchi");var npc=delivery.Mobs.Single(m=>m.Npc==6);
            var last=await Ophiuchi(6);Check.That(last.Attributes.TryGetValue("TakerActiveTracker",out var active)&&active.Any(b=>b!=0),"Actual phase3 NPC exposes Give");
            Check.That(last.Attributes.TryGetValue("Position",out var at)&&Math.Abs(BitConverter.ToDouble(at)-npc.X)<1&&Math.Abs(BitConverter.ToDouble(at,8)-npc.Y)<1&&Math.Abs(BitConverter.ToDouble(at,16)-npc.Z)<1,"Resolved current NPC6 placement, not old Ophiuchi1");
            p.SendRpc($"80 c7 {MissionNpcId(last)} 0e 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid=6783"),"Normal Give consumes tracked disk");
            step="normal-finish";if(!p.Received("80 a2 00").Any()){await Ride(false);await p.WaitForRpcAsync("80 a2 00",TimeSpan.FromSeconds(20));}
            var summary=p.Received("80 a2 00").Single().Bytes;Check.Equal(DisturbingTrendsFixture.ExpectedInfo,BitConverter.ToUInt32(summary,11),"Current policy information");Check.Equal(DisturbingTrendsFixture.ExpectedXp,BitConverter.ToUInt32(summary,15),"Current policy XP");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={id} AND missionId=201201"),"One earned completion");Check.Equal((long)PlayerProgression.ExperienceForLevel(50)+DisturbingTrendsFixture.ExpectedXp,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={id}"),"Current default50 quote paid once");Check.Equal(134350L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),"Current information paid once");
            Check.Equal(0,p.World.Problems.Count,"Final session parser clean");step="complete";
        }
        catch(Exception e){primary=e;try{await Saved();}catch(Exception snapshotError){Console.Error.WriteLine("Failure checkpoint unavailable: "+snapshotError.Message);}throw;}
        finally
        {
            Exception? proofFailure=null;try{Record();}catch(Exception e){proofFailure=e;}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-disturbing-trends-"+context.Database.Name);
            try
            {
                string dll=context.Server.SourceAssembly;
                string? packetPath=null,packetSha=null,packetFailure=null;
                try
                {
                    string raw=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt"));
                    packetPath=stem+"-PacketLog.txt";File.WriteAllText(packetPath,raw);
                    packetSha=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
                }
                catch(Exception error){packetFailure=error.ToString();proofFailure??=error;}

                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Database=context.Database.Name,Step=step,Failure=primary?.ToString()??proofFailure?.ToString(),Checkpoints=checkpoints,Npcs=npcs,Sessions=sessions,ServerPid=context.Server.ProcessId,context.Server.WorkingDirectory,PacketLogPath=packetPath,PacketLogSha256=packetSha,PacketLogFailure=packetFailure,SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),NativePickingProven=false,NativeWalkingProven=false,CarryRelogPhase=2,ActivePhaseTwoProxyRelogProven=false,MovementPolicy="Test-only movement follows the persisted open-floor route; it is not native walking proof"},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());Console.WriteLine("  DISTURBING evidence: "+stem+" step="+step);
            }
            catch(Exception e){proofFailure??=e;Console.Error.WriteLine("Proof failure: "+e);}
            finally{p.Dispose();}
            if(primary==null&&proofFailure!=null)throw new ScenarioFailure("Required Disturbing Trends evidence failed: "+proofFailure);
        }
    }
}
