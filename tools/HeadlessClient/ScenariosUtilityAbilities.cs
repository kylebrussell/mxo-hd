namespace HeadlessClient;

public static partial class Scenarios
{
    private static int AbilityGoid(ushort id) => unchecked((int)(0x80000000u | ((uint)id << 10)));

    private static string AbilityLine(uint stringId) => $"81 67 17 00 ?? ?? {Le(stringId)}";

    /// <summary>
    /// A mission safe spawned locked (locked="1"): Pick Lock (use 8) runs DoorPickLock. Without Open Locks loaded, then
    /// without a lockpick, it fails with retail's lines; with both it succeeds, the safe goes to UnlockedState (0x841) and
    /// its Open gives the goal's item.
    /// </summary>
    private static async Task MissionLockedSafe(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("Picker") { Level = 50, Experience = (int)hds.PlayerProgression.ExperienceForLevel(50) });
        await context.Database.ExecuteAsync(LockedSafeLoadoutFixture.SeedSql(player.CharId));
        Check.Equal((long)LockedSafeLoadoutFixture.Rows().Count,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={player.CharId} AND is_loaded=0"),"Only the legitimate owned unloaded prerequisite closure is seeded");
        await player.EnterWorldAsync();
        player.SendRpc("80 94 05 00 03");
        await player.WaitForTextAsync("Locked Safe Fixture");
        await player.WaitForRpcAsync("80 97 05 00");
        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForRpcAsync("80 99");
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(context, player);
        await ApproachMissionObjectFixture(context,player,1);
        var safe = await MissionObjectView(context, player, "Safe");
        Check.Equal(0x836u, BitConverter.ToUInt32(safe.Attributes["CurrentState"]), "The locked safe spawns in its LockedState");

        player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00"); // Open isn't offered while locked; a forged one does nothing
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 08 00");
        await player.WaitForRpcAsync(AbilityLine(0x7a000141)); // "Make sure you have %1z loaded."
        Check.Equal(0, player.Received("81 19").Count, "A locked safe opens no loot window");

        player.SkipReceived();player.SendRpc(LockedSafeLoadoutFixture.LoadRequest());
        await player.WaitForRpcAsync("80 b2 8e 00 0a 00 2c 01"); // Actual selected142/level10/slot300 load acknowledgement.
        Check.Equal((long)LockedSafeLoadoutFixture.Rows().Count,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={player.CharId} AND slot<520 AND is_loaded=1"),"Normal admission loads the complete owned batch");
        player.SendRpc("81 48"); // a round trip, so the loadout change has landed
        await player.WaitForRpcAsync("81 49");
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 08 00");
        await player.WaitForRpcAsync(AbilityLine(0x7a000142)); // "...a lockpick tool in your inventory."

        await context.Database.ExecuteAsync($"INSERT INTO inventory(charId,goid,slot) VALUES ({player.CharId},4,30)");
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 08 00");
        await player.WaitForRpcAsync(AbilityLine(0x7a000145)); // "Lock Picked Successfully!"
        await player.WaitForViewUpdateAsync(safe.ViewId, "01 20 41 08 00 00");
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
        await player.WaitForRpcAsync("81 19");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(0, player.World.Problems.Count, "Locked safe protocol");
    }

    /// <summary>Recall Team Member and Recall Mission Team teleport team members in the district to the caster.</summary>
    private static async Task RecallTeam(ScenarioContext context)
    {
        var caster = await context.AddPlayerAsync(new CharacterSeed("Recaller") { Level = 50 });
        var member = await context.AddPlayerAsync(new CharacterSeed("Recalled") { X = 17020 + 3000, Level = 50 });
        var outsider = await context.AddPlayerAsync(new CharacterSeed("Outsider") { X = 17020 - 3000, Level = 50 });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={caster.CharId}; " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({caster.CharId},300,{AbilityGoid(382)},50,1),({caster.CharId},301,{AbilityGoid(486)},50,1)");
        await caster.EnterWorldAsync();
        await member.EnterWorldAsync();
        await outsider.EnterWorldAsync();
        var seen = await caster.WaitForViewNamedAsync(member.Handle, TimeSpan.FromSeconds(20));

        caster.SendRpc(GroupInvite(Team, member.Handle, ""));
        await member.WaitForRpcAsync("80 8f 0c 00");
        member.SendRpc(InviteAnswer(Team, 0, caster.Handle));
        await member.WaitForRpcAsync("80 8e");
        await caster.WaitForRpcAsync($"70 03 {Le(member.CharId)} 09 00");

        Cast(caster, 382, seen.ViewId);
        await caster.WaitForRpcAsync(AbilityLine(0x7a00015b));  // "You begin to teleport a team member to your location."
        await member.WaitForRpcAsync(AbilityLine(0x7a00015a));  // "Your team member %2s is attempting to teleport you..."
        await member.WaitUntilAsync(() => context.Server.DebugLog().Contains("Recalled Recalled to Recaller at 17020"),
            "Recall Team Member teleports the member to the caster", TimeSpan.FromSeconds(10));

        Cast(caster, 486);
        await caster.WaitForRpcAsync(AbilityLine(0x7a000164)); // "You begin to teleport your team to your location."
        await member.WaitUntilAsync(() => context.Server.DebugLog().Split("Recalled Recalled to Recaller").Length >= 3,
            "Recall Mission Team teleports the team", TimeSpan.FromSeconds(10));
        Check.That(!context.Server.DebugLog().Contains("Recalled Outsider"), "Players outside the team stay where they are");
        Check.Equal(0, caster.World.Problems.Count, "Recall protocol (caster)");
        Check.Equal(0, member.World.Problems.Count, "Recall protocol (member)");
    }

