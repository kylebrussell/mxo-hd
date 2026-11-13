using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task PosterBoxFloorDiagnostic(ScenarioContext context)
    {
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal)&&context.Database.Name.Skip(8).All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"Owned disposable database only");
        long xp=(long)PlayerProgression.ExperienceForLevel(50);
        // Existing PR194 fixture pattern: REQUIRED actual CSR110 identity only in this disposable database.
        await context.Database.ExecuteAsync($@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT 990110,'PosterFloorUser',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=28;
DELETE FROM characters WHERE charId=110;
INSERT INTO characters(charId,userId,worldId,status,handle,firstName,lastName,background,x,y,z,rotation,healthC,healthM,innerStrC,innerStrM,level,profession,alignment,pvpflag,conquest_points,exp,cash,district,districtId,factionId,crewId,is_deleted,is_online,created)
 VALUES(110,990110,1,0,'CSR_Tester','First','Last','',17000,495,2300,0,5000,5000,2000,2000,50,2,2,0,0,{xp},1000,'slums',1,0,0,0,0,NOW());
DELETE FROM rsivalues WHERE charid=110;
CREATE TEMPORARY TABLE poster_pose_rsi AS SELECT * FROM rsivalues WHERE charid=2;
UPDATE poster_pose_rsi SET charid=110;INSERT INTO rsivalues SELECT * FROM poster_pose_rsi;DROP TEMPORARY TABLE poster_pose_rsi;
DELETE FROM char_abilities WHERE char_id=110;
DELETE FROM inventory WHERE charId=110;
");
        await context.Database.ExecuteAsync(PosterHuntFixture.SeedPrerequisites(110));
        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,"PosterFloorUser","CSR_Tester");
        string step="world-entry";string? baselinePayload=null;Exception? primary=null;MissionProgress.SavedState? before=null,after=null;var views=new List<ViewCreation>();
        var options=new JsonSerializerOptions{IncludeFields=true};
        try
        {
            Check.Equal(110u,p.CharId,"Actual required CSR110 identity, no surrogate guard");await LoadAbilitiesAsync(context,p,2,17,1006,77);await p.EnterWorldAsync();
            step="normal-offer";int cursor=p.World.RpcSnapshot().Count;p.SendRpc("80 94 05 00 02");await p.WaitForRpcAsync("80 97 05 00");
            var entries=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
            int index=MissionContactChoice.CriticalIndex(entries,"Poster Hunt");p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            before=await MissionCheckpoint(context,p);Check.Equal(201202,before.Mission,"Normal accepted Poster Hunt");Check.Equal(0,before.Phase,"First phase only");Check.Equal(3,before.Objects.Count,"Existing first-area count unchanged");
            var original=before.Objects.Single(o=>o.Id==1);Check.Equal((ushort)548,original.Goid,"Actual Box548, never GO3");Check.Equal(0x0d1003dcu,original.Spot.mxoStaticId,"Naturally selected exact source, no placement injection");
            Check.That(new[]{23940d,2245d,15050d}.SequenceEqual(new[]{original.Spot.pos_x,original.Spot.pos_y,original.Spot.pos_z}),"Known raw source coordinates");Check.Equal("00000000f204353f00000000f304353f",original.Spot.quat.ToLowerInvariant(),"Known raw source quaternion");
            step="normal-elevator";await EnterMissionArea(context,p);
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            Check.That(NavMesh.Locate(1,24960,2095,14100,out var nav)&&nav.floorY==2000,"Source floor2000 approach");
            Check.That(NavMesh.Project(1,24960,2095,14100,out var projection,reachableFrom:new[]{25579.31146596289,2095d,13959.481277120463}),"Approach connected to normal arrival");
            Check.That(projection.x==24960&&projection.y==2095&&projection.z==14100,"Inset/direct source approach unchanged");
            await MoveForMissionTest(p,24960,2095,14100);
            // Capture after normal entry, so normal enter/waypoint activity is not attributed to the probe.
            before=await MissionCheckpoint(context,p);
            baselinePayload=await context.Database.ScalarAsync("SELECT payload FROM char_active_missions WHERE charId=110");
            var normalViews=p.World.CreationSnapshot().Where(v=>v.Goid==548&&v.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&key[0]==0xff).ToArray();
            Check.That(normalViews.Length>=3,"All normal mission Box views already present");views.AddRange(normalViews);
            step="probe-on";p.SendChat("?posterboxfloor on");
            bool Probe(ViewCreation v)=>v.Goid==548&&v.Attributes.TryGetValue("Position",out var b)&&BitConverter.ToDouble(b)==25100&&BitConverter.ToDouble(b,8)==2040&&BitConverter.ToDouble(b,16)==14100;
            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Any(Probe),"Private free Box548 actual03",TimeSpan.FromSeconds(10));
            var probe=p.World.CreationSnapshot().Last(Probe);views.Add(probe);
            Check.That(probe.Attributes["Orientation"].SequenceEqual(Convert.FromHexString(PosterBoxFloorProbe.Identity)),"Identity orientation");
            Check.That(probe.Attributes["MissionKey"].SequenceEqual(new byte[4]),"No quest key");
            Check.That(probe.Attributes["UseCount"].SequenceEqual(new byte[]{1}),"Source-backed UseCount1");
            Check.That(probe.Attributes["CurrentState"].SequenceEqual(BitConverter.GetBytes(0x835u)),"Closed source state835");
            // Observe the existing dynamic request handler. No container executor or mission authority is registered.
            step="ordinary-use-observed";p.SendRpc($"80 c7 {MissionNpcId(probe)} 03 00");
            await p.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[OBJECT-USE] char=110 view="+probe.ViewId+" "),"Actual ordinary Open request logged",TimeSpan.FromSeconds(5));
            step="probe-off";int offStart=context.Server.DebugLog().Length;p.SendChat("?posterboxfloor off");
            await p.WaitUntilAsync(()=>context.Server.DebugLog()[offStart..].Contains("[POSTER-BOX-FLOOR] off char=110 view="+probe.ViewId),"Only private probe removed",TimeSpan.FromSeconds(5));
            after=await MissionCheckpoint(context,p);
            string StableCheckpoint(string json)
            {
                var node=System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
                // Ordinary OnViewUse invokes the existing checkpoint capture even for unrelated entities.
                // Those three existing elapsed-clock maps may advance; no authored state is exempted.
                node.Remove("NpcElapsed");node.Remove("WaveElapsed");node.Remove("GoalElapsed");return node.ToJsonString();
            }
            Check.Equal(StableCheckpoint(baselinePayload!),StableCheckpoint(await context.Database.ScalarAsync("SELECT payload FROM char_active_missions WHERE charId=110")),"Every non-clock checkpoint field unchanged");
            Check.Equal(JsonSerializer.Serialize(before.Objects,options),JsonSerializer.Serialize(after.Objects,options),"All saved sources/prototypes/states unchanged");
            Check.That(before.Done.SequenceEqual(after.Done),"All goals unchanged");Check.Equal(0,after.Items.Count,"No mission items granted");
            Check.Equal(xp,await context.Database.LongAsync("SELECT exp FROM characters WHERE charId=110"),"No XP change");Check.Equal(1000L,await context.Database.LongAsync("SELECT cash FROM characters WHERE charId=110"),"No info change");
            Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110"),"No inventory change");Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM char_missions WHERE charId=110 AND missionId=201202"),"No receipt");
            Check.Equal(0,p.World.Problems.Count,"Clean protocol parser");step="complete";
        }
        catch(Exception e){primary=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-poster-box-floor-"+context.Database.Name);
            try
            {
                string checkpoint;try{checkpoint=await context.Database.ScalarAsync("SELECT payload FROM char_active_missions WHERE charId=110");}catch(Exception e){checkpoint="UNAVAILABLE: "+e;}
                string dll=context.Server.SourceAssembly;
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=primary?.ToString(),Character=p.CharId,Before=before,After=after,BaselineCheckpoint=baselinePayload,FinalCheckpoint=checkpoint,
                    ActualBoxes=views.Select(v=>new{v.Goid,v.ViewId,v.SpawnId,Attributes=v.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))}),
                    SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),
                    Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),ProtocolProblems=p.World.Problems.ToArray(),NativeUiProven=false},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());Console.WriteLine("  POSTER BOX FLOOR evidence: "+stem+" step="+step);
            }
            catch(Exception e){if(primary==null)throw;Console.Error.WriteLine("Secondary proof failure; preserving original failure: "+e);}
        }
    }
}
