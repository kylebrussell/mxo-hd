using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    // Package-local test-only driver. Every selected completion is earned over the protocol; SQL seeds prerequisites only.
    private static async Task MervAfter301207(ScenarioContext context)
    {
        const string handle="MervAfter207",filePattern="merovingian_1_*.xml",evidenceLabel="merv-after-301207";
        const byte org=3;const double startX=17000,startZ=2300;
        var ids=MervAfter301207Fixture.Missions;var prerequisites=MervAfter301207Fixture.Prerequisites;
        var sessions=new List<HeadlessPlayer>();var evidence=new List<object>();
        string step="entry";int expectedMission=0,expectedPhase=0;bool carryRelogged=false;var fields=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
        MervAfter301207Fixture.RequireSource(context.Server.SourceAssemblySha256);
        var seed=MervAfter301207Fixture.BeforeMargin(new CharacterSeed(handle){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),X=startX,Z=startZ,Health=5000,MaxHealth=5000,
            InnerStrength=2000,MaxInnerStrength=2000,Organization=org});
        var p=await context.AddPlayerAsync(seed);
        sessions.Add(p);
        var restoredBaselines=new HashSet<int>();
        MervAfterProgressProof.Acceptance? acceptance=null;
        var ledgerOperations=new List<MervLedgerOperation>();var npcBindings=new Dictionary<string,ViewCreation>();var viewOwners=new Dictionary<(int Session,ushort View,byte Spawn),string>();
        byte[][] Wire()=>sessions.SelectMany(a=>a.World.RpcSnapshot()).Select(r=>r.Bytes).ToArray();
        void CaptureRestoredBaselines()
        {
            var current=p.World.RpcSnapshot().Select(r=>r.Bytes).ToArray();
            int offset=sessions.Take(sessions.Count-1).Sum(s=>s.World.RpcSnapshot().Count);
            foreach(byte op in new byte[]{0xe5,0xe4})
            {
                int index=Array.FindIndex(current,b=>b.Length==10&&b[0]==0x80&&b[1]==op&&BitConverter.ToUInt32(b,6)==0);
                Check.That(index>=0,"Actual restored session has its initial wallet totals");
                restoredBaselines.Add(offset+index);
            }
            evidence.Add(new{Step=step,Operation="explicit-restored-wallet-baselines",Session=sessions.Count,Indices=restoredBaselines.ToArray()});
        }
        var activeOperation="";var activeKind="";int operationStart=0;bool operationOpen=false;
        void Begin(string kind){Check.That(!operationOpen,"No overlapping ledger operations");activeOperation=step;activeKind=kind;operationStart=Wire().Length;operationOpen=true;}
        void End(){Check.That(operationOpen,"Ledger operation owner");ledgerOperations.Add(new(activeOperation,activeKind,operationStart,Wire().Length));operationOpen=false;}
        async Task Barrier(){p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
        async Task<MissionProgress.SavedState?> Current()
        {
            string hex=await context.Database.ScalarAsync($"SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId={p.CharId}");
            if(string.IsNullOrWhiteSpace(hex))return null;
            return System.Text.Json.JsonSerializer.Deserialize<MissionProgress.SavedState>(System.Text.Encoding.UTF8.GetString(Convert.FromHexString(hex)),fields);
        }
        async Task<MissionProgress.SavedState?> Receipt()
        {
            string hex=await context.Database.ScalarAsync($"SELECT IFNULL(HEX(receipt),'') FROM char_active_missions WHERE charId={p.CharId}");
            return string.IsNullOrWhiteSpace(hex)?null:System.Text.Json.JsonSerializer.Deserialize<MissionProgress.SavedState>(System.Text.Encoding.UTF8.GetString(Convert.FromHexString(hex)),fields);
        }
        async Task<IReadOnlyList<MervAfterProgressProof.InventoryRow>> ItemRows(uint goid)
        {
            string json=await context.Database.ScalarAsync($"SELECT COALESCE(JSON_ARRAYAGG(JSON_OBJECT('Character',charId,'Slot',slot,'Goid',goid,'Count',count)),'[]') FROM inventory WHERE charId={p.CharId} AND goid={goid}");
            return System.Text.Json.JsonSerializer.Deserialize<MervAfterProgressProof.InventoryRow[]>(json)!;
        }
        async Task WaitPersisted(int mission,int nextPhase,bool terminal,int absoluteCursor)
        {
            Check.That(acceptance?.Mission==mission,"Transition uses the actual accepted mission token");
            int phase=nextPhase-1;
            await p.WaitUntilAsync(()=>
            {
                var current=Current().GetAwaiter().GetResult();
                var receipt=terminal?Receipt().GetAwaiter().GetResult():null;
                long count=terminal?context.Database.LongAsync($"SELECT COALESCE(SUM(completions),0) FROM char_missions WHERE charId={p.CharId} AND missionId={mission}").GetAwaiter().GetResult():0;
                return MervAfterProgressProof.Advanced(acceptance!,phase,terminal?phase:phase+1,current,receipt,count,Wire(),absoluteCursor);
            },"Exact accepted identity/start/instance next phase or terminal CAS receipt+fresh summary",TimeSpan.FromSeconds(15));
        }
        async Task Exit(int absoluteCursor,bool terminal)
        {
            var state=await Current();
            evidence.Add(new{Step=step,Operation="pre-exit-owned-checkpoint",State=state,Acceptance=acceptance,ExpectedMission=expectedMission,ExpectedPhase=expectedPhase});
            if(state!=null)Check.That(acceptance!=null&&MervAfterProgressProof.Owns(acceptance,state),"Exit retains accepted identity/start/instance");
            if(state?.Mission==expectedMission&&state.Phase==expectedPhase)
            {
                Check.That(state.Flags.GetValueOrDefault("waitingForExit"),"Current ordinary exit needs authoritative waiting-for-exit");
                await MervFollowupRide(context,p,false,evidence,expectedMission,expectedPhase);
            }
            await WaitPersisted(expectedMission,expectedPhase+1,terminal,absoluteCursor);
        }

        async Task<MissionProgress.SavedState> Saved()
        {
            string payload=await CarriedMissionNpcFixture.ReadOwnedAsync(context,p);
            evidence.Add(new{Step=step,Payload=payload});return System.Text.Json.JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,fields)!;
        }
        async Task<(long Xp,long Info)> Account()=> (await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"));
        async Task<ViewCreation> CurrentNpc(XElement npc,int start,bool corpse=false)
        {
            ushort id=ushort.Parse((string)npc.Attribute("id")!);var checkpoint=await Saved();
            Check.That(acceptance!=null&&MervAfterProgressProof.Owns(acceptance,checkpoint),"NPC belongs to the actual accepted owner");
            Check.Equal(expectedMission,checkpoint.Mission,"NPC belongs to the currently selected mission");Check.Equal(expectedPhase,checkpoint.Phase,"NPC belongs to the actual current phase snapshot");
            var owned=checkpoint.Mobs.Single(m=>m.Npc==id&&(corpse||!m.Dead));
            await MervAfterNpcRoute(context,p,checkpoint,id,evidence);
            await MoveForMissionTest(p,owned.X+100,owned.Y,owned.Z);
            var afterApproach=await Saved();
            Check.That(acceptance!=null&&MervAfterProgressProof.Owns(acceptance,afterApproach),"NPC approach keeps actual accepted owner");
            var scope=MervAfterCurrentNpc.CaptureAfterApproach(checkpoint,afterApproach,id);checkpoint=afterApproach;
            owned=checkpoint.Mobs.Single(m=>m.Npc==id&&(corpse||!m.Dead));MervAfterCurrentNpc.CurrentView? observed=null;
            await p.WaitUntilAsync(()=>
            {
                try{var current=Saved().GetAwaiter().GetResult();Check.That(acceptance!=null&&MervAfterProgressProof.Owns(acceptance,current)&&current.Phase==expectedPhase,"NPC wait keeps accepted phase owner");observed=MervAfterCurrentNpc.Require(p.World.CurrentOwnViewLife(),scope,current);return true;}
                catch(InvalidDataException){return false;}
            },"Exact current own-life NPC generation/latest pose/side/UUID "+id,TimeSpan.FromSeconds(12));
            var view=observed!.View;var generation=(sessions.Count,view.ViewId,view.SpawnId);
            if(viewOwners.TryGetValue(generation,out string? priorOwner))Check.Equal(priorOwner,owned.Identity,"One current session/view/spawn cannot impersonate another saved actor");
            viewOwners[generation]=owned.Identity;npcBindings[owned.Identity]=view;
            evidence.Add(new{Step=step,Operation="current-live-npc",Scope=scope,Observed=observed});
            evidence.Add(new{Step=step,Npc=id,owned.Identity,owned.X,owned.Y,owned.Z,View=view.ViewId,Spawn=view.SpawnId});return view;
        }
        async Task<ViewCreation> CurrentObject(int id,int start)
        {
            var checkpoint=await Saved();var owned=checkpoint.Objects.Single(o=>o.Id==id);
            Check.Equal(expectedMission,checkpoint.Mission,"Object belongs to current selected mission");Check.Equal(expectedPhase,checkpoint.Phase,"Object belongs to current owned phase");
            var view=await MervFollowupObject(context,p,id,start,evidence);
            if(owned.OpenFloor!=null)return view;
            // Native bound sources use their actual source binding/current life, never a fabricated free witness.
            var afterApproach=await Saved();Check.That(acceptance!=null&&MervAfterProgressProof.Owns(acceptance,afterApproach),"Object approach keeps accepted owner");
            var scope=MervAfterCurrentBoundBody.CaptureAfterApproach(checkpoint,afterApproach,(ushort)id);
            var current=MervAfterCurrentBoundBody.Require(p.World.CurrentOwnViewLife(),scope,afterApproach,MissionBoundFurnitureRouteProof.SupportsGoid(owned.Goid)?MissionBoundFurnitureRouteProof.Read(context.RepositoryRoot,afterApproach,id):null);
            evidence.Add(new{Step=step,Operation="current-bound-body",Scope=scope,Observed=current});return current.View;
        }
        Exception? scenarioFailure=null;
        try
        {
        await LoadAbilitiesAsync(context,p,2,17,1006,77);await p.EnterWorldAsync();await Barrier();
        var baseline=MervRewardLedger.ReadBaseline(Wire());var durableBaseline=await Account();
        Check.Equal((long)baseline.Experience,durableBaseline.Xp,"Initial wire/SQL XP baseline");Check.Equal((long)baseline.Information,durableBaseline.Info,"Initial wire/SQL cash baseline");
        foreach(int id in ids)
        {
            var mission=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),filePattern).Select(XElement.Load)
                .Single(m=>(int)m.Attribute("id")! == id);
            expectedMission=id;step="mission-"+id;
            var missionStart=await Account();
            int missionAbsoluteCursor=Wire().Length;
            int logStart=context.Server.DebugLog().Length;int ends=p.Received("80 a2 00").Count;
            int listStart=p.World.RpcSnapshot().Count;
            p.SkipReceived();p.SendRpc($"80 94 05 00 {org:x2}");await p.WaitForRpcAsync("80 97 05 00");
            var entries=p.World.RpcSnapshot().Skip(listStart).Select(r=>r.Bytes).Where(b=>b.Length>17 && b[0]==0x80 && b[1]==0x96 && BitConverter.ToUInt16(b,2)==5).ToArray();
            // The list contains indices and titles, not definition IDs. Resolve the actual Critical entry,
            // then verify the selected definition from the accepted mission's authoritative staging record.
            int index=MissionContactChoice.CriticalIndex(entries,(string)mission.Attribute("title")!);
            p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            Check.Equal((long)id,await context.Database.LongAsync($"SELECT JSON_EXTRACT(payload,'$.Mission') FROM char_active_missions WHERE charId={p.CharId}"),"Actual accepted definition ID matches selected Critical title");
            var accepted=await Saved();acceptance=MervAfterProgressProof.Capture(accepted);
            Check.Equal(id,acceptance.Mission,"Acceptance token is from the actual requested definition");
            evidence.Add(new{Step=step,Operation="normal-accepted-mission-token",Acceptance=acceptance,Checkpoint=accepted,AbsoluteRpcCursor=Wire().Length});
            int phase=0;
            foreach(var area in mission.Elements("phase"))
            {
                int phaseAbsoluteCursor=Wire().Length;int rpcStart=p.World.RpcSnapshot().Count,viewsStart=p.World.CreationSnapshot().Count,sectionsStart=p.World.ViewSnapshot().Count;
                var killed=new HashSet<(ushort,byte)>();
                var provenGoals=new HashSet<int>();int finalPhase=mission.Elements("phase").Count()-1;
                bool Done(int n)
                {
                    if(provenGoals.Contains(n))return true;
                    bool optional=(int?)area.Elements("objective").Single(g=>(int)g.Attribute("goal")! ==n).Attribute("optional")==1;
                    var state=Current().GetAwaiter().GetResult();var receipt=state==null?Receipt().GetAwaiter().GetResult():null;
                    long count=state==null?context.Database.LongAsync($"SELECT COALESCE(SUM(completions),0) FROM char_missions WHERE charId={p.CharId} AND missionId={id}").GetAwaiter().GetResult():0;
                    bool done=MervAfterProgressProof.GoalDone(acceptance!,phase,finalPhase,n,optional,state,receipt,count,
                        Wire(),phaseAbsoluteCursor,missionAbsoluteCursor);
                    if(done)
                    {
                        provenGoals.Add(n);
                        evidence.Add(new{Step=step,Operation="scoped-current-goal-proof",Acceptance=acceptance,Phase=phase,Goal=n,Optional=optional,
                            PhaseAbsoluteRpcCursor=phaseAbsoluteCursor,MissionAbsoluteRpcCursor=missionAbsoluteCursor,
                            Current=state,TerminalCasReceipt=receipt,Completions=count});
                    }
                    return done;
                }
                expectedPhase=phase;step=$"mission-{id}-phase-{phase}";
                if(id==301212&&phase==2&&!carryRelogged)
                {
                    var before=await Saved();Check.That(before.Items.ContainsKey(6827),"Computer Parts earned by current Take before carry relog");
                    Check.That(acceptance!=null&&MervAfterProgressProof.Owns(acceptance,before),"Carry is the same normal acceptance");
                    var beforeRows=await ItemRows(6827);var ownedBefore=MervAfterProgressProof.RequireOwnedItem(beforeRows,p.CharId,6827,before.Items[6827]);
                    evidence.Add(new{Step=step,Operation="actual-owned-parts-before-logout",Rows=beforeRows,Owned=ownedBefore});
                    string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
                    var account=await Account();Begin("Relog");p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");p.Dispose();var partsCarryWait=System.Diagnostics.Stopwatch.StartNew();await Task.Delay(TimeSpan.FromSeconds(33));double partsCarryWaitSeconds=partsCarryWait.Elapsed.TotalSeconds;
                    p=await HeadlessPlayer.LoginAsync(context.Server.Address,username,handle);sessions.Add(p);await p.EnterWorldAsync();p.SendRpc("0c 01");p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
                    CaptureRestoredBaselines();var after=await Saved();Check.Equal(before.Identity,after.Identity,"Carry identity survives relog");Check.Equal(before.Started,after.Started,"Carry Started survives relog");
                    Check.Equal(before.Mission,after.Mission,"Selected carry mission survives relog");
                    Check.Equal(System.Text.Json.JsonSerializer.Serialize(new{before.SiteDoors,before.OpenFloorSites,before.OpenFloorCurrentSiteIndex,before.EverDone,Npc=before.NpcInterior.ValueKind==System.Text.Json.JsonValueKind.Undefined?null:before.NpcInterior.GetRawText()},fields),System.Text.Json.JsonSerializer.Serialize(new{after.SiteDoors,after.OpenFloorSites,after.OpenFloorCurrentSiteIndex,after.EverDone,Npc=after.NpcInterior.ValueKind==System.Text.Json.JsonValueKind.Undefined?null:after.NpcInterior.GetRawText()},fields),"Exact admitted site and earned goal history survives carry relog");
                    Check.Equal(before.Phase,after.Phase,"Current delivery phase survives relog");Check.Equal(before.Items[6827],after.Items[6827],"Computer Parts exact slot survives relog");Check.Equal(account,await Account(),"Relog pays no reward");
                    Check.That(MervAfterProgressProof.Owns(acceptance!,after),"Restored carry retains actual acceptance instance too");
                    var afterRows=await ItemRows(6827);var ownedAfter=MervAfterProgressProof.RequireOwnedItem(afterRows,p.CharId,6827,after.Items[6827]);
                    Check.Equal(ownedBefore,ownedAfter,"Actual inventory character/item/slot/count survives relog");
                    evidence.Add(new{Step=step,Operation="actual-owned-parts-after-login",WaitSeconds=partsCarryWaitSeconds,Rows=afterRows,Owned=ownedAfter});
                    Check.Equal(System.Text.Json.JsonSerializer.Serialize(before.Objects,fields),System.Text.Json.JsonSerializer.Serialize(after.Objects,fields),"Admitted fixed witnesses survive relog");
                    await Barrier();End();
                    carryRelogged=true;rpcStart=0;viewsStart=0;sectionsStart=0;listStart=0;ends=0;
                }
                await MervFollowupRide(context,p,true,evidence,expectedMission,expectedPhase);
                var goals=area.Elements("objective").Where(g=>(string)g.Attribute("type")! != "Protect").ToList();
                while(goals.Any(g=>!Done((int)g.Attribute("goal")!)))
                {
                    // Complete open optional goals first so an automatic final mandatory action cannot skip them.
                    var goal=goals.Where(g=>!Done((int)g.Attribute("goal")!) && ((int?)g.Attribute("after") is not int after || Done(after)))
                        .OrderBy(g=>(int?)g.Attribute("optional")==1?0:1).FirstOrDefault();
                    Check.That(goal!=null,$"Mission{id} phase{phase+1} has an eligible remaining goal");
                    step=$"mission-{id}-phase-{phase}-goal-{(int)goal!.Attribute("goal")!}";
                    var preGoal=await Account();Begin((string)goal.Attribute("type")!);
                    evidence.Add(new{Step=step,PreGoalXp=preGoal.Xp,PreGoalInfo=preGoal.Info,OperationStart=operationStart});
                    string type=(string)goal!.Attribute("type")!;int number=(int)goal.Attribute("goal")!;
                    Console.WriteLine($"  Merv{id} phase{phase+1} goal{number} {type}");
                    XElement Npc(string key)=>mission.Elements("npc").Single(n=>(string)n.Attribute("id")! == key);
                    switch(type)
                    {
                        case "Talk":case "Get":case "Give":case "Defeat":case "Loot":
                        {
                            string reference=(string)goal.Attribute("npc")!;
                            var npc=Npc(type=="Loot"?reference.Split(',')[0]:reference);
                            var view=await CurrentNpc(npc,viewsStart,type=="Loot");
                            bool hostile=view.Attributes.TryGetValue("IsEnemy",out var enemy) && enemy[0]!=0;
                            var key=(view.ViewId,view.SpawnId);
                            if(type=="Defeat" || type=="Loot" && !killed.Contains(key) &&
                                (!view.Attributes.TryGetValue("IsDead",out var dead) || dead[0]==0))
                            {
                                if(type=="Loot"){End();Begin("Defeat");}
                                await DefeatMissionNpc(p,view);killed.Add(key);
                                if(type=="Loot"){await Barrier();End();Begin("Loot");}
                            }
                            if(type=="Loot")
                            {
                                // Select THIS corpse even when an earlier DefeatAll left another corpse window open.
                                view=await CurrentNpc(npc,viewsStart,true);
                                p.SendRpc($"80 c7 {MissionNpcId(view)} 05 00");await p.WaitForRpcAsync("81 19");
                                p.SendRpc("81 1f");
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
                            var view=await CurrentObject(objectId,viewsStart);
                            if(!MissionStaging.IsClickable(view.Goid))
                            {await p.WaitUntilAsync(()=>Done(number),"nonclickable mission object proximity",TimeSpan.FromSeconds(10));break;}
                            bool computer=MissionStaging.IsComputer(view.Goid);
                            string label=computer ? type=="Put"?"Put":type=="Use" || item==0?"Hack":"Take" : "Open";
                            byte use=ObjectStates.UseNumber(view.Goid,label) ?? throw new Exception($"No sourced {label} use for{view.Goid}");
                            if(computer)
                            {
                                var stateScope=MervAfterComputerState.Capture(p.World.CurrentOwnViewLife(),view);
                                uint CurrentState()=>MervAfterComputerState.Read(p.World.CurrentOwnViewLife(),stateScope.Capability);
                                evidence.Add(new{Step=step,Operation="current-computer-birth-state-capability",Scope=stateScope});
                                await p.WaitUntilAsync(()=>ObjectStates.Offered(view.Goid,CurrentState()).Contains(use),
                                    $"actual enabled computer {label} use{use}",TimeSpan.FromSeconds(10));
                            }
                            int loot=p.Received("81 19").Count;
                            Console.WriteLine($"    object{objectId} goid{view.Goid} {label} use{use}");
                            p.SendRpc($"80 c7 {MissionNpcId(view)} {use:x2} 00");
                            await p.WaitUntilAsync(()=>Done(number) || p.Received("81 19").Count>loot,"object action or container window",TimeSpan.FromSeconds(10));
                            if(!Done(number))
                            {
                                if(type=="Put")
                                {
                                    long slot=await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={p.CharId} AND goid={item} LIMIT 1");
                                    Check.That(slot>0 && slot<97,"Put uses an actual owned mission item bag slot");
                                    p.SendRpc($"81 21 {slot:x2}");
                                }
                                else p.SendRpc("81 1f");
                            }
                            break;
                        }
                        case "DefeatAll":
                        {
                            for(int attempts=0;!Done(number)&&attempts<100;attempts++)
                            {
                                var state=await Saved();Check.That(acceptance!=null&&MervAfterProgressProof.Owns(acceptance,state)&&state.Phase==expectedPhase,"DefeatAll keeps current accepted stage");
                                var named=mission.Elements("npc").Where(n=>(int?)n.Attribute("at")==phase+1).Select(n=>checked((ushort)(int)n.Attribute("id")!)).ToArray();
                                var owned=MervAfterCurrentNpc.CurrentHostiles(state,named).LastOrDefault();
                                if(owned==null){await Task.Delay(250);continue;}
                                var target=await CurrentNpc(new XElement("npc",new XAttribute("id",owned.Npc)),viewsStart);
                                await DefeatMissionNpc(p,target);killed.Add((target.ViewId,target.SpawnId));
                            }
                            break;
                        }
                        default:throw new Exception("Unsupported Merv goal "+type);
                    }
                    await p.WaitUntilAsync(()=>Done(number),$"Merv{id} phase{phase+1} actual goal{number} done",TimeSpan.FromSeconds(20));
                    await Barrier();End();
                    if(type=="Give"&&(uint?)goal.Attribute("item")==6827)
                    {
                        var remaining=await ItemRows(6827);Check.Equal(0,remaining.Count,"Actual parts inventory row removed by normal Give");
                        evidence.Add(new{Step=step,Operation="actual-owned-parts-after-give",Rows=remaining});
                    }

                }
                var current=await Current();
                bool terminal=phase+1==mission.Elements("phase").Count();
                if(current?.Mission==id&&current.Phase==phase)
                {Begin("Exit");await Exit(missionAbsoluteCursor,terminal);await Barrier();End();}
                else await WaitPersisted(id,phase+1,terminal,missionAbsoluteCursor);
                phase++;
            }
            var expected=MervAfter301207Fixture.Payouts[id];
            var completions=Wire().Skip(missionAbsoluteCursor).Where(b=>b.Length==26&&b[0]==0x80&&b[1]==0xa2&&b[2]==0).ToArray();
            Check.Equal(1,completions.Length,"Exactly one earned completion RPC");
            Check.Equal(expected.Info,BitConverter.ToUInt32(completions[0],11),"Independent literal info payout on wire");Check.Equal(expected.Xp,BitConverter.ToUInt32(completions[0],15),"Independent literal XP payout on wire");
            await Barrier();var rewarded=await Account();evidence.Add(new{Step="reward-"+id,ExpectedXp=expected.Xp,ExpectedInfo=expected.Info,StartXp=missionStart.Xp,StartInfo=missionStart.Info,AfterXp=rewarded.Xp,AfterInfo=rewarded.Info,Completion=Convert.ToHexString(completions[0])});
            Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT IFNULL(HEX(payload),'NULL') FROM char_active_missions WHERE charId={p.CharId}"),"Terminal durable payload cleared after earned mission");
            var selectedItems=mission.Descendants("objective").Select(o=>(uint?)o.Attribute("item")??0).Where(i=>i!=0).Distinct().ToArray();
            if(selectedItems.Length>0)Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid IN ({string.Join(",",selectedItems)})"),"Completed mission retains no owned goal items");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={id}"),"Selected chain completion earned exactly once");
            Check.Equal(0,p.World.Problems.Count,"Prima chain protocol problems");
        }
        Check.That(carryRelogged,"Earned parts carry/relog exercised");
        int nextCursor=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc("80 94 05 00 03");await p.WaitForRpcAsync("80 97 05 00");
        var next=p.World.RpcSnapshot().Skip(nextCursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
        MissionContactChoice.CriticalIndex(next,"Mod Box");evidence.Add(new{Step="actual-next-offer",Records=next.Select(Convert.ToHexString).ToArray()});
        Check.Equal((long)ids.Length,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)})"),"Every selected completion earned, not granted");
        await p.WaitUntilAsync(()=>MervRewardLedger.HasCompleteMissionAwards(Wire(),5),"All earned mission gain/total families",TimeSpan.FromSeconds(10));
        var ledger=MervRewardLedger.Reconcile(Wire(),ledgerOperations,baseline.Experience,baseline.Information,ids.Select(id=>MervAfter301207Fixture.Payouts[id].Xp).ToArray(),ids.Select(id=>MervAfter301207Fixture.Payouts[id].Info).ToArray(),restoredBaselines);
        var completedAccount=await Account();Check.Equal((long)ledger.Experience,completedAccount.Xp,"Independent received XP ledger equals durable SQL");Check.Equal((long)ledger.Information,completedAccount.Info,"Independent received corpse/mission cash ledger equals durable SQL");evidence.Add(new{Step="earned-ledger",Ledger=ledger});
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6827"),"Delivered parts no longer owned");
        step="completed-relog";string completedUser=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
        Begin("Relog");p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");p.Dispose();var carryWait=System.Diagnostics.Stopwatch.StartNew();await Task.Delay(TimeSpan.FromSeconds(33));double carryWaitSeconds=carryWait.Elapsed.TotalSeconds;
        p=await HeadlessPlayer.LoginAsync(context.Server.Address,completedUser,handle);sessions.Add(p);await p.EnterWorldAsync();p.SendRpc("0c 01");await Barrier();
        CaptureRestoredBaselines();End();var finalLedger=MervRewardLedger.Reconcile(Wire(),ledgerOperations,baseline.Experience,baseline.Information,ids.Select(id=>MervAfter301207Fixture.Payouts[id].Xp).ToArray(),ids.Select(id=>MervAfter301207Fixture.Payouts[id].Info).ToArray(),restoredBaselines);
        Check.Equal(ledger.Experience,finalLedger.Experience,"Completed relog preserves independently reconciled XP baseline");Check.Equal(ledger.Information,finalLedger.Information,"Completed relog preserves independently reconciled cash baseline");evidence.Add(new{Step="final-relog-ledger",Ledger=finalLedger});
        Check.Equal(completedAccount,await Account(),"Completed relog pays no duplicate rewards");Check.That(MervRewardLedger.HasNoCompletionReplay(p.World.RpcSnapshot().Select(r=>r.Bytes).ToArray()),"Completed relog replays no completion summary");
        Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT IFNULL(HEX(payload),'NULL') FROM char_active_missions WHERE charId={p.CharId}"),"Completed relog revives no active payload");
        Check.Equal(5L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)}) AND completions=1"),"All five receipts remain exactly one after completed relog");
        foreach(var session in sessions)Check.Equal(0,session.World.Problems.Count,"Every retained session parser clean");
        step="complete";
        }
        catch(Exception error) {scenarioFailure=error;throw;}
        finally
        {
            try
            {
            if(evidenceLabel!=null)
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-"+evidenceLabel+"-"+context.Database.Name);
                File.WriteAllText(stem+"-debug.log",context.Server.DebugLog());File.WriteAllText(stem+"-stdout.log",context.Server.FullOutput());
                var dll=context.Server.SourceAssembly;string? packetLogPath=null,packetLogSha=null,packetLogFailure=null;
                try{packetLogPath=stem+"-PacketLog.txt";File.Copy(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt"),packetLogPath,true);
                    packetLogSha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(packetLogPath)));}
                catch(Exception capture){packetLogFailure=capture.ToString();if(scenarioFailure==null)throw;}
                await File.WriteAllTextAsync(stem+".json",System.Text.Json.JsonSerializer.Serialize(new{
                    DriverPid=Environment.ProcessId,ServerPid=context.Server.ProcessId,Database=context.Database.Name,WorkingDirectory=context.Server.WorkingDirectory,PacketLogPath=packetLogPath,PacketLogSha256=packetLogSha,PacketLogFailure=packetLogFailure,RestoredBaselineIndices=restoredBaselines.ToArray(),SourceAssembly=dll,SourceConfiguration=new DirectoryInfo(Path.GetDirectoryName(dll)!).Parent!.Name,
                    SourceSha256=context.Server.SourceAssemblySha256,
                    Passed=step=="complete"&&scenarioFailure==null,Step=step,Failure=scenarioFailure?.ToString(),SelectedMissions=ids,Prerequisites=prerequisites,Evidence=evidence,LedgerOperations=ledgerOperations,OpenOperation=operationOpen?new{Step=activeOperation,Kind=activeKind,Start=operationStart}:null,Sessions=sessions.Select(s=>new{s.Handle,Rpc=s.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),Views=s.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=s.World.Problems.ToArray()}).ToArray(),
                    CurrentPayloadHex=await context.Database.ExecuteAsync($"SELECT IFNULL(HEX(payload),'NULL') FROM char_active_missions WHERE charId={p.CharId}"),
                    CompletionReceipts=await context.Database.ExecuteAsync($"SELECT missionId,completions FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)}) ORDER BY missionId"),
                    Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),
                    ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),
                    ProtocolProblems=p.World.Problems.ToArray(),NativeReachabilityProven=false,NativeRenderingProven=false,
                    MovementPolicy="Existing test-only movement/weaken helpers; completion is protocol evidence, not wall/room reachability proof"
                },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("  Required mission evidence "+stem+".json");
            }
            catch(Exception proofError)
            {
                if(scenarioFailure==null)throw; // A successful scenario cannot silently omit required proof.
                Console.Error.WriteLine("Required evidence capture also failed; preserving original scenario exception: "+proofError);
            }
            }
            finally{foreach(var session in sessions)session.Dispose();}
        }
    }

}
