using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static IReadOnlyDictionary<string,string> PosterPoseEnvironment(string variant)=>new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="201202",["MXO_POSTER_BOX_POSE_PROBE"]=variant};
    private static async Task PosterBoxPoseDiagnostic(ScenarioContext context,string variant)
    {
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal)&&context.Database.Name.Skip(8).All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"Owned disposable database only");
        long xp=(long)PlayerProgression.ExperienceForLevel(50);
        // Existing PR194 fixture pattern: REQUIRED actual CSR110 identity only in this disposable database.
        await context.Database.ExecuteAsync($@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT 990110,'PosterPoseUser',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=28;
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
        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,"PosterPoseUser","CSR_Tester");
        string step="world-entry";Exception? primary=null;MissionProgress.SavedState? before=null,after=null;var views=new List<ViewCreation>();
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
            Check.That(NavMesh.Locate(1,24080,2095,15080,out var nav)&&Math.Abs(nav.floorY-2000)<.01,"Literal grounded approach floor2000");
            Check.That(NavMesh.Project(1,24080,2095,15080,out var projected,reachableFrom:new[]{24080d,2095d,15080d},requireDirect:true)&&Math.Abs(projected.x-24080)<.01&&Math.Abs(projected.y-2095)<.01&&Math.Abs(projected.z-15080)<.01,"Direct projection retains approach");
            step="actual-03";await MoveForMissionTest(p,24080,2095,15080);
            foreach(var obj in before.Objects.OrderBy(o=>o.Id))
            {
                double x=obj.Spot.pos_x,y=obj.Id==1&&variant=="source-native"?2100:obj.Spot.pos_y,z=obj.Spot.pos_z;
                bool At(ViewCreation v)=>v.Goid==548&&v.Attributes.TryGetValue("Position",out var b)&&BitConverter.ToDouble(b)==x&&BitConverter.ToDouble(b,8)==y&&BitConverter.ToDouble(b,16)==z;
                await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Any(At),"Actual expected Box pose object"+obj.Id,TimeSpan.FromSeconds(10));var view=p.World.CreationSnapshot().Last(At);views.Add(view);
                string q=obj.Id==1&&variant=="source-native"?"00000000f304353f00000000f20435bf":obj.Spot.quat;
                Check.That(Convert.FromHexString(q).SequenceEqual(view.Attributes["Orientation"]),"Exact expected quaternion object"+obj.Id);
                Check.That(new byte[]{0xff,(byte)before.Instance,(byte)(before.Instance>>8),2}.SequenceEqual(view.Attributes["MissionKey"]),"Mission key unchanged");
                Check.That(new byte[]{1}.SequenceEqual(view.Attributes["UseCount"]),"UseCount1 unchanged");
            }
            string NonPose(ViewCreation v)=>string.Join(";",v.Attributes.Where(a=>a.Key!="Position"&&a.Key!="Orientation").OrderBy(a=>a.Key).Select(a=>a.Key+"="+Convert.ToHexString(a.Value)));
            Check.That(views.Skip(1).All(v=>NonPose(v)==NonPose(views[0])),"Other creation fields match unmodified sibling Box proxies");
            Check.That(context.Server.DebugLog().Contains("[POSTER-BOX-POSE] DIAGNOSTIC variant="+variant+" char=110 mission=201202 phase=0 object=1 source=dc03100d"),"Actual strict diagnostic hook activated");
            after=await MissionCheckpoint(context,p);Check.Equal(JsonSerializer.Serialize(before.Objects,options),JsonSerializer.Serialize(after.Objects,options),"All saved sources/prototypes/states unchanged");
            Check.Equal(0,after.Done.Length,"No goal completed by the diagnostic");Check.Equal(0,after.Items.Count,"No mission item granted");
            Check.Equal(xp,await context.Database.LongAsync("SELECT exp FROM characters WHERE charId=110"),"No XP change");Check.Equal(1000L,await context.Database.LongAsync("SELECT cash FROM characters WHERE charId=110"),"No info change");
            Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110"),"No inventory change");Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM char_missions WHERE charId=110 AND missionId=201202"),"No completion receipt");
            Check.Equal(0,p.World.Problems.Count,"Diagnostic protocol parser clean");step="complete";
        }
        catch(Exception e){primary=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-poster-box-pose-"+variant+"-"+context.Database.Name);
            try
            {
                string checkpoint;try{checkpoint=await context.Database.ScalarAsync("SELECT payload FROM char_active_missions WHERE charId=110");}catch(Exception e){checkpoint="UNAVAILABLE: "+e;}
                string dll=context.Server.SourceAssembly;
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Variant=variant,Step=step,Failure=primary?.ToString(),Character=p.CharId,Before=before,After=after,FinalCheckpoint=checkpoint,
                    ActualBoxes=views.Select(v=>new{v.Goid,v.ViewId,v.SpawnId,Attributes=v.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))}),
                    SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),
                    Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),ProtocolProblems=p.World.Problems.ToArray(),NativeUiProven=false},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());Console.WriteLine("  POSTER BOX POSE evidence: "+stem+" step="+step);
            }
            catch(Exception e){if(primary==null)throw;Console.Error.WriteLine("Secondary proof failure; preserving original failure: "+e);}
        }
    }
}
