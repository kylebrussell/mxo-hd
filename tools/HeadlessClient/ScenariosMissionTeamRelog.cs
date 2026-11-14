namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MissionTeamRelog(ScenarioContext context) => MissionRelogReplay(context, false);
    private static Task MissionObjectivesRelog(ScenarioContext context) => MissionRelogReplay(context, true);
    private static Task MissionObjectiveUiRelog(ScenarioContext context) => MissionRelogReplay(context,true,true);
    private static async Task MissionRelogReplay(ScenarioContext context, bool checkObjectiveRows, bool checkLoadingReplay=false)
    {
        var original=await StartRelogMission(context);
        var npc=await VisitMissionNpc(context,original,"Herschel");
        original.SendRpc($"80 c7 {MissionNpcId(npc)} 0d 00"); await original.WaitForRpcAsync("80 a0 01 06 00 01");
        var saved=await MissionCheckpoint(context,original);
        original.SendChat("?save"); await original.WaitForRpcAsync("2e 07");
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        original.SendRpc("80 fe 00"); await original.WaitForRpcAsync("80 fd"); original.Dispose();
        // Real CleanDeadPlayers drops the old live group and margin reservation; no server restart or simulated restore.
        await Task.Delay(TimeSpan.FromSeconds(33));
        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,username,original.Handle);
        await p.EnterWorldAsync(!checkLoadingReplay); await p.WaitForRpcAsync("80 9c");
        await p.WaitUntilAsync(()=>p.Received("80 8e").Count>0,"restored native team information",TimeSpan.FromSeconds(6));
        if(checkLoadingReplay)
        {
            Check.Equal(0,p.Received("80 a0").Count,"No restored objective record before LoadingComplete");
            int before=p.World.RpcSnapshot().Count;
            p.SendRpc("0c 00");await Task.Delay(200);
            Check.Equal(0,p.Received("80 a0").Count,"Wrong loading status cannot replay goals");
            p.SendRpc("0c 01");
            await p.WaitUntilAsync(()=>p.Received("80 a0 01 06 00 01").Count==1,"post-loading completed tick",TimeSpan.FromSeconds(6));
            var replay=p.World.RpcSnapshot().Skip(before).Where(r=>r.Opcode==0xa0).ToList();
            Check.Equal(3,replay.Count,"One loading replay: two open widgets then one completed tick");
            Check.That(replay[0].Bytes[2]==1 && replay[0].Bytes[5]==0 && replay[1].Bytes[2]==2 && replay[1].Bytes[5]==0 && replay[2].Bytes[2]==1 && replay[2].Bytes[5]==1,"Goal IDs and post-loading row/tick order");
            Check.That(replay[0].DatagramSequence.HasValue && replay[2].DatagramSequence.HasValue && replay[0].DatagramSequence!=replay[2].DatagramSequence && replay[1].DatagramSequence!=replay[2].DatagramSequence,"Completion received in a different actual UDP datagram from both row creations");
            p.SendRpc("0c 01");await Task.Delay(700);
            Check.Equal(3,p.Received("80 a0").Count,"Duplicate LoadingComplete never replays again");
        }
        if (checkObjectiveRows)
        {
            await p.WaitForRpcAsync("80 a0 01 06 00 01");
            var objectives = p.World.RpcSnapshot().Where(r => r.Bytes.Length >= 6 && r.Bytes[0] == 0x80 && r.Bytes[1] == 0xa0).Select(r => r.Bytes).ToList();
            int created = objectives.FindIndex(r => r[2] == 1 && r[5] == 0);
            int completed = objectives.FindIndex(r => r[2] == 1 && r[5] == 1);
            Check.That(created >= 0 && completed > created, "Completed objective row is created before its restored tick");
            Check.Equal(1, objectives.Count(r => r[2] == 2 && r[5] == 0), "Second identical-text Get remains a distinct unfinished row");
            Check.Equal(0, objectives.Count(r => r[2] == 2 && r[5] == 1), "Replay never completes the second Get");
        }
        var records=p.World.RpcSnapshot();
        int teamAt=records.FindIndex(r=>r.Opcode==0x8e),missionAt=records.FindIndex(r=>r.Opcode==0x9c);
        Check.That(teamAt>=0 && teamAt<missionAt,"Native team state precedes restored mission HUD");
        byte[] team=records[teamAt].Bytes; uint teamId=BitConverter.ToUInt32(team,6);
        Check.Equal(p.CharId,BitConverter.ToUInt32(team,2),"Team bootstrap belongs to authenticated player");
        Check.Equal(p.CharId,BitConverter.ToUInt32(team,12),"Solo player is the cached native leader");
        int members=BitConverter.ToUInt16(team,17);
        Check.Equal((ushort)1,BitConverter.ToUInt16(team,members),"One actual team member");
        Check.Equal(p.CharId,BitConverter.ToUInt32(team,members+3),"Own member in full team bootstrap");
        var ownId=BytePattern.Parse("02 00 02 80 20 "+Le(teamId));
        await p.WaitUntilAsync(()=>p.World.ViewSnapshot().Any(ownId.Occurs),"nonzero own MissionTeamID update",TimeSpan.FromSeconds(6));
        var restored=await MissionCheckpoint(context,p);
        Check.Equal(saved.Identity,restored.Identity,"Team bootstrap preserves mission identity");
        Check.That(restored.Done.Contains(1),"First Get remains complete");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6825"),"No duplicate grant");
        if (checkObjectiveRows)
        {
            var giver = await VisitMissionNpc(context,p,"Herschel");
            p.SendRpc($"80 c7 {MissionNpcId(giver)} 0d 00"); await p.WaitForRpcAsync("80 a0 02 06 00 01");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6825"),"Completed Get never grants again");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6820"),"Remaining Get grants its own second device once");
            Check.That((await MissionCheckpoint(context,p)).Done.Contains(2),"Only the normal second Get completes its objective");
        }
        // This proves the server path only; Claude must verify the actual UI now emits its Abort request.
        p.SendRpc("80 a6"); await p.WaitForRpcAsync("80 a2 08");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId} AND payload IS NOT NULL"),"Abort clears durable mission");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6825"),"Abort removes mission ownership once");
        if(checkObjectiveRows) Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6820"),"Abort also cleans the second device");
        Check.Equal(0,p.World.Problems.Count,"Team replay and Abort protocol problems");
        string proof=checkObjectiveRows ? Path.Combine(Path.GetTempPath(),"mxo-mission-objectives-success-"+Guid.NewGuid().ToString("N")+".log")
            : Path.Combine(context.Server.WorkingDirectory,"MissionTeamRelog-Proof.txt");
        File.WriteAllText(proof,context.Server.DebugLog()); Console.WriteLine("Team relog debug proof: "+proof);
    }
}
