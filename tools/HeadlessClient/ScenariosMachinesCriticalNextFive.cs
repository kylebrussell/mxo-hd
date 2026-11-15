using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MachinesCriticalNextFive(ScenarioContext context)
    {
        const string handle="MachinesNextFive", filePattern="machines_1_2_*.xml"; const byte org=2;
        var ids=MachinesCriticalNextFiveFixture.Missions;var prerequisites=MachinesCriticalNextFiveFixture.Prerequisites;
        var p=await context.AddPlayerAsync(new CharacterSeed(handle){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),X=17000,Z=2300,Health=5000,MaxHealth=5000,
            InnerStrength=2000,MaxInnerStrength=2000,Organization=org,CompletedMissions=prerequisites});
        Check.That(!ids.Append(MachinesCriticalNextFiveFixture.NextMission).Intersect(prerequisites).Any(),"Selected and next missions are never prerequisite grants");
        var actors=new List<HeadlessPlayer>{p};var checkpoints=new List<object>();var targets=new List<object>();var rewards=new List<object>();
        int missionCreationCursor=0;var followerIdentities=new Dictionary<int,string>();
        var arrivalProofs=new List<object>();var carryProofs=new List<object>();string step="entry";Exception? scenarioFailure=null;
        var fields=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
        async Task<MissionProgress.SavedState?> Saved(string label)
        {
            string raw=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={p.CharId}");
            checkpoints.Add(new{Step=step,Label=label,Payload=raw});
            return string.IsNullOrEmpty(raw)||raw=="NULL"?null:System.Text.Json.JsonSerializer.Deserialize<MissionProgress.SavedState>(raw,fields);
        }
        async Task Barrier(){p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
        async Task<ViewCreation> MachineNpc(XElement definition,int cursor,bool introduced)
        {
            var state=(await Saved("npc-target-before"))!;int id=(int)definition.Attribute("id")!;
            var current=state.Mobs.Single(m=>m.Npc==id);
            await MoveForMissionTest(p,current.X+100,current.Y,current.Z);await Barrier();
            state=(await Saved("npc-target-current"))!;current=state.Mobs.Single(m=>m.Npc==id);
            int ownCursor=introduced?cursor:missionCreationCursor;
            bool Matches(ViewCreation v)=>v.Goid==599&&v.Name==current.Name&&
                (v.Attributes.TryGetValue("DontAttack",out var friendly)&&friendly.Length==1&&friendly[0]==1)==current.Friendly&&
                (v.Attributes.TryGetValue("IsEnemy",out var enemy)&&enemy.Length==1&&enemy[0]!=0)==!current.Friendly&&
                MachinesCriticalNextFiveFixture.NpcLevel(v.Attributes)==current.Level&&
                (current.Friendly?v.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&key[0]==current.Phase&&
                BitConverter.ToUInt16(key,1)==state.Instance&&key[3]==2:!v.Attributes.ContainsKey("MissionKey"))&&
                v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
                Math.Abs(BitConverter.ToDouble(pos)-current.X)<0.001&&Math.Abs(BitConverter.ToDouble(pos,8)-current.Y)<0.001&&Math.Abs(BitConverter.ToDouble(pos,16)-current.Z)<0.001;
            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(ownCursor).GroupBy(v=>v.ViewId).Select(g=>g.Last()).Any(Matches),"Current saved NPC position/side/mission generation",TimeSpan.FromSeconds(15));
            var view=p.World.CreationSnapshot().Skip(ownCursor).GroupBy(v=>v.ViewId).Select(g=>g.Last()).Last(Matches);
            targets.Add(new{Role="NPC",state.Mission,state.Phase,state.Identity,state.Instance,CreationCursor=ownCursor,Definition=id,Current=current,view.ViewId,view.SpawnId,
                Attributes=view.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))});
            return view;
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
            var state=(await Saved(enter?"before-ride-entry":"before-ride-exit"))!;
            var door=state.Waypoints.Single();var lobby=state.Exits.Single();
            double[] pose=enter?lobby:new[]{door.x,door.y,door.z};
            uint key=enter?InteriorAdmissionFixture.DoorAt(new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}
                .SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),lobby):BitConverter.ToUInt32(Convert.FromHexString(door.doorHex));
            await MoveForMissionTest(p,pose[0],pose[1]-50,pose[2]);
            var free=state.Objects.Where(o=>o.OpenFloor!=null&&o.OpenFloor.SourceGoid!=o.Goid).ToArray();
            if(enter)foreach(var obj in free)Check.That(InteriorAdmissionFixture.Body(p.World.CreationSnapshot(),missionCreationCursor,obj.Goid,obj.OpenFloor!.Centre)==null,"No early free prop03 before ordinary floor arrival");
            int views=p.World.CreationSnapshot().Count,packetCursor=InteriorAdmissionDeliveryProof.Read(PacketPath()).Count(c=>c=='\n'),log=context.Server.DebugLog().Length;
            p.SkipReceived();p.SendRpc($"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00");
            if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"ordinary saved-door Ride commit",TimeSpan.FromSeconds(15));
            var arrived=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            if(enter&&free.Length>0)
            {
                Check.That(arrived!=null,"Free prop introduction requires actual normal Ride root, not a movement-aid substitute");
                var root=Enumerable.Range(1,3).Select(i=>double.Parse(arrived!.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                await p.WaitUntilAsync(()=>free.All(o=>InteriorAdmissionFixture.Body(p.World.CreationSnapshot(),views,o.Goid,o.OpenFloor!.Centre)!=null),"Stationary first free prop03 after arrival receipt",TimeSpan.FromSeconds(15));
                string raw=InteriorAdmissionDeliveryProof.Read(PacketPath());
                foreach(var obj in free)arrivalProofs.Add(new{state.Mission,state.Phase,state.Identity,obj.Id,InitialRawSource=obj.Spot,Placement=obj.OpenFloor,
                    Receipt=InteriorAdmissionDeliveryProof.Require(raw,p.CharId,packetCursor,root,obj.Goid,obj.OpenFloor!.Centre),Stationary=true,ReceiptIsNotRoomReady=true});
            }
            if(enter)await WaitSaved(s=>s!=null&&s.Phase==state.Phase&&s.Flags.TryGetValue("entered",out bool entered)&&entered,"actual entered flag after ordinary Ride");
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
                await Ride(true);
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
                                uint CurrentState()
                                {
                                    uint state=BitConverter.ToUInt32(view.Attributes["CurrentState"]);
                                    byte[] prefix=WireFormat.FromHex($"{Le(view.ViewId)} 01 08");
                                    foreach(byte[] section in p.World.ViewSnapshot().Skip(sectionsStart))
                                        for(int at=BytePattern.IndexOf(section,prefix,0);at>=0;at=BytePattern.IndexOf(section,prefix,at+1))
                                            if(at+prefix.Length+4<=section.Length)state=BitConverter.ToUInt32(section,at+prefix.Length);
                                    return state;
                                }
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
                            if(to=="exit")await Ride(false);
                            else if(to=="hardline")await ZionUpload(p);
                            else if(to.StartsWith("npc:"))await MachineNpc(Npc(to[4..]),viewsStart,true);
                            else if(to.StartsWith("object:"))await MachinesNextObject(context,p,int.Parse(to[7..]),viewsStart,targets);
                            else throw new Exception("Unsupported escort destination "+to);
                            break;
                        }
                        case "Upload":await ZionUpload(p);break;
                        case "DefeatAll":
                        {
                            var names=new HashSet<string>(new[]{"Machine Program","Zion Soldier","Exile Soldier"});
                            foreach(var npc in mission.Elements("npc"))names.Add((string)npc.Attribute("handle")!);
                            List<ViewCreation> Targets()=>p.World.CreationSnapshot().Skip(viewsStart).GroupBy(v=>v.ViewId).Select(g=>g.Last())
                                .Where(v=>v.Goid==599 && names.Contains(v.Name??"") && !killed.Contains((v.ViewId,v.SpawnId)) &&
                                    v.Attributes.TryGetValue("IsEnemy",out var e) && e[0]!=0 &&
                                    (!v.Attributes.TryGetValue("IsDead",out var d) || d[0]==0)).ToList();
                            for(int attempts=0;!Done(number) && attempts<100;attempts++)
                            {
                                await p.WaitUntilAsync(()=>Done(number)||Targets().Count>0,"next current-phase hostile or wave completion",TimeSpan.FromSeconds(30));
                                if(Done(number))break;
                                var currentState=(await Saved("wave-current-owner"))!;
                                Check.Equal(phase,currentState.Phase,"DefeatAll remains in its authored current phase");
                                var currentActors=currentState.Mobs.Where(m=>!m.Dead&&!m.Friendly&&m.Hostile).ToArray();
                                var target=Targets().LastOrDefault(v=>currentActors.Any(m=>m.Name==v.Name&&
                                    MachinesCriticalNextFiveFixture.NpcLevel(v.Attributes)==m.Level&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
                                    BitConverter.ToDouble(pos)==m.X&&BitConverter.ToDouble(pos,8)==m.Y&&BitConverter.ToDouble(pos,16)==m.Z));
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
                                var follower=afterGoal.Mobs.Single(m=>m.Npc==1);Check.That(!follower.Dead&&follower.Health>0,"Current protected follower stays alive without a health override");
                                if(followerIdentities.TryGetValue(id,out string? identity))Check.Equal(identity,follower.Identity,"Same protected actor identity across phase carry");else followerIdentities[id]=follower.Identity;
                            }
                    }
                    if(id==201203&&phase==0&&number==2)
                    {
                        var carried=(await Saved("carry-before"))!;Check.That(carried.Items.ContainsKey(6796)&&!carried.Done.Contains(3),"Earned Flyer carried while Folder Search is pending");
                        ushort slot=carried.Items[6796];string history=System.Text.Json.JsonSerializer.Serialize(new{carried.Objects,carried.SiteDoors,carried.OpenFloorCurrentSiteIndex,carried.OpenFloorSites},fields);
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
                        var freeCarry=carried.Objects.Where(o=>o.OpenFloor!=null&&o.OpenFloor.SourceGoid!=o.Goid).ToArray();
                        if(freeCarry.Length>0)
                        {
                            await p.WaitUntilAsync(()=>freeCarry.All(o=>InteriorAdmissionFixture.Body(p.World.CreationSnapshot(),restoreViews,o.Goid,o.OpenFloor!.Centre)!=null),"Stationary carry restore first free03",TimeSpan.FromSeconds(15));
                            string raw=InteriorAdmissionDeliveryProof.Read(PacketPath());
                            foreach(var obj in freeCarry)arrivalProofs.Add(new{Role="CarryRestore",carried.Mission,carried.Phase,carried.Identity,obj.Id,
                                Receipt=InteriorAdmissionDeliveryProof.Require(raw,p.CharId,restorePackets,restoreRoot,obj.Goid,obj.OpenFloor!.Centre),Stationary=true,ReceiptIsNotRoomReady=true});
                        }
                        var restored=(await Saved("carry-restored"))!;
                        Check.Equal(carried.Identity,restored.Identity,"Same earned active mission identity");Check.Equal(carried.Started,restored.Started,"Original start time");
                        Check.Equal(carried.Phase,restored.Phase,"Same active phase");Check.Equal(slot,restored.Items[6796],"Exact earned Flyer slot");
                        Check.That(carried.Done.SequenceEqual(restored.Done)&&carried.EverDone.SequenceEqual(restored.EverDone),"Completed/pending goal history survives ordinary relog");
                        Check.Equal(history,System.Text.Json.JsonSerializer.Serialize(new{restored.Objects,restored.SiteDoors,restored.OpenFloorCurrentSiteIndex,restored.OpenFloorSites},fields),"Exact object raw spots/witnesses, site doors/current index/full history survive");
                        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6796"),"Relog cannot duplicate earned Flyer");
                        Check.Equal(carryXp,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Relog grants no XP");
                        Check.Equal(carryCash,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Relog grants no information");
                        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId={id}"),"Relog grants no completion");
                        carryProofs.Add(new{CarryXp=carryXp,CarryCash=carryCash,carried.Identity,carried.Started,carried.Phase,Item=6796,Slot=slot,Before=carried,After=restored});
                        rpcStart=0;viewsStart=0;sectionsStart=0;nextCreationCursor=0;missionCreationCursor=0;completionCursor=0;
                    }
                }
                var endState=await WaitSaved(s=>s==null||s.Phase!=phase||(s.Flags.TryGetValue("waitingForExit",out bool waiting)&&waiting),"phase goals durably complete");
                if(endState?.Phase==phase){nextCreationCursor=p.World.CreationSnapshot().Count;await Ride(false);}
                phaseCreationCursor=nextCreationCursor;phase++;
                // Exact cold preparation at the retained actual exit takes28.58s. QA synchronization only:
                // no authored, resolver/search, transport or RPC timer is changed by this bounded wait.
                if(phase<mission.Elements("phase").Count())await WaitSaved(s=>s!=null&&s.Phase==phase,"next phase persisted after normal departure",60);
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
            Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT IFNULL(HEX(payload),'NULL') FROM char_active_missions WHERE charId={p.CharId}"),"Completed mission has no active payload");
            rewards.Add(new{Mission=id,Expected=expected,Summary=Convert.ToHexString(end),BeforeXp=beforeXp,AfterXp=afterXp,BeforeCash=beforeCash,AfterCash=afterCash,LootedCash=lootedCash,AllWireXp=allXp});
            Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={id}"),"Selected chain completion earned exactly once");
            Check.Equal(0,p.World.Problems.Count,"Prima chain protocol problems");
        }
        step="next-offer";await MoveForMissionTest(p,17000,495,2300);int nextOfferCursor=p.World.RpcSnapshot().Count;p.SkipReceived();
        p.SendRpc("80 94 05 00 02");await p.WaitForRpcAsync("80 97 05 00");
        var nextEntries=p.World.RpcSnapshot().Skip(nextOfferCursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
        MissionContactChoice.CriticalIndex(nextEntries,"Coroner's Report");
        foreach(var actor in actors)Check.Equal(0,actor.World.Problems.Count,"Every owned session parser clean");step="complete";
        Check.Equal((long)ids.Length,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)})"),"Every selected completion earned, not granted");
        }
        catch(Exception error) {scenarioFailure=error;throw;}
        finally
        {
            try
            {
                string dll=context.Server.SourceAssembly,stem=Path.Combine(Path.GetTempPath(),"mxo-machines-critical-next-five-"+context.Database.Name);
                string? receipts=null,receiptError=null;
                try{receipts=await context.Database.ExecuteAsync($"SELECT missionId,completions FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)}) ORDER BY missionId");}
                catch(Exception error){receiptError=error.ToString();}
                string rawPackets=InteriorAdmissionDeliveryProof.Read(PacketPath());File.WriteAllText(stem+"-PacketLog.txt",rawPackets);
                await File.WriteAllTextAsync(stem+".json",System.Text.Json.JsonSerializer.Serialize(new{
                    Passed=step=="complete"&&scenarioFailure==null,Step=step,Failure=scenarioFailure?.ToString(),Database=context.Database.Name,
                    ServerPid=context.Server.ProcessId,SourceAssembly=dll,SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dll))),
                    SelectedMissions=ids,Prerequisites=prerequisites,Checkpoints=checkpoints,Targets=targets,Rewards=rewards,Carry=carryProofs,Arrivals=arrivalProofs,RawPacketLogPath=stem+"-PacketLog.txt",RawPacketLogSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawPackets))),
                    Receipts=receipts,ReceiptCaptureError=receiptError,
                    Sessions=actors.Select(a=>new{a.CharId,Rpc=a.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=a.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=a.World.Problems.ToArray()}),
                    NativeWalkingProven=false,NativePickingProven=false,MovementPolicy="Existing test-only movement and weaken1 helpers, aggro disabled. Every selected item/goal/receipt/reward earned over normal RPCs; no source override or replay grants."
                },new System.Text.Json.JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());
                Console.WriteLine("MACHINES NEXT FIVE EVIDENCE="+stem);
            }
            catch(Exception proofError){if(scenarioFailure==null)throw;Console.Error.WriteLine("Evidence capture failed: "+proofError);}
            finally{foreach(var actor in actors)actor.Dispose();}
        }
    }

    private static async Task<ViewCreation> MachinesNextObject(ScenarioContext context,HeadlessPlayer p,int id,int creationCursor,List<object> evidence)
    {
        var saved=await MissionCheckpoint(context,p);var source=saved.Objects.Single(o=>o.Id==id);
        SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
        double[] centre;string quaternion;double[] standing;
        if(source.OpenFloor!=null)
        {
            var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
            var witness=MissionOpenFloorWitness.Read(System.Text.Json.JsonSerializer.SerializeToElement(source.OpenFloor,options));
            witness.Require(saved.Mission,saved.Phase,id,source.Spot.mxoStaticId,MissionSites.Goid(source.Spot),source.Goid);
            centre=witness.Centre.ToArray();quaternion=witness.Quaternion;standing=witness.Standing.ToArray();
            for(int i=0;i<witness.Route.Count;i+=3)await MoveForMissionTest(p,witness.Route[i],witness.Route[i+1],witness.Route[i+2]);
            Check.That(NavMesh.Locate(1,standing[0],standing[1],standing[2],out var at)&&Math.Abs(at.floorY+95-standing[1])<1e-7,"Current persisted standing witness is grounded");
        }
        else
        {
            Check.That(MissionSites.Goid(source.Spot)==source.Goid,"Converted source requires an admitted current open-floor witness");
            var pose=MissionObjectPoses.For(new MissionObjectState{spot=source.Spot,goid=source.Goid});centre=new[]{pose.X,pose.Y,pose.Z};quaternion=Convert.ToHexString(pose.Orientation);
            double[]? qualified=null;
            foreach(var delta in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(0d,0d)})
            {
                double x=pose.X+delta.Item1,z=pose.Z+delta.Item2;
                if(NavMesh.Locate(1,x,pose.Y,z,out var nav))
                {
                    var point=new[]{x,nav.floorY+95,z};
                    if(ContainerActions.InReach(point.SelectMany(BitConverter.GetBytes).ToArray(),pose.X,pose.Y,pose.Z)){qualified=point;break;}
                }
            }
            Check.That(qualified!=null,"Legacy bound object has an actual grounded in-range approach; no old anchor fallback");standing=qualified!;
            await MoveForMissionTest(p,standing[0],standing[1],standing[2]);
        }
        bool Matches(ViewCreation v)=>v.Goid==source.Goid&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
            Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(pos,i*8)==centre[i])&&
            v.Attributes.TryGetValue("Orientation",out var q)&&q.SequenceEqual(Convert.FromHexString(quaternion));
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(creationCursor).Any(Matches),"Current source-matched object view",TimeSpan.FromSeconds(15));
        var view=p.World.CreationSnapshot().Skip(creationCursor).Last(Matches);
        evidence.Add(new{saved.Mission,saved.Phase,ObjectId=id,Source=source.Spot,CanonicalSource=new{source.Spot.mxoStaticId,source.Spot.pos_x,source.Spot.pos_y,source.Spot.pos_z,source.Spot.quat},Concrete=source.Goid,OpenFloor=source.OpenFloor,Standing=standing,
            CreationCursor=creationCursor,view.ViewId,view.SpawnId,Attributes=view.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))});
        return view;
    }
}
