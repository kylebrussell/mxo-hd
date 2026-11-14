using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static IReadOnlyDictionary<string,string> ZionBoxVisibilityEnvironment()=>InteriorAdmissionFixture.Environment(InteriorAdmissionFixture.Zion).Concat(new[]{new KeyValuePair<string,string>(ZionBoxVisibilityProbe.EnvironmentKey,"1")}).ToDictionary(x=>x.Key,x=>x.Value);
    private static async Task ZionBoxVisibilityDiagnostic(ScenarioContext context)
    {
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal)&&context.Database.Name.Skip(8).All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"Owned disposable database only");
        InteriorAdmissionFixture.RequireSource(Environment.GetEnvironmentVariable(InteriorAdmissionFixture.ExpectedSourceEnvironment),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))));
        long xp=(long)PlayerProgression.ExperienceForLevel(50);
        await context.Database.ExecuteAsync($@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT 990110,'ZionVisibilityUser',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=28;
DELETE FROM characters WHERE charId=110;
INSERT INTO characters(charId,userId,worldId,status,handle,firstName,lastName,background,x,y,z,rotation,healthC,healthM,innerStrC,innerStrM,level,profession,alignment,pvpflag,conquest_points,exp,cash,district,districtId,factionId,crewId,is_deleted,is_online,created)
 VALUES(110,990110,1,0,'CSR_Tester','First','Last','',7800,95,13795,0,5000,5000,2000,2000,50,2,1,0,0,{xp},1000,'slums',1,0,0,0,0,NOW());
DELETE FROM rsivalues WHERE charid=110;
CREATE TEMPORARY TABLE zion_visibility_rsi AS SELECT * FROM rsivalues WHERE charid=2;
UPDATE zion_visibility_rsi SET charid=110;INSERT INTO rsivalues SELECT * FROM zion_visibility_rsi;DROP TEMPORARY TABLE zion_visibility_rsi;
DELETE FROM char_abilities WHERE char_id=110;
DELETE FROM inventory WHERE charId=110;
");

        await context.Database.ExecuteAsync(InteriorAdmissionFixture.Seed(InteriorAdmissionFixture.Zion,110));

        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,"ZionVisibilityUser","CSR_Tester");
        var fields=new JsonSerializerOptions{IncludeFields=true};bool passed=false;string step="entry";Exception? failure=null;var probes=new List<object>();
        string before="",after="",afterStateError="",accepted="";
        async Task<string> State()=>await context.Database.ScalarAsync("SELECT JSON_OBJECT('cash',cash,'xp',exp,'items',COALESCE((SELECT GROUP_CONCAT(CONCAT(slot,':',goid,':',count,':',purity) ORDER BY slot SEPARATOR '|') FROM inventory WHERE charId=110),''),'active',COALESCE((SELECT payload FROM char_active_missions WHERE charId=110),''),'receipt',COALESCE((SELECT receipt FROM char_active_missions WHERE charId=110),''),'history',COALESCE((SELECT GROUP_CONCAT(CONCAT(missionId,':',completions) ORDER BY missionId SEPARATOR '|') FROM char_missions WHERE charId=110),'')) FROM characters WHERE charId=110");
        async Task<MissionProgress.SavedState> Saved()=>JsonSerializer.Deserialize<MissionProgress.SavedState>(await context.Database.ScalarAsync("SELECT payload FROM char_active_missions WHERE charId=110"),fields)!;
        static async Task Barrier(HeadlessPlayer p){p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
        try
        {
            Check.Equal((long)InteriorAdmissionFixture.Zion.Prerequisites.Distinct().Count(),await context.Database.LongAsync("SELECT COUNT(*) FROM char_missions WHERE charId=110"),"Only earlier receipts, before Margin");
            await LoadAbilitiesAsync(context,p,2,17,1006,77);await p.EnterWorldAsync();p.SendRpc("0c 01");await Barrier(p);
            step="ordinary-contact-accept";int offer=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc("80 94 05 00 01");await p.WaitForRpcAsync("80 97 05 00");
            int index=MissionContactChoice.CriticalIndex(p.World.RpcSnapshot().Skip(offer).Select(x=>x.Bytes),"Hardline Hunter");
            p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");int initial=p.World.CreationSnapshot().Count;
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");var saved=await Saved();
            Check.Equal(101209,saved.Mission,"Normally selected Hardline Hunter");Check.Equal(0,saved.Phase,"Actual phase0");Check.Equal(1,saved.Objects.Count,"Single actual authored object");
            var obj=saved.Objects.Single();Check.Equal(0x0d100397u,obj.Spot.mxoStaticId,"Exact native failed source, no chooser/source fallback");Check.Equal((ushort)548,obj.Goid,"Actual Box concrete model");
            Check.That(obj.OpenFloor!=null&&obj.OpenFloor.Centre.SequenceEqual(new[]{ZionBoxVisibilityProbe.X,ZionBoxVisibilityProbe.Y,ZionBoxVisibilityProbe.Z})&&obj.OpenFloor.Quaternion.Equals(ZionBoxVisibilityProbe.Identity,StringComparison.OrdinalIgnoreCase),"Exact persisted failed pose");
            accepted=JsonSerializer.Serialize(saved,fields);
            step="ordinary-elevator";var lobby=saved.Exits.Single();uint lobbyKey=InteriorAdmissionFixture.DoorAt(new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),lobby);
            await MoveForMissionTest(p,lobby[0],lobby[1]-50,lobby[2]);int rideLog=context.Server.DebugLog().Length;
            p.SendRpc($"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(lobbyKey))} 03 00");await p.WaitUntilAsync(()=>context.Server.DebugLog()[rideLog..].Contains("[MISSION] Teleported to "),"Ordinary elevator commit",TimeSpan.FromSeconds(15));
            var w=obj.OpenFloor!;for(int i=0;i<w.Route.Length;i+=3)await MoveForMissionTest(p,w.Route[i],w.Route[i+1],w.Route[i+2]);await Barrier(p);
            await p.WaitUntilAsync(()=>InteriorAdmissionFixture.Body(p.World.CreationSnapshot(),initial,548,w.Centre)!=null,"Original current mission body",TimeSpan.FromSeconds(15));
            var original=InteriorAdmissionFixture.Body(p.World.CreationSnapshot(),initial,548,w.Centre)!;
            Check.That(!original.Attributes["MissionKey"].SequenceEqual(new byte[4]),"Original retains actual mission authority");Check.That(!original.Attributes.ContainsKey("CurrentState"),"Original rawGOB Closed0x835 omitted");
            Check.Equal(ZionBoxVisibilityProbe.Identity,Convert.ToHexString(original.Attributes["Orientation"]),"Original exact quaternion");
            before=await State();int diagnosticRpc=p.World.RpcSnapshot().Count;int diagnosticViews=p.World.ViewSnapshot().Count;
            probes.Add(new{Original=true,original.ViewId,original.SpawnId,Attributes=original.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value)),RideLog=context.Server.DebugLog()[rideLog..],Accepted=accepted});
            foreach(int delta in new[]{0,-50,-100,-150,-200,-300})
            {
                step="duplicate-"+delta;int cursor=p.World.CreationSnapshot().Count;int log=context.Server.DebugLog().Length;p.SendChat("?zionbox "+delta);
                await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[ZION-BOX-VISIBILITY] on char=110"),"Owned duplicate creation",TimeSpan.FromSeconds(8));
                await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(cursor).Any(v=>v.Goid==548),"Actual03 duplicate",TimeSpan.FromSeconds(8));
                var v=p.World.CreationSnapshot().Skip(cursor).Single(v=>v.Goid==548);
                Check.That(new[]{ZionBoxVisibilityProbe.X,ZionBoxVisibilityProbe.Y,ZionBoxVisibilityProbe.Z+delta}.SequenceEqual(Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(v.Attributes["Position"],i*8))),"Only literal SOUTH offset changes duplicate pose");
                Check.Equal(ZionBoxVisibilityProbe.Identity,Convert.ToHexString(v.Attributes["Orientation"]),"Same original quaternion");Check.That(v.Attributes["MissionKey"].SequenceEqual(new byte[4]),"Private key0 duplicate has no mission authority");
                Check.That(v.Attributes["UseCount"].SequenceEqual(original.Attributes["UseCount"])&&!v.Attributes.ContainsKey("CurrentState")&&!v.Attributes.ContainsKey("HalfExtents"),"Same original state/defaults, no bounds override");
                Check.That(v.ViewId!=original.ViewId&&v.SpawnId!=original.SpawnId,"Independent private view and spawn");await Barrier(p);Check.Equal(before,await State(),"Checkpoint/items/cash/XP/receipts unchanged by duplicate");
                step="off-"+delta;int off=context.Server.DebugLog().Length;int offViews=p.World.ViewSnapshot().Count;p.SendChat("?zionbox off");
                await p.WaitUntilAsync(()=>context.Server.DebugLog()[off..].Contains("[ZION-BOX-VISIBILITY] off char=110 view="+v.ViewId),"Owned duplicate removed",TimeSpan.FromSeconds(5));
                var delete=BytePattern.Parse($"01 00 01 01 00 {v.ViewId&255:x2} {v.ViewId>>8:x2}");await p.WaitUntilAsync(()=>p.World.ViewSnapshot().Skip(offViews).Any(delete.Occurs),"Private-only wire deletion",TimeSpan.FromSeconds(5));await Barrier(p);
                Check.Equal(before,await State(),"Off does not mutate original checkpoint/account");probes.Add(new{Delta=delta,v.ViewId,v.SpawnId,Attributes=v.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value)),OffViewCursor=offViews});
            }
            var originalDelete=BytePattern.Parse($"01 00 01 01 00 {original.ViewId&255:x2} {original.ViewId>>8:x2}");Check.That(!p.World.ViewSnapshot().Skip(diagnosticViews).Any(originalDelete.Occurs),"Original mission view never deleted by clone/off");
            Check.That(!p.World.RpcSnapshot().Skip(diagnosticRpc).Any(v=>v.Bytes.Length>=2&&((v.Bytes[0]==0x80&&v.Bytes[1]==0xa0)||(v.Bytes[0]==0x81&&(v.Bytes[1]==0x19||v.Bytes[1]==0x20)))),"No loot/claim/goal output; no probe Open attempted");
            step="actual-world-reset";int reset=p.World.CreationSnapshot().Count;int resetLog=context.Server.DebugLog().Length;p.SendChat("?zionbox 0");await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(reset).Any(v=>v.Goid==548),"Final duplicate before actual reset",TimeSpan.FromSeconds(5));
            var last=p.World.CreationSnapshot().Skip(reset).Single(v=>v.Goid==548);
            p.SkipReceived();p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");
            await p.WaitUntilAsync(()=>context.Server.DebugLog()[resetLog..].Contains("[ZION-BOX-VISIBILITY] off char=110 view="+last.ViewId),"Actual LA reset removes final lease",TimeSpan.FromSeconds(5));
            after=await State();Check.Equal(before,after,"Checkpoint/items/cash/XP/receipts unchanged through actual reset");Check.Equal(0,p.World.Problems.Count,"Clean parser");probes.Add(new{Reset=true,last.ViewId,last.SpawnId,Protocol="80fc01/80fd/80fe01/8107/8108/8109/05"});passed=true;step="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            try{after=await State();}catch(Exception e){afterStateError=e.ToString();}
            string dll=context.Server.SourceAssembly;string proof=Path.Combine(Path.GetTempPath(),"mxo-zion-box-visibility-"+context.Database.Name+".json");
            try{await File.WriteAllTextAsync(proof,JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Before=before,After=after,AfterStateError=afterStateError,Accepted=accepted,Probes=probes,SourceAssembly=dll,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),Rpc=p.World.RpcSnapshot().Select(x=>x.Hex),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems.ToArray(),Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),NativeRenderingProven=false,DiagnosticOnly=true,OriginalExecutorUntouched=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Zion Box visibility proof before teardown: "+proof);}catch(Exception e){if(failure==null)throw;Console.Error.WriteLine(e);}
        }
    }
}
