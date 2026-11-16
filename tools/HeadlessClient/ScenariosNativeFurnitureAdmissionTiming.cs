using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    // Normal chooser measurement, deliberately separate from the captured furniture fixture.
    private static async Task NativeFurnitureAdmissionTiming(ScenarioContext context)
    {
        const int missionId=201209;
        const string pin="MXO_NATIVE_FURNITURE_TIMING_SOURCE_SHA256";
        string? expected=Environment.GetEnvironmentVariable(pin);
        if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(context.Server.SourceAssemblySha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Mandatory current furniture timing source pin: "+pin);
        NativeBoundFurnitureFixture.RequireOwned(context.Database.Name);
        int[] earlier=MachinesCriticalNextFiveFixture.Prerequisites.Concat(Enumerable.Range(201202,7)).Distinct().ToArray();
        Check.That(!earlier.Contains(missionId),"Selected mission is not a seeded completion");
        var definition=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),"machines_1_2_9*.xml").Select(XElement.Load).Single(m=>(int)m.Attribute("id")! == missionId);
        string title=(string)definition.Attribute("title")!;
        HeadlessPlayer? p=null;Exception? failure=null;bool passed=false,initiallyUnseeded=false;string step="character",hex="",payload="";
        var timings=new List<object>();var clock=Stopwatch.StartNew();long? requestStart=null;int requestCursor=0;
        async Task<long> SelectedReceipts()=>await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p!.CharId} AND missionId={missionId}");
        async Task<string> ActiveHex()=>await context.Database.ScalarAsync($"SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId={p!.CharId}");
        void Begin(string name){step=name;p!.SkipReceived();requestCursor=p.World.RpcSnapshot().Count;requestStart=Stopwatch.GetTimestamp();}
        void End(){timings.Add(new{Stage=step,StartTicks=requestStart,EndTicks=Stopwatch.GetTimestamp(),Milliseconds=(Stopwatch.GetTimestamp()-requestStart!.Value)*1000d/Stopwatch.Frequency,RpcStart=requestCursor,RpcEnd=p!.World.RpcSnapshot().Count});requestStart=null;}
        try
        {
            p=await context.AddPlayerAsync(new CharacterSeed("FurnitureTiming"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=2,CompletedMissions=earlier});
            Check.Equal(0L,await SelectedReceipts(),"Fresh selected mission initially uncompleted");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId} AND payload IS NOT NULL"),"No seeded active progress");
            initiallyUnseeded=true;await p.EnterWorldAsync();
            Begin("list-8094");p.SendRpc("80 94 05 00 02");await p.WaitForRpcAsync("80 97 05 00");End();
            var entries=p.World.RpcSnapshot().Skip(requestCursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
            int index=MissionContactChoice.CriticalIndex(entries,title);
            Begin("select-8098");p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");End();
            Check.Equal(0L,await SelectedReceipts(),"Select does not complete selected mission");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId} AND payload IS NOT NULL"),"Preview does not seed active progress");
            Begin("accept-809b");p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");End();
            hex=await ActiveHex();payload=MissionBoundCabinetFixture.DecodeCheckpointHex(hex);
            var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,new JsonSerializerOptions{IncludeFields=true})??throw new InvalidDataException("Accepted checkpoint missing");
            Check.Equal(missionId,saved.Mission,"Actual accepted definition");Check.Equal(0,saved.Phase,"Actual initial phase");
            Check.That(saved.Done.Length==0,"No objective completed by measurement");Check.Equal(0L,await SelectedReceipts(),"No selected completion grant");
            Check.Equal(0,p.World.Problems.Count,"Strict wire parser problems");passed=true;step="accepted-only";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            if(p!=null)try{hex=await ActiveHex();if(!string.IsNullOrEmpty(hex))payload=MissionBoundCabinetFixture.DecodeCheckpointHex(hex);}catch(Exception e){payload+=" FINAL READ ERROR: "+e;}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-native-furniture-admission-timing-"+context.Database.Name);
            string packet=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt"));
            await File.WriteAllTextAsync(stem+"-PacketLog.txt",packet,Encoding.UTF8);
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),context.Database.Name,context.Server.ProcessId,Source=context.Server.SourceAssembly,SourceSha256=context.Server.SourceAssemblySha256,SelectedMission=missionId,Title=title,EarlierReceipts=earlier,InitiallyUnseeded=initiallyUnseeded,ElapsedMilliseconds=clock.Elapsed.TotalMilliseconds,Stopwatch.Frequency,Timings=timings,PendingStageStartTicks=requestStart,PendingRpcCursor=requestCursor,CheckpointHex=hex,Checkpoint=payload,Debug=context.Server.DebugLog(),PacketSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet))),Rpc=p?.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)),Views=p?.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p?.World.Problems,Scope="Normal list/select/accept only; no captured active state, forced source, mission completion, navigation or native visibility proof. No latency pass threshold; existing RPC synchronization deadlines still apply. Machines201209 only:301210 furniture occurs in a later phase."},new JsonSerializerOptions{WriteIndented=true}),Encoding.UTF8);
            p?.Dispose();Console.WriteLine("NATIVE FURNITURE ADMISSION TIMING="+stem);
        }
    }
}

