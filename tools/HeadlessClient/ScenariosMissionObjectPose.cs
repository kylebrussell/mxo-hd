using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static IReadOnlyDictionary<string,string> MissionPoseEnvironment(string variant)=>new Dictionary<string,string>(NeighbourhoodMissionFixture.Environment){["MXO_MISSION_OBJECT_POSE_PROBE"]=variant};
    private static async Task MissionObjectPoseDiagnostic(ScenarioContext context,string variant)
    {
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Only owned disposable DB");
        // Same ordinary Harness seed pattern, with the diagnostic's REQUIRED actual CSR110 identity; no main-DB reads.
        await context.Database.ExecuteAsync(@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT 990110,'PoseProbeUser',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=28;
DELETE FROM characters WHERE charId=110;
INSERT INTO characters(charId,userId,worldId,status,handle,firstName,lastName,background,x,y,z,rotation,healthC,healthM,innerStrC,innerStrM,level,profession,alignment,pvpflag,conquest_points,exp,cash,district,districtId,factionId,crewId,is_deleted,is_online,created)
 VALUES(110,990110,1,0,'CSR_Tester','First','Last','',76015,485,15845,128,2000,2000,1000,1000,10,2,1,0,0,1282500,1000,'slums',1,0,0,0,0,NOW());
DELETE FROM rsivalues WHERE charid=110;
CREATE TEMPORARY TABLE pose_rsi AS SELECT * FROM rsivalues WHERE charid=2;
UPDATE pose_rsi SET charid=110;INSERT INTO rsivalues SELECT * FROM pose_rsi;DROP TEMPORARY TABLE pose_rsi;
DELETE FROM char_abilities WHERE char_id=110;
DELETE FROM inventory WHERE charId=110;
");
        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,"PoseProbeUser","CSR_Tester");
        bool passed=false;string phase="world-entry";string? failure=null;object? before=null,after=null;ViewCreation? computer=null;
        try
        {
            Check.Equal(110u,p.CharId,"Actual diagnostic CSR110, not a relaxed surrogate guard");await LoadAbilitiesAsync(context,p,2,17);await p.EnterWorldAsync();
            ViewCreation contact=null!;await p.WaitUntilAsync(()=>{contact=p.ViewsOfGoid(6520).LastOrDefault(v=>v.Attributes.TryGetValue("ContactID",out var id)&&BitConverter.ToUInt32(id)==7)!;return contact!=null;},"Actual Chessman7",TimeSpan.FromSeconds(10));
            p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(contact)} 02 00");await p.WaitForRpcAsync("80 97 00 00");
            var entry=p.Received("80 96").Single(r=>{int at=BitConverter.ToUInt16(r.Bytes,6),n=BitConverter.ToUInt16(r.Bytes,at);return Encoding.ASCII.GetString(r.Bytes,at+2,n-1)=="Retrieval";});
            p.SendRpc($"80 98 06 00 {Le(BitConverter.ToUInt16(entry.Bytes,4))} 01 02 00");await p.WaitForRpcAsync("80 99");p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");
            phase="accepted";var saved=await MissionCheckpoint(context,p);before=saved;Check.Equal(3072,saved.Mission,"Normal accepted Retrieval3072");var original=saved.Objects.Single(o=>o.Id==1);
            Check.Equal((ushort)3,original.Goid,"Exact GO3");Check.Equal(0x15b01176u,original.Spot.mxoStaticId,"Exact source key bytes7611b015");
            Check.That(new[]{92740d,4445d,13535d}.SequenceEqual(new[]{original.Spot.pos_x,original.Spot.pos_y,original.Spot.pos_z}),"Checkpoint original source position: "+JsonSerializer.Serialize(new[]{original.Spot.pos_x,original.Spot.pos_y,original.Spot.pos_z}));
            Check.Equal("00000000f20435bf00000000f304353f",original.Spot.quat.ToLowerInvariant(),"Checkpoint keeps source quaternion");
            before=new{Saved=saved,Experience=await context.Database.LongAsync("SELECT exp FROM characters WHERE charId=110"),Cash=await context.Database.LongAsync("SELECT cash FROM characters WHERE charId=110"),Items=await context.Database.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110")};
            await EnterMissionArea(context,p);phase="actual-view";
            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Any(v=>v.Goid==3&&v.Attributes.ContainsKey("MissionKey")),"Actual mission computer03",TimeSpan.FromSeconds(10));computer=p.World.CreationSnapshot().Last(v=>v.Goid==3&&v.Attributes.ContainsKey("MissionKey"));
            byte[] pos=computer.Attributes["Position"];var expected=variant=="full-native"?new[]{91865d,4300d,12660d}:new[]{92740d,variant=="native-y"?4300d:4445d,13535d};
            Check.Equal(WireFormat.ToHex(expected.SelectMany(BitConverter.GetBytes).ToArray()),WireFormat.ToHex(pos),"Actual03 exact variant position");
            string quat=variant=="full-native"?"0000000000000000000000000000803f":original.Spot.quat;
            Check.Equal(quat.ToUpperInvariant(),Convert.ToHexString(computer.Attributes["Orientation"]),"Actual03 exact variant quaternion");
            Check.Equal(0x961u,BitConverter.ToUInt32(computer.Attributes["CurrentState"]),"Take state unchanged");
            Check.That(new byte[]{0xff,(byte)saved.Instance,(byte)(saved.Instance>>8),2}.SequenceEqual(computer.Attributes["MissionKey"]),"Mission ownership key unchanged: "+Convert.ToHexString(computer.Attributes["MissionKey"]));
            Check.That(context.Server.DebugLog().Contains("[MISSION-POSE-PROBE] DIAGNOSTIC variant="+variant+" char=110 mission=3072 phase=0 object=1 source=7611b015"),"Actual exact-identity probe activated");
            var current=await MissionCheckpoint(context,p);var spot=current.Objects.Single(o=>o.Id==1).Spot;
            Check.That(new[]{92740d,4445d,13535d}.SequenceEqual(new[]{spot.pos_x,spot.pos_y,spot.pos_z}),"Display overlay never persists: "+JsonSerializer.Serialize(new[]{spot.pos_x,spot.pos_y,spot.pos_z}));Check.Equal(original.Spot.quat,spot.quat,"Source quaternion never persists an overlay");
            Check.Equal(1282500L,await context.Database.LongAsync("SELECT exp FROM characters WHERE charId=110"),"No diagnostic XP");Check.Equal(1000L,await context.Database.LongAsync("SELECT cash FROM characters WHERE charId=110"),"No diagnostic info");Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110"),"No diagnostic item");
            after=current;Check.Equal(0,p.World.Problems.Count,"Diagnostic wire clean");phase="complete";passed=true;
        }
        catch(Exception ex){failure=ex.ToString();throw;}
        finally
        {
            string checkpoint;try{checkpoint=await context.Database.ScalarAsync("SELECT payload FROM char_active_missions WHERE charId=110");}catch(Exception ex){checkpoint="UNAVAILABLE: "+ex.Message;}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-mission-pose-"+variant+"-"+context.Database.Name);
            await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Phase=phase,Failure=failure,Variant=variant,Character=p.CharId,Before=before,After=after,FinalCheckpoint=checkpoint,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceAssemblySha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Computer=computer==null?null:new{computer.Goid,computer.ViewId,computer.SpawnId,Attributes=computer.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))},ReceivedRpcs=p.World.RpcSnapshot().Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(WireFormat.ToHex).ToArray(),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
            Console.WriteLine("MISSION POSE PRETEARDOWN "+stem);
        }
    }
}
