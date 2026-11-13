using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task NeighbourhoodRetrievalPersistence(ScenarioContext context)
    {
        var original=await context.AddPlayerAsync(new CharacterSeed("NeighbourhoodRun")
        {
            X=NeighbourhoodMissionFixture.X,Y=NeighbourhoodMissionFixture.Y,Z=NeighbourhoodMissionFixture.Z,Rotation=128,
            Level=10,Experience=NeighbourhoodMissionFixture.Experience,Organization=1,Health=2000,MaxHealth=2000,InnerStrength=1000,MaxInnerStrength=1000,
            Items=new[]{(90,6831u,1)}
        });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={original.CharId}");
        await LoadAbilitiesAsync(context,original,NeighbourhoodMissionFixture.Abilities);await original.EnterWorldAsync();
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        HeadlessPlayer p=original;ServerInstance server=context.Server;
        var extraServers=new List<ServerInstance>();var sessions=new List<object>();var processes=new List<object>();
        var completions=new List<object>();var lifetimes=new List<object>();var fields=new JsonSerializerOptions{IncludeFields=true};bool passed=false;
        void Record()
        {
            sessions.Add(new{p.Handle,p.CharId,Problems=p.World.Problems.ToArray(),Rpcs=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray()});
            processes.Add(new{SourceAssembly=server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(server.SourceAssembly)!)!.Name,
                SourceAssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(server.SourceAssembly))),Output=server.FullOutput(),Debug=server.DebugLog()});
        }
        void CheckProblems()
        {
            Check.Equal(0,p.World.Problems.Count,"Current owned session protocol problems");
            foreach(string error in new[]{"Packet handling failed","Visibility pass failed","Unhandled exception","[MISSION] Tick failed","Completion transaction deferred"})
                Check.Equal(0,server.OutputLines(error).Count,"Current owned server: "+error);
        }
        async Task Restart(bool active)
        {
            p.SendChat("?save");await p.WaitForRpcAsync("2e 07");CheckProblems();Record();
            int ownedPid=server.ProcessId;
            p.Dispose();server.Dispose(); // Only this Harness-owned process; no native fixture or external process.
            bool gone=false;try{using var owned=Process.GetProcessById(ownedPid);gone=owned.HasExited;}catch(ArgumentException){gone=true;}
            Check.That(gone,"Previous owned Harness process exited before replacement starts");lifetimes.Add(new{DisposedPid=ownedPid,ExitedBeforeNext=true});
            server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,NeighbourhoodMissionFixture.Environment);extraServers.Add(server);
            p=await HeadlessPlayer.LoginAsync(server.Address,username,original.Handle);await p.EnterWorldAsync();
            if(active){await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");}
        }
        async Task OfferAndAccept()
        {
            await MoveForMissionTest(p,NeighbourhoodMissionFixture.X,NeighbourhoodMissionFixture.Y,NeighbourhoodMissionFixture.Z);
            ViewCreation contact=null!;
            await p.WaitUntilAsync(()=>
            {
                contact=p.ViewsOfGoid(6520).LastOrDefault(v=>v.Attributes.TryGetValue("ContactID",out var id)&&BitConverter.ToUInt32(id)==7)!;
                return contact!=null;
            },"Actual The Chessman contact view",TimeSpan.FromSeconds(10));
            p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(contact)} 02 00");await p.WaitForRpcAsync("80 95");await p.WaitForRpcAsync("80 97 00 00");
            var entry=p.Received("80 96").Single(r=>
            {
                int at=BitConverter.ToUInt16(r.Bytes,6),length=BitConverter.ToUInt16(r.Bytes,at);
                return Encoding.ASCII.GetString(r.Bytes,at+2,length-1)=="Retrieval";
            });
            Check.Equal((byte)0,entry.Bytes[8],"Neighborhood org0");Check.Equal((byte)0,entry.Bytes[14],"Standard plot0");
            ushort index=BitConverter.ToUInt16(entry.Bytes,4);
            p.SendRpc($"80 98 06 00 {Le(index)} 01 02 00");await p.WaitForRpcAsync("80 99");
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");
            Check.Equal(3072,(await MissionCheckpoint(context,p)).Mission,"Normal list selection accepted existing canonical Retrieval3072");
        }
        async Task<ViewCreation> Search(int creationStart)
        {
            await EnterMissionArea(server,p);var computer=await ZionObject(server,p,1,creationStart);
            byte use=ObjectStates.UseNumber(computer.Goid,"Take")??throw new ScenarioFailure("No native Take action on the actual mission computer");
            uint State()
            {
                uint state=BitConverter.ToUInt32(computer.Attributes["CurrentState"]);byte[] prefix=WireFormat.FromHex($"{Le(computer.ViewId)} 01 08");
                foreach(var section in p.World.ViewSnapshot())for(int at=BytePattern.IndexOf(section,prefix,0);at>=0;at=BytePattern.IndexOf(section,prefix,at+1))
                    if(at+prefix.Length+4<=section.Length)state=BitConverter.ToUInt32(section,at+prefix.Length);
                return state;
            }
            await p.WaitUntilAsync(()=>ObjectStates.Offered(computer.Goid,State()).Contains(use),"Actual computer Take enabled",TimeSpan.FromSeconds(10));
            // Stay on actual NAV, within ordinary object reach and outside the existing exit radius while carrying the item.
            var checkpoint=await MissionCheckpoint(context,p);var position=computer.Attributes["Position"];
            double ox=BitConverter.ToDouble(position),oy=BitConverter.ToDouble(position,8),oz=BitConverter.ToDouble(position,16);
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            (double X,double Y,double Z)? standing=null;
            foreach(var offset in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(300d,0d),(-300d,0d),(0d,300d),(0d,-300d),(212d,212d),(-212d,-212d),(212d,-212d),(-212d,212d)})
            {
                double x=ox+offset.Item1,z=oz+offset.Item2;
                if(NavMesh.Locate(1,x,oy,z,out var nav)&&!MissionExit.HasLeft(checkpoint.Waypoints.ToList(),checkpoint.Exits.ToList(),x,nav.floorY+95,z))
                {standing=(x,nav.floorY+95,z);break;}
            }
            Check.That(standing.HasValue,"Actual computer has a NAV standing point outside6m exit boundary");
            await MoveForMissionTest(p,standing!.Value.X,standing.Value.Y,standing.Value.Z);
            p.SendRpc($"80 c7 {MissionNpcId(computer)} {use:x2} 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");
            var saved=await MissionCheckpoint(context,p);Check.That(saved.Done.Contains(1)&&saved.Items.ContainsKey(6831),"Actual completed Search and owned data item checkpoint");
            Check.Equal(2L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831"),"One quest item plus unrelated identical item");
            return computer;
        }
        async Task<string> Finish(int run)
        {
            await LeaveMissionArea(p);var end=await p.WaitForRpcAsync("80 a2 00",TimeSpan.FromSeconds(12));
            Check.Equal(6300u,BitConverter.ToUInt32(end.Bytes,11),"Existing level10 info policy");Check.Equal(4040u,BitConverter.ToUInt32(end.Bytes,15),"Existing level10 XP floor");
            Check.Equal(1282500L+run*4040,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Exactly one XP pair per run");
            Check.Equal(1000L+run*6300,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Exactly one info pair per run");
            Check.Equal((long)run,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId=3072"),"Earned repeated completion count");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831 AND slot=90"),"Unrelated identical item survives owned cleanup");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831"),"Only actual quest item removed");
            string receipt=await context.Database.ScalarAsync($"SELECT receipt FROM char_active_missions WHERE charId={p.CharId} AND payload IS NULL");
            completions.Add(new{Run=run,Receipt=receipt,Summary=Convert.ToHexString(end.Bytes)});return receipt;
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
            int cursor=p.World.CreationSnapshot().Count;await OfferAndAccept();var first=await MissionCheckpoint(context,p);var computer=await Search(cursor);var carried=await MissionCheckpoint(context,p);
            await Restart(true);var restored=await MissionCheckpoint(context,p);
            Check.Equal(carried.Identity,restored.Identity,"Same acceptance after cold process restart");Check.Equal(carried.Instance,restored.Instance,"Same mission key");
            Check.Equal(JsonSerializer.Serialize(carried.Objects,fields),JsonSerializer.Serialize(restored.Objects,fields),"Exact object placement/state preserved");
            Check.Equal(JsonSerializer.Serialize(carried.Mobs),JsonSerializer.Serialize(restored.Mobs),"Exact placed NPC identity/health/loot preserved");
            Check.Equal(carried.Items[6831],restored.Items[6831],"Same owned item slot");Check.That(restored.Done.Contains(1),"Search remains done");
            Check.That(!server.DebugLog().Contains(" listed "),"Cold restore resolved before any contact list could populate its private cache");
            string receipt1=await Finish(1);Replay(receipt1,false);
            cursor=p.World.CreationSnapshot().Count;await OfferAndAccept();var second=await MissionCheckpoint(context,p);Check.That(first.Identity!=second.Identity,"Repeat uses a new durable identity");Replay(receipt1,true);
            await Search(cursor);string receipt2=await Finish(2);Check.That(receipt1!=receipt2,"Each actual run has its own receipt");Replay(receipt2,false);
            await Restart(false);p.SendChat("?missionpersist status");await p.WaitForTextAsync("Mission persistence: none");
            Check.Equal(1290580L,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Duplicate finish/relog never pays XP again");
            Check.Equal(13600L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Duplicate finish/relog never pays info again");
            CheckProblems();passed=true;
        }
        finally
        {
            try
            {
                Record();string path=Path.Combine(Path.GetTempPath(),"mxo-neighbourhood-persistence-"+context.Database.Name+".json");
                await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Passed=passed,Database=context.Database.Name,Sessions=sessions,Processes=processes,Completions=completions,Lifetimes=lifetimes,NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
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
