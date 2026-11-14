using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MissionSafeAnchorsScenario(ScenarioContext context)
    {
        var player=await context.AddPlayerAsync(new CharacterSeed("SafeAnchors"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=2,
            X=17000,Y=495,Z=2300,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync(MachinesChainFixture.SeedPrerequisites(player.CharId));
        string phase="entry";Exception? failure=null;bool passed=false;var fields=new JsonSerializerOptions{IncludeFields=true};
        var rawCheckpoints=new List<object>();var checkpoints=new List<object>();var operations=new List<object>();var actors=new List<HeadlessPlayer>{player};ServerInstance? cold=null;
        string dll=context.Server.SourceAssembly;
        string sourceHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)));
        DateTime sourceMtime=File.GetLastWriteTimeUtc(dll);int originalPid=context.Server.ProcessId;int? coldPid=null;
        string? coldDll=null,coldHash=null;DateTime? coldMtime=null;
        uint charId=player.CharId;string handle=player.Handle;int creationStart=0;
        async Task<(MissionProgress.SavedState State,MissionOpenFloorWitness Witness,string Source)> Saved()
        {
            string payload=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={charId}");
            rawCheckpoints.Add(new{Phase=phase,Payload=payload}); // Preserve before decoding or any assertion.
            using var json=JsonDocument.Parse(payload);var root=json.RootElement;
            Check.Equal(2201,root.GetProperty("Mission").GetInt32(),"Normal Agent Gray accepted2201");
            Check.Equal(0,root.GetProperty("Phase").GetInt32(),"Open-floor Safe proof remains in phase0 before Take");
            var obj=root.GetProperty("Objects").EnumerateArray().Single(x=>x.GetProperty("Id").GetInt32()==1);
            var witness=MissionOpenFloorWitness.Read(obj.GetProperty("OpenFloor"));witness.Require(2201,0,1,0x0d10003e,1410,552);
            var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,fields)!;var source=state.Objects.Single(x=>x.Id==1);
            Check.Equal((ushort)552,source.Goid,"Ordinary source1410 Safe prototype552");
            Check.Equal(0x0d10003eu,source.Spot.mxoStaticId,"Unchanged normal seed source; no forced spot");
            Check.Equal((27300d,2146d,13100d),(source.Spot.pos_x,source.Spot.pos_y,source.Spot.pos_z),"Legacy source checkpoint remains immutable");
            Check.That(MissionSafeAnchors.TryCurrent(source.Spot,552,out var native),"Exact audited source1410 identity/provenance");
            Check.That(Convert.FromHexString(witness.Quaternion).SequenceEqual(native.Orientation),"Published placement preserves full native source quaternion");
            string rawSource=obj.GetProperty("Spot").GetRawText();checkpoints.Add(new{Phase=phase,Payload=payload,Witness=witness.RawJson,Source=rawSource});
            return(state,witness,rawSource);
        }
        async Task Accept()
        {
            creationStart=player.World.CreationSnapshot().Count;int cursor=player.World.RpcSnapshot().Count;player.SkipReceived();player.SendRpc("80 94 05 00 02");await player.WaitForRpcAsync("80 97 05 00");
            var entries=player.World.RpcSnapshot().Skip(cursor).Where(r=>r.Bytes.Length>17&&r.Bytes[0]==0x80&&r.Bytes[1]==0x96&&BitConverter.ToUInt16(r.Bytes,2)==5).Select(r=>r.Bytes).ToArray();
            int index=MissionContactChoice.CriticalIndex(entries,"The End of the Beginning");
            player.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await player.WaitForRpcAsync("80 99");
            player.SendRpc("80 9b 07 00 01");await player.WaitForRpcAsync("80 9e 00");
            Check.Equal(2201L,await context.Database.LongAsync($"SELECT JSON_EXTRACT(payload,'$.Mission') FROM char_active_missions WHERE charId={charId}"),"Current selected Critical definition, not historical offer");
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
        async Task<(ViewCreation View,MissionOpenFloorWitness Witness,string Source)> ViewAtPublishedPlan()
        {
            var saved=await Saved();var w=saved.Witness;
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            Check.That(NavMesh.Locate(1,w.Standing[0],w.Standing[1],w.Standing[2],out var nav),"Published standing point is on actual NAV");
            Check.Equal(nav.floorY+95,w.Standing[1],"Published standing height follows actual floor");
            Check.Equal(nav.floorY+50,w.Centre[1],"Safe base uses exact measured support minY-50");
            // Existing ?tp headless movement follows published route points; it is not native movement/walking/scene proof.
            for(int i=0;i<w.Route.Count;i+=3)await MoveForMissionTest(player,w.Route[i],w.Route[i+1],w.Route[i+2]);
            bool AtPlan(ViewCreation v)=>v.Goid==552&&v.Attributes.TryGetValue("Position",out var p)&&p.Length==24&&
                BitConverter.ToDouble(p)==w.Centre[0]&&BitConverter.ToDouble(p,8)==w.Centre[1]&&BitConverter.ToDouble(p,16)==w.Centre[2];
            await player.WaitUntilAsync(()=>player.World.CreationSnapshot().Skip(creationStart).Any(AtPlan),"Current Safe at published open-floor centre",TimeSpan.FromSeconds(10));
            var view=player.World.CreationSnapshot().Skip(creationStart).Last(AtPlan);
            Check.That(view.Attributes["Orientation"].SequenceEqual(Convert.FromHexString(w.Quaternion)),"Actual03 exact persisted native quaternion");
            byte use=ObjectStates.UseNumber(552,"Open")??throw new ScenarioFailure("552 lacks sourced Open");Check.Equal((byte)3,use,"Unchanged native Open3");
            uint state=view.Attributes.TryGetValue("CurrentState",out var s)?BitConverter.ToUInt32(s):ObjectStates.DefaultState(552);
            Check.Equal(0x835u,state,"Existing received/default Closed835");Check.That(ObjectStates.Offered(552,state).Contains(use),"Closed state offers Open3");
            Check.Equal(100f,ObjectStates.FindUsable(552,use).maxDistance,"Existing authored native range100 unchanged");
            byte[] standing=w.Standing.SelectMany(BitConverter.GetBytes).ToArray();
            Check.That(ContainerActions.InReach(standing,w.Centre[0],w.Centre[1],w.Centre[2]),"Existing server container reach admits published standing witness");
            operations.Add(new{Phase=phase,CreationCursor=creationStart,view.ViewId,view.SpawnId,Position=Convert.ToHexString(view.Attributes["Position"]),Orientation=Convert.ToHexString(view.Attributes["Orientation"]),Witness=w.RawJson});
            return(view,w,saved.Source);
        }
        void NoSourceBinding()
        {
            foreach(var actor in actors)foreach(var section in actor.World.ViewSnapshot())
                Check.Equal(0,MissionOpenFloorWitness.SourceBindingHeaders(section,1410,0x0d10003e).Count,"No bound source1410 view: no legal source Hidden mutation producer");
        }
        async Task Abort()
        {
            int cursor=player.World.RpcSnapshot().Count;player.SkipReceived();player.SendRpc("80 a6");await player.WaitForRpcAsync("80 a2 08");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={charId} AND payload IS NOT NULL"),"Normal Abort removes current checkpoint");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={charId} AND goid=6799"),"Abort has no invented reward item");
            NoSourceBinding();operations.Add(new{Phase=phase,RpcCursor=cursor,Records=player.World.RpcSnapshot().Skip(cursor).Select(r=>r.Hex).ToArray()});
        }
        try
        {
            await player.EnterWorldAsync();phase="first-ordinary-accept";await Accept();await RideToPublishedRoot();var first=await ViewAtPublishedPlan();
            phase="first-ordinary-abort";await Abort();
            phase="return-original-offer-origin";await MoveForMissionTest(player,17000,495,2300);
            phase="sameworld-reaccept";await Accept();await RideToPublishedRoot();var second=await ViewAtPublishedPlan();
            Check.Equal(first.Source,second.Source,"Abort/reaccept never rewrites source geometry");NoSourceBinding();
            phase="fixture-save-published-standing";
            async Task<double[]> StoredPosition()=> (await context.Database.ScalarAsync($"SELECT x+0e0,y+0e0,z+0e0 FROM characters WHERE charId={charId}"))
                .Split('\t').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray();
            var beforeSave=await StoredPosition();int saveCursor=player.World.RpcSnapshot().Count;
            player.SkipReceived();player.SendChat("?save");
            await player.WaitForRpcAsync("2e 0700000000000000000000006400002E00240000000000000000000000000000000000");
            var stored=await StoredPosition();double[] nativeStanding=second.Witness.Standing.Select(v=>(double)(float)v).ToArray();
            operations.Add(new{Phase=phase,SaveCursor=saveCursor,BeforeSave=beforeSave,SavedPosition=stored,NativeF32Standing=nativeStanding,
                PublishedStanding=second.Witness.Standing.ToArray(),Records=player.World.RpcSnapshot().Skip(saveCursor).Select(r=>r.Hex).ToArray()});
            Check.That(stored.SequenceEqual(nativeStanding)&&stored.SequenceEqual(second.Witness.Standing),"Existing save persists the exact representable published standing point before owned cold stop");
            // QA ?tp needs explicit existing save preparation; this is not native full-jackout proof.
            phase="pre-open-cold-logout";player.SendRpc("80 fe 00");await player.WaitForRpcAsync("80 fd");player.Dispose();context.Server.Dispose();
            cold=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="220110"});
            coldPid=cold.ProcessId;coldDll=cold.SourceAssembly;coldHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(coldDll)));coldMtime=File.GetLastWriteTimeUtc(coldDll);
            Check.Equal(dll,cold.SourceAssembly,"Cold owned server selects the same frozen source assembly");
            phase="cold-relog";player=await HeadlessPlayer.LoginAsync(cold.Address,"user"+charId,handle);actors.Add(player);creationStart=player.World.CreationSnapshot().Count;await player.EnterWorldAsync();await player.WaitForRpcAsync("80 9c");
            var coldSelf=player.World.CreationSnapshot().Last(v=>v.ViewId==2);
            byte[] coldPosition=coldSelf.Attributes["Position"];
            double[] coldStart=Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(coldPosition,i*8)).ToArray();
            operations.Add(new{Phase=phase,ColdSelfPosition=Convert.ToHexString(coldPosition),ColdCoordinates=coldStart,ExpectedStanding=second.Witness.Standing.ToArray()});
            Check.That(coldStart.SequenceEqual(second.Witness.Standing),"Cold login starts at the persisted clear standing point, before any fixture movement");
            var again=await ViewAtPublishedPlan();Check.Equal(second.Source,again.Source,"Cold restore leaves source spot byte-for-byte unchanged");
            Check.That(second.Witness.Centre.SequenceEqual(again.Witness.Centre)&&second.Witness.Standing.SequenceEqual(again.Witness.Standing)&&
                second.Witness.Route.SequenceEqual(again.Witness.Route)&&second.Witness.Quaternion==again.Witness.Quaternion&&
                second.Witness.InputFingerprint==again.Witness.InputFingerprint&&second.Witness.RawFingerprint==again.Witness.RawFingerprint,
                "Cold restore replays the fixed persisted placement without replanning");NoSourceBinding();
            phase="ordinary-open";int openCursor=player.World.RpcSnapshot().Count;player.SkipReceived();player.SendRpc($"80 c7 {MissionNpcId(again.View)} 03 00");await player.WaitForRpcAsync("81 19");
            phase="ordinary-take";player.SendRpc("81 1f");await player.WaitForRpcAsync("80 a0 01 06 00 01");
            var carried=await MissionCheckpoint(context,player);Check.That(carried.Items.ContainsKey(6799),"Normal Open/Take earned authored dossier6799");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={charId} AND goid=6799"),"Exactly one earned dossier");
            operations.Add(new{Phase=phase,OpenCursor=openCursor,Records=player.World.RpcSnapshot().Skip(openCursor).Select(r=>r.Hex).ToArray()});
            phase="ordinary-exit";if(carried.Phase==0){await LeaveMissionArea(player);await player.WaitForRpcAsync("80 9e 00");}
            Check.Equal(1,(await MissionCheckpoint(context,player)).Phase,"Normal exit/proximity advances authored second phase");NoSourceBinding();
            foreach(var actor in actors)Check.Equal(0,actor.World.Problems.Count,"Every Safe session protocol parser clean");phase="complete";passed=true;
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-mission-safe-open-floor-"+context.Database.Name);
            try
            {
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Phase=phase,Failure=failure?.ToString(),SourceAssembly=dll,
                    SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=sourceHash,SourceMtimeUtc=sourceMtime,OriginalProcessId=originalPid,ColdProcessId=coldPid,
                    RawCheckpoints=rawCheckpoints,Checkpoints=checkpoints,Operations=operations,Sessions=actors.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),
                        Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()}),
                    ColdSourceAssembly=coldDll,ColdSourceSha256=coldHash,ColdSourceMtimeUtc=coldMtime,OriginalDebug=context.Server.DebugLog(),OriginalOutput=context.Server.FullOutput(),ColdDebug=cold?.DebugLog(),ColdOutput=cold?.FullOutput(),
                    NativeRenderingProven=false,NativePickingProven=false,NativeWalkingProven=false,StaticMutationRequested=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog()+"\n--- Cold ---\n"+cold?.FullOutput()+"\n--- Cold debug ---\n"+cold?.DebugLog());
                Console.WriteLine("SAFE OPEN FLOOR EVIDENCE before teardown="+stem);
            }
            catch(Exception evidence){if(failure==null)throw;Console.Error.WriteLine("Safe evidence failure: "+evidence);}
            finally{foreach(var actor in actors)actor.Dispose();cold?.Dispose();}
        }
    }
}