    /// <summary>
    /// Disable Security Devices on a Security_Panel (rule 1012) and Disarm Traps on a trapped safe (DisarmTraps_Test),
    /// each gated on the ability and its tool; the object moves to the state its script gives the result.
    /// </summary>
    private static async Task SecurityAndTraps(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("Infiltrator") { Level = 50 });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={player.CharId}; " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({player.CharId},300,{AbilityGoid(218)},22,1),({player.CharId},301,{AbilityGoid(119)},10,1)");
        await player.EnterWorldAsync();

        player.SendChat("?objstate 466");
        await player.WaitForTextAsync("Object 466");
        await player.WaitUntilAsync(() => player.ViewsOfGoid(466).Count > 0, "Security panel view", TimeSpan.FromSeconds(10));
        var panel = player.ViewsOfGoid(466).Last();
        player.SendRpc($"80 c7 {MissionNpcId(panel)} 01 00");
        await player.WaitForTextAsync("disable device tool");
        await context.Database.ExecuteAsync($"INSERT INTO inventory(charId,goid,slot) VALUES ({player.CharId},616,30),({player.CharId},2500,31)");
        player.SendRpc($"80 c7 {MissionNpcId(panel)} 01 00");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("Disable Security Devices on 466: result 1") ||
                                          context.Server.DebugLog().Contains("Disable Security Devices on 466: result 2"),
            "The contest grants access or trips the alarm", TimeSpan.FromSeconds(10));
        bool granted = context.Server.DebugLog().Contains("Disable Security Devices on 466: result 1");
        await player.WaitForViewUpdateAsync(panel.ViewId, granted ? "01 02 3a 08 00 00" : "01 02 3b 08 00 00");

        player.SendChat("?trapobj 1416");
        await player.WaitForTextAsync("Object 1416");
        await player.WaitUntilAsync(() => player.ViewsOfGoid(1416).Count > 0, "Trapped safe view", TimeSpan.FromSeconds(10));
        var trapped = player.ViewsOfGoid(1416).Last();
        Cast(player, 119, trapped.ViewId);
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("Disarm Traps on 1416 in 0x839:"),
            "Disarm Traps contests the trap", TimeSpan.FromSeconds(10));
        await player.WaitUntilAsync(() => player.Received(AbilityLine(0x7600024e)).Count + player.Received(AbilityLine(0x7600024f)).Count +
                                          player.Received(AbilityLine(0x76000250)).Count > 0,
            "A disarm line arrives", TimeSpan.FromSeconds(5));
        Check.Equal(0, player.World.Problems.Count, "Security and trap protocol");
    }
}
