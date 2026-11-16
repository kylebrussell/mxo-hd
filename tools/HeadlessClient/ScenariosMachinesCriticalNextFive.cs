using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MachinesCriticalNextFive(ScenarioContext context)
    {
        MachinesCriticalNextFiveFixture.RequirePin(Environment.GetEnvironmentVariable(MachinesCriticalNextFiveFixture.SourcePin),context.Server.SourceAssemblySha256);
        const string handle="MachinesNextFive", filePattern="machines_1_2_*.xml"; const byte org=2;
        var ids=MachinesCriticalNextFiveFixture.Missions;var prerequisites=MachinesCriticalNextFiveFixture.Prerequisites;
        var p=await context.AddPlayerAsync(new CharacterSeed(handle){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),X=17000,Z=2300,Health=5000,MaxHealth=5000,
            InnerStrength=2000,MaxInnerStrength=2000,Organization=org,CompletedMissions=prerequisites});
        Check.That(!ids.Append(MachinesCriticalNextFiveFixture.NextMission).Intersect(prerequisites).Any(),"Selected and next missions are never prerequisite grants");
        var actors=new List<HeadlessPlayer>{p};var checkpoints=new List<object>();var targets=new List<object>();var rewards=new List<object>();
        int missionCreationCursor=0;var followerIdentities=new Dictionary<int,string>();
        MissionOutsideUploadProof.Token? outside=null;
        bool outsideAfterUpload=false;var arrivalProofs=new List<object>();var carryProofs=new List<object>();string step="entry";Exception? scenarioFailure=null;var caseClock=System.Diagnostics.Stopwatch.StartNew();
        ServerInstance? coldServer=null;object? coldProof=null;int initialPid=context.Server.ProcessId;
        string? retiredPackets=null,retiredDebug=null,retiredOutput=null;
        var fields=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
        async Task<MissionProgress.SavedState?> Saved(string label)
        {
            string hex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')");
            string? raw=hex=="NULL"?null:MissionBoundCabinetFixture.DecodeCheckpointHex(hex);
            checkpoints.Add(new{Step=step,Label=label,PayloadHex=hex,Payload=raw});
            return MachinesCriticalNextFiveFixture.ReadCheckpointHex(hex);
        }
        async Task Barrier(){int cursor=p.World.RpcSnapshot().Count;p.SendRpc("81 48");await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>BytePattern.Parse("81 49").IsMatch(r.Bytes)),"fresh same-session8149 barrier",TimeSpan.FromSeconds(10));}
        async Task<ViewCreation> MachineNpc(XElement definition,int cursor,bool introduced)
        {
            var state=(await Saved("npc-target-before"))!;int id=(int)definition.Attribute("id")!;
            var current=state.Mobs.Single(m=>m.Npc==id);
            if(outsideAfterUpload)await Reacquire(current,state,TimeSpan.FromSeconds(15));
            await MoveForMissionTest(p,current.X+100,current.Y,current.Z);await Barrier();
            state=(await Saved("npc-target-current"))!;current=state.Mobs.Single(m=>m.Npc==id);
            ViewCreation? view=null;
            await p.WaitUntilAsync(()=>(view=ZionAfter1212FollowerReadiness.CurrentNpc(p.World.CurrentOwnViewLife(),p.Handle,current,state.Instance))!=null,"Current saved NPC identity/side/latest position",TimeSpan.FromSeconds(15));
            targets.Add(new{Role="NPC",state.Mission,state.Phase,state.Identity,state.Instance,Definition=id,Current=current,view!.ViewId,view.SpawnId,
                Attributes=view.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))});
            return view!;
        }
        async Task<MissionProgress.SavedState?> WaitSaved(Func<MissionProgress.SavedState?,bool> predicate,string label,int timeoutSeconds=20)
        {
            var deadline=DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while(DateTime.UtcNow<deadline){var value=await Saved(label);if(predicate(value))return value;await Task.Delay(100);}
            throw new TimeoutException("Persisted mission condition: "+label);
        }
        string PacketPath()=>Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        async Task Ride(bool enter)
        {
            var before=(await Saved(enter?"before-entry":"before-exit"))!;
            var requestLife=p.World.CurrentOwnViewLife();var originalLife=requestLife;int viewCursor=-1,packetLine=-1,logOffset=-1;
            var street=MervFollowupStreetEntry.ForSite(before);var npc=MissionNpcInteriorRide.ForSite(before);
            var standing=enter?null:street?.Lobby.Standing??npc?.Lobby.Standing;
            var boundary=new MissionOutsideUploadProof.RequestBoundary();
            void Mark()=>boundary.Mark(()=>{requestLife=p.World.CurrentOwnViewLife();Check.That(originalLife.Current&&requestLife.Current&&ReferenceEquals(originalLife.Owner,requestLife.Owner)&&originalLife.Life==requestLife.Life,"Completed exit request retains exact owner/life");viewCursor=p.World.ViewSnapshot().Count;packetLine=InteriorAdmissionDeliveryProof.Read(PacketPath()).Count(c=>c=='\n');logOffset=context.Server.DebugLog().Length;});
            await MachinesNextRide(context,p,enter,arrivalProofs.Add,beforeRequest:!enter&&standing!=null?Mark:null);
            if(enter){outsideAfterUpload=false;outside=null;}
            var after=await Saved(enter?"after-entry-observation":"after-exit-observation");
            if(enter)Check.That(after!=null&&after.Mission==before.Mission&&after.Phase==before.Phase&&after.Identity==before.Identity&&after.Started==before.Started,"Ordinary entry keeps exact current mission/phase owner");
            if(!enter&&standing!=null)
            {
                boundary.RequireMarked();MissionOutsideUploadProof.RequirePhysicalTrip(context.Server.DebugLog()[logOffset..],standing);
                NpcInteriorComputerState.Teleport? arrival=null;
                await p.WaitUntilAsync(()=>{try{arrival=NpcInteriorComputerState.FreshTeleport(p.World.CurrentOwnViewLife(),requestLife,viewCursor,standing);return true;}catch(InvalidDataException){return false;}},"Completed interior exit actual typed lobby arrival",TimeSpan.FromSeconds(15));
                MissionMidphaseReturnFixture.Arrival? receipt=null;
                await p.WaitUntilAsync(()=>{try{receipt=MissionMidphaseReturnFixture.RequireArrival(InteriorAdmissionDeliveryProof.Read(PacketPath()),p.CharId,packetLine,arrival!);return true;}catch(InvalidDataException){return false;}},"Completed interior exit exact ACK",TimeSpan.FromSeconds(10));
                var xml=new System.Xml.XmlDocument();xml.Load(Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),"machines_1_2_*.xml").Single(f=>(int)XElement.Load(f).Attribute("id")! == before.Mission));
                var authored=MissionXml.Parse(xml.DocumentElement!);
                if(after!=null&&MachinesNoSiteUploadFixture.IsNoSiteUpload(authored,after.Phase))
                {
                    MachinesNoSiteUploadFixture.RequireTransition(before,after,authored);
                    outside=new MissionOutsideUploadProof.Token(after,p.World.CurrentOwnViewLife());
                    MissionOutsideUploadProof.RequireCurrent(outside,after,requestLife);outsideAfterUpload=false;
                }
                arrivalProofs.Add(new{Kind="Completed ordinary downride BEFORE no-site/outside phase",before.Mission,CompletedPhase=before.Phase,TypedArrival=arrival,Receipt=receipt,NextPhase=after?.Phase,MovementAid=false});
            }
            // entered means the legacy content-area trigger played, not immediate floor arrival.
            // Published route/NPC/object approaches enter naturally; no pose/flag is changed here.
        }
        async Task Upload()
        {
            var current=(await Saved("before-outside-upload"))!;
            Check.That(ids.Contains(current.Mission),"Outside Upload retains the selected Machines mission");
            if(!outsideAfterUpload)
            {
                if(outside==null)
                {
                    var street=MervFollowupStreetEntry.ForSite(current);var npc=MissionNpcInteriorRide.ForSite(current);
                    var standing=street?.Lobby.Standing??npc?.Lobby.Standing??throw new ScenarioFailure("Outside trip requires exact saved lobby Standing");
                    outside=await MissionOutsideUploadProof.BeforeOutsideAsync(p,current,
                        async()=>(await Saved("ordinary-lobby-before-outside-aid"))!,
                        mark=>MachinesNextRide(context,p,false,arrivalProofs.Add,waitForPhaseAdvance:false,beforeRequest:mark),
                        standing,PacketPath(),context.Server.DebugLog,arrivalProofs.Add);
                }
                else MissionOutsideUploadProof.RequireCurrent(outside,current,p.World.CurrentOwnViewLife());
                // Disclosed outside point movement only AFTER the ordinary downride/ACK cutpoint proof.
                await MoveForMissionTest(p,17000,495,2200);outsideAfterUpload=true;
            }
            else
            {
                // Two same-booth Uploads reuse the actual owned outside proof, not a fabricated second floor trip.
                Check.That(outside!=null,"Existing outside Upload must retain its earned arrival token");
                MissionOutsideUploadProof.RequireCurrent(outside!,current,p.World.CurrentOwnViewLife());
            }
            ViewCreation? booth=null;
            await p.WaitUntilAsync(()=>(booth=ZionCurrentUploadBooth.Find(p.World.CurrentOwnViewLife()))!=null,"exact still-live current Mara booth",TimeSpan.FromSeconds(10));
            targets.Add(new{Role="Upload booth",booth!.ViewId,booth.SpawnId,booth.RelatedStaticObjectId});
            p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(booth)} 04 00");await p.WaitForRpcAsync("81 18");
        }
        async Task Reacquire(MissionProgress.SavedMob actor,MissionProgress.SavedState before,TimeSpan budget)
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();var life=p.World.CurrentOwnViewLife();
            TimeSpan Remaining(){var remaining=budget-clock.Elapsed;if(remaining<=TimeSpan.Zero)throw new TimeoutException("Current hostile reacquire exhausted original combat budget");return remaining;}
            if(outsideAfterUpload)
            {
                var beforeUp=(await Saved("before-ordinary-return-up"))!;
                Check.That(outside!=null,"Return up requires the owned outside token");
                MissionOutsideUploadProof.RequireCurrent(outside!,beforeUp,p.World.CurrentOwnViewLife());
                var street=MervFollowupStreetEntry.ForSite(beforeUp);var npc=MissionNpcInteriorRide.ForSite(beforeUp);
                var standing=street?.Floor.Standing??npc?.Floor.Standing??throw new ScenarioFailure("Return requires exact saved floor Standing");
                await MissionOutsideUploadProof.BeforeOutsideAsync(p,beforeUp,async()=>(await Saved("after-ordinary-return-up"))!,
                    mark=>MachinesNextRide(context,p,true,arrivalProofs.Add,beforeRequest:mark),standing,PacketPath(),context.Server.DebugLog,arrivalProofs.Add,returningInside:true).WaitAsync(Remaining());
                outsideAfterUpload=false;outside=null;
            }
            var owned=(await Saved("reacquire-after-ordinary-entry"))!;ZionAfter1212RidePlan.SameEntry(before,owned);
            Check.That(life.Current&&p.World.CurrentOwnViewLife().Life==life.Life,"Ordinary hostile return retains actual current owner/life");
            var current=owned.Mobs.Single(m=>m.Npc==actor.Npc&&m.Identity==actor.Identity);
            await MachinesNextCertifiedActorRoute(context,p,owned,current,targets.Add).WaitAsync(Remaining());
            await MoveForMissionTest(p,current.X+100,current.Y,current.Z).WaitAsync(Remaining());await Barrier().WaitAsync(Remaining());
        }
        async Task Claim(byte[] window)
        {
            var offered=MachinesCriticalNextFiveFixture.LootIdentities(window,false);int cursor=p.World.RpcSnapshot().Count;
            p.SendRpc("81 1f");await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length>=14&&r.Bytes[0]==0x81&&r.Bytes[1]==0x20),"current TakeAll acknowledgement",TimeSpan.FromSeconds(10));
            byte[] accepted=p.World.RpcSnapshot().Skip(cursor).Single(r=>r.Bytes.Length>=14&&r.Bytes[0]==0x81&&r.Bytes[1]==0x20).Bytes;
            Check.That(MachinesCriticalNextFiveFixture.LootIdentities(accepted,true).Order().SequenceEqual(offered.Order()),"Exact original loot-window identities are claimed once");
            targets.Add(new{Role="LootClaim",Window=Convert.ToHexString(window),Accepted=Convert.ToHexString(accepted)});
        }
        try
        {
        await LoadAbilitiesAsync(context,p,2,17,1006,77);await p.EnterWorldAsync();
        foreach(int id in ids)
        {
            var mission=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),filePattern).Select(XElement.Load)
                .Single(m=>(int)m.Attribute("id")! == id);
            step="mission-"+id;await MoveForMissionTest(p,17000,495,2300);await Barrier();
            int phaseCreationCursor=p.World.CreationSnapshot().Count;missionCreationCursor=phaseCreationCursor;
            long beforeXp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),beforeCash=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
            long lootedCash=0;var corpseClaims=new HashSet<string>();
            var rewardSegments=new List<(HeadlessPlayer Player,int Start)>{(p,p.World.RpcSnapshot().Count)};
            int completionCursor=p.World.RpcSnapshot().Count;
            int listStart=p.World.RpcSnapshot().Count;
            p.SkipReceived();p.SendRpc($"80 94 05 00 {org:x2}");await p.WaitForRpcAsync("80 97 05 00");
            var entries=p.World.RpcSnapshot().Skip(listStart).Select(r=>r.Bytes).Where(b=>b.Length>17 && b[0]==0x80 && b[1]==0x96 && BitConverter.ToUInt16(b,2)==5).ToArray();
            // The list contains indices and titles, not definition IDs. Resolve the actual Critical entry,
            // then verify the selected definition from the accepted mission's authoritative staging record.
            int index=MissionContactChoice.CriticalIndex(entries,(string)mission.Attribute("title")!);
            p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            Check.Equal((long)id,await context.Database.LongAsync($"SELECT JSON_EXTRACT(payload,'$.Mission') FROM char_active_missions WHERE charId={p.CharId}"),"Actual accepted definition ID matches selected Critical title");
            await Saved("accepted");int phase=0;
            foreach(var area in mission.Elements("phase"))
            {
                int rpcStart=p.World.RpcSnapshot().Count,viewsStart=phaseCreationCursor,sectionsStart=p.World.ViewSnapshot().Count;
                int nextCreationCursor=viewsStart;
                var killed=new HashSet<(ushort,byte)>();
                bool Done(int n)=>p.World.RpcSnapshot().Skip(rpcStart).Any(r=>BytePattern.Parse($"80 a0 {n:x2} 06 00 01").IsMatch(r.Bytes));
                var authoredDoc=new System.Xml.XmlDocument();authoredDoc.LoadXml(mission.ToString());var authoredMission=MissionXml.Parse(authoredDoc.DocumentElement!);
                if(MachinesNoSiteUploadFixture.IsNoSiteUpload(authoredMission,phase))
                {
                    var noSite=(await Saved("authored-no-site-upload-after-real-downride"))!;
                    MachinesNoSiteUploadFixture.RequireState(noSite,authoredMission);
                    Check.That(outside!=null,"No-site Upload follows an actual completed ordinary downride/ACK");
                    MissionOutsideUploadProof.RequireCurrent(outside!,noSite,p.World.CurrentOwnViewLife());
                }
                else await Ride(true);
                if(id is 201204 or 201205)
                {
                    var followerState=(await Saved("follower-phase-carry"))!;Check.Equal(phase,followerState.Phase,"Follower belongs to current active mission phase");
                    var follower=followerState.Mobs.Single(m=>m.Npc==1);Check.That(!follower.Dead&&follower.Health>0,"Follower is alive after ordinary departure/arrival");
                    if(followerIdentities.TryGetValue(id,out string? priorFollower))Check.Equal(priorFollower,follower.Identity,"Sheldon/Stewart identity continues across sites");else followerIdentities[id]=follower.Identity;
                }
                var goals=area.Elements("objective").Where(g=>(string)g.Attribute("type")! != "Protect").ToList();
                while(goals.Any(g=>!Done((int)g.Attribute("goal")!)))
                {
                    // Complete open optional goals first so an automatic final mandatory action cannot skip them.
                    var goal=goals.Where(g=>!Done((int)g.Attribute("goal")!) && ((int?)g.Attribute("after") is not int after || Done(after)))
                        .OrderBy(g=>(int?)g.Attribute("optional")==1?0:1).FirstOrDefault();
                    Check.That(goal!=null,$"Mission{id} phase{phase+1} has an eligible remaining goal");
                    string type=(string)goal!.Attribute("type")!;int number=(int)goal.Attribute("goal")!;
                    step=$"mission{id}-phase{phase}-goal{number}-{type}";await Saved("before-goal");
                    nextCreationCursor=p.World.CreationSnapshot().Count;
                    Console.WriteLine($"  Machines{id} phase{phase+1} goal{number} {type}");
                    XElement Npc(string key)=>mission.Elements("npc").Single(n=>(string)n.Attribute("id")! == key);
                    switch(type)
                    {
                        case "Talk":case "Get":case "Give":case "Defeat":case "Loot":
                        {
                            string reference=(string)goal.Attribute("npc")!;
                            var npc=Npc(type=="Loot"?reference.Split(',')[0]:reference);
                            var view=await MachineNpc(npc,viewsStart,(int?)npc.Attribute("at")==phase+1);
                            bool hostile=view.Attributes.TryGetValue("IsEnemy",out var enemy) && enemy[0]!=0;
                            var key=(view.ViewId,view.SpawnId);
                            if(type=="Defeat" || type=="Loot" && !killed.Contains(key) &&
                                (!view.Attributes.TryGetValue("IsDead",out var dead) || dead[0]==0))
                            {p.SkipReceived();await DefeatMissionNpc(p,view);killed.Add(key);}
                            if(type=="Loot")
                            {
                                // Select THIS corpse even when an earlier DefeatAll left another corpse window open.
                                p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(view)} 05 00");var lootWindow=await p.WaitForRpcAsync("81 19");
                                var corpse=(await Saved("corpse-claim-owner"))!.Mobs.Single(m=>m.Npc==(int)npc.Attribute("id")!);
                                if(corpseClaims.Add(corpse.Identity))lootedCash+=BitConverter.ToUInt32(lootWindow.Bytes,8);
                                await Claim(lootWindow.Bytes);
                            }
                            else if(type!="Defeat" && !(type=="Talk" && hostile))
                            {
                                if(type is "Get" or "Give")
                                {
                                    string tracker=type=="Get"?"GiverActiveTracker":"TakerActiveTracker";
                                    Check.That(view.Attributes.TryGetValue(tracker,out var active) && active.Any(b=>b!=0),$"{type} is exposed by the actual NPC {tracker}");
                                }
                                string action=type=="Talk"?"09":type=="Get"?"0d":"0e";p.SendRpc($"80 c7 {MissionNpcId(view)} {action} 00");}
                            // Hostile Find/Talk is proximity, as the native client offers no Talk interaction on it.
                            break;
                        }
                        case "Search":case "Use":case "Put":
                        {
                            int objectId=(int)goal.Attribute("object")!;uint item=(uint?)goal.Attribute("item")??0;
                            var view=await MachinesNextObject(context,p,objectId,viewsStart,targets);
                            if(!MissionStaging.IsClickable(view.Goid))
                            {await p.WaitUntilAsync(()=>Done(number),"nonclickable mission object proximity",TimeSpan.FromSeconds(10));break;}
                            bool computer=MissionStaging.IsComputer(view.Goid);
                            string label=computer ? type=="Put"?"Put":type=="Use" || item==0?"Hack":"Take" : "Open";
                            byte use=ObjectStates.UseNumber(view.Goid,label) ?? throw new Exception($"No sourced {label} use for{view.Goid}");
                            if(computer)
                            {
                                var capability=NpcInteriorComputerState.Capture(p.World.CurrentOwnViewLife(),view).Capability;
                                uint CurrentState()=>NpcInteriorComputerState.Read(p.World.CurrentOwnViewLife(),capability);
                                await p.WaitUntilAsync(()=>ObjectStates.Offered(view.Goid,CurrentState()).Contains(use),
                                    $"actual enabled computer {label} use{use}",TimeSpan.FromSeconds(10));
                            }
                            int loot=p.World.RpcSnapshot().Count;
                            Console.WriteLine($"    object{objectId} goid{view.Goid} {label} use{use}");
                            p.SendRpc($"80 c7 {MissionNpcId(view)} {use:x2} 00");
                            await p.WaitUntilAsync(()=>Done(number) || p.World.RpcSnapshot().Skip(loot).Any(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x81&&r.Bytes[1]==0x19),"object action or container window",TimeSpan.FromSeconds(10));
                            if(!Done(number))
                            {
                                if(type=="Put")
                                {
                                    long slot=await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={p.CharId} AND goid={item} LIMIT 1");
                                    Check.That(slot>0 && slot<97,"Put uses an actual owned mission item bag slot");
                                    p.SendRpc($"81 21 {slot:x2}");
                                }
                                else await Claim(p.World.RpcSnapshot().Skip(loot).Last(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x81&&r.Bytes[1]==0x19).Bytes);
                            }
                            break;
                        }
                        case "Escort":
                        {
                            foreach(string key in ((string)goal.Attribute("npc")!).Split(','))
                            {
                                var npc=Npc(key);
                                if((int?)npc.Attribute("at")==phase+1)
                                {await MachineNpc(npc,viewsStart,true);await Task.Delay(800);}
                            }
                            string to=(string?)goal.Attribute("to")??"exit";
                            if(to=="exit")
                            {
                                var exitState=(await Saved("escort-current-owner"))!;var xml=new System.Xml.XmlDocument();xml.LoadXml(mission.ToString());
                                int intent=p.World.RpcSnapshot().Count;
                                await MachinesNextEscortExit(context,p,MissionXml.Parse(xml.DocumentElement!),number,exitState,intent,intent,targets.Add);
                            }
                            else if(to=="hardline")await Upload();
                            else if(to.StartsWith("npc:"))await MachineNpc(Npc(to[4..]),viewsStart,true);
                            else if(to.StartsWith("object:"))await MachinesNextObject(context,p,int.Parse(to[7..]),viewsStart,targets);
                            else throw new Exception("Unsupported escort destination "+to);
                            break;
                        }
                        case "Upload":await Upload();break;
                        case "DefeatAll":
                        {
                            var names=new HashSet<string>(new[]{"Machine Program","Zion Soldier","Exile Soldier"});
                            foreach(var npc in mission.Elements("npc"))names.Add((string)npc.Attribute("handle")!);
                            var authoredCurrentStageIds=mission.Elements("npc").Where(n=>(int?)n.Attribute("at")==phase+1).Select(n=>checked((ushort)(int)n.Attribute("id")!)).ToHashSet();
                            List<ViewCreation> Targets()
                            {
                                var owned=Saved("current-hostile-stage").GetAwaiter().GetResult()!;Check.That(owned.Mission==id&&owned.Phase==phase,"Exact current hostile mission/stage");
                                return owned.Mobs.Where(m=>MachinesCriticalNextFiveFixture.CurrentHostile(m,authoredCurrentStageIds))
                                    .Select(m=>ZionAfter1212FollowerReadiness.CurrentNpc(p.World.CurrentOwnViewLife(),p.Handle,m,owned.Instance)).Where(v=>v!=null&&!killed.Contains((v.ViewId,v.SpawnId))).Select(v=>v!).ToList();
                            }
                            for(int attempts=0;!Done(number) && attempts<100;attempts++)
                            {
                                var window=System.Diagnostics.Stopwatch.StartNew();
                                if(!Done(number)&&Targets().Count==0)
                                {
                                    var owned=(await Saved("missing-visible-current-hostile"))!;
                                    Check.That(owned.Mission==id&&owned.Phase==phase,"Reacquire only the current authored hostile stage");
                                    var absent=owned.Mobs.LastOrDefault(m=>MachinesCriticalNextFiveFixture.CurrentHostile(m,authoredCurrentStageIds));
                                    if(absent!=null)await Reacquire(absent,owned,TimeSpan.FromSeconds(Math.Max(.001,30-window.Elapsed.TotalSeconds)));
                                }
                                await p.WaitUntilAsync(()=>Done(number)||Targets().Count>0,"next current-phase hostile or wave completion",TimeSpan.FromSeconds(Math.Max(.001,30-window.Elapsed.TotalSeconds)));
                                if(Done(number))break;
                                var currentState=(await Saved("wave-current-owner"))!;
                                Check.Equal(phase,currentState.Phase,"DefeatAll remains in its authored current phase");
                                var target=Targets().LastOrDefault();
                                Check.That(target!=null,"Wave target is an actual live current saved actor, not a historical same-name view");
                                p.SkipReceived();await DefeatMissionNpc(p,target!);killed.Add((target!.ViewId,target.SpawnId));
                            }
                            break;
                        }
                        default:throw new Exception("Unsupported Prima goal "+type);
                    }
                    await p.WaitUntilAsync(()=>Done(number),$"Machines{id} phase{phase+1} actual goal{number} done",TimeSpan.FromSeconds(30));
                    var afterGoal=await Saved("after-goal");
                    if(afterGoal!=null&&afterGoal.Phase==phase)
                    {
                        if(id==201202&&phase==2&&number==2)
                        {
                            Check.That(afterGoal.Mobs.Any(m=>m.Npc==1&&!m.Friendly)&&afterGoal.Mobs.Any(m=>m.Npc==2&&!m.Friendly),"Both named Security arrivals follow the second final-building poster");
                            Check.Equal(4,afterGoal.Mobs.Count(m=>m.Npc>=1000&&m.Hostile),"Actual four generated alarm hostiles");
                        }
                        if(id==201205&&phase==1&&number==3)
                            Check.Equal(3,afterGoal.Mobs.Count(m=>m.Wave==1),"All three repair reinforcements are actually staged");
                        if(id is 201204 or 201205)
                            {
                                var follower=afterGoal.Mobs.Single(m=>m.Npc==1);Check.That(!follower.Dead&&follower.Health>0,"Current protected follower stays alive under disclosed weaken assistance; no unaided survival claim");
                                if(followerIdentities.TryGetValue(id,out string? identity))Check.Equal(identity,follower.Identity,"Same protected actor identity across phase carry");else followerIdentities[id]=follower.Identity;
                            }
                    }
                    if(id==201203&&phase==0&&number==2)
                    {
                        var carried=(await Saved("carry-before"))!;Check.That(carried.Items.ContainsKey(6796)&&!carried.Done.Contains(3),"Earned Flyer carried while Folder Search is pending");
                        ushort slot=carried.Items[6796];
                        string itemRows=await context.Database.ExecuteAsync($"SELECT charId,goid,slot,`count` FROM inventory WHERE charId={p.CharId} AND goid=6796 ORDER BY slot");
                        var itemBefore=MissionBoundCabinetFixture.RequireMissionInventory(itemRows,p.CharId,6796,slot);
                        string history=System.Text.Json.JsonSerializer.Serialize(new{carried.Objects,carried.SiteDoors,carried.OpenFloorCurrentSiteIndex,carried.OpenFloorSites},fields);
                        long carryXp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),carryCash=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
                        long carryReceipt=await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId={id}");Check.Equal(0L,carryReceipt,"No selected receipt before carry logout");
                        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
                        p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");p.Dispose();await Task.Delay(TimeSpan.FromSeconds(33));
                        p=await HeadlessPlayer.LoginAsync(context.Server.Address,username,handle);actors.Add(p);rewardSegments.Add((p,0));
                        int restoreViews=p.World.CreationSnapshot().Count,restorePackets=InteriorAdmissionDeliveryProof.Read(PacketPath()).Count(c=>c=='\n');
                        var restoreRoot=new[]{double.Parse(await context.Database.ScalarAsync($"SELECT x FROM characters WHERE charId={p.CharId}"),CultureInfo.InvariantCulture),
                            double.Parse(await context.Database.ScalarAsync($"SELECT y FROM characters WHERE charId={p.CharId}"),CultureInfo.InvariantCulture),
                            double.Parse(await context.Database.ScalarAsync($"SELECT z FROM characters WHERE charId={p.CharId}"),CultureInfo.InvariantCulture)};
                        await p.EnterWorldAsync();await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");
                        var own=p.World.CreationSnapshot().Last(v=>v.ViewId==2&&v.Goid==12);Check.That(own.Attributes.TryGetValue("Position",out var ownPosition)&&ownPosition.Length==24,"Actual carry self position");
                        restoreRoot=Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(own.Attributes["Position"],i*8)).ToArray();
                        var freeCarry=carried.Objects.Where(o=>o.OpenFloor!=null&&o.OpenFloor.SourceGoid!=o.Goid).ToArray();
                        if(freeCarry.Length>0)
                        {
                            await p.WaitUntilAsync(()=>freeCarry.All(o=>ZionCurrentFixedBody.Find(p.World.CurrentOwnViewLife(),o,carried.Instance)!=null),"Stationary carry restore first free03",TimeSpan.FromSeconds(15));
                            string raw=InteriorAdmissionDeliveryProof.Read(PacketPath());
                            foreach(var obj in freeCarry)arrivalProofs.Add(new{Role="CarryRestore",carried.Mission,carried.Phase,carried.Identity,obj.Id,
                                Receipt=InteriorAdmissionDeliveryProof.Require(raw,p.CharId,restorePackets,restoreRoot,obj.Goid,obj.OpenFloor!.Centre),Stationary=true,ReceiptIsNotRoomReady=true});
                        }
                        var restored=(await Saved("carry-restored"))!;
                        Check.Equal(carried.Identity,restored.Identity,"Same earned active mission identity");Check.Equal(carried.Started,restored.Started,"Original start time");
                        Check.Equal(carried.Phase,restored.Phase,"Same active phase");Check.Equal(slot,restored.Items[6796],"Exact earned Flyer slot");
                        Check.That(carried.Done.SequenceEqual(restored.Done)&&carried.EverDone.SequenceEqual(restored.EverDone),"Completed/pending goal history survives ordinary relog");
                        Check.Equal(history,System.Text.Json.JsonSerializer.Serialize(new{restored.Objects,restored.SiteDoors,restored.OpenFloorCurrentSiteIndex,restored.OpenFloorSites},fields),"Exact object raw spots/witnesses, site doors/current index/full history survive");
                        var itemAfter=MissionBoundCabinetFixture.RequireMissionInventory(await context.Database.ExecuteAsync($"SELECT charId,goid,slot,`count` FROM inventory WHERE charId={p.CharId} AND goid=6796 ORDER BY slot"),p.CharId,6796,slot);
                        Check.Equal(itemBefore,itemAfter,"Relog preserves exact nonstackable Flyer inventory owner/goid/slot/raw-count");
                        Check.Equal(carryXp,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Relog grants no XP");
                        Check.Equal(carryCash,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Relog grants no information");
                        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId={id}"),"Relog grants no completion");
                        carryProofs.Add(new{CarryXp=carryXp,CarryCash=carryCash,carried.Identity,carried.Started,carried.Phase,Item=6796,Slot=slot,InventoryBefore=itemBefore,InventoryAfter=itemAfter,Before=carried,After=restored});
                        rpcStart=0;viewsStart=0;sectionsStart=0;nextCreationCursor=0;missionCreationCursor=0;completionCursor=0;
                    }
                }
                var endState=await WaitSaved(s=>s==null||s.Phase!=phase||(s.Flags.TryGetValue("waitingForExit",out bool waiting)&&waiting),"phase goals durably complete");
                if(endState?.Phase==phase&&!endState.Flags.GetValueOrDefault("leaveNotNeeded")){nextCreationCursor=p.World.CreationSnapshot().Count;await Ride(false);}
                phaseCreationCursor=nextCreationCursor;phase++;
                // Exact cold preparation at the retained actual exit takes28.58s. QA synchronization only:
                // no authored, resolver/search, transport or RPC timer is changed by this bounded wait.
                if(phase<mission.Elements("phase").Count())await WaitSaved(s=>s!=null&&s.Phase==phase,"next phase persisted after normal departure",MachinesCriticalNextFiveFixture.PhaseTransitionSeconds);
                else await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(completionCursor).Any(r=>BytePattern.Parse("80 a2 00").IsMatch(r.Bytes)),"mission completed over wire",TimeSpan.FromSeconds(15));
            }
            await Barrier();
            var wire=rewardSegments.SelectMany(s=>s.Player.World.RpcSnapshot().Skip(s.Start)).Select(r=>r.Bytes).ToArray();
            var end=wire.Single(b=>b.Length>=19&&b[0]==0x80&&b[1]==0xa2&&b[2]==0);
            var expected=MachinesCriticalNextFiveFixture.ExpectedReward(id);
            Check.Equal(expected.Info,BitConverter.ToUInt32(end,11),"Independent current-policy mission information");
            Check.Equal(expected.Xp,BitConverter.ToUInt32(end,15),"Independent current-policy mission experience");
            var missionGains=wire.Where(b=>b.Length==10&&b[0]==0x80&&b[1]==0xe6&&b[6]==2).ToArray();
            Check.Equal(1,missionGains.Length,"One mission XP gain event");Check.Equal(expected.Xp,BitConverter.ToUInt32(missionGains[0],2),"Mission gain independently matches recorded amount");
            long allXp=wire.Where(b=>b.Length==10&&b[0]==0x80&&b[1]==0xe6).Sum(b=>(long)BitConverter.ToUInt32(b,2));
            long afterXp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),afterCash=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
            Check.Equal(beforeXp+allXp,afterXp,"Persistent XP equals actual kill and mission gain events exactly once");
            Check.Equal(beforeCash+lootedCash+expected.Info,afterCash,"Persistent information equals selected corpse windows plus one mission reward");
            Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')"),"Completed mission has no active payload");
            rewards.Add(new{Mission=id,Expected=expected,Summary=Convert.ToHexString(end),BeforeXp=beforeXp,AfterXp=afterXp,BeforeCash=beforeCash,AfterCash=afterCash,LootedCash=lootedCash,AllWireXp=allXp});
            Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={id}"),"Selected chain completion earned exactly once");
            Check.Equal(0,p.World.Problems.Count,"Prima chain protocol problems");
        }
        step="next-offer";await MoveForMissionTest(p,17000,495,2300);int nextOfferCursor=p.World.RpcSnapshot().Count;p.SkipReceived();
        p.SendRpc("80 94 05 00 02");await p.WaitForRpcAsync("80 97 05 00");
        var nextEntries=p.World.RpcSnapshot().Skip(nextOfferCursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
        MissionContactChoice.CriticalIndex(nextEntries,"Coroner's Report");
        foreach(var actor in actors)Check.Equal(0,actor.World.Problems.Count,"Every owned session parser clean");
        step="cold-process-receipt-check";string walletBefore=await context.Database.ScalarAsync($"SELECT CONCAT(exp,':',cash) FROM characters WHERE charId={p.CharId}");
        string coldUsername=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
        p.SkipReceived();p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");p.Dispose();
        retiredPackets=InteriorAdmissionDeliveryProof.Read(PacketPath());retiredDebug=context.Server.DebugLog();retiredOutput=context.Server.FullOutput();
        context.Server.Dispose();bool exited;try{using var process=System.Diagnostics.Process.GetProcessById(initialPid);exited=process.HasExited;}catch(ArgumentException){exited=true;}
        Check.That(exited,"Owned old server exits before cold replacement");
        coldServer=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,MachinesCriticalNextFiveFixture.Environment);
        Check.Equal(context.Server.SourceAssemblySha256,coldServer.SourceAssemblySha256,"Cold replacement retains exact qualified source");
        p=await HeadlessPlayer.LoginAsync(coldServer.Address,coldUsername,handle);actors.Add(p);await p.EnterWorldAsync();await Barrier();
        string walletAfter=await context.Database.ScalarAsync($"SELECT CONCAT(exp,':',cash) FROM characters WHERE charId={p.CharId}");Check.Equal(walletBefore,walletAfter,"Cold login grants no XP or information");
        Check.Equal((long)ids.Length,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)}) AND completions=1"),"Cold process preserves all five earned receipts once");
        Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')"),"Cold process cannot revive completed progress");
        Check.That(!p.World.RpcSnapshot().Any(r=>r.Bytes.Length>1&&r.Bytes[0]==0x80&&r.Bytes[1]==0xa2),"Cold process emits no completion replay");Check.Equal(0,p.World.Problems.Count,"Cold parser clean");
        coldProof=new{DisposedPid=initialPid,ExitedBeforeNext=exited,ReplacementPid=coldServer.ProcessId,Source=coldServer.SourceAssemblySha256,WalletBefore=walletBefore,WalletAfter=walletAfter,NoReplay=true};step="complete";

        Check.Equal((long)ids.Length,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)})"),"Every selected completion earned, not granted");
        }
        catch(Exception error) {scenarioFailure=error;throw;}
        finally
        {
            try
            {
                string dll=context.Server.SourceAssembly,stem=Path.Combine(Path.GetTempPath(),"mxo-machines-critical-next-five-"+context.Database.Name);
                string? receipts=null,receiptError=null,currentPayloadHex=null,currentPayload=null,currentPayloadError=null;
                try{currentPayloadHex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')");currentPayload=currentPayloadHex=="NULL"?null:MissionBoundCabinetFixture.DecodeCheckpointHex(currentPayloadHex);}
                catch(Exception error){currentPayloadError=error.ToString();}
                try{receipts=await context.Database.ExecuteAsync($"SELECT missionId,completions FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)}) ORDER BY missionId");}
                catch(Exception error){receiptError=error.ToString();}
                string rawPackets=retiredPackets??InteriorAdmissionDeliveryProof.Read(PacketPath());File.WriteAllText(stem+"-PacketLog.txt",rawPackets);
                await File.WriteAllTextAsync(stem+".json",System.Text.Json.JsonSerializer.Serialize(new{
                    Passed=step=="complete"&&scenarioFailure==null,Step=step,Failure=scenarioFailure?.ToString(),Database=context.Database.Name,ElapsedSeconds=caseClock.Elapsed.TotalSeconds,
                    CurrentPayloadHex=currentPayloadHex,CurrentPayload=currentPayload,CurrentPayloadCaptureError=currentPayloadError,
                    ServerPid=initialPid,ColdRestart=coldProof,SourceAssembly=dll,SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dll))),
                    SelectedMissions=ids,Prerequisites=prerequisites,Checkpoints=checkpoints,Targets=targets,Rewards=rewards,Carry=carryProofs,Arrivals=arrivalProofs,RawPacketLogPath=stem+"-PacketLog.txt",RawPacketLogSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawPackets))),
                    Receipts=receipts,ReceiptCaptureError=receiptError,
                    ColdServer=coldServer==null?null:new{Pid=coldServer.ProcessId,Source=coldServer.SourceAssemblySha256,Output=coldServer.FullOutput(),Debug=coldServer.DebugLog(),PacketLog=InteriorAdmissionDeliveryProof.Read(Path.Combine(coldServer.WorkingDirectory,"PacketLog.txt"))},
                    Sessions=actors.Select(a=>new{a.CharId,Rpc=a.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=a.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=a.World.Problems.ToArray()}),
                    NativeWalkingProven=false,NativePickingProven=false,MovementPolicy="Existing test-only movement and weaken1 helpers, aggro disabled. Every selected item/goal/receipt/reward earned over normal RPCs; no source override or replay grants."
                },new System.Text.Json.JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
                File.WriteAllText(stem+".log",(retiredOutput??context.Server.FullOutput())+"\n--- Debug ---\n"+(retiredDebug??context.Server.DebugLog()));
                Console.WriteLine("MACHINES NEXT FIVE EVIDENCE="+stem);
            }
            catch(Exception proofError){if(scenarioFailure==null)throw;Console.Error.WriteLine("Evidence capture failed: "+proofError);}
            finally{foreach(var actor in actors)actor.Dispose();coldServer?.Dispose();}
        }
    }

    private static Task<ViewCreation> MachinesNextObject(ScenarioContext context,HeadlessPlayer p,int id,int creationCursor,List<object> evidence)
        =>MachinesNextObjectQualified(context,p,id,creationCursor,evidence.Add);
}
