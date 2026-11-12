namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MissionTeamRelog(ScenarioContext context)
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
        await p.EnterWorldAsync(); await p.WaitForRpcAsync("80 9c");
        await p.WaitUntilAsync(()=>p.Received("80 8e").Count>0,"restored native team information",TimeSpan.FromSeconds(6));
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
        // This proves the server path only; Claude must verify the actual UI now emits its Abort request.
        p.SendRpc("80 a6"); await p.WaitForRpcAsync("80 a2 08");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId} AND payload IS NOT NULL"),"Abort clears durable mission");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6825"),"Abort removes mission ownership once");
        Check.Equal(0,p.World.Problems.Count,"Team replay and Abort protocol problems");
        string proof=Path.Combine(context.Server.WorkingDirectory,"MissionTeamRelog-Proof.txt");
        File.WriteAllText(proof,context.Server.DebugLog()); Console.WriteLine("Team relog debug proof: "+proof);
    }
}
