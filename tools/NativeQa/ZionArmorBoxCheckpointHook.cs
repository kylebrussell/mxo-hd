using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
namespace NativeQa;

public static class ZionArmorBoxCheckpointHook
{
    public static async Task<byte[]> RunAsync(NativeQaBootstrapContext context)
    {
        ZionArmorBoxCheckpoint.RequireContext(context);var db=context.OwnedDatabase!;var server=context.ActualServer!;
        var expected=ZionArmorBoxCheckpoint.Verified();var witness=expected.ElevatorWitnesses.Single();var obj=expected.Objects.Single();var pose=obj.OpenFloor!;
        HeadlessPlayer? actor=null;string stage="persisted-before-login",beforeHex="",afterHex="",beforeAccount="",beforeItems="",receipts="";bool passed=false;Exception? failure=null;object? receipt=null;
        var fields=new JsonSerializerOptions{IncludeFields=true};
        async Task<string> Payload()=>await db.ScalarAsync("SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId=110");
        async Task<string> Receipts()=>await db.ExecuteAsync("SELECT missionId FROM char_missions WHERE charId=110 ORDER BY missionId");
        async Task<string> Account()=>await db.ExecuteAsync("SELECT exp,cash FROM characters WHERE charId=110");
        async Task<string> Items()=>await db.ExecuteAsync("SELECT charId,goid,slot,count FROM inventory WHERE charId=110 ORDER BY slot,goid");
        void Identity(string hex)
        {
            var s=JsonSerializer.Deserialize<MissionProgress.SavedState>(ZionArmorBoxCheckpoint.StrictUtf8Hex(hex),fields)??throw new ScenarioFailure("No persisted restored mission");
            Check.Equal(expected.Identity,s.Identity,"Earned mission identity");Check.Equal(expected.Started,s.Started,"Earned mission start");Check.Equal(expected.Mission,s.Mission,"Earned selected mission");Check.Equal(expected.Phase,s.Phase,"No beyond-capture phase");
            Check.Equal(JsonSerializer.Serialize(expected.Objects,fields),JsonSerializer.Serialize(s.Objects,fields),"Immutable captured raw objects and fixed DTO");
            Check.Equal(JsonSerializer.Serialize(expected.ElevatorWitnesses,fields),JsonSerializer.Serialize(s.ElevatorWitnesses,fields),"Exact persisted measured witness");
            Check.Equal(expected.OpenFloorCurrentSiteIndex,s.OpenFloorCurrentSiteIndex,"Current captured site index");
            Check.Equal(JsonSerializer.Serialize(expected.OpenFloorSites,fields),JsonSerializer.Serialize(s.OpenFloorSites,fields),"Captured full site history");
            Check.Equal(0,s.Items.Count,"No earned item injected or taken");
            ZionArmorBoxCheckpoint.RequireUnchangedGoals(expected,s);
        }
        try
        {
            beforeHex=await Payload();Check.Equal(Convert.ToHexString(ZionArmorBoxCheckpoint.Payload),beforeHex,"Exact capture bytes before restore");Identity(beforeHex);
            receipts=await Receipts();Check.That(receipts.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).SequenceEqual(ZionArmorBoxCheckpoint.EarlierReceipts.Order()),"Only earlier receipts");beforeAccount=await Account();beforeItems=await Items();
            stage="ordinary-restore";actor=await HeadlessPlayer.LoginAsync(context.Address,"CSR_AbilityQA","CSR_Tester");Check.Equal(110u,actor.CharId,"Actual CSR110");await actor.EnterWorldAsync();
            await actor.WaitForRpcAsync("80 9c");Identity(await Payload());
            var own=actor.World.CreationSnapshot().Single(v=>v.ViewId==2);
            Check.That(!actor.World.CreationSnapshot().Any(v=>v.Goid==548&&v.Attributes.TryGetValue("Position",out var p)&&p.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(p,i*8)==pose.Centre[i])),"No early captured free548 before Ride");
            stage="ordinary-lobby-ride";string packetPath=Path.Combine(server.WorkingDirectory,"PacketLog.txt");int packetCursor=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c=='\n'),viewCursor=actor.World.CreationSnapshot().Count;
            actor.SendRpc("80 c8 "+WireFormat.ToHex(BitConverter.GetBytes(witness.LobbyKey))+" 03 00");
            await actor.WaitUntilAsync(()=>ZionArmorBoxCheckpoint.CurrentBody(actor.World.CreationSnapshot(),viewCursor,own,pose)!=null,"Captured548 exact key/orientation/current-ownlife after stationary ordinary Ride",TimeSpan.FromSeconds(15));
            receipt=InteriorAdmissionDeliveryProof.Require(InteriorAdmissionDeliveryProof.Read(packetPath),110,packetCursor,pose.Route.Take(3).ToArray(),548,pose.Centre);
            Identity(await Payload());Check.Equal(receipts,await Receipts(),"No active/later receipt");Check.Equal(beforeItems,await Items(),"No probe item mutation");Check.Equal(beforeAccount,await Account(),"No reward/XP injection");
            // Authored goals do not yet allow a downward ride. Release normally from this accepted floor.
            stage="ordinarylogout";actor.SkipReceived();actor.SendRpc("80 fe 00");await actor.WaitForRpcAsync("80 fd");actor.Dispose();await Task.Delay(TimeSpan.FromSeconds(33));
            afterHex=await Payload();Identity(afterHex);Check.Equal(receipts,await Receipts(),"Receipt boundary after release");Check.Equal(beforeItems,await Items(),"Owned items after release");Check.Equal(beforeAccount,await Account(),"Account after release");Check.Equal(0,actor.World.Problems.Count,"Actual current CSR parser");passed=true;stage="complete";
            return Array.Empty<byte>();
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            try
            {
                if(afterHex.Length==0)try{afterHex=await Payload();}catch(Exception e){afterHex="READ FAILED: "+e.Message;}
                Directory.CreateDirectory(context.OutputDirectory);string raw=InteriorAdmissionDeliveryProof.Read(Path.Combine(server.WorkingDirectory,"PacketLog.txt"));
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"checkpoint-PacketLog.txt"),raw);
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"checkpoint-proof.json"),JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Database=db.Name,server.ProcessId,server.SourceAssembly,server.SourceAssemblySha256,CaptureSha256=ZionArmorBoxCheckpoint.PayloadSha256,BeforeHex=beforeHex,AfterHex=afterHex,BeforeAccount=beforeAccount,BeforeItems=beforeItems,EarlierReceipts=receipts,ArrivalReceipt=receipt,NativeStart="Captured floor after ordinary headless entry and normal logout; not native door-entry proof",RawSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))),Rpcs=actor?.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),Views=actor?.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=actor?.World.Problems.ToArray(),NativeRenderingProven=false,NativeDoorEntryProven=false,NewChainProven=false},new JsonSerializerOptions{WriteIndented=true}));
            }
            finally{actor?.Dispose();}
        }
    }
}
