namespace HeadlessClient;

public static class TeamMissionFixture
{
    public const int HolderX = 23828, HelperX = 23878, Y = 495, Z = 13984;
}

public static partial class Scenarios
{
    private static async Task TeamSharedMissionKill(ScenarioContext context)
    {
        var seed=new CharacterSeed("TeamHolder"){Level=50,Organization=3,X=TeamMissionFixture.HolderX,Y=TeamMissionFixture.Y,Z=TeamMissionFixture.Z};
        var players=await context.AddPlayersInWorldAsync(seed,seed with {Handle="TeamHelper",X=TeamMissionFixture.HelperX},
            seed with {Handle="TeamOutsider",Z=TeamMissionFixture.Z+70});
        var owner=players[0];var helper=players[1];var outsider=players[2];
        owner.SendRpc(GroupInvite(Team,helper.Handle,""));await helper.WaitForRpcAsync("80 8f 0c 00");
        helper.SendRpc(InviteAnswer(Team,0,owner.Handle));await helper.WaitForRpcAsync("80 8e");
        owner.SendRpc("80 94 05 00 03");await owner.WaitForTextAsync("Team Progress Fixture");await owner.WaitForRpcAsync("80 97 05 00");
        owner.SendRpc("80 98 06 00 00 00 01 02 00");await owner.WaitForRpcAsync("80 99");
        owner.SendRpc("80 9b 07 00 01");await owner.WaitForRpcAsync("80 9c");
        var target=await helper.WaitForViewNamedAsync("Team Security");
        int helperStart=helper.World.RpcSnapshot().Count,outsideStart=outsider.World.RpcSnapshot().Count;
        helper.SendRpc("3d 01");
        helper.SendRpc($"40 {Le(target.ViewId)} {Le((ushort)target.SpawnId)} 00 00 00 00 00 00 00");
        await owner.WaitForRpcAsync("80 a0 01 06 00 01");await helper.WaitForTextAsync("teammate's mission progress");
        var saved=await MervFollowupCheckpoint(context,owner);Check.That(saved.Done.Contains(1),"Teammate kill durably completes the holder's goal");
        Check.Equal(1,saved.Mobs.Count(m=>m.Npc==1&&m.Dead),"Shared kill is settled once in the holder's checkpoint");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId IN ({helper.CharId},{outsider.CharId}) AND payload IS NOT NULL"),"Helping never creates another mission checkpoint");
        Check.Equal(0,outsider.World.RpcSnapshot().Skip(outsideStart).Count(r=>Contains(r.Bytes,"teammate's mission progress")),"Outsider does not receive team progress");
        Check.Equal(1,helper.World.RpcSnapshot().Skip(helperStart).Count(r=>Contains(r.Bytes,"teammate's mission progress")),"One shared-kill notice");
        var contact=await owner.WaitForViewNamedAsync("Team Contact");owner.SendRpc($"80 c7 {MissionNpcId(contact)} 09 00");await owner.WaitForRpcAsync("80 a2 00");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={owner.CharId} AND missionId=399911"),"Holder receives exactly one completion receipt");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId IN ({helper.CharId},{outsider.CharId}) AND missionId=399911"),"No invented helper or outsider receipt");
    }
}
