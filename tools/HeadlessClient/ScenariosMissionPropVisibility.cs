using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MissionPropVisibility(ScenarioContext context)
    {
        var c=InteriorAdmissionFixture.Machines;Check.That(c.Prerequisites.Length==20&&c.Prerequisites.Distinct().Count()==20&&!c.Prerequisites.Contains(c.Mission),"Prior receipts unique and selected mission disjoint before DB writes");
        Check.That(MissionPropProbe.Allowed("1","1",context.Database.Name,110,true),"Disposable CSR110 schema only");
        Check.Equal(20,c.Prerequisites.Length,"Exactly earlier20 receipts before Margin");
        Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM users WHERE userId=990110"),"Private fixture account not owned by another row");
        await context.Database.ExecuteAsync($@"INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5) SELECT 990110,'MissionPropUser',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=28;
DELETE FROM char_active_missions WHERE charId=110;DELETE FROM char_missions WHERE charId=110;DELETE FROM inventory WHERE charId=110;DELETE FROM char_abilities WHERE char_id=110;DELETE FROM rsivalues WHERE charid=110;DELETE FROM characters WHERE charId=110;
INSERT INTO characters(charId,userId,worldId,status,handle,firstName,lastName,background,x,y,z,rotation,healthC,healthM,innerStrC,innerStrM,level,profession,alignment,pvpflag,conquest_points,exp,cash,district,districtId,factionId,crewId,is_deleted,is_online,created) VALUES(110,990110,1,0,'CSR_Tester','First','Last','',17000,495,2300,0,5000,5000,2000,2000,50,2,2,0,0,{PlayerProgression.ExperienceForLevel(50)},1000,'slums',1,0,0,0,0,NOW());
CREATE TEMPORARY TABLE mission_prop_rsi AS SELECT * FROM rsivalues WHERE charid=2;UPDATE mission_prop_rsi SET charid=110;INSERT INTO rsivalues SELECT * FROM mission_prop_rsi;DROP TEMPORARY TABLE mission_prop_rsi;
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) SELECT 110,slot,ability_id,level,is_loaded,ability_name FROM char_abilities WHERE char_id=2;
"+InteriorAdmissionFixture.Seed(c,110));
        using var player=await HeadlessPlayer.LoginAsync(context.Server.Address,"MissionPropUser","CSR_Tester");
        await InteriorAdmissionProof(context,c,player,async(p,target,proof)=>
        {
            Check.Equal(110u,p.CharId,"Actual authenticated CSR110");Check.Equal(201201,target.Mission,"Actual accepted normal mission");Check.Equal(1,target.Phase,"Actual Computer phase");
            var obj=target.Objects.Single(o=>o.Id==1);Check.That(obj.Goid==3&&obj.OpenFloor!=null,"Current original freeGO3 witness required");var pose=obj.OpenFloor!;
            var original=p.World.CreationSnapshot().GroupBy(v=>v.ViewId).Select(g=>g.Last()).Last(v=>v.Goid==3&&v.Attributes.TryGetValue("Position",out var at)&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(at,i*8)==pose.Centre[i]));
            Check.Equal(pose.Quaternion,Convert.ToHexString(original.Attributes["Orientation"]),"Actual original quaternion");
            for(int i=0;i<pose.Route.Length;i+=3)await MoveForMissionTest(p,pose.Route[i],pose.Route[i+1],pose.Route[i+2]);
            async Task Barrier(){p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
            await Barrier();p.SendChat("?save");await p.WaitForRpcAsync("2e 07");
            async Task<string> State()=>await context.Database.ScalarAsync("SELECT JSON_OBJECT('cash',cash,'xp',exp,'x',x,'y',y,'z',z,'items',COALESCE((SELECT GROUP_CONCAT(CONCAT(slot,':',goid,':',count,':',purity) ORDER BY slot SEPARATOR '|') FROM inventory WHERE charId=110),''),'active',COALESCE((SELECT payload FROM char_active_missions WHERE charId=110),''),'receipt',COALESCE((SELECT receipt FROM char_active_missions WHERE charId=110),''),'history',COALESCE((SELECT GROUP_CONCAT(CONCAT(missionId,':',completions) ORDER BY missionId SEPARATOR '|') FROM char_missions WHERE charId=110),'')) FROM characters WHERE charId=110");
            string before=await State();var controls=new List<(bool Same,ushort Goid,int Delta)>{(false,3,0),(true,3,0)};
            controls.AddRange(from goid in new ushort[]{3,548,552} from delta in new[]{0,25,50,75,95,100} from same in new[]{false,true} where goid!=3||delta!=0 select(same,goid,delta));
            ViewCreation? previous=null;
            foreach(var control in controls)
            {
                int cursor=p.World.CreationSnapshot().Count,sectionCursor=p.World.ViewSnapshot().Count,log=context.Server.DebugLog().Length;string key=control.Same?"same":"zero";
                p.SendChat($"?missionprop {key} {control.Goid} {control.Delta}");await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION-PROP] on char=110"),"Scoped private clone created",TimeSpan.FromSeconds(8));
                await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(cursor).Any(v=>v.Goid==control.Goid),"Actual private03 creation",TimeSpan.FromSeconds(8));var clone=p.World.CreationSnapshot().Skip(cursor).Single(v=>v.Goid==control.Goid);
                var expectedPosition=(byte[])original.Attributes["Position"].Clone();
                double y=BitConverter.ToDouble(expectedPosition,8);if(control.Goid!=3)y+=-25.00377655029297-(control.Goid==548?-40:-50);
                if(control.Goid!=3||control.Delta!=0)Buffer.BlockCopy(BitConverter.GetBytes(y+control.Delta),0,expectedPosition,8,8);
                Check.That(clone.Attributes["Position"].SequenceEqual(expectedPosition),"Independent literal/support-floor position oracle");
                Check.That(clone.Attributes["Orientation"].SequenceEqual(original.Attributes["Orientation"]),"Literal quaternion");
                Check.That(clone.Attributes["MissionKey"].SequenceEqual(control.Same?original.Attributes["MissionKey"]:new byte[4]),"Literal original/zero mission key");
                Check.That(clone.Attributes["UseCount"].SequenceEqual(original.Attributes["UseCount"]),"Literal use count");
                if(control.Goid==3)Check.That(clone.Attributes["CurrentState"].SequenceEqual(original.Attributes["CurrentState"]),"Original actual state retained");else Check.That(!clone.Attributes.ContainsKey("CurrentState"),"Measured asset raw closed default unchanged");
                Check.That(!clone.Attributes.ContainsKey("HalfExtents"),"No model bounds override");
                if(previous!=null)Check.That(p.World.ViewSnapshot().Skip(sectionCursor).Any(BytePattern.Parse($"01 00 01 01 00 {Le(previous.ViewId)}").IsMatch),"Replacement removes only previous private lease");
                int rpc=p.World.RpcSnapshot().Count,useLog=context.Server.DebugLog().Length;p.SendRpc($"80 c7 {MissionNpcId(clone)} {(control.Goid==3?5:3):x2} 00");
                await p.WaitUntilAsync(()=>context.Server.DebugLog()[useLog..].Contains("[OBJECT-USE] char=110 view="+clone.ViewId+" "),"Normal private use processed",TimeSpan.FromSeconds(5));await Barrier();var replies=p.World.RpcSnapshot().Skip(rpc).Select(r=>r.Bytes).ToArray();
                Check.That(replies.All(r=>MissionPropReplies.Allowed(control.Goid,r)),"Only ordinary generic empty loot, never mission output");
                if(control.Goid==3)Check.That(replies.Any(r=>MissionPropReplies.Empty(r)),"Actual exact zero-item generic GO3 reply");
                Check.Equal(before,await State(),"No actor pose/item/goal/reward/checkpoint/history mutation from diagnostic");
                proof.Add(new{Step="private-clone",control.Goid,control.Delta,Key=key,clone.ViewId,clone.SpawnId,OriginalView=original.ViewId,OriginalSpawn=original.SpawnId,Source=obj.Spot.mxoStaticId,RawOriginal=original.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value)),RawClone=clone.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value)),Replies=replies.Select(Convert.ToHexString).ToArray(),Before=before,After=await State(),NativeDrawingProven=false});previous=clone;
            }
            int off=p.World.ViewSnapshot().Count;p.SendChat("?missionprop off");await p.WaitUntilAsync(()=>p.World.ViewSnapshot().Skip(off).Any(BytePattern.Parse($"01 00 01 01 00 {Le(previous!.ViewId)}").IsMatch),"Private lease off",TimeSpan.FromSeconds(5));await Barrier();Check.Equal(before,await State(),"Off does not mutate original mission");Check.Equal(0,p.World.Problems.Count,"Diagnostic session parser clean");
        });
    }
}
public static class MissionPropReplies
{
    private static readonly byte[] EmptyLoot=Convert.FromHexString("811900000000100000000000000000000000");
    public static bool Empty(byte[] rpc)=>rpc.SequenceEqual(EmptyLoot);
    public static bool Allowed(ushort goid,byte[] rpc)=>rpc.Length<2||((rpc[0]!=0x81||rpc[1]!=0x19||goid==3&&Empty(rpc))&&!(rpc[0]==0x81&&rpc[1]==0x20)&&!(rpc[0]==0x80&&(rpc[1]==0xa0||rpc[1]==0xa2||rpc[1]==0xe6)));
}
