using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task SneakScaledGrant(ScenarioContext context)
    {
        SneakScaledGrantFixture.RequireSource(Environment.GetEnvironmentVariable(SneakScaledGrantFixture.SourcePin),context.Server.SourceAssemblySha256);
        var sessions=new List<HeadlessPlayer>();var proofs=new List<object>();string stage="pre-Margin-seed";Exception? failure=null;bool passed=false;
        async Task<string> Archive(HeadlessPlayer p)=>await context.Database.ExecuteAsync($"SELECT char_id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={p.CharId} ORDER BY slot");
        async Task<string> Account(HeadlessPlayer p)=>await context.Database.ExecuteAsync($"SELECT exp,cash FROM characters WHERE charId={p.CharId}");
        async Task NoChildArchive(HeadlessPlayer p)=>Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND (ability_id & 4227858432)=2147483648 AND ((ability_id & 4193280)>>10)=1111"),"Hidden1111 is never archived");
        async Task Ready(HeadlessPlayer p,ushort level)
        {
            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Any(v=>v.ViewId==2),"Actual own03 before ready",TimeSpan.FromSeconds(15));
            var before=p.World.RpcSnapshot();Check.Equal(0,before.Count(r=>SneakScaledGrantFixture.ChildBase(r.Bytes)),"No initial hidden child base of ANY level before readiness");
            Check.Equal(0,before.Count(r=>SneakScaledGrantFixture.ChildMembership(r.Bytes)),"No initial membership of ANY level before readiness");
            int cursor=before.Count;p.SendRpc("0c 01");
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>SneakScaledGrantFixture.Membership(r.Bytes,level)),"Sourced initial1111 membership",TimeSpan.FromSeconds(15));
            var received=p.World.RpcSnapshot().Skip(cursor).ToArray();int b=Array.FindIndex(received,r=>SneakScaledGrantFixture.Base(r.Bytes,level)),m=Array.FindIndex(received,r=>SneakScaledGrantFixture.Membership(r.Bytes,level));
            Check.That(b>=0&&m>b,"Sourced base precedes membership after actual ready");
            int features=p.World.RpcSnapshot().Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x81&&r.Bytes[1]==0x49);p.SendRpc("0c 01");p.SendRpc("81 48");
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x81&&r.Bytes[1]==0x49)>features,"Actual feature reply after duplicate ready",TimeSpan.FromSeconds(15));
            Check.Equal(1,p.World.RpcSnapshot().Count(r=>SneakScaledGrantFixture.Membership(r.Bytes,level)),"Duplicate ready is idempotent");
            proofs.Add(new{Stage=stage,p.CharId,Level=level,ReadyRpcCursor=cursor,BaseIndex=b,MembershipIndex=m});
        }
        async Task ActivateAndOff(HeadlessPlayer p,ushort level)
        {
            // Exceed the existing .5s duplicate-use guard; no cast/reuse/detection timer is changed.
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds+.1));
            int cursor=p.World.RpcSnapshot().Count;p.SkipReceived();Cast(p,1111);
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>SneakScaledGrantFixture.Stealth209(r.Bytes,level)),"Actual normal1111 Stealth209 uses learned level",TimeSpan.FromSeconds(15));
            await p.WaitForRpcAsync("80 bd 05 57 04 00 00 00 00 00 01");
            var active=p.World.RpcSnapshot().Skip(cursor).Select(r=>Convert.ToHexString(r.Bytes)).ToArray();
            Check.Equal(1,p.World.RpcSnapshot().Skip(cursor).Count(r=>SneakScaledGrantFixture.Stealth209(r.Bytes,level)),"One sourced209 activation record");
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds+.1));
            p.SkipReceived();Cast(p,1111);await p.WaitForRpcAsync("80 bd 05 57 04 00 00 00 00 00 00");
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>SneakScaledGrantFixture.Stealth209(r.Bytes,level,true)),"Actual209 removal keeps sourced snapshot",TimeSpan.FromSeconds(15));
            proofs.Add(new{Stage=stage,p.CharId,CharacterLevel=50,LearnedLevel=level,ActualActivationRpcs=active});
        }
        try
        {
            var actors=new List<(HeadlessPlayer Player,ushort Level)>();
            foreach(ushort level in new ushort[]{2,17,50})
            {
                stage="pre-Margin-seed-"+level;
                var p=await context.AddPlayerAsync(new CharacterSeed("Sneak"+level){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=1,
                    X=17000+actors.Count*200,Y=495,Z=2300,OwnedLoadedAbilities=SneakScaledGrantFixture.Loadout(level)});sessions.Add(p);actors.Add((p,level));
                string archive=await Archive(p);await NoChildArchive(p);proofs.Add(new{Stage=stage,p.CharId,LearnedLevel=level,ArchiveBeforeWorld=archive});
                await p.EnterWorldAsync();await Ready(p,level);stage="normal-cast-"+level;await ActivateAndOff(p,level);
            }
            var selected=actors.Single(a=>a.Level==17);var mid=selected.Player;string originalArchive=await Archive(mid),originalAccount=await Account(mid);
            stage="ordinary-parent-unload-reload";int cursor=mid.World.RpcSnapshot().Count;
            SetAbilityLoaded(mid,SneakScaledGrantFixture.ParentSlot,151,17,false);
            await mid.WaitUntilAsync(()=>mid.World.RpcSnapshot().Skip(cursor).Any(r=>SneakScaledGrantFixture.Membership(r.Bytes,17,true)),"Normal151 unload removes exact17 membership",TimeSpan.FromSeconds(15));
            var removed=mid.World.RpcSnapshot().Skip(cursor).ToArray();int unload=Array.FindIndex(removed,r=>r.Bytes.SequenceEqual(ServerPackets.BuildAbilityUnload(1111))),off=Array.FindIndex(removed,r=>SneakScaledGrantFixture.Membership(r.Bytes,17,true));
            Check.That(unload>=0&&off>unload,"Hidden child unload precedes membership removal");
            cursor=mid.World.RpcSnapshot().Count;SetAbilityLoaded(mid,SneakScaledGrantFixture.ParentSlot,151,17,true);
            await mid.WaitUntilAsync(()=>mid.World.RpcSnapshot().Skip(cursor).Any(r=>SneakScaledGrantFixture.Membership(r.Bytes,17)),"Normal151 reload restores17",TimeSpan.FromSeconds(15));
            await ActivateAndOff(mid,17);Check.Equal(originalArchive,await Archive(mid),"Normal unload/reload preserves exact owned rows");await NoChildArchive(mid);
            stage="normal-relog";string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={mid.CharId}");
            uint id=mid.CharId;mid.SkipReceived();mid.SendRpc("80 fe 00");await mid.WaitForRpcAsync("80 fd");mid.Dispose();await Task.Delay(TimeSpan.FromSeconds(33));
            var restored=await HeadlessPlayer.LoginAsync(context.Server.Address,username,"Sneak17");sessions.Add(restored);Check.Equal(id,restored.CharId,"Same owned character");
            await restored.EnterWorldAsync();await Ready(restored,17);await ActivateAndOff(restored,17);
            Check.Equal(originalArchive,await Archive(restored),"Relog keeps owned parent/prerequisite rows and levels");Check.Equal(originalAccount,await Account(restored),"No XP/cash changes from grant lifecycle");await NoChildArchive(restored);
            foreach(var p in sessions)Check.Equal(0,p.World.Problems.Count,"Actual framing across owners and relog");passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            var finalRows=new List<object>();foreach(var p in sessions.GroupBy(p=>p.CharId).Select(g=>g.Last()))
                try{finalRows.Add(new{p.CharId,Archive=await Archive(p),Account=await Account(p)});}catch(Exception error){finalRows.Add(new{p.CharId,ReadError=error.ToString()});}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-sneak-scaled-grant-"+context.Database.Name),packet=InteriorAdmissionDeliveryProof.Read(Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt"));
            await File.WriteAllTextAsync(stem+"-PacketLog.txt",packet);await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n---Debug---\n"+context.Server.DebugLog());
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Database=context.Database.Name,ServerPid=context.Server.ProcessId,
                Source=context.Server.SourceAssembly,SourceSha256=context.Server.SourceAssemblySha256,ExpectedSource=Environment.GetEnvironmentVariable(SneakScaledGrantFixture.SourcePin),Proofs=proofs,FinalRows=finalRows,
                RawPacketLog=stem+"-PacketLog.txt",RawPacketSha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(packet))),
                Sessions=sessions.Select(p=>new{p.CharId,p.Handle,Rpcs=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems.ToArray()}),
                Qualification="Exact151->1111 only; captures50 only, low levels use stored function2. No active-buff refresh/detection/noise/movement or native listing claim. Test NPC list is >8k from clear pose, aggro/wander off; no RNG/detection-rule override."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("SNEAK GRANT EVIDENCE="+stem);foreach(var p in sessions)p.Dispose();
        }
    }
}
