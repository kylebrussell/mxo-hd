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
        await player.WaitForRpcAsync(ModPattern(133, 522, true)); // the previous style's records come off first
        await player.WaitForRpcAsync("80 bd 05 85 00 00 00 00 00 00 00"); // and its toggle
        await player.WaitForRpcAsync(ModPattern(132, 522));
        await player.WaitForRpcAsync("80 bd 05 84 00 00 00 00 00 00 01");
        await player.WaitForRpcAsync("81 67 ?? ?? ?? ?? 1c 22 00 c6 01 84 00 00 00"); // "You are now using the %1z martial arts style."
        Check.Equal(0, player.Received(ModPattern(1112, 522)).Count, "A loaded but inactive style contributes nothing");

        // Special moves need their stored stance: Tomo Nage needs Aikido, which isn't loaded
        Cast(player, 412, mob.ViewId);
        await player.WaitForRpcAsync("80 ba 9c 01 00 00 6b 0d"); // the client's ID_ABILITY_NOT_IN_CORRECT_STYLE
        Check.That(!context.Server.DebugLog().Contains("TomoNageAbility is the next move"), "A refused special does nothing");
        // Wounding Throw needs Spy Style, which is loaded: the server switches to it as the client's picker does
        Cast(player, 281, mob.ViewId);
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await player.WaitForRpcAsync(ModPattern(1112, 1032)); // Spy Style ThrowAccuracy
        // Using Kung-Fu from the Actions window sends 80 b9 85 00, not 3e: it switches the style too
        player.SendRpc("80 b9 85 00 00 00");
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 85 00 00 00");
        await player.WaitForRpcAsync(ModPattern(1112, 1032, true));
        await player.WaitForRpcAsync("80 bd 05 58 04 00 00 00 00 00 00");
        await player.WaitForRpcAsync(ModPattern(133, 522));
        await player.WaitForRpcAsync("80 bd 05 85 00 00 00 00 00 00 01");
        await player.WaitForRpcAsync("81 67 ?? ?? ?? ?? 1c 22 00 c6 01 85 00 00 00");
        Check.Equal(0, player.Received("80 ac 85 00").Count, "A style switch is no cast: no 80 ac echo");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("now fights with Kung-Fu"), "80 b9 style switch logged",
            TimeSpan.FromSeconds(10));

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

    private static async Task RuleBoundStyleChoreography(ScenarioContext context)
    {
        // Retail requests: 40 target-u32 01 6b from the Uriah tutorial capture line 10645;
        // 3e ability selection and 80 b9 ability-u16/view-u16 from docs/COMBAT-STYLES.md.
        var artist = await context.AddPlayerAsync(NearNpc("RuleArtist") with
        {
            OwnedLoadedAbilities = new (ushort, ushort)[] { (101, 50), (412, 50) }
        });
        var gunman = await context.AddPlayerAsync(NearNpc("RuleGunman") with
        {
            Items = new (int, uint, int)[] { (1, 2503, 1) },
            OwnedLoadedAbilities = new (ushort, ushort)[] { (158, 50), (246, 50), (250, 50) }
        });
        await artist.EnterWorldAsync();
        await gunman.EnterWorldAsync();

        var first = await artist.WaitForViewNamedAsync("Trainman");
        SelectStyle(artist, 101);
        await artist.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 65 00 00 00");
        artist.SendRpc($"40 {HandleOf(first)} 01 6b");
        ushort firstView = await WaitForCombatView(artist);
        Cast(artist, 412, first.ViewId);
        await AssertRuleMoveAsync(artist, firstView, 751, "Tomo Nage with its explicit ability rule");

        var second = await gunman.WaitForViewNamedAsync("WinterFrost");
        // Bag slot 1 to weapon slot 105 is the stock equip request (docs/CLIENT-SETUP-PLAN.md).
        gunman.SendRpc("63 01 69");
        await gunman.WaitForRpcAsync("65 01 00 69 00 00");
        await gunman.WaitForViewUpdateAsync(2, "02 80 80 80 80 08 c7 09 00 00");
        SelectStyle(gunman, 158);
        await gunman.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 9e 00 00 00");
        ushort observedPrevious = CombatViewOf(gunman);
        // An equipped weapon has its own selection; use the interlock button's stock request.
        gunman.SendRpc($"40 {HandleOf(second)} 01 6b");
        ushort secondView = await WaitForCombatView(gunman, observedPrevious);
        await WaitForEntry(gunman, secondView);
        byte[] opening = CombatEntries(gunman, secondView).First(e => BitConverter.ToUInt16(e, 32) == 1);
        int transitionOffset = opening[0] == 1 ? 45 : 53;
        uint transition = BitConverter.ToUInt32(opening, transitionOffset);
        Check.That(transition >= 21076 && transition <= 21079, "Equipped dual SMGs enter a stock dual-weapon stance");
        Check.Equal(0x24000b8bu, BitConverter.ToUInt32(opening, transitionOffset + 4), "Stock transition resource");

        Cast(gunman, 250, second.ViewId);
        await AssertRuleMoveAsync(gunman, secondView, 21082, "Completed Controlled Burst cast joins the current round");
        Check.Equal(0, artist.World.Problems.Count + gunman.World.Problems.Count, "Rule-bound choreography protocol");
    }

    private static async Task AssertRuleMoveAsync(HeadlessPlayer player, ushort view, uint expected, string detail)
    {
        await player.WaitUntilAsync(() => CombatEntries(player, view).Any(entry =>
            BitConverter.ToUInt32(entry, 61) == expected && BitConverter.ToUInt32(entry, 65) == 0x24000b8b),
            detail, TimeSpan.FromSeconds(20));
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
        Cast(knifer,123);
        var weakness=await knifer.WaitForRpcAsync(ModPattern(123,1034));
        Check.Equal(25,BitConverter.ToInt32(weakness.Bytes,13),"Find Weakness uses source character50");
        Cast(knifer, 146, mob.ViewId);
        // Stored17, Spy Style29% + Find Weakness25%: trunc(17 * binary32(1.54))=26.
        await knifer.WaitUntilAsync(() => context.Server.DebugLog().Contains("[EFFECTS] 146 on WinterFrost: Dot 1157 26 25 s"),
            "ThrownDOT snapshots Spy Style plus Find Weakness influence", TimeSpan.FromSeconds(10));
        await knifer.WaitUntilAsync(() => context.Server.DebugLog().Contains("146 ticks on WinterFrost for 3"),
            "ThrownDOT ticks int(26 / 25 x 2.5 + .5)", TimeSpan.FromSeconds(10));

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

    private static async Task CombatExileWeapons(ScenarioContext context)
    {
        var samples = new[] { (Ability: (ushort)208, Health: (ushort)490),
            (Ability: (ushort)207, Health: (ushort)390), (Ability: (ushort)1022, Health: (ushort)0) };
        // A separate owner per cast keeps each item attack on its ordinary direct-object path.
        var attackers = await context.AddPlayersInWorldAsync(samples.Select(sample =>
            NearNpc($"Exile{sample.Ability}") with
            {
                InnerStrength = 500, MaxInnerStrength = 500,
                OwnedLoadedAbilities = new[] { (sample.Ability, (ushort)50) }
            }).ToArray());
        attackers[0].SendChat("?exileweakness wood Trainman");
        await attackers[0].WaitForTextAsync("Trainman now has Exile weakness 464");
        foreach (var (attacker, sample) in attackers.Zip(samples))
        {
            var target = await attacker.WaitForViewNamedAsync("Trainman");
            Cast(attacker, sample.Ability, target.ViewId);
            await attacker.WaitUntilAsync(() => ViewUpdateValues(attacker, target.ViewId, NpcHealthUpdate).Contains(sample.Health),
                $"Exile attack {sample.Ability} changes Trainman's health to {sample.Health}", TimeSpan.FromSeconds(15));
            Check.Equal(0, attacker.World.Problems.Count, "Exile item attack protocol");
        }
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
