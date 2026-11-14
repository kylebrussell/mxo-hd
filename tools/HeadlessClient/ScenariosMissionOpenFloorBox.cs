using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MissionOpenFloorBoxScenario(ScenarioContext context)=>MissionOpenFloorBoxScenarioCore(context,false);
    private static Task MissionStandingInteriorScenario(ScenarioContext context)=>MissionOpenFloorBoxScenarioCore(context,true);
    private static async Task MissionOpenFloorBoxScenarioCore(ScenarioContext context,bool requireInterior)
    {
        HeadlessPlayer player=null!;uint charId=0;string handle="";
        string phase="initial-preparation";Exception? failure=null;bool passed=false;var fields=new JsonSerializerOptions{IncludeFields=true};
        var sourceIdentities=new HashSet<(ushort Goid,uint Key)>();var checkpoints=new List<object>();var rawCheckpoints=new List<object>();var operations=new List<object>();var actors=new List<HeadlessPlayer>();var servers=new List<object>();ServerInstance? cold=null;
        string dll=context.Server.SourceAssembly;int creationStart=0;string? originalSha=null;DateTime? originalMtime=null;int originalPid=0;
        void CaptureServer(ServerInstance server,string role)
        {
            var file=new FileInfo(server.SourceAssembly);string sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.FullName)));
            if(role=="original"){originalSha=sha;originalMtime=file.LastWriteTimeUtc;originalPid=server.ProcessId;}
            servers.Add(new{Role=role,server.ProcessId,server.SourceAssembly,Configuration=Directory.GetParent(file.DirectoryName!)!.Name,
                Sha256=sha,MtimeUtc=file.LastWriteTimeUtc.ToString("O"),file.Length,CapturedUtc=DateTimeOffset.UtcNow});
        }
        async Task<(MissionProgress.SavedState State,MissionOpenFloorWitness Witness,string Source)> Saved(int objectId=1)
        {
            string payload=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={charId}");
            rawCheckpoints.Add(new{Phase=phase,RequestedObjectId=objectId,Payload=payload,CapturedUtc=DateTimeOffset.UtcNow});
            using var json=JsonDocument.Parse(payload);var root=json.RootElement;
            Check.Equal(201202,root.GetProperty("Mission").GetInt32(),"Normal Agent Gray accepted201202");
            Check.Equal(0,root.GetProperty("Phase").GetInt32(),"Open-floor Box proof remains in phase0 before Take");
            var obj=root.GetProperty("Objects").EnumerateArray().Single(x=>x.GetProperty("Id").GetInt32()==objectId);
            Check.Equal(3,root.GetProperty("Objects").GetArrayLength(),"Whole authored first-phase three-object set remains present");
            var sourceElement=obj.GetProperty("Spot");
            var witness=MissionOpenFloorWitness.Read(obj.GetProperty("OpenFloor"));
            witness.Require(201202,0,objectId,sourceElement.GetProperty("mxoStaticId").GetUInt32(),
                BitConverter.ToUInt16(Convert.FromBase64String(sourceElement.GetProperty("type").GetString()!),0),548);
            sourceIdentities.Add((witness.SourceGoid,witness.SourceKey));
            var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,fields)!;var source=state.Objects.Single(x=>x.Id==objectId);
            Check.Equal((ushort)548,source.Goid,"Ordinary source1415 Box prototype548");
            if(objectId==1)
            {
                Check.Equal(0x0d1003dcu,source.Spot.mxoStaticId,"Unchanged normal seed source; no forced spot");
                Check.Equal((23940d,2245d,15050d),(source.Spot.pos_x,source.Spot.pos_y,source.Spot.pos_z),"Raw checkpoint source remains immutable");
                Check.Equal("00000000f204353f00000000f304353f",source.Spot.quat.ToLowerInvariant(),"Raw source quaternion unchanged");
            }
            string rawSource=obj.GetProperty("Spot").GetRawText();checkpoints.Add(new{Phase=phase,Payload=payload,Witness=witness.RawJson,Source=rawSource});
            return(state,witness,rawSource);
        }
        async Task<string[]> WholeSet()
        {
            var all=new List<MissionOpenFloorWitness>();
            for(int id=1;id<=3;id++)all.Add((await Saved(id)).Witness);
            Check.Equal(3,all.Select(w=>string.Join(",",w.Centre.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)))).Distinct().Count(),"All three published Box centres are distinct; no fixture repacking");
            if(requireInterior)
            {
                var third=all.Single(w=>w.ObjectId==3);
                Check.Equal("25841.595703125,2040,14214.611328125",string.Join(",",third.Centre.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))),"Unchanged normal S3 body");
                Check.Equal("25689.16015625,2095,14143.16796875",string.Join(",",third.Standing.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))),"S3 uses the already legal route without a representational sliver");
            }
            return all.Select(w=>w.RawJson).ToArray();
        }
        async Task Accept()
        {
            creationStart=player.World.CreationSnapshot().Count;int cursor=player.World.RpcSnapshot().Count;player.SkipReceived();player.SendRpc("80 94 05 00 02");await player.WaitForRpcAsync("80 97 05 00");
            var entries=player.World.RpcSnapshot().Skip(cursor).Where(r=>r.Bytes.Length>17&&r.Bytes[0]==0x80&&r.Bytes[1]==0x96&&BitConverter.ToUInt16(r.Bytes,2)==5).Select(r=>r.Bytes).ToArray();
            int index=MissionContactChoice.CriticalIndex(entries,"Poster Hunt");
            player.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await player.WaitForRpcAsync("80 99");
            player.SendRpc("80 9b 07 00 01");await player.WaitForRpcAsync("80 9e 00");
            Check.Equal(201202L,await context.Database.LongAsync($"SELECT JSON_EXTRACT(payload,'$.Mission') FROM char_active_missions WHERE charId={charId}"),"Current selected Critical definition, not historical offer");
            operations.Add(new{Phase=phase,OfferCursor=cursor,SelectedIndex=index,Records=player.World.RpcSnapshot().Skip(cursor).Select(r=>r.Hex).ToArray()});
        }
        async Task RideToPublishedRoot()
        {
            var w=(await Saved()).Witness;
            Match site=Regex.Matches(context.Server.DebugLog(),
                @"\[MISSION\] Phase \d+ at a .* floor in .* \(([-\d.]+),([-\d.]+),([-\d.]+)\), elevator .*; street door at ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
            double Number(int i)=>double.Parse(site.Groups[i].Value,CultureInfo.InvariantCulture);
            // Existing fixture movement reaches the actual street door; do not jump to a legacy site centre afterwards.
            await MoveForMissionTest(player,Number(4),Number(5),Number(6));
            int log=context.Server.DebugLog().Length;player.SendChat("?missionfloor");
            await player.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),
                "actual ordinary Ride executor before following the open-floor route",TimeSpan.FromSeconds(10));
            Match ride=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
            double[] actual=Enumerable.Range(1,3).Select(i=>double.Parse(ride.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            Check.That(actual.SequenceEqual(w.Route.Take(3)),"Existing Ride arrival exactly equals the published route root");
            operations.Add(new{Phase=phase,RideLogCursor=log,ActualRide=actual,PublishedRoot=w.Route.Take(3).ToArray(),RawRide=ride.Value});
        }
        async Task<(ViewCreation View,MissionOpenFloorWitness Witness,string Source)> ViewAtPublishedPlan(int objectId=1)
        {
            var saved=await Saved(objectId);var w=saved.Witness;
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            Check.That(NavMesh.Locate(1,w.Standing[0],w.Standing[1],w.Standing[2],out var nav),"Published standing point is on actual NAV");
            Check.Equal(nav.floorY+95,w.Standing[1],"Published standing height follows actual floor");
            Check.Equal(nav.floorY+40,w.Centre[1],"Box base uses exact measured support minY-40");
            // Existing ?tp headless movement follows published route points; it is not native movement/walking/scene proof.
            for(int i=0;i<w.Route.Count;i+=3)await MoveForMissionTest(player,w.Route[i],w.Route[i+1],w.Route[i+2]);
            bool AtPlan(ViewCreation v)=>v.Goid==548&&v.Attributes.TryGetValue("Position",out var p)&&p.Length==24&&
                BitConverter.ToDouble(p)==w.Centre[0]&&BitConverter.ToDouble(p,8)==w.Centre[1]&&BitConverter.ToDouble(p,16)==w.Centre[2];
            await player.WaitUntilAsync(()=>player.World.CreationSnapshot().Skip(creationStart).Any(AtPlan),"Current Box at published open-floor centre",TimeSpan.FromSeconds(10));
            var view=player.World.CreationSnapshot().Skip(creationStart).Last(AtPlan);
            Check.That(view.Attributes["Orientation"].SequenceEqual(Convert.FromHexString(w.Quaternion)),"Actual03 exact persisted native quaternion");
            byte use=ObjectStates.UseNumber(548,"Open")??throw new ScenarioFailure("548 lacks sourced Open");Check.Equal((byte)3,use,"Unchanged native Open3");
            uint state=view.Attributes.TryGetValue("CurrentState",out var s)?BitConverter.ToUInt32(s):ObjectStates.DefaultState(548);
            Check.Equal(0x835u,state,"Existing received/default Closed835");Check.That(ObjectStates.Offered(548,state).Contains(use),"Closed state offers Open3");
            Check.Equal(100f,ObjectStates.FindUsable(548,use).maxDistance,"Existing authored native range100 unchanged");
            byte[] standing=w.Standing.SelectMany(BitConverter.GetBytes).ToArray();
            Check.That(ContainerActions.InReach(standing,w.Centre[0],w.Centre[1],w.Centre[2]),"Existing server container reach admits published standing witness");
            operations.Add(new{Phase=phase,CreationCursor=creationStart,view.ViewId,view.SpawnId,Position=Convert.ToHexString(view.Attributes["Position"]),Orientation=Convert.ToHexString(view.Attributes["Orientation"]),Witness=w.RawJson});
            return(view,w,saved.Source);
        }
        void NoSourceBinding()
        {
            foreach(var actor in actors)foreach(var section in actor.World.ViewSnapshot())foreach(var source in sourceIdentities)
                Check.Equal(0,MissionOpenFloorWitness.SourceBindingHeaders(section,source.Goid,source.Key).Count,"No bound source view: no legal source Hidden mutation producer");
        }
        async Task Abort()
        {
            int cursor=player.World.RpcSnapshot().Count;player.SkipReceived();player.SendRpc("80 a6");await player.WaitForRpcAsync("80 a2 08");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={charId} AND payload IS NOT NULL"),"Normal Abort removes current checkpoint");

            NoSourceBinding();operations.Add(new{Phase=phase,RpcCursor=cursor,Records=player.World.RpcSnapshot().Skip(cursor).Select(r=>r.Hex).ToArray()});
        }
        try
        {
            CaptureServer(context.Server,"original");
            player=await context.AddPlayerAsync(new CharacterSeed("OpenFloorBox"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=2,
                X=17000,Y=495,Z=2300,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
            actors.Add(player);charId=player.CharId;handle=player.Handle;
            await context.Database.ExecuteAsync(PosterHuntFixture.SeedPrerequisites(charId));phase="entry";
            await player.EnterWorldAsync();phase="first-ordinary-accept";await Accept();await RideToPublishedRoot();var first=await ViewAtPublishedPlan();var firstSet=await WholeSet();
            phase="first-ordinary-abort";await Abort();
            phase="return-original-offer-origin";await MoveForMissionTest(player,17000,495,2300);
            phase="sameworld-reaccept";await Accept();await RideToPublishedRoot();var second=await ViewAtPublishedPlan();var fixedSet=await WholeSet();
            Check.That(firstSet.Length==fixedSet.Length,"All three natural objects remain present on same-world reaccept");
            Check.Equal(first.Source,second.Source,"Abort/reaccept never rewrites source geometry");NoSourceBinding();
            phase="pre-open-save";int saveCursor=player.World.RpcSnapshot().Count;player.SkipReceived();player.SendChat("?save");await player.WaitForRpcAsync("2e 07");
            string savedPosition=await context.Database.ScalarAsync($"SELECT CONCAT(x+0e0,',',y+0e0,',',z+0e0) FROM characters WHERE charId={charId}");
            double[] savedCoordinates=savedPosition.Split(',').Select(x=>double.Parse(x,CultureInfo.InvariantCulture)).ToArray();
            operations.Add(new{Phase=phase,SaveCursor=saveCursor,SaveRecords=player.World.RpcSnapshot().Skip(saveCursor).Select(r=>r.Hex).ToArray(),SavedDatabasePosition=savedPosition,ExpectedStanding=second.Witness.Standing.ToArray()});
            Check.That(savedCoordinates.SequenceEqual(second.Witness.Standing),"Existing QA save persists exact standing bits before controlled cold shutdown; no SQL pose override");
            phase="pre-open-cold-logout";player.SendRpc("80 fe 00");await player.WaitForRpcAsync("80 fd");player.Dispose();context.Server.Dispose();
            cold=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="201202"});
            CaptureServer(cold,"cold");
            Check.Equal(dll,cold.SourceAssembly,"Cold owned server selects the same frozen source assembly");
            phase="cold-relog";player=await HeadlessPlayer.LoginAsync(cold.Address,"user"+charId,handle);actors.Add(player);creationStart=player.World.CreationSnapshot().Count;await player.EnterWorldAsync();await player.WaitForRpcAsync("80 9c");
            var coldSelf=player.World.CreationSnapshot().Last(v=>v.ViewId==2);
            operations.Add(new{Phase=phase,ColdSelfView=coldSelf.ViewId,coldSelf.Goid,coldSelf.SpawnId,RawAttributes=coldSelf.Attributes.ToDictionary(x=>x.Key,x=>Convert.ToHexString(x.Value)),ExpectedStanding=second.Witness.Standing.ToArray()});
            byte[] coldPosition=coldSelf.Attributes["Position"];
            double[] coldStart=Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(coldPosition,i*8)).ToArray();
            operations.Add(new{Phase=phase,ColdSelfPosition=Convert.ToHexString(coldPosition),ExpectedStanding=second.Witness.Standing.ToArray()});
            Check.That(coldStart.SequenceEqual(second.Witness.Standing),"Cold login starts at the persisted clear standing point, before any fixture movement");
            Check.That(fixedSet.SequenceEqual(await WholeSet()),"Cold restore preserves all three fixed witnesses, no cross-object repacking");
            var again=await ViewAtPublishedPlan();Check.Equal(second.Source,again.Source,"Cold restore leaves source spot byte-for-byte unchanged");
            Check.That(second.Witness.Centre.SequenceEqual(again.Witness.Centre)&&second.Witness.Standing.SequenceEqual(again.Witness.Standing)&&
                second.Witness.Route.SequenceEqual(again.Witness.Route)&&second.Witness.Quaternion==again.Witness.Quaternion&&
                second.Witness.InputFingerprint==again.Witness.InputFingerprint&&second.Witness.RawFingerprint==again.Witness.RawFingerprint,
                "Cold restore replays the fixed persisted placement without replanning");NoSourceBinding();
            for(int objectId=1;objectId<=3;objectId++)
            {
                phase="ordinary-open-object"+objectId;var current=await ViewAtPublishedPlan(objectId);
                int cursor=player.World.RpcSnapshot().Count;player.SkipReceived();player.SendRpc($"80 c7 {MissionNpcId(current.View)} 03 00");
                await player.WaitForRpcAsync($"80 a0 {objectId:x2} 06 00 01");
                Check.That((await MissionCheckpoint(context,player)).Done.Contains((ushort)objectId),"Normal Open completed the authored current Use goal");
                operations.Add(new{Phase=phase,RpcCursor=cursor,Records=player.World.RpcSnapshot().Skip(cursor).Select(r=>r.Hex).ToArray()});
            }
            phase="stationary-final-goal";
            // Several ordinary mission ticks at the last published standing point must not advance.
            await Task.Delay(1600);
            var carried=await MissionCheckpoint(context,player);
            Check.Equal(0,carried.Phase,"Final Open beside the interior elevator remains in phase0 until a real exit");
            Check.That(carried.Flags.GetValueOrDefault("waitingForExit"),"Area goals are complete and waiting for departure");
            Check.That(carried.DepartedSite==null,"No transport departure was invented from stationary proximity");
            phase="ordinary-exit";if(carried.Phase==0){await LeaveMissionArea(player);await player.WaitForRpcAsync("80 9e 00");}
            Check.Equal(1,(await MissionCheckpoint(context,player)).Phase,"All three normal Open Use goals plus exit advance authored second phase");NoSourceBinding();
            foreach(var actor in actors)Check.Equal(0,actor.World.Problems.Count,"Every Box session protocol parser clean");phase="complete";passed=true;
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-mission-box-open-floor-"+context.Database.Name);
            string? finalPayload=null,finalPayloadError=null;
            try{if(charId!=0)finalPayload=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={charId}");}
            catch(Exception read){finalPayloadError=read.ToString();}
            Exception? requiredEvidenceFailure=null;
            if(finalPayloadError!=null)
            {
                passed=false;
                if(failure==null){requiredEvidenceFailure=new ScenarioFailure("Required final raw checkpoint read failed: "+finalPayloadError);failure=requiredEvidenceFailure;}
            }
            try
            {
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Phase=phase,Failure=failure?.ToString(),SourceAssembly=dll,
                    SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=originalSha,SourceMtimeUtc=originalMtime,OriginalProcessId=originalPid,Servers=servers,Database=context.Database.Name,FinalRawPayload=finalPayload,FinalRawPayloadError=finalPayloadError,RawCheckpoints=rawCheckpoints,
                    Checkpoints=checkpoints,Operations=operations,Sessions=actors.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),
                        Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()}),
                    ColdSourceAssembly=cold?.SourceAssembly,OriginalDebug=context.Server.DebugLog(),OriginalOutput=context.Server.FullOutput(),ColdDebug=cold?.DebugLog(),ColdOutput=cold?.FullOutput(),
                    NativeRenderingProven=false,NativePickingProven=false,NativeWalkingProven=false,StaticMutationRequested=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog()+"\n--- Cold ---\n"+cold?.FullOutput()+"\n--- Cold debug ---\n"+cold?.DebugLog());
                Console.WriteLine("BOX OPEN FLOOR EVIDENCE before teardown="+stem);
            }
            catch(Exception evidence){if(failure==null)throw;Console.Error.WriteLine("Box evidence failure: "+evidence);}
            finally{foreach(var actor in actors)actor.Dispose();cold?.Dispose();}
            if(requiredEvidenceFailure!=null)throw requiredEvidenceFailure;
        }
    }
}
