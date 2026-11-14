using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MissionLootClaimClose(ScenarioContext context)
    {
        var p = await context.AddPlayerAsync(new CharacterSeed("LootClaim") { Level=50, Organization=2,
            Experience=(int)PlayerProgression.ExperienceForLevel(50), X=17000,Y=495,Z=2300,
            Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,
            Items=Enumerable.Range(1,96).Select(slot=>(slot,2568u,1)).ToArray() });
        await context.Database.ExecuteAsync(MachinesChainFixture.SeedPrerequisites(p.CharId));
        string step="entry"; Exception? failure=null; var checkpoints=new List<object>(); var operations=new List<object>();
        bool trigger=false; var options=new JsonSerializerOptions { IncludeFields=true };
        async Task<MissionProgress.SavedState> Saved()
        {
            string raw=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={p.CharId}");
            checkpoints.Add(new { Step=step, Payload=raw });
            return JsonSerializer.Deserialize<MissionProgress.SavedState>(raw,options)!;
        }
        async Task Barrier() { p.SendRpc("81 48"); await p.WaitForRpcAsync("81 49"); }
        async Task<ViewCreation> Safe(int creationCursor)
        {
            var saved=await Saved(); var source=saved.Objects.Single(o=>o.Id==1);
            Check.Equal(0x0d10003eu,source.Spot.mxoStaticId,"Existing seed source remains naturally selected");
            var w=MissionOpenFloorWitness.Read(JsonSerializer.SerializeToElement(source.OpenFloor,options));
            w.Require(2201,0,1,0x0d10003e,1410,552);
            for(int i=0;i<w.Route.Count;i+=3) await MoveForMissionTest(p,w.Route[i],w.Route[i+1],w.Route[i+2]);
            bool AtPlan(ViewCreation v)=>v.Goid==552&&v.Attributes.TryGetValue("Position",out var at)&&at.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(at,i*8)==w.Centre[i])&&v.Attributes.TryGetValue("Orientation",out var orientation)&&orientation.SequenceEqual(Convert.FromHexString(w.Quaternion));
            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(creationCursor).Any(AtPlan),"Current resolved Safe creation",TimeSpan.FromSeconds(10));
            return p.World.CreationSnapshot().Skip(creationCursor).Last(AtPlan);
        }
        async Task<byte[]> Open(ViewCreation safe)
        {
            p.SkipReceived(); p.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
            var window=(await p.WaitForRpcAsync("81 19")).Bytes;
            Check.Equal((ushort)1,BitConverter.ToUInt16(window,16),"One remaining dossier in ordinary Safe window");
            Check.Equal(6799u,BitConverter.ToUInt32(window,22),"Window item is dossier6799");
            return window[18..26];
        }
        try
        {
            await p.EnterWorldAsync(); step="normal-accept"; int creationCursor=p.World.CreationSnapshot().Count; int cursor=p.World.RpcSnapshot().Count;
            p.SendRpc("80 94 05 00 02"); await p.WaitForRpcAsync("80 97 05 00");
            var entries=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
            int selected=MissionContactChoice.CriticalIndex(entries,"The End of the Beginning");
            p.SendRpc($"80 98 06 00 {Le((ushort)selected)} 01 02 00"); await p.WaitForRpcAsync("80 99"); p.SendRpc("80 9b 07 00 01"); await p.WaitForRpcAsync("80 9e 00");
            Check.Equal(2201,(await Saved()).Mission,"Normal selected mission, never granted");
            await EnterMissionArea(context,p); var safe=await Safe(creationCursor);
            step="full-bag"; byte[] identity=await Open(safe); cursor=p.World.RpcSnapshot().Count;
            p.SendRpc("81 1f"); var full=await p.WaitForRpcAsync("81 20"); await Barrier();
            Check.Equal((ushort)0,BitConverter.ToUInt16(full.Bytes,12),"Full bag removes no item identities");
            Check.Equal(0,p.World.RpcSnapshot().Skip(cursor).Count(r=>r.Bytes.SequenceEqual(new byte[]{0x81,0x1a})),"Full bag keeps Safe window open");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6799"),"Full bag grants no dossier");
            Check.That(!(await Saved()).Done.Contains(1),"Full bag leaves Search pending");
            operations.Add(new { Step=step, Records=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Hex).ToArray() });
            // Retrying an unchanged full bag is harmless; closing/reopening retains exact item identity.
            p.SkipReceived(); p.SendRpc("81 1f"); Check.Equal((ushort)0,BitConverter.ToUInt16((await p.WaitForRpcAsync("81 20")).Bytes,12),"Repeated full-bag claim remains empty");
            p.SendRpc("81 1a"); await Barrier(); Check.That(identity.SequenceEqual(await Open(safe)),"Reopen retains exact unclaimed identity");
            await FreeLastBagSlot(p);
            step="late-checkpoint-rollback"; await context.Database.InstallMissionRollbackTriggerAsync(false); trigger=true;
            creationCursor=p.World.CreationSnapshot().Count; cursor=p.World.RpcSnapshot().Count; p.SkipReceived(); p.SendRpc("81 1f"); await p.WaitForTextAsync("Mission progress could not be saved"); await Barrier();
            var rolled=p.World.RpcSnapshot().Skip(cursor).ToArray();
            var retryAck=rolled.Where(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x81&&r.Bytes[1]==0x20).ToArray();
            Check.Equal(1,retryAck.Length,"Late rollback acknowledges the pending request exactly once");
            Check.Equal("81200c0000000000000000000000",Convert.ToHexString(retryAck[0].Bytes).ToLowerInvariant(),"Rollback acknowledges zero claims with unchanged response fields");
            Check.Equal(0,rolled.Count(r=>r.Bytes.SequenceEqual(new byte[]{0x81,0x1a})),"Late rollback keeps the container window open");
            Check.Equal(0,rolled.Count(r=>BytePattern.Parse("80 a0 01 06 00 01").Occurs(r.Bytes)),"Late rollback emits no done goal");
            Check.Equal(0,rolled.Count(r=>BytePattern.Parse("5e ?? ?? 8f 1a 00 00").Occurs(r.Bytes)),"Late rollback emits no inventory-add success");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6799"),"Late rollback undoes staged item ownership");
            var restored=await Saved(); Check.That(!restored.Items.ContainsKey(6799)&&!restored.Done.Contains(1),"Item and goal roll back together");
            operations.Add(new { Step=step, Records=rolled.Select(r=>r.Hex).ToArray() });
            await context.Database.RemoveMissionRollbackTriggerAsync(); trigger=false;
            step="ordinary-retry"; safe=await Safe(creationCursor); Check.That(identity.SequenceEqual(await Open(safe)),"Rollback/reopen retains exact claimed identity");
            cursor=p.World.RpcSnapshot().Count; p.SkipReceived(); p.SendRpc("81 1f"); var claim=await p.WaitForRpcAsync("81 20"); await p.WaitForRpcAsync("81 1a"); await p.WaitForRpcAsync("80 a0 01 06 00 01"); await Barrier();
            Check.Equal((ushort)1,BitConverter.ToUInt16(claim.Bytes,12),"Successful claim removes one identity");
            Check.That(identity.SequenceEqual(claim.Bytes[14..22]),"Ack is byte-exact original window identity, not inventory slot");
            var success=p.World.RpcSnapshot().Skip(cursor).ToArray();
            int ack=Array.FindIndex(success,r=>r.Bytes.Length>1&&r.Bytes[0]==0x81&&r.Bytes[1]==0x20), close=Array.FindIndex(success,r=>r.Bytes.SequenceEqual(new byte[]{0x81,0x1a})), goal=Array.FindIndex(success,r=>BytePattern.Parse("80 a0 01 06 00 01").Occurs(r.Bytes));
            Check.That(ack>=0&&close>ack&&goal>close,"Claim and close precede the goal RPC");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6799 AND slot=96"),"One earned dossier in freed bag slot");
            operations.Add(new { Step=step, Records=success.Select(r=>r.Hex).ToArray() });
            step="repeat-after-close"; p.SkipReceived(); p.SendRpc("81 1f"); var repeat=await p.WaitForRpcAsync("81 20"); await Barrier();
            Check.Equal((ushort)0,BitConverter.ToUInt16(repeat.Bytes,12),"Repeat has no new claimed identities");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6799"),"Repeat cannot duplicate dossier");
            foreach(var session in new[]{p}) Check.Equal(0,session.World.Problems.Count,"Parser clean");
            step="complete";
        }
        catch(Exception e) { failure=e; try { await Saved(); } catch { } throw; }
        finally
        {
            try
            {
                if(trigger) await context.Database.RemoveMissionRollbackTriggerAsync();
                string dll=context.Server.SourceAssembly, stem=Path.Combine(Path.GetTempPath(),"mxo-mission-loot-claim-close-"+context.Database.Name);
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new { Passed=step=="complete"&&failure==null, Step=step, Failure=failure?.ToString(), context.Database.Name,
                    ServerPid=context.Server.ProcessId, SourceAssembly=dll, SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name, SourceMtimeUtc=File.GetLastWriteTimeUtc(dll), SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),
                    Checkpoints=checkpoints, Operations=operations, Rpc=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(), Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(), Problems=p.World.Problems.ToArray(),
                    NativeUiClosureProven=false, NativePickingProven=false, NativeWalkingProven=false },new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog()); Console.WriteLine("LOOT CLAIM EVIDENCE="+stem);
            }
            finally { p.Dispose(); }
        }
    }
}
