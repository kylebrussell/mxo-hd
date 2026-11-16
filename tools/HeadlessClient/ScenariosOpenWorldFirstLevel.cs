using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task OpenWorldFirstLevel(ScenarioContext context)
    {
        OpenWorldFirstLevelFixture.RequireSource(Environment.GetEnvironmentVariable(OpenWorldFirstLevelFixture.SourcePin),context.Server.SourceAssemblySha256);
        var sessions=new List<HeadlessPlayer>();var proof=new List<object>();Exception? failure=null;bool passed=false;string stage="seed";string final="unavailable";
        try
        {
            var p=(await context.AddPlayersInWorldAsync(OpenWorldFirstLevelFixture.Seed))[0];sessions.Add(p);uint id=p.CharId;
            Check.Equal(0L,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={id}"),"No earned XP injected");
            foreach(var source in OpenWorldFirstLevelFixture.Spawns)
            {
                stage="ordinary-kill-"+source[0];OpenWorldCurrentNpc.Current? current=null;
                await p.WaitUntilAsync(()=>{try{current=OpenWorldCurrentNpc.Require(p.World.CurrentOwnViewLife(),source[0],source[1],source[2]);return true;}catch(InvalidDataException){return false;}},"Current ordinary captured Runner",TimeSpan.FromSeconds(20));
                var target=current!;var nav=OpenWorldFirstLevelFixture.Approach(target.Position);
                // Disclosed point movement aid; native walking/collision must be checked independently.
                await MoveForMissionTest(p,nav.x,nav.y,nav.z);var refreshed=OpenWorldCurrentNpc.Require(p.World.CurrentOwnViewLife(),source[0],source[1],source[2]);
                Check.Equal(target.View.SpawnId,refreshed.View.SpawnId,"Same target generation before ordinary combat");
                int cursor=p.World.RpcSnapshot().Count;p.SendRpc("3d 01");p.SendRpc($"40 {Le(refreshed.View.ViewId)} {Le((ushort)refreshed.View.SpawnId)} 00 00 00 00 00 00 00");
                await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>OpenWorldFirstLevelFixture.FightingGain(r.Bytes,391)),"Ordinary full-damage kill awards literal391XP",TimeSpan.FromSeconds(120));
                Check.Equal(1,p.World.RpcSnapshot().Skip(cursor).Count(r=>OpenWorldFirstLevelFixture.FightingGain(r.Bytes,391)),"One kill reward");
                long before=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}");
                p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(refreshed.View)} 05 00");var window=await p.WaitForRpcAsync("81 19");uint money=OpenWorldFirstLevelFixture.LootInfo(window.Bytes);
                Check.That(money<=495&&money%5==0,"Level1 pocket cash follows captured5*roll range, including zero");p.SendRpc("81 1f");await p.WaitForRpcAsync("81 20");
                Check.Equal(before+money,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),"Actual corpse money committed once");
                p.SendRpc("81 1f");p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
                Check.Equal(before+money,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),"Repeated TakeAll does not duplicate cash");
                proof.Add(new{Source=source,View=refreshed.View,Current=refreshed.Position,refreshed.Life,Approach=new[]{nav.x,nav.y,nav.z},Money=money,WalletBefore=before,WalletAfter=before+money});
            }
            stage="earned-level";await p.WaitUntilAsync(()=>p.World.CurrentOwnViewLife().Events.Any(e=>OpenWorldFirstLevelFixture.OwnLevelTwo(e.Bytes)),"Actual own-view level2/maxHP200/maxIS103/levelFX",TimeSpan.FromSeconds(15));
            Check.Equal(782L,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={id}"),"Two ordinary kills earned782XP");Check.Equal(2L,await context.Database.LongAsync($"SELECT level FROM characters WHERE charId={id}"),"Threshold500 crossed naturally");
            string account=await context.Database.ExecuteAsync($"SELECT level,exp,cash FROM characters WHERE charId={id}");string items=await context.Database.ExecuteAsync($"SELECT slot,goid,count FROM inventory WHERE charId={id} ORDER BY slot");
            string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={id}");
            stage="normal-quit";p.SkipReceived();p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");p.Dispose();await Task.Delay(TimeSpan.FromSeconds(33));
            var restored=await HeadlessPlayer.LoginAsync(context.Server.Address,username,"FirstLevel");sessions.Add(restored);Check.Equal(id,restored.CharId,"Same character after ordinary Quit");await restored.EnterWorldAsync();
            Check.Equal(account,await context.Database.ExecuteAsync($"SELECT level,exp,cash FROM characters WHERE charId={id}"),"Earned account persisted unchanged");Check.Equal(items,await context.Database.ExecuteAsync($"SELECT slot,goid,count FROM inventory WHERE charId={id} ORDER BY slot"),"Claimed inventory persists");
            Check.Equal(0,restored.World.RpcSnapshot().Count(r=>OpenWorldFirstLevelFixture.FightingGain(r.Bytes,391)),"No kill replay on login");
            foreach(var session in sessions)Check.Equal(0,session.World.Problems.Count,"Actual session parser");passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try{final=await context.Database.ExecuteAsync("SELECT charId,level,exp,cash FROM characters WHERE handle='FirstLevel'");}catch(Exception error){final=error.ToString();}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-open-world-first-level-"+context.Database.Name),packet=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt"));
            await File.WriteAllTextAsync(stem+"-PacketLog.txt",packet);await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Schema=context.Database.Name,context.Server.ProcessId,Source=context.Server.SourceAssembly,SourceSha256=context.Server.SourceAssemblySha256,Proofs=proof,Final=final,PacketSha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(packet))),Sessions=sessions.Select(s=>new{s.CharId,Rpcs=s.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)),Views=s.World.ViewSnapshot().Select(Convert.ToHexString),Problems=s.World.Problems})},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("FIRST LEVEL EVIDENCE="+stem);foreach(var session in sessions)session.Dispose();
        }
    }
}
