using System.Text.RegularExpressions;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static int StyleGoid(ushort ability) => unchecked((int)(0x80000000u | ((uint)ability << 10)));

    private static Task LoadAbilitiesAsync(ScenarioContext context, HeadlessPlayer player, params ushort[] abilities) =>
        context.Database.ExecuteAsync($"INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", abilities.Select((a, i) => $"({player.CharId},{400 + i},{StyleGoid(a)},50,1)")) + ";");

    private static void SelectStyle(HeadlessPlayer player, ushort style) =>
        player.SendRpc($"3e 6b {Le((uint)style)} 00 00");

    /// <summary>3e switches the active style: its own records replace the previous style's, special moves follow their
    /// stored stance, and the interlock exchanges come from the style's ILDB set.</summary>
    private static async Task CombatStyleSwitching(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(NearNpc("Stylist") with { InnerStrength = 500, MaxInnerStrength = 500 });
        await LoadAbilitiesAsync(context, player, 133, 132, 1112);
        await player.EnterWorldAsync();
        var mob = await player.WaitForViewNamedAsync("WinterFrost");

        SelectStyle(player, 133);
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 85 00 00 00");
        await player.WaitForRpcAsync(ModPattern(133, 522)); // Kung-Fu MeleeAcc
        await player.WaitForRpcAsync(ModPattern(133, 1009));
        SelectStyle(player, 132);
        await player.WaitForRpcAsync(ModPattern(132, 522));
        await player.WaitForRpcAsync(ModPattern(133, 522, true)); // the previous style's records come off after
        Check.Equal(0, player.Received(ModPattern(1112, 522)).Count, "A loaded but inactive style contributes nothing");

        // Special moves need their stored stance: Tomo Nage needs Aikido, which isn't loaded
        Cast(player, 412, mob.ViewId);
        await player.WaitForTextAsync("requires the Aikido combat style");
        Check.That(!context.Server.DebugLog().Contains("TomoNageAbility is the next move"), "A refused special does nothing");
        // Wounding Throw needs Spy Style, which is loaded: the server switches to it as the client's picker does
        Cast(player, 281, mob.ViewId);
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await player.WaitForRpcAsync(ModPattern(1112, 1032)); // Spy Style ThrowAccuracy
        SelectStyle(player, 133);
        await player.WaitForRpcAsync(ModPattern(133, 522));
        await player.WaitForRpcAsync(ModPattern(1112, 1032, true));

        // A Kung-Fu fight uses the W exchange set against the NPC's Self-Defense set
        player.SendRpc("3d 01");
        player.SendRpc($"40 {Le(mob.ViewId)} {Le((ushort)mob.SpawnId)} 00 00 00 00 00 00 00");
        var styled = new Regex(@"animation \d+ \((W[SPDB] vs S[SPDB]|S[SPDB] vs W[SPDB])");
        await player.WaitUntilAsync(() => styled.IsMatch(context.Server.DebugLog()), "Kung-Fu exchanges in the interlock",
            TimeSpan.FromSeconds(30));
        Check.That(!new Regex(@"animation \d+ \(S[SPDB] vs S[SPDB]").IsMatch(context.Server.DebugLog()),
            "No Self-Defense-only exchange while the player fights in Kung-Fu");
        player.SendRpc("44 00");
        Check.Equal(0, player.World.Problems.Count, "Style switching protocol");
    }

    /// <summary>Taunt, thrown DOTs and Exile weapons against test NPCs.</summary>
    private static async Task CombatSpecialsNpc(ScenarioContext context)
    {
        var knifer = await context.AddPlayerAsync(NearNpc("Knifer") with { InnerStrength = 500, MaxInnerStrength = 500 });
        var brawler = await context.AddPlayerAsync(NearNpc("Brawler", 100));
        await LoadAbilitiesAsync(context, knifer, 1112);
        foreach (var player in new[] { knifer, brawler }) await player.EnterWorldAsync();
        var mob = await knifer.WaitForViewNamedAsync("WinterFrost");
        var trainman = await knifer.WaitForViewNamedAsync("Trainman");

        // Taunt: the brawler fights the NPC; the knifer taunts it and the NPC turns on the knifer
        var mobForBrawler = await brawler.WaitForViewNamedAsync("WinterFrost");
        brawler.SendRpc("3d 01");
        brawler.SendRpc($"40 {Le(mobForBrawler.ViewId)} {Le((ushort)mobForBrawler.SpawnId)} 00 00 00 00 00 00 00");
        await brawler.WaitUntilAsync(() => context.Server.DebugLog().Contains("[COMBAT] Round"), "The brawler's fight runs",
            TimeSpan.FromSeconds(20));
        Cast(knifer, 647);
        await knifer.WaitUntilAsync(() => context.Server.DebugLog().Contains("Taunt: WinterFrost now hates"),
            "Taunt reaches the NPC within its radius", TimeSpan.FromSeconds(10));
        await knifer.WaitUntilAsync(() => Regex.IsMatch(context.Server.DebugLog(), @"\[AGGRO\] WinterFrost (attacks|opens fire on) " + knifer.CharId),
            "The taunted NPC turns on the taunter", TimeSpan.FromSeconds(10));
        int seen = ViewUpdateValues(brawler, 2, "02 80 80 80 40").Count;
        await Task.Delay(TimeSpan.FromSeconds(14)); // three rounds or more
        var health = ViewUpdateValues(brawler, 2, "02 80 80 80 40");
        var after = health.Skip(Math.Max(0, seen - 1)).ToList();
        Check.That(after.Zip(after.Skip(1)).All(p => p.Second >= p.First),
            "The NPC stops hitting the other player while the taunter fights it: " + string.Join(",", after));

        // Poison Knife (Spy Style): its ThrownDOT goes on the NPC when the 2 s cast ends
        SelectStyle(knifer, 1112);
        await knifer.WaitForRpcAsync(ModPattern(1112, 1032)); // Spy Style ThrowAccuracy
        Cast(knifer, 146, mob.ViewId);
        await knifer.WaitUntilAsync(() => context.Server.DebugLog().Contains("[EFFECTS] 146 on WinterFrost: Dot 1157 17 25 s"),
            "Poison Knife's ThrownDOT on the NPC", TimeSpan.FromSeconds(10));
        await knifer.WaitUntilAsync(() => context.Server.DebugLog().Contains("146 ticks on WinterFrost for 2"),
            "ThrownDOT ticks int(17 / 25 x 2.5 + .5)", TimeSpan.FromSeconds(10));

        // Exile weapons on another NPC (not the fight's target, so they land at once): 10, x10 against the weakness
        knifer.SendChat("?exileweakness wood Trainman");
        await knifer.WaitForTextAsync("Trainman now has Exile weakness 464");
        Cast(knifer, 208, trainman.ViewId);
        // Trainman has 500 health: the health updates show the damage (the debug log can drop lines under load)
        await knifer.WaitUntilAsync(() => ViewUpdateValues(knifer, trainman.ViewId, NpcHealthUpdate).Contains(490),
            "Silver Bullet does its base 10 to a wood-weak NPC", TimeSpan.FromSeconds(20));
        Cast(knifer, 207, trainman.ViewId);
        await knifer.WaitUntilAsync(() => ViewUpdateValues(knifer, trainman.ViewId, NpcHealthUpdate).Contains(390),
            "Wooden Stake does x10 to a wood-weak NPC", TimeSpan.FromSeconds(20));
        Check.Equal(0, knifer.World.Problems.Count + brawler.World.Problems.Count, "Specials protocol");
    }

    /// <summary>The same specials between two flagged players, with the caster buffs.</summary>
    private static async Task CombatSpecialsPvp(ScenarioContext context)
    {
        var caster = await context.AddPlayerAsync(new CharacterSeed("Berserk") { Level = 50, Organization = 1, InnerStrength = 500, MaxInnerStrength = 500 });
        var victim = await context.AddPlayerAsync(new CharacterSeed("Severed") { X = 17120, Level = 50, Organization = 2 });
        await LoadAbilitiesAsync(context, caster, 132, 1112);
        foreach (var player in new[] { caster, victim }) await player.EnterWorldAsync();
        foreach (var player in new[] { caster, victim })
        {
            player.SendChat("?pvpflag on");
            await player.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        }
        var victimView = await caster.WaitForViewNamedAsync(victim.Handle);

        SelectStyle(caster, 1112);
        await caster.WaitForRpcAsync(ModPattern(1112, 1032));
        Cast(caster, 341, victimView.ViewId);
        await victim.WaitForRpcAsync(ModPattern(341, 1157)); // Sever Artery's ThrownDOT record
        await victim.WaitForRpcAsync(ModPattern(341, 115));  // MovementSpeed -50 for 30 s
        var slow = victim.Received(ModPattern(341, 115)).Last();
        Check.Equal(30f, BitConverter.ToSingle(slow.Bytes, 18), "Sever Artery's stored 30 s");

        SelectStyle(caster, 132);
        await caster.WaitForRpcAsync(ModPattern(132, 522));
        Cast(caster, 535, victimView.ViewId);
        await caster.WaitForRpcAsync(ModPattern(535, 159)); // Berserker: Melee Damage +11 on the caster
        await caster.WaitForRpcAsync(ModPattern(535, 180));
        Check.Equal(0, victim.Received(ModPattern(535, 159)).Count, "Berserker's buff goes to the caster, not the target");

        Cast(caster, 647);
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("[ABILITY] 647"), "Taunt cast", TimeSpan.FromSeconds(10));
        Check.That(!context.Server.DebugLog().Contains("Taunt: Severed"), "Taunt does not affect players");
        Check.Equal(0, caster.World.Problems.Count + victim.World.Problems.Count, "PvP specials protocol");
    }
}
