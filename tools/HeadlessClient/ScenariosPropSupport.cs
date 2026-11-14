using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task PropSupportDiagnostic(ScenarioContext context)
    {
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal)&&context.Database.Name.Skip(8).All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"Owned disposable database only");
        SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
        Check.That(NavMesh.Locate(1,-4242.57080078125,1695,-5942.9,out var standing)&&standing.sector==1129&&standing.region==13&&standing.floorY==1600,"Actual fixed standing NAV floor");
        // Height calibration only; no use/range policy assertion.
        long xp=(long)PlayerProgression.ExperienceForLevel(50);
        await context.Database.ExecuteAsync($@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT 990110,'PropSupportUser',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=28;
DELETE FROM characters WHERE charId=110;
INSERT INTO characters(charId,userId,worldId,status,handle,firstName,lastName,background,x,y,z,rotation,healthC,healthM,innerStrC,innerStrM,level,profession,alignment,pvpflag,conquest_points,exp,cash,district,districtId,factionId,crewId,is_deleted,is_online,created)
 VALUES(110,990110,1,0,'CSR_Tester','First','Last','',-4242.57080078125,1695,-5942.9,0,5000,5000,2000,2000,50,2,2,0,0,{xp},1000,'slums',1,0,0,0,0,NOW());
DELETE FROM rsivalues WHERE charid=110;
CREATE TEMPORARY TABLE prop_support_rsi AS SELECT * FROM rsivalues WHERE charid=2;
UPDATE prop_support_rsi SET charid=110;INSERT INTO rsivalues SELECT * FROM prop_support_rsi;DROP TEMPORARY TABLE prop_support_rsi;
DELETE FROM char_abilities WHERE char_id=110;
DELETE FROM inventory WHERE charId=110;
");

        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,"PropSupportUser","CSR_Tester");
        bool passed=false;string step="entry";Exception? failure=null;var probes=new List<object>();
        string before="",after="";
        async Task<string> State()=>await context.Database.ScalarAsync("SELECT CONCAT(cash,':',exp,':',(SELECT COUNT(*) FROM inventory WHERE charId=110),':',(SELECT COUNT(*) FROM char_active_missions WHERE charId=110),':',(SELECT COUNT(*) FROM char_missions WHERE charId=110)) FROM characters WHERE charId=110");
        static async Task Barrier(HeadlessPlayer p){p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
        try
        {
            await LoadAbilitiesAsync(context,p,2,17);await p.EnterWorldAsync();p.SendRpc("0c 01");await Barrier(p);
            before=await State();
            foreach(ushort goid in new ushort[]{3,548,552})
            foreach(int delta in new[]{0,25,50,75,100})
            { string pass=goid+"-"+delta;
                step="on-"+pass;int cursor=p.World.CreationSnapshot().Count;int log=context.Server.DebugLog().Length;
                p.SendChat("?propsupport "+goid+" "+delta);await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[PROP-SUPPORT] on char=110"),"Private Computer probe created",TimeSpan.FromSeconds(8));
                await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(cursor).Any(v=>v.Goid==goid),"Actual free asset creation",TimeSpan.FromSeconds(8));
                var v=p.World.CreationSnapshot().Skip(cursor).Single(v=>v.Goid==goid);
                Check.That(new[]{PropSupportProbe.X,PropSupportProbe.CentreY(goid,delta),PropSupportProbe.Z}.SequenceEqual(Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(v.Attributes["Position"],i*8))),"Measured floor-relative centre");
                Check.Equal(PropSupportProbe.Identity.ToLowerInvariant(),Convert.ToHexString(v.Attributes["Orientation"]).ToLowerInvariant(),"Explicit identity baseline");
                Check.That(v.Attributes["MissionKey"].SequenceEqual(new byte[4]),"No mission identity");Check.That(v.Attributes["UseCount"].SequenceEqual(new byte[]{1}),"Explicit UseCount1");
                if(goid==3)Check.That(v.Attributes["CurrentState"].SequenceEqual(BitConverter.GetBytes(0x961u)),"Exact failed mission CurrentState0x961");
                else Check.That(!v.Attributes.ContainsKey("CurrentState"),"Container rawGOB default0x835 omitted");
                Check.That(!v.Attributes.ContainsKey("HalfExtents"),"No bounds override");
                step="ordinary-take-"+pass;await Barrier(p);int rpc=p.World.RpcSnapshot().Count;int useLog=context.Server.DebugLog().Length;
                byte[] use=WireFormat.FromHex($"80 c7 {MissionNpcId(v)} {(goid==3?5:3):x2} 00");p.SendRpc(use);
                await p.WaitUntilAsync(()=>context.Server.DebugLog()[useLog..].Contains("[OBJECT-USE] char=110 view="+v.ViewId+" "),"Normal Take5 logged",TimeSpan.FromSeconds(5));await Barrier(p);
                var replies=p.World.RpcSnapshot().Skip(rpc).ToArray();Check.Equal(0,replies.Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x81&&r.Bytes[1]==0x19),"No container executor registered");
                step="off-"+pass;int off=context.Server.DebugLog().Length;int offView=p.World.ViewSnapshot().Count;p.SendChat("?propsupport off");
                await p.WaitUntilAsync(()=>context.Server.DebugLog()[off..].Contains("[PROP-SUPPORT] off char=110 view="+v.ViewId),"Owned Computer removed",TimeSpan.FromSeconds(5));
                var delete=BytePattern.Parse($"01 00 01 01 00 {v.ViewId&255:x2} {v.ViewId>>8:x2}");
                await p.WaitUntilAsync(()=>p.World.ViewSnapshot().Skip(offView).Any(delete.Occurs),"Current operation owned deletion",TimeSpan.FromSeconds(5));await Barrier(p);
                probes.Add(new{Pass=pass,v.ViewId,v.SpawnId,v.Goid,Attributes=v.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value)),SentUse=Convert.ToHexString(use),UseRpcCursor=rpc,OffViewCursor=offView,Replies=replies.Select(r=>r.Hex)});
            }
            step="world-reset";int resetCursor=p.World.CreationSnapshot().Count;int resetLog=context.Server.DebugLog().Length;
            p.SendChat("?propsupport 0");await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(resetCursor).Any(v=>v.Goid==3),"Probe before reset",TimeSpan.FromSeconds(5));
            var resetProbe=p.World.CreationSnapshot().Skip(resetCursor).Single(v=>v.Goid==3);
            p.SkipReceived();p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
            p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");
            await p.WaitUntilAsync(()=>context.Server.DebugLog()[resetLog..].Contains("[PROP-SUPPORT] off char=110 view="+resetProbe.ViewId),"World reset invalidates exact probe lease",TimeSpan.FromSeconds(5));
            probes.Add(new{Reset=true,resetProbe.ViewId,resetProbe.SpawnId,Protocol="80fc01/80fd/80fe01/8107/8108/8109/05"});
            after=await State();Check.Equal(before,after,"Cash/XP/inventory/active checkpoint/receipts unchanged");Check.Equal(0,p.World.Problems.Count,"Clean protocol parser");step="complete";passed=true;
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string dll=context.Server.SourceAssembly;string proof=Path.Combine(Path.GetTempPath(),"mxo-prop-support-"+context.Database.Name+".json");
            try{await File.WriteAllTextAsync(proof,JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Before=before,After=after,Standing=new{standing.sector,standing.region,standing.polygon,standing.floorY},Probes=probes,SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems.ToArray(),Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),NativeRenderingProven=false,NativePickingProven=false,MissionExecutor=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Prop support proof before teardown: "+proof);}
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
