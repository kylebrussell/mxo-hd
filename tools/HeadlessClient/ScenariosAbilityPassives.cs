namespace HeadlessClient;

public static partial class Scenarios
{
    private static string PassiveRecord(ushort ability, ushort stat, int value, bool off = false) =>
        $"80 bc 45 00 ?? ?? 00 {Le((uint)ability)} {Le(stat)} {Le(unchecked((uint)value))} {(off ? "01" : "00")} 00 00 00 00";

    private static void SetAbilityLoaded(HeadlessPlayer player, ushort slot, ushort ability, ushort level, bool loaded) =>
        player.SendRpc($"80 ae 00 00 00 00 {(loaded ? "00 00 0a 00" : "0a 00 00 00")} 01 00 00 {Le(slot)} {Le((uint)ability)} {Le(level)}");

    private static async Task AbilityPassivesLifecycle(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("Passives") { Level = 50, Info = 10000000 });
        int Goid(ushort id) => unchecked((int)(0x80000000u | ((uint)id << 10)));
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={player.CharId}; " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({player.CharId},300,{Goid(45)},50,1),({player.CharId},301,{Goid(269)},50,1)," +
            $"({player.CharId},302,{Goid(52)},49,1),({player.CharId},303,{Goid(37)},15,0)");
        await player.EnterWorldAsync();
        await player.WaitForRpcAsync(PassiveRecord(52, 643, 196));
        var self = player.World.CreationSnapshot().Last(v => v.ViewId == 2);
        Check.Equal((ushort)575, BitConverter.ToUInt16(self.Attributes["MaxHealth"]), "Character-level Aikido passive changes the live maximum");
        Check.Equal((ushort)220, BitConverter.ToUInt16(self.Attributes["InnerStrengthMax"]), "Execute Program changes the live IS maximum");
        Check.Equal(0, player.Received("80 bc 45 00 ?? ?? 00 25 00 00 00").Count, "Unloaded Write Code has no stat record");

        player.SendRpc("80 b7 34 00 2e 01 01"); // owned Hacker in slot302, upgrade49 ->50
        await player.WaitForRpcAsync(PassiveRecord(52, 643, 200));
        Check.That(player.Received(PassiveRecord(52, 643, 196, true)).Count > 0, "Upgrade replaces the old modifier");
        SetAbilityLoaded(player, 303, 37, 50, true); // claiming level50 must not replace the owned15
        await player.WaitForRpcAsync("80 b2 25 00 0f 00 2f 01");
        await player.WaitForRpcAsync(PassiveRecord(37, 649, 15));
        SetAbilityLoaded(player, 300, 52, 50, true); // wrong ability for this slot cannot replace Execute Program
        player.SendRpc("81 48"); await player.WaitForRpcAsync("81 49");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={player.CharId} AND slot=300"),
            "Forged loadout does not replace an owned slot");
        SetAbilityLoaded(player, 300, 45, 50, false);
        await player.WaitForRpcAsync(PassiveRecord(45, 1056, 10, true));
        await player.WaitUntilAsync(() => player.World.ViewSnapshot().Any(bytes =>
            BytePattern.Parse("02 00 02 80 80 80 04 c8 00").Occurs(bytes) ||
            BytePattern.Parse("02 00 02 80 80 80 14 c8 00 c8 00").Occurs(bytes)),
            "Max IS returns to200, with current IS clamped if it regenerated above200", TimeSpan.FromSeconds(10));
        SetAbilityLoaded(player, 300, 45, 50, true);
        await player.WaitForRpcAsync(PassiveRecord(45, 1056, 10));

        player.SendChat("?die");
        await player.WaitForRpcAsync(PassiveRecord(52, 643, 168));
        Check.That(player.Received(PassiveRecord(45, 1056, 8)).Count > 0, "Death Effect rescales learned-level passives");
        string username = await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={player.CharId}");
        using var server = await ServerInstance.StartAsync(context.RepositoryRoot, context.Database.Name, StillNpcs);
        using var again = await HeadlessPlayer.LoginAsync(server.Address, username, player.Handle);
        await again.EnterWorldAsync();
        await again.WaitForRpcAsync(PassiveRecord(52, 643, 168));
        Check.Equal((ushort)216, BitConverter.ToUInt16(again.World.CreationSnapshot().Last(v => v.ViewId == 2).Attributes["InnerStrengthMax"]),
            "Fresh-server login restores the penalized bonus once, without persisting a buffed baseline");
        Check.Equal(0, again.World.Problems.Count, "Passive reconnect protocol");
    }

    private static async Task AbilityHealMagnitudes(ScenarioContext context)
    {
        var caster = await context.AddPlayerAsync(new CharacterSeed("HealCaster") { Level = 50, Health = 100 });
        var ally = await context.AddPlayerAsync(new CharacterSeed("HealAlly") { X = 17120, Level = 50, Health = 100, MaxHealth = 4000 });
        int Goid(ushort id) => unchecked((int)(0x80000000u | ((uint)id << 10)));
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{ally.CharId}); " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({caster.CharId},300,{Goid(71)},50,1),({caster.CharId},301,{Goid(233)},50,1),({caster.CharId},302,{Goid(370)},50,1)");
        await caster.EnterWorldAsync(); await ally.EnterWorldAsync();
        Check.Equal((ushort)4000, BitConverter.ToUInt16(ally.World.CreationSnapshot().Last(v => v.ViewId == 2).Attributes["MaxHealth"]),
            "The healing recipient has a different maximum from the caster");
        var seen = await caster.WaitForViewNamedAsync(ally.Handle);
        caster.SendRpc(GroupInvite(Team, ally.Handle, ""));
        await ally.WaitForRpcAsync("80 8f 0c 00");
        ally.SendRpc(InviteAnswer(Team, 0, caster.Handle));
        await ally.WaitForRpcAsync("80 8e");
        await caster.WaitForRpcAsync($"70 03 {Le(ally.CharId)} 09 00");
        Cast(caster, 1098, seen.ViewId);
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("Heal 1098 -> HealAlly for 206"),
            "Physician group heal applies the caster's15% influence", TimeSpan.FromSeconds(10));
        Check.That(context.Server.DebugLog().Contains("Heal 1098 -> HealCaster for 206"),
            "A group's recipients receive the same source amount despite different maximum health");
        foreach (var row in new (ushort Slot, ushort Ability)[] { (300, 71), (301, 233), (302, 370) })
            SetAbilityLoaded(caster, row.Slot, row.Ability, 50, false);
        await caster.WaitForRpcAsync(PassiveRecord(370, 1141, 5, true));
        Cast(caster, 54, seen.ViewId);
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("Heal 54 -> HealAlly for 37"),
            "Unloading the passives restores the ordinary stored heal amount", TimeSpan.FromSeconds(10));
        Check.Equal(0, caster.World.Problems.Count, "Healing magnitude protocol");
        Check.Equal(0, ally.World.Problems.Count, "Recipient healing protocol");
    }

    private static async Task AbilityPassiveDamage(ScenarioContext context)
    {
        var caster = await context.AddPlayerAsync(NearNpc("ViralCaster") with { Level = 50, Organization = 1 });
        var enemy = await context.AddPlayerAsync(NearNpc("ViralEnemy", 100) with { Level = 50, Organization = 2, Health = 5000, MaxHealth = 5000 });
        int viral = unchecked((int)(0x80000000u | (95u << 10)));
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{enemy.CharId}); " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES ({caster.CharId},300,{viral},50,1)");
        await caster.EnterWorldAsync(); await enemy.EnterWorldAsync();
        foreach (var player in new[] { caster, enemy })
        {
            player.SendChat("?pvpflag on");
            await player.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        }
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        var mob = await caster.WaitForViewNamedAsync("WinterFrost");
        Cast(caster, 474, target.ViewId);
        var virus = await enemy.WaitForRpcAsync(ModPattern(474, 1157));
        int total = BitConverter.ToInt32(virus.Bytes, 13);
        Check.That(total >= 995 && total <= 1012, "A viral DOT's captured866..880 base gains the caster's15% influence once");
        Cast(caster, 330, target.ViewId);
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("lands on ViralEnemy for 487"),
            "PvP direct ability uses viral influence", TimeSpan.FromSeconds(10));
        await Task.Delay(4500);
        Cast(caster, 330, mob.ViewId);
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains($"330 lands on view {mob.ViewId} for 487"),
            "NPC direct ability uses the same viral influence", TimeSpan.FromSeconds(10));
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Passive damage protocol");
    }
}
