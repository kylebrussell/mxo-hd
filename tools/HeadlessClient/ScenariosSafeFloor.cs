using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task SafeFloorDiagnostic(ScenarioContext context)
    {
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal)&&context.Database.Name.Skip(8).All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"Owned disposable database only");
        SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
        Check.That(NavMesh.Locate(1,25100,2095,14020,out var standing)&&standing.sector==209&&standing.region==54&&standing.polygon==12&&standing.floorY==2000,"Actual fixed standing NAV floor");
        Check.That(Math.Sqrt(80*80+45*45)<100,"Unchanged source MaxDistance100 is sufficient even for centre distance");
        long xp=(long)PlayerProgression.ExperienceForLevel(50);
        await context.Database.ExecuteAsync($@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT 990110,'SafeFloorUser',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=28;
DELETE FROM characters WHERE charId=110;
INSERT INTO characters(charId,userId,worldId,status,handle,firstName,lastName,background,x,y,z,rotation,healthC,healthM,innerStrC,innerStrM,level,profession,alignment,pvpflag,conquest_points,exp,cash,district,districtId,factionId,crewId,is_deleted,is_online,created)
 VALUES(110,990110,1,0,'CSR_Tester','First','Last','',25100,2095,14020,0,5000,5000,2000,2000,50,2,2,0,0,{xp},1000,'slums',1,0,0,0,0,NOW());
DELETE FROM rsivalues WHERE charid=110;
CREATE TEMPORARY TABLE safe_floor_rsi AS SELECT * FROM rsivalues WHERE charid=2;
UPDATE safe_floor_rsi SET charid=110;INSERT INTO rsivalues SELECT * FROM safe_floor_rsi;DROP TEMPORARY TABLE safe_floor_rsi;
DELETE FROM char_abilities WHERE char_id=110;
DELETE FROM inventory WHERE charId=110;
");

        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,"SafeFloorUser","CSR_Tester");
        bool passed=false;string step="entry";Exception? failure=null;var probes=new List<object>();
        string before="",after="";
        async Task<string> State()=>await context.Database.ScalarAsync("SELECT CONCAT(cash,':',exp,':',(SELECT COUNT(*) FROM inventory WHERE charId=110),':',(SELECT COUNT(*) FROM char_active_missions WHERE charId=110),':',(SELECT COUNT(*) FROM char_missions WHERE charId=110)) FROM characters WHERE charId=110");
        static async Task Barrier(HeadlessPlayer p){p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
        try
        {
            await LoadAbilitiesAsync(context,p,2,17);await p.EnterWorldAsync();p.SendRpc("0c 01");await Barrier(p);
            before=await State();
            for(int pass=0;pass<2;pass++)
            {
                step="on-"+pass;int cursor=p.World.CreationSnapshot().Count;int log=context.Server.DebugLog().Length;
                p.SendChat("?safefloor on");await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[SAFE-FLOOR] on char=110"),"Private Safe probe created",TimeSpan.FromSeconds(8));
                await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(cursor).Any(v=>v.Goid==552),"Actual free552 creation",TimeSpan.FromSeconds(8));
                var v=p.World.CreationSnapshot().Skip(cursor).Single(v=>v.Goid==552);
                Check.That(new[]{25100d,2050d,14100d}.SequenceEqual(Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(v.Attributes["Position"],i*8))),"Measured floor-relative centre");
                Check.Equal("0000000000000000000000000000803f",Convert.ToHexString(v.Attributes["Orientation"]).ToLowerInvariant(),"Explicit identity baseline");
                Check.That(v.Attributes["MissionKey"].SequenceEqual(new byte[4]),"No mission identity");Check.That(v.Attributes["UseCount"].SequenceEqual(new byte[]{1}),"Source ContainerOpen prerequisite");
                Check.That(v.Attributes["CurrentState"].SequenceEqual(BitConverter.GetBytes(0x835u)),"Native Closed835 baseline");
                Check.That(!v.Attributes.ContainsKey("HalfExtents"),"No bounds override");
                step="ordinary-open-"+pass;await Barrier(p);int rpc=p.World.RpcSnapshot().Count;int useLog=context.Server.DebugLog().Length;
                byte[] use=WireFormat.FromHex($"80 c7 {MissionNpcId(v)} 03 00");p.SendRpc(use);
                await p.WaitUntilAsync(()=>context.Server.DebugLog()[useLog..].Contains("[OBJECT-USE] char=110 view="+v.ViewId+" "),"Normal Open3 logged",TimeSpan.FromSeconds(5));await Barrier(p);
                var replies=p.World.RpcSnapshot().Skip(rpc).ToArray();Check.Equal(0,replies.Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x81&&r.Bytes[1]==0x19),"No container executor registered");
                step="off-"+pass;int off=context.Server.DebugLog().Length;int offView=p.World.ViewSnapshot().Count;p.SendChat("?safefloor off");
                await p.WaitUntilAsync(()=>context.Server.DebugLog()[off..].Contains("[SAFE-FLOOR] off char=110 view="+v.ViewId),"Owned Safe removed",TimeSpan.FromSeconds(5));
                var delete=BytePattern.Parse($"01 00 01 01 00 {v.ViewId&255:x2} {v.ViewId>>8:x2}");
                await p.WaitUntilAsync(()=>p.World.ViewSnapshot().Skip(offView).Any(delete.Occurs),"Current operation owned deletion",TimeSpan.FromSeconds(5));await Barrier(p);
                probes.Add(new{Pass=pass,v.ViewId,v.SpawnId,v.Goid,Attributes=v.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value)),SentUse=Convert.ToHexString(use),UseRpcCursor=rpc,OffViewCursor=offView,Replies=replies.Select(r=>r.Hex)});
            }
            after=await State();Check.Equal(before,after,"Cash/XP/inventory/active checkpoint/receipts unchanged");Check.Equal(0,p.World.Problems.Count,"Clean protocol parser");step="complete";passed=true;
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string dll=context.Server.SourceAssembly;string proof=Path.Combine(Path.GetTempPath(),"mxo-safe-floor-"+context.Database.Name+".json");
            try{await File.WriteAllTextAsync(proof,JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Before=before,After=after,Standing=new{standing.sector,standing.region,standing.polygon,standing.floorY},Probes=probes,SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems.ToArray(),Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),NativeRenderingProven=false,NativePickingProven=false,MissionExecutor=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Safe floor proof before teardown: "+proof);}
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
