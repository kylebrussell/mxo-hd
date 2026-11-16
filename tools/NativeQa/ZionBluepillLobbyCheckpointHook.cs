using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
namespace NativeQa;
// A captured-state diagnostic, never a newly earned chain or native walking/render proof.
public static class ZionBluepillLobbyCheckpointHook
{
    public static async Task<byte[]> RunAsync(NativeQaBootstrapContext context)
    {
        ZionBluepillLobbyCheckpoint.RequireContext(context);
        var db=context.OwnedDatabase!;var server=context.ActualServer!;
        var expected=ZionBluepillLobbyCheckpoint.Verify(ZionBluepillLobbyCheckpoint.Payload);
        var directory=new DirectoryInfo(Path.GetDirectoryName(server.SourceAssembly)!);
        while(directory!=null&&!File.Exists(Path.Combine(directory.FullName,"data","ability_catalog.json")))directory=directory.Parent;
        string root=directory?.FullName??throw new InvalidDataException("Actual source repository missing");
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        var plan=MissionPublicStreetProof.ReadCurrent(root,expected);
        HeadlessPlayer? player=null;var proof=new List<object>();bool passed=false;string stage="guarded-seed",afterHex="",account="",items="",receipts="";Exception? failure=null;
        var fields=new JsonSerializerOptions{IncludeFields=true};
        async Task<string> Payload()=>await db.ScalarAsync("SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId=110");
        async Task<string> Account()=>await db.ExecuteAsync("SELECT exp,cash FROM characters WHERE charId=110");
        async Task<string> Items()=>await db.ExecuteAsync("SELECT charId,goid,slot,count FROM inventory WHERE charId=110 ORDER BY slot,goid");
        async Task<string> Receipts()=>await db.ExecuteAsync("SELECT missionId FROM char_missions WHERE charId=110 ORDER BY missionId");
        async Task Same()
        {
            afterHex=await Payload();var current=JsonSerializer.Deserialize<MissionProgress.SavedState>(ZionArmorBoxCheckpoint.StrictUtf8Hex(afterHex),fields)??throw new InvalidDataException("Captured mission missing");
            ZionBluepillLobbyCheckpoint.RequireUnchanged(expected,current);
            Check.Equal(account,await Account(),"No probe XP/cash change");Check.Equal(items,await Items(),"No probe inventory change");Check.Equal(receipts,await Receipts(),"No probe receipt change");
        }
        async Task Barrier(){player!.SkipReceived();player.SendRpc("81 48");await player.WaitForRpcAsync("81 49");}
        async Task Move(double[] at)
        {
            await Same();player!.SkipReceived();player.SendChat(FormattableString.Invariant($"?tp {at[0]} {at[1]} {at[2]} 0 0 0"));await player.WaitForTextAsync("Teleported to 0/0");await Barrier();await Same();
        }
        async Task Open()
        {
            await Same();var life=player!.World.CurrentOwnViewLife();int views=player.World.ViewSnapshot().Count,rpcs=player.World.RpcSnapshot().Count,debug=server.DebugLog().Length;
            player.SendRpc("80 c8 "+WireFormat.ToHex(BitConverter.GetBytes(plan.Door.Key))+" 03 00");await Barrier();
            await player.WaitUntilAsync(()=>
            {
                string log=server.DebugLog();if(log.Length<debug||!log[debug..].Contains($"[DOOR] Use of static {plan.Door.Key} type {plan.Door.Goid} (",StringComparison.Ordinal))return false;
                try{_=NpcInteriorComputerState.CurrentDoor(player.World.CurrentOwnViewLife(),plan.Door);return true;}catch(InvalidDataException){return false;}
            },"Fresh ordinary public Open and exact current0834",TimeSpan.FromSeconds(15));
            await Barrier();Check.Equal(0,MissionPublicStreetWire.Teleports(player.World.CurrentOwnViewLife(),life,views).Count,"Public Open is not a mission car ride");
            proof.Add(new{Stage=stage,plan.Door,BeforeViewCursor=views,BeforeRpcCursor=rpcs,DebugOffset=debug,Current=NpcInteriorComputerState.CurrentDoor(player.World.CurrentOwnViewLife(),plan.Door),Qualification="Fresh server request log/current0834 plus later barrier; not exact03 ACK"});await Same();
        }
        try
        {
            // Context/source/capture guards execute again at the SQL-producing boundary.
            await db.ExecuteAsync(ZionBluepillLobbyCheckpoint.SeedSql(context,plan.Outside[^1]));
            Check.Equal(Convert.ToHexString(ZionBluepillLobbyCheckpoint.Payload),await Payload(),"Verbatim capture seeded unchanged");
            account=await Account();items=await Items();receipts=await Receipts();
            Check.That(receipts.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).SequenceEqual(ZionBluepillLobbyCheckpoint.EarlierReceipts.Order()),"29 earlier story receipts plus standard986 only");
            stage="ordinary-login";player=await HeadlessPlayer.LoginAsync(context.Address,"CSR_AbilityQA","CSR_Tester");Check.Equal(110u,player.CharId,"Owned CSR110");await player.EnterWorldAsync();await player.WaitForRpcAsync("80 9c");await Same();
            stage="outside-public-open";foreach(var point in plan.Outside)await Move(point);await Open();
            stage="derived-public-path-to-lobby";foreach(var point in plan.Inside)await Move(point);
            await player.WaitUntilAsync(()=>player.World.RpcSnapshot().Where(r=>r.Bytes.Length==35&&r.Bytes[0]==0x80&&r.Bytes[1]==0x9e).LastOrDefault() is var latest&&latest!=null&&ZionBluepillLobbyCheckpoint.LobbyWaypoint(latest.Bytes),"Actual lobby809e owns921699406 at native west plane",TimeSpan.FromSeconds(15));
            await Same();proof.Add(new{Stage=stage,LobbyWaypoint=Convert.ToHexString(player.World.RpcSnapshot().Last(r=>ZionBluepillLobbyCheckpoint.LobbyWaypoint(r.Bytes)).Bytes),LobbyStanding=plan.Lobby,CarUsed=false});
            stage="ordinary-public-return";foreach(var point in plan.Inside.Reverse())await Move(point);await Open();foreach(var point in plan.Outside.Reverse())await Move(point);
            // Outside[-1] is the exact independently verified outside approach, not a synthetic car destination.
            await Move(plan.Outside[^1]);await Same();
            stage="ordinary-quit-release";player.SkipReceived();player.SendRpc("80 fe 00");await player.WaitForRpcAsync("80 fd");player.Dispose();await Task.Delay(TimeSpan.FromSeconds(33));await Same();
            string pose=await db.ExecuteAsync("SELECT x,y,z FROM characters WHERE charId=110");var xyz=pose.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(v=>double.Parse(v,System.Globalization.CultureInfo.InvariantCulture)).ToArray();Check.That(xyz.Length==3&&xyz.Zip(plan.Outside[^1]).All(p=>Math.Abs(p.First-p.Second)<.01),"Native starts at derived outside approach after normal release");
            Check.Equal(0,player.World.Problems.Count,"Captured diagnostic framing");passed=true;stage="complete";return Array.Empty<byte>();
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                try{afterHex=await Payload();}catch(Exception error){afterHex="READ FAILED: "+error.Message;}
                Directory.CreateDirectory(context.OutputDirectory);string packet=InteriorAdmissionDeliveryProof.Read(Path.Combine(server.WorkingDirectory,"PacketLog.txt"));
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"bluepill-lobby-PacketLog.txt"),packet);
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"bluepill-lobby-proof.json"),JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Database=db.Name,server.ProcessId,server.SourceAssembly,server.SourceAssemblySha256,CaptureSha256=ZionBluepillLobbyCheckpoint.PayloadSha256,AfterHex=afterHex,Account=account,Inventory=items,Receipts=receipts,Proof=proof,Rpcs=player?.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)),Views=player?.World.ViewSnapshot().Select(Convert.ToHexString),Problems=player?.World.Problems,PacketSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet))),NativeWalkingProven=false,CarRenderingProven=false,NewChainProven=false,Qualification="Captured accepted state; derived NAV movement aids, ordinary public Open/lobby waypoint/return/quit; no car invocation"},new JsonSerializerOptions{WriteIndented=true}));
            }
            finally{player?.Dispose();}
        }
    }
}
