using hds;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MissionCourier(ScenarioContext context)
    {
        const int id=20001;int txn=40;var beforeRelogRpc=Array.Empty<string>();var beforeRelog03=Array.Empty<string>();var dll=context.Server.SourceAssembly;
        Console.WriteLine($"  COURIER DLL={dll} SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)))}");
        var player=await context.AddPlayerAsync(new CharacterSeed("CourierActor"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=1,X=23828,Y=495,Z=13984,Info=1000});
        string handle=player.Handle;uint charId=player.CharId;string phase="world-entry";string? failure=null;int completed=0;
        try
        {
        await player.EnterWorldAsync();
        async Task<int> Offer()
        {
            ushort request=(ushort)txn++;player.SkipReceived();player.SendRpc($"80 94 {Le(request)} 01");await player.WaitForRpcAsync($"80 97 {Le(request)}");
            var entries=player.World.RpcSnapshot().Where(r=>r.Bytes.Length>17&&r.Bytes[0]==0x80&&r.Bytes[1]==0x96&&BitConverter.ToUInt16(r.Bytes,2)==request).Select(r=>r.Bytes).ToList();
            string Title(byte[] b)=>Encoding.ASCII.GetString(b,17,b.Length-18);
            Check.That(entries.Any(b=>b[14]==1&&Title(b)=="Welcome to the Matrix"),"Next critical remains independently available");
            byte[] courier=entries.Single(b=>Title(b)=="Courier");Check.Equal((byte)0,courier[14],"Actual Courier list plot flag0");Check.Equal((uint)2000,BitConverter.ToUInt32(courier,9),"Actual Tyndall contact2000");
            return BitConverter.ToUInt16(courier,4);
        }
        async Task Act(string name,byte action,int goal)
        {
            var view=await VisitMissionNpc(context,player,name);
            if(action is 0x0d or 0x0e)
            {
                string tracker=action==0x0d?"GiverActiveTracker":"TakerActiveTracker";
                var actual=player.World.CreationSnapshot().Last(v=>v.Name==name);
                Check.That(actual.Attributes.TryGetValue(tracker,out var active)&&active.Any(b=>b!=0),"Actual native "+tracker+" offered");
            }
            player.SkipReceived();player.SendRpc($"80 c7 {MissionNpcId(view)} {action:x2} 00");await player.WaitForRpcAsync($"80 a0 {goal:x2} 06 00 01");
        }
        long startXp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={charId}");
        async Task Paid(int count)
        {
            Check.Equal((long)count,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={charId} AND missionId={id}"),"Courier earned exact repeat count");
            Check.Equal(startXp+count*22000,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={charId}"),"XP paid once per accepted run");
            Check.Equal(1000L+count*31500,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={charId}"),"Info paid once per accepted run");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={charId} AND goid=6831"),"Delivery item consumed");
        }
        for(int run=1;run<=2;run++)
        {
            phase=$"run{run}-offer";int index=await Offer();player.SkipReceived();player.SendRpc($"80 98 {Le((ushort)txn++)} {Le((ushort)index)} 01 02 00");await player.WaitForRpcAsync("80 99");
            player.SendRpc($"80 9b {Le((ushort)txn++)} 01");await player.WaitForRpcAsync("80 9c");await player.WaitForRpcAsync("80 9e 00");
            phase=$"run{run}-pickup";await EnterMissionArea(context,player);await Act("Leto",0x09,1);await Act("Leto",0x0d,2);
            var owned=await MissionCheckpoint(context,player);ushort slot=owned.Items[6831];
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={charId} AND goid=6831"),"Get grants exactly one owned disk");
            if(run==1)
            {
                phase="mid-carry-logout";player.SendRpc("80 fe 00");await player.WaitForRpcAsync("80 fd");beforeRelogRpc=player.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray();beforeRelog03=player.World.ViewSnapshot().Select(b=>Convert.ToHexString(b)).ToArray();player.Dispose();
                Console.WriteLine("  COURIER mid-carry normal logout; waiting reservation cleanup33s");await Task.Delay(TimeSpan.FromSeconds(33));
                phase="mid-carry-relog";player=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+charId,handle);await player.EnterWorldAsync();await player.WaitForRpcAsync("80 9c");
                var restored=await MissionCheckpoint(context,player);Check.Equal(slot,restored.Items[6831],"Relog restores exact owned item slot");Check.That(restored.Done.Contains(2),"Relog preserves completed Get");
                // Stale Get cannot duplicate the disk or complete a different goal.
                var leto=await VisitMissionNpc(context,player,"Leto");player.SendRpc($"80 c7 {MissionNpcId(leto)} 0d 00");await Task.Delay(250);
                Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={charId} AND goid=6831"),"Duplicate Get after relog is idempotent");
            }
            phase=$"run{run}-delivery";player.SkipReceived();await LeaveMissionArea(player);await player.WaitForRpcAsync("80 9e 00");
            await EnterMissionArea(context,player);await Act("Neris",0x09,1);await Act("Neris",0x0e,2);
            var neris=await player.WaitForViewNamedAsync("Neris");player.SendRpc($"80 c7 {MissionNpcId(neris)} 0e 00");await Task.Delay(200);
            await LeaveMissionArea(player);await player.WaitForRpcAsync("80 a2 00",TimeSpan.FromSeconds(20));await Paid(run);
            await LeaveMissionArea(player);player.SendRpc($"80 c7 {MissionNpcId(neris)} 0e 00");await Task.Delay(250);await Paid(run);
            completed=run;Console.WriteLine($"  COURIER run{run} completed normal Talk/Get/Give/exit; duplicate award/item checks PASS");
        }
        await Offer();Check.Equal(0,player.World.Problems.Count,"Courier protocol parser clean");
        phase="complete";
        }
        catch(Exception ex){failure=ex.ToString();throw;}
        finally
        {
            // Save uniquely BEFORE harness teardown, including failed runs and both sides of relog.
            string checkpoint;
            try{checkpoint=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={charId}");}
            catch(Exception ex){checkpoint="CHECKPOINT READ FAILED: "+ex;}
            var evidence=new {Database=context.Database.Name,Phase=phase,Failure=failure,CompletedRuns=completed,
                Dll=dll,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),Configuration=new DirectoryInfo(Path.GetDirectoryName(dll)!).Parent!.Name,
                Character=charId,Checkpoint=checkpoint,BeforeRelogRpc=beforeRelogRpc,BeforeRelog03=beforeRelog03,
                ActualRpc=player.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Actual03=player.World.ViewSnapshot().Select(b=>Convert.ToHexString(b)).ToArray()};
            string path=Path.Combine(Path.GetTempPath(),"mxo-mission-courier-"+context.Database.Name);
            try
            {
                string json=JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true});
                File.WriteAllText(path+".json",json);File.WriteAllText(path+".log",context.Server.DebugLog());
                if(failure==null&&completed==2){File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-mission-courier-wire.json"),json);File.WriteAllText(Path.Combine(Path.GetTempPath(),"mxo-mission-courier-success-debug.log"),context.Server.DebugLog());}
                Console.WriteLine($"  COURIER EVIDENCE={path} phase={phase} completed={completed} failure={failure!=null}");
            }
            catch(Exception ex){Console.WriteLine("  COURIER EVIDENCE WRITE FAILED: "+ex);}
            player.Dispose();
        }
    }
}
