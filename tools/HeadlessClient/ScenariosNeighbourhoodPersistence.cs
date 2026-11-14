using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task NeighbourhoodRetrievalPersistence(ScenarioContext context) => RunNeighbourhoodRetrieval(context,false);
    private static Task MissionComputerAnchorsPersistence(ScenarioContext context) => RunNeighbourhoodRetrieval(context,true);
    private static Task MissionComputer1407Persistence(ScenarioContext context) => RunNeighbourhoodRetrieval(context,true,true);
    private static async Task RunNeighbourhoodRetrieval(ScenarioContext context,bool sourceAnchors,bool source1407=false)
    {
        int contactId=source1407?8:7,missionId=source1407?3082:3072;
        double actorX=source1407?33000:NeighbourhoodMissionFixture.X,actorY=source1407?-305:NeighbourhoodMissionFixture.Y,actorZ=source1407?-100949:NeighbourhoodMissionFixture.Z;
        double nativeX=source1407?40931:91865,nativeY=source1407?3285:4300,nativeZ=source1407?-88280:12660;
        double approachX=source1407?40820:91865,approachY=source1407?3295:4295,approachZ=source1407?-88280:12740;
        var environment=source1407?MissionComputer1407Fixture.Environment:NeighbourhoodMissionFixture.Environment;
        ServerInstance server=context.Server;
        var starts=new List<object>();var checkpointEvidence=new List<object>();
        string startsPath=Path.Combine(Path.GetTempPath(),"mxo-mission-computer-processes-"+context.Database.Name+".json");
        (int Pid,string Assembly,string Configuration,string Hash,string MtimeUtc) serverMetadata=default;
        void CaptureServerStart()
        {
            string assembly=server.SourceAssembly;
            serverMetadata=(server.ProcessId,assembly,Directory.GetParent(Path.GetDirectoryName(assembly)!)!.Name,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),File.GetLastWriteTimeUtc(assembly).ToString("O"));
            starts.Add(new{ProcessId=serverMetadata.Pid,SourceAssembly=serverMetadata.Assembly,SourceConfiguration=serverMetadata.Configuration,
                SourceAssemblySha256=serverMetadata.Hash,SourceAssemblyMtimeUtc=serverMetadata.MtimeUtc});
            File.WriteAllText(startsPath,JsonSerializer.Serialize(new{Database=context.Database.Name,Starts=starts},new JsonSerializerOptions{WriteIndented=true}));
        }
        CaptureServerStart(); // Before actor setup/login; survives any later failure or process disposal.
        var original=await context.AddPlayerAsync(new CharacterSeed("NeighbourhoodRun")
        {
            X=actorX,Y=actorY,Z=actorZ,Rotation=source1407?64:128,
            Level=10,Experience=NeighbourhoodMissionFixture.Experience,Organization=1,Health=2000,MaxHealth=2000,InnerStrength=1000,MaxInnerStrength=1000,
            Items=new[]{(90,6831u,1)}
        });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={original.CharId}");
        await LoadAbilitiesAsync(context,original,NeighbourhoodMissionFixture.Abilities);await original.EnterWorldAsync();
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        HeadlessPlayer p=original;
        var extraServers=new List<ServerInstance>();var sessions=new List<object>();var processes=new List<object>();
        var contactSelections=new List<object>();HeadlessPlayer? lastContactSession=null;
        byte[]? automaticCompletion=null;var exitBranches=new List<object>();
        var resolvedSearches=new List<object>();var completions=new List<object>();var lifetimes=new List<object>();var fields=new JsonSerializerOptions{IncludeFields=true};bool passed=false;bool sourceAreaEntered=false;object? oldCheckpointProof=null;
        void Record()
        {
            sessions.Add(new{p.Handle,p.CharId,Problems=p.World.Problems.ToArray(),Rpcs=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray()});
            processes.Add(new{ProcessId=serverMetadata.Pid,SourceAssembly=serverMetadata.Assembly,SourceConfiguration=serverMetadata.Configuration,
                SourceAssemblySha256=serverMetadata.Hash,SourceAssemblyMtimeUtc=serverMetadata.MtimeUtc,Output=server.FullOutput(),Debug=server.DebugLog()});
        }
        void CheckProblems()
        {
            if(source1407)Check.That(!p.ViewsOfGoid(1407).Any(),"No mission-created bound source1407 role at any lifecycle stage");
            Check.Equal(0,p.World.Problems.Count,"Current owned session protocol problems");
            foreach(string error in new[]{"Packet handling failed","Visibility pass failed","Unhandled exception","[MISSION] Tick failed","Completion transaction deferred"})
                Check.Equal(0,server.OutputLines(error).Count,"Current owned server: "+error);
        }
        async Task<MissionProgress.SavedState> CaptureCheckpoint(string stage)
        {
            string raw=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={p.CharId}");
            string path=Path.Combine(Path.GetTempPath(),"mxo-mission-computer-checkpoint-"+context.Database.Name+"-"+checkpointEvidence.Count+".json");
            await File.WriteAllTextAsync(path,raw); // Exact payload is durable BEFORE decode or witness assertions.
            checkpointEvidence.Add(new{Stage=stage,Path=path,Raw=raw,Sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))});
            return JsonSerializer.Deserialize<MissionProgress.SavedState>(raw,fields)!;
        }
        async Task Restart(bool active)
        {
            p.SendChat("?save");await p.WaitForRpcAsync("2e 07");CheckProblems();Record();
            int ownedPid=serverMetadata.Pid;
            p.Dispose();server.Dispose(); // Only this Harness-owned process; no native fixture or external process.
            bool gone=false;try{using var owned=Process.GetProcessById(ownedPid);gone=owned.HasExited;}catch(ArgumentException){gone=true;}
            Check.That(gone,"Previous owned Harness process exited before replacement starts");lifetimes.Add(new{DisposedPid=ownedPid,ExitedBeforeNext=true});
            server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,environment);extraServers.Add(server);CaptureServerStart();
            p=await HeadlessPlayer.LoginAsync(server.Address,username,original.Handle);await p.EnterWorldAsync();
            if(active){await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");}
        }
        async Task OfferAndAccept()
        {
            // Capture BEFORE return movement: CreationSnapshot is historical, not the current live-view table.
            int contactCursor=source1407&&ReferenceEquals(lastContactSession,p)?p.World.CreationSnapshot().Count:0;
            await MoveForMissionTest(p,actorX,actorY,actorZ);
            ViewCreation contact=null!;
            await p.WaitUntilAsync(()=>
            {
                contact=(source1407?MissionComputer1407Fixture.ContactSince(p.World.CreationSnapshot(),contactCursor):
                    p.ViewsOfGoid(6520).LastOrDefault(v=>v.Attributes.TryGetValue("ContactID",out var id)&&BitConverter.ToUInt32(id)==contactId))!;
                return contact!=null;
            },"Actual source contact view "+contactId,TimeSpan.FromSeconds(10));
            int requestCursor=p.World.RpcSnapshot().Count;
            lastContactSession=p;contactSelections.Add(new{CursorBeforeMovement=contactCursor,RpcCursorBeforeUse=requestCursor,ContactId=contactId,contact.ViewId,contact.SpawnId});
            p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(contact)} 02 00");await p.WaitForRpcAsync("80 95");await p.WaitForRpcAsync("80 97 00 00");
            var entry=source1407?MissionComputer1407Fixture.RetrievalSince(p.World.RpcSnapshot(),requestCursor):p.Received("80 96").Single(r=>
            {
                int at=BitConverter.ToUInt16(r.Bytes,6),length=BitConverter.ToUInt16(r.Bytes,at);
                return Encoding.ASCII.GetString(r.Bytes,at+2,length-1)=="Retrieval";
            });
            Check.Equal((byte)0,entry.Bytes[8],"Neighborhood org0");Check.Equal((byte)0,entry.Bytes[14],"Standard plot0");
            ushort index=BitConverter.ToUInt16(entry.Bytes,4);
            p.SendRpc($"80 98 06 00 {Le(index)} 01 02 00");await p.WaitForRpcAsync("80 99");
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");
            Check.Equal(missionId,(await MissionCheckpoint(context,p)).Mission,"Normal list selection accepted canonical Retrieval "+missionId);
        }
        async Task<ViewCreation> Search(int creationStart,HeadlessPlayer creationOwner,bool performTake=true)
        {
            automaticCompletion=null;bool exitsAtWitness=false;
            if(!sourceAnchors || !sourceAreaEntered)await EnterMissionArea(server,p);
            sourceAreaEntered=true;
            ViewCreation computer;
            if(sourceAnchors)
            {
                string expectedQuaternion=source1407?"00000000F304353F00000000F20435BF":"00000000F304353300000000FFFF7F3F";
                MissionOpenFloorPlacement? resolved=null;
                if(sourceAnchors)
                {
                    var storedObject=(await CaptureCheckpoint("BeforeResolvedWitness")).Objects.Single(o=>o.Id==1);
                    Check.Equal(source1407?133170595u:363860342u,storedObject.Spot.mxoStaticId,"Same normally chosen source key at every acceptance");
                    Check.Equal(source1407?(40931d,3430d,-88280d):(92740d,4445d,13535d),(storedObject.Spot.pos_x,storedObject.Spot.pos_y,storedObject.Spot.pos_z),"Every fixed result retains the exact raw source spot");
                    Check.Equal(source1407?"00000000f204353f00000000f304353f":"00000000f20435bf00000000f304353f",storedObject.Spot.quat.ToLowerInvariant(),"Raw source quaternion remains byte-exact");
                    resolved=storedObject.OpenFloor;
                    Check.That(resolved!=null,"Normally accepted Computer has a durable fixed open-floor plan");
                    Check.Equal(storedObject.Spot.mxoStaticId,resolved!.SourceKey,"Fixed pose belongs to the exact saved source");
                    Check.Equal((ushort)3,resolved.ConcreteGoid,"Fixed pose belongs to the free GO3 proxy");
                    Check.Equal((ushort)1,resolved.ObjectId,"Fixed pose belongs to actual mission object1");
                    var centre=resolved!.Centre;var witness=resolved.Standing;
                    nativeX=centre[0];nativeY=centre[1];nativeZ=centre[2];
                    approachX=witness[0];approachY=witness[1];approachZ=witness[2];expectedQuaternion=resolved.Quaternion;
                    Check.Equal(source1407?"00000000f304353f00000000f20435bf":"00000000f304353300000000ffff7f3f",expectedQuaternion.ToLowerInvariant(),"Audited full native quaternion is not normalized or replaced");
                }
                // Planned NAV point/segment witness, not native walking/LOS certification.
                await MoveForMissionTest(p,approachX,approachY,approachZ);
                if(sourceAnchors)
                {
                    int currentCursor=ReferenceEquals(p,creationOwner)?creationStart:0;
                    ViewCreation? fresh=null;
                    await p.WaitUntilAsync(()=>{fresh=MissionComputer1407Fixture.ComputerSince(p.World.CreationSnapshot(),currentCursor,nativeX,nativeY,nativeZ,expectedQuaternion);return fresh!=null;},"Fresh Computer at committed open-floor pose",TimeSpan.FromSeconds(10));
                    computer=fresh!;
                    resolvedSearches.Add(new{Source=resolved!.SourceKey,Resolved=resolved,CreationCursor=currentCursor,computer.ViewId,computer.SpawnId,ActualPosition=Convert.ToHexString(computer.Attributes["Position"]),ActualQuaternion=Convert.ToHexString(computer.Attributes["Orientation"])});
                }
                else
                {
                    await p.WaitUntilAsync(()=>p.ViewsOfGoid(3).Any(v=>v.Attributes.TryGetValue("Position",out var pos)&&BitConverter.ToDouble(pos)==nativeX&&BitConverter.ToDouble(pos,8)==nativeY&&BitConverter.ToDouble(pos,16)==nativeZ),"Resolved source computer view",TimeSpan.FromSeconds(10));
                    computer=p.ViewsOfGoid(3).Last(v=>v.Attributes.TryGetValue("Position",out var pos)&&BitConverter.ToDouble(pos)==nativeX&&BitConverter.ToDouble(pos,16)==nativeZ);
                }
                Check.Equal(expectedQuaternion.ToUpperInvariant(),Convert.ToHexString(computer.Attributes["Orientation"]),"Full committed quaternion bytes");
            }
            else computer=await ZionObject(server,p,1,creationStart);
            byte use=ObjectStates.UseNumber(computer.Goid,"Take")??throw new ScenarioFailure("No native Take action on the actual mission computer");
            uint State()
            {
                uint state=BitConverter.ToUInt32(computer.Attributes["CurrentState"]);byte[] prefix=WireFormat.FromHex($"{Le(computer.ViewId)} 01 08");
                foreach(var section in p.World.ViewSnapshot())for(int at=BytePattern.IndexOf(section,prefix,0);at>=0;at=BytePattern.IndexOf(section,prefix,at+1))
                    if(at+prefix.Length+4<=section.Length)state=BitConverter.ToUInt32(section,at+prefix.Length);
                return state;
            }
            await p.WaitUntilAsync(()=>ObjectStates.Offered(computer.Goid,State()).Contains(use),"Actual computer Take enabled",TimeSpan.FromSeconds(10));
            // Use the actual NAV witness; the existing exit law determines active carry versus immediate completion.
            var checkpoint=await MissionCheckpoint(context,p);var position=computer.Attributes["Position"];
            double ox=BitConverter.ToDouble(position),oy=BitConverter.ToDouble(position,8),oz=BitConverter.ToDouble(position,16);
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            (double X,double Y,double Z)? standing=null;
            if(sourceAnchors)
            {
                Check.That(NavMesh.Locate(1,approachX,approachY,approachZ,out var nav)&&Math.Abs(nav.floorY+95-approachY)<1e-7,"Persisted planned witness is still exactly grounded");
                exitsAtWitness=MissionExit.HasLeft(checkpoint.Waypoints.ToList(),checkpoint.Exits.ToList(),approachX,approachY,approachZ);
                if(source1407)Check.That(!exitsAtWitness,"Planned witness permits carry/relog before ordinary exit");
                standing=(approachX,approachY,approachZ);
            }
            else
            foreach(var offset in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(300d,0d),(-300d,0d),(0d,300d),(0d,-300d),(212d,212d),(-212d,-212d),(212d,-212d),(-212d,212d)})
            {
                double x=ox+offset.Item1,z=oz+offset.Item2;
                if(NavMesh.Locate(1,x,oy,z,out var nav)&&!MissionExit.HasLeft(checkpoint.Waypoints.ToList(),checkpoint.Exits.ToList(),x,nav.floorY+95,z))
                {standing=(x,nav.floorY+95,z);break;}
            }
            Check.That(standing.HasValue,"Actual computer has a qualified NAV standing point");
            await MoveForMissionTest(p,standing!.Value.X,standing.Value.Y,standing.Value.Z);
            if(!performTake)return computer;
            int takeCursor=p.World.RpcSnapshot().Count;
            p.SendRpc($"80 c7 {MissionNpcId(computer)} {use:x2} 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");
            exitBranches.Add(new{Standing=new[]{standing.Value.X,standing.Value.Y,standing.Value.Z},HasLeft=exitsAtWitness,Automatic=sourceAnchors&&!source1407&&exitsAtWitness,RpcCursor=takeCursor});
            if(sourceAnchors&&!source1407&&exitsAtWitness)
            {
                await p.WaitUntilAsync(()=>{automaticCompletion=MissionComputer1407Fixture.AutomaticExitSince(p.World.RpcSnapshot(),takeCursor);return automaticCompletion!=null;},"Current Take goal tick then ordinary automatic completion",TimeSpan.FromSeconds(12));
                return computer;
            }
            var saved=await MissionCheckpoint(context,p);Check.That(saved.Done.Contains(1)&&saved.Items.ContainsKey(6831),"Actual completed Search and owned data item checkpoint");
            Check.Equal(2L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831"),"One quest item plus unrelated identical item");
            return computer;
        }
        async Task<string> Finish(int run)
        {
            sourceAreaEntered=false;
            byte[] endBytes;
            if(automaticCompletion!=null)endBytes=automaticCompletion;
            else{await LeaveMissionArea(p);endBytes=(await p.WaitForRpcAsync("80 a2 00",TimeSpan.FromSeconds(12))).Bytes;}
            Check.Equal(6300u,BitConverter.ToUInt32(endBytes,11),"Existing level10 info policy");Check.Equal(4040u,BitConverter.ToUInt32(endBytes,15),"Existing level10 XP floor");
            Check.Equal(1282500L+run*4040,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Exactly one XP pair per run");
            Check.Equal(1000L+run*6300,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Exactly one info pair per run");
            Check.Equal((long)run,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={missionId}"),"Earned repeated completion count");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831 AND slot=90"),"Unrelated identical item survives owned cleanup");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831"),"Only actual quest item removed");
            string receipt=await context.Database.ScalarAsync($"SELECT receipt FROM char_active_missions WHERE charId={p.CharId} AND payload IS NULL");
            completions.Add(new{Run=run,Receipt=receipt,Summary=Convert.ToHexString(endBytes)});return receipt;
        }
        void Replay(string receipt,bool active)
        {
            // Invoke only the existing repository against this owned disposable DB, never a client reward command.
            Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned disposable replay repository");
            var config=new ServerConfig(Path.Combine(context.RepositoryRoot,"hds","bin","Debug","net8.0","Config.xml.dist"));config.LoadDbParams();config.dbParams.DatabaseName=context.Database.Name;
            var old=hds.shared.Store.config;hds.shared.Store.config=config;
            try
            {
                var access=new hds.databases.MyWorldDbAccess();
                if(active)
                {
                    bool refused=false;try{access.CompleteActiveMission(p.CharId,receipt,4040,6300);}catch(MissionCheckpointException){refused=true;}
                    Check.That(refused,"An old finish cannot replace the new active identity");
                }
                else Check.That(access.CompleteActiveMission(p.CharId,receipt,4040,6300).AlreadyCommitted,"Duplicate finish returns existing receipt");
            }
            finally{hds.shared.Store.config=old;}
        }
        try
        {
            HeadlessPlayer cursorSession=p;int cursor=p.World.CreationSnapshot().Count;await OfferAndAccept();var first=await MissionCheckpoint(context,p);
            if(sourceAnchors)
            {
                await EnterMissionArea(server,p);sourceAreaEntered=true;
                var old=await MissionCheckpoint(context,p);var source=old.Objects.Single(o=>o.Id==1).Spot;
                Check.Equal(source1407?(40931d,3430d,-88280d):(92740d,4445d,13535d),(source.pos_x,source.pos_y,source.pos_z),"Old raw legacy spot remains checkpoint authority");
                Check.Equal(source1407?133170595u:363860342u,source.mxoStaticId,"Exact normally selected source key");
                Check.Equal((ushort)(source1407?1407:1415),NumericalUtils.ByteArrayToUint16(source.type,1),"Exact normally selected source type");
                string before=JsonSerializer.Serialize(old.Objects,fields);await Restart(true);
                var reloaded=await MissionCheckpoint(context,p);Check.Equal(before,JsonSerializer.Serialize(reloaded.Objects,fields),"Cold restore never rewrites source spot");
                Check.Equal(old.Identity,reloaded.Identity,"Old checkpoint restored before Search");
                oldCheckpointProof=new{old.Identity,SourceSpot=source,Before=before,After=JsonSerializer.Serialize(reloaded.Objects,fields)};
            }
            var computer=await Search(cursor,cursorSession);
            if(automaticCompletion==null)
            {
            var carried=await MissionCheckpoint(context,p);
            await Restart(true);var restored=await MissionCheckpoint(context,p);
            Check.Equal(carried.Identity,restored.Identity,"Same acceptance after cold process restart");Check.Equal(carried.Instance,restored.Instance,"Same mission key");
            Check.Equal(JsonSerializer.Serialize(carried.Objects,fields),JsonSerializer.Serialize(restored.Objects,fields),"Exact object placement/state preserved");
            Check.Equal(JsonSerializer.Serialize(carried.Mobs),JsonSerializer.Serialize(restored.Mobs),"Exact placed NPC identity/health/loot preserved");
            Check.Equal(carried.Items[6831],restored.Items[6831],"Same owned item slot");Check.That(restored.Done.Contains(1),"Search remains done");
            Check.That(!server.DebugLog().Contains(" listed "),"Cold restore resolved before any contact list could populate its private cache");
            }
            bool firstAutomatic=automaticCompletion!=null;
            string receipt1=await Finish(1);if(!sourceAnchors)Replay(receipt1,false);
            if(firstAutomatic)
            {
                await Restart(false);
                Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId} AND payload IS NOT NULL"),"Completed cold no active mission");
                Check.Equal(receipt1,await context.Database.ScalarAsync($"SELECT receipt FROM char_active_missions WHERE charId={p.CharId} AND payload IS NULL"),"Completed cold receipt unchanged");
                Check.Equal(0,p.World.RpcSnapshot().Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&r.Bytes[1]==0xa2),"Completed cold session never replays a completion");
                Check.Equal(1286540L,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Completed cold no extra XP");
                Check.Equal(7300L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Completed cold no extra Info");
            }
            cursorSession=p;cursor=p.World.CreationSnapshot().Count;await OfferAndAccept();var second=await MissionCheckpoint(context,p);Check.That(first.Identity!=second.Identity,"Repeat uses a new durable identity");if(!sourceAnchors)Replay(receipt1,true);
            await Search(cursor,cursorSession);string receipt2=await Finish(2);Check.That(receipt1!=receipt2,"Each actual run has its own receipt");if(!sourceAnchors)Replay(receipt2,false);
            if(source1407)
            {
                // Same live world: another fresh independent proxy after two earned Takes/exits, then normal Abort.
                cursorSession=p;cursor=p.World.CreationSnapshot().Count;await OfferAndAccept();
                var third=await MissionCheckpoint(context,p);Check.That(third.Identity!=second.Identity,"Reaccept after Take/exit has a new identity without relog");
                await Search(cursor,cursorSession,false);
                p.SendRpc("80 a6");await p.WaitForRpcAsync("80 a2 08");
                Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId} AND payload IS NOT NULL"),"Ordinary Abort clears only active mission");
                Check.Equal(2L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={missionId}"),"Abort adds no completion receipt");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831 AND slot=90"),"Abort preserves unrelated identical item");
                sourceAreaEntered=false;
            }
            await Restart(false);p.SendChat("?missionpersist status");await p.WaitForTextAsync("Mission persistence: none");
            Check.Equal(1290580L,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Duplicate finish/relog never pays XP again");
            Check.Equal(13600L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Duplicate finish/relog never pays info again");
            CheckProblems();passed=true;
        }
        finally
        {
            try
            {
                Record();string path=Path.Combine(Path.GetTempPath(),(source1407?"mxo-mission-computer-1407-":sourceAnchors?"mxo-mission-computer-anchors-":"mxo-neighbourhood-persistence-")+context.Database.Name+".json");
                await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Passed=passed,SourceAnchors=sourceAnchors,Source1407=source1407,OldCheckpointProof=oldCheckpointProof,ResolvedSearches=resolvedSearches,ExitBranches=exitBranches,CheckpointEvidence=checkpointEvidence,ServerStarts=starts,StartsPath=startsPath,Database=context.Database.Name,Sessions=sessions,Processes=processes,Completions=completions,Lifetimes=lifetimes,NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Neighborhood proof: "+path);
            }
            finally
            {
                try {if(p!=original)p.Dispose();}
                finally {foreach(var owned in extraServers)owned.Dispose();}
            }
        }
    }
}
