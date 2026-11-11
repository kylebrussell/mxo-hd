namespace HeadlessClient;

public static partial class Scenarios
{
    private static void Cast(HeadlessPlayer player, ushort ability, ushort view = 2) =>
        player.SendRpc($"80 b9 {Le(ability)} {Le(view)}");

    private static string ModPattern(ushort ability, ushort target, bool off = false) =>
        $"80 bc ?? ?? ?? ?? 00 {Le((uint)ability)} {Le(target)} ?? ?? ?? ?? {(off ? "01" : "00")}";

    private static async Task AbilityParityCosts(ScenarioContext context)
    {
        var player = (await context.AddPlayersInWorldAsync(new CharacterSeed("EmptyIS") { Level = 50, InnerStrength = 0 }))[0];
        Cast(player, 1064);
        await player.WaitForTextAsync("not have enough Inner Strength");
        Check.Equal(0, player.Received("80 ac 28 04").Count, "An unaffordable class buff has no cast echo");
        Check.Equal(0, player.Received(ModPattern(1064, 106)).Count, "An unaffordable class buff changes no vitals");
        Check.Equal(0, player.Received("80 bd 01 28 04").Count, "An unaffordable cast starts no cooldown");
        Check.Equal(0, player.World.Problems.Count, "Cost-denial protocol problems");
    }

    private static async Task AbilityParitySupport(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            NearNpc("Healer") with { Health = 100 }, NearNpc("Ally", 100) with { Health = 100 },
            NearNpc("Outsider", 200) with { Health = 100 }, NearNpc("FarAlly", 1000) with { Health = 100 });
        var healer = players[0]; var ally = players[1]; var outsider = players[2]; var far = players[3];
        var allyView = await healer.WaitForViewNamedAsync("Ally");
        var npc = await healer.WaitForViewNamedAsync("WinterFrost");

        Cast(healer, 77, allyView.ViewId);
        await ally.WaitUntilAsync(() => ViewUpdateValues(ally, 2, "02 80 80 80 40").Any(v => v >= 225),
            "Repair RSI heals the selected player", TimeSpan.FromSeconds(10));
        Check.That(!context.Server.DebugLog().Contains("Heal 77 -> Healer"), "A targeted heal does not heal its caster instead");

        // An NPC selection must be rejected before IS or cooldown is consumed.
        Cast(healer, 77, npc.ViewId);
        await healer.WaitForTextAsync("Choose a living friendly target within range");
        Check.Equal(0, ViewUpdateValues(healer, npc.ViewId, NpcHealthUpdate).Count, "A heal never damages the selected NPC");
        Cast(healer, 967, npc.ViewId);
        await healer.WaitUntilAsync(() => context.Server.DebugLog().Contains("Heal 967 -> Healer for 50"),
            "Self-only Mobius Code ignores a hostile selection", TimeSpan.FromSeconds(10));

        Cast(healer, 169, allyView.ViewId);
        await ally.WaitForRpcAsync(ModPattern(169, 108));
        int beforeTicks = ViewUpdateValues(ally, 2, "02 80 80 80 40").Count;
        await ally.WaitUntilAsync(() => context.Server.DebugLog().Contains("169 heals Ally"), "Emergency Repairs HOT tick", TimeSpan.FromSeconds(10));
        Check.That(ViewUpdateValues(ally, 2, "02 80 80 80 40").Count > beforeTicks, "HOT sends the target a health update");

        foreach (var member in new[] { ally, far })
        {
            healer.SendRpc(GroupInvite(Team, member.Handle, ""));
            await member.WaitForRpcAsync("80 8f 0c 00");
            member.SendRpc(InviteAnswer(Team, 0, healer.Handle));
            await member.WaitForRpcAsync("80 8e");
        }
        await healer.WaitForRpcAsync("80 bd 01 ad 02 00 00 00 80 40 00"); // Emergency Repairs shares aaHeal1
        Cast(healer, 371, npc.ViewId);
        await healer.WaitUntilAsync(() => context.Server.DebugLog().Contains("Heal 371 -> Ally"), "Group heal reaches nearby teammate", TimeSpan.FromSeconds(10));
        Check.That(context.Server.DebugLog().Contains("Heal 371 -> Healer"), "Group heal includes caster");
        Check.That(!context.Server.DebugLog().Contains("Heal 371 -> Outsider"), "Group heal excludes unrelated player");
        Check.That(!context.Server.DebugLog().Contains("Heal 371 -> FarAlly"), "Group heal respects seven-metre radius");

        // Class buffs have real maximum-health effects, and replacing the class removes the previous one.
        Cast(healer, 1064);
        await healer.WaitForRpcAsync(ModPattern(1064, 106));
        await healer.WaitForViewUpdateAsync(2, "02 80 80 01 7d 02"); // maximum health 500 + 137
        var healerForAlly = await ally.WaitForViewNamedAsync("Healer");
        await ally.WaitForViewUpdateAsync(healerForAlly.ViewId, "02 80 02 7d 02");
        Cast(healer, 1089);
        await healer.WaitForRpcAsync(ModPattern(1064, 106, true));
        await healer.WaitForRpcAsync(ModPattern(1089, 174));
        await healer.WaitForViewUpdateAsync(2, "02 80 80 01 f4 01");
        await ally.WaitForViewUpdateAsync(healerForAlly.ViewId, "02 80 02 f4 01");
        Check.Equal(0, healer.World.Problems.Count, "Support protocol problems");
    }

    private static async Task AbilityParityControls(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("Controller") { Level = 50, Organization = 1 },
            new CharacterSeed("Victim") { X = 17120, Level = 50, Organization = 2 },
            new CharacterSeed("Rooted") { X = 17220, Level = 50, Organization = 2 });
        var caster = players[0]; var victim = players[1]; var rooted = players[2];
        var victimView = await caster.WaitForViewNamedAsync("Victim");
        var rootView = await caster.WaitForViewNamedAsync("Rooted");
        foreach (var target in new[] { victim, rooted })
        {
            target.SendChat("?pvpflag on");
            await target.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        }
        victim.SendChat("?additem 2565");
        await victim.WaitForRpcAsync("80 b2");

        caster.SendChat("?pvpflag on");
        await caster.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        var casterForVictim = await victim.WaitForViewNamedAsync("Controller");
        victim.SendRpc($"41 {HandleOf(casterForVictim)}");
        await victim.WaitForRpcAsync("81 68");

        Cast(caster, 315, victimView.ViewId);
        await victim.WaitForRpcAsync(ModPattern(315, 102), TimeSpan.FromSeconds(10));
        int shotsAtStun = context.Server.DebugLog().Split('\n').Count(l => l.Contains("Victim fires"));
        Cast(victim, 967);
        await victim.WaitForTextAsync("cannot act while stunned");
        victim.SendRpc($"41 {HandleOf(casterForVictim)}");
        // A forged position also cannot move a stunned character on the server.
        var move = WireFormat.FromHex("02 00 01 08").Concat(BitConverter.GetBytes(18000f))
            .Concat(BitConverter.GetBytes(495f)).Concat(BitConverter.GetBytes(2693f)).Concat(new byte[] { 0, 0 }).ToArray();
        victim.World.SendViewList(move);
        await victim.WaitForViewUpdateAsync(2, "01 0c");
        await Task.Delay(4700);
        Check.Equal(shotsAtStun, context.Server.DebugLog().Split('\n').Count(l => l.Contains("Victim fires")),
            "Stun blocks new RPC41 requests and pauses ongoing gunfire");
        await victim.WaitForRpcAsync(ModPattern(315, 102, true), TimeSpan.FromSeconds(10));
        victim.SendRpc("44 00");
        Cast(victim, 967);
        await victim.WaitForRpcAsync("80 bd 01 c7 03");

        // A different target avoids the stun immunity timer. This root protects the hit that applied it,
        // then has a 100% break chance on the next damage event (stored Deactivate MinValue -100).
        Cast(caster, 1135, rootView.ViewId);
        await rooted.WaitForRpcAsync(ModPattern(1135, 99), TimeSpan.FromSeconds(10));
        Cast(rooted, 967); // root permits abilities
        await rooted.WaitForRpcAsync("80 bd 01 c7 03");
        Cast(caster, 329, rootView.ViewId);
        await rooted.WaitForRpcAsync(ModPattern(1135, 99, true), TimeSpan.FromSeconds(10));
        Check.Equal(0, victim.World.Problems.Count + rooted.World.Problems.Count, "Control protocol problems");
    }

    private static async Task AbilityParityArea(ScenarioContext context)
    {
        var caster = (await context.AddPlayersInWorldAsync(NearNpc("AreaHacker")))[0];
        var frost = await caster.WaitForViewNamedAsync("WinterFrost");
        var train = await caster.WaitForViewNamedAsync("Trainman");
        var smith = await caster.WaitForViewNamedAsync("Smithling");
        var below = await caster.WaitForViewNamedAsync("WinterHelper");
        Cast(caster, 466, frost.ViewId);
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("466 on Smithling"), "Area virus infects secondary target", TimeSpan.FromSeconds(10));
        // Switch the caster's live fight to another mob. The secondary DOTs must remain independent of that fight.
        caster.SendRpc($"40 {HandleOf(train)} 01 6b");
        await caster.WaitForRpcAsync($"46 {HandleOf(train)} 01");
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("466 ticks on WinterFrost") &&
            context.Server.DebugLog().Contains("466 ticks on Smithling"), "Independent area DOT ticks", TimeSpan.FromSeconds(10));
        Check.Equal(0, ViewUpdateValues(caster, below.ViewId, NpcHealthUpdate).Count, "Area DOT does not reach another floor");
        caster.SendChat("?weaken 1");
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("[DEV] Weakened"), "Weakened test targets", TimeSpan.FromSeconds(10));
        Cast(caster, 359, frost.ViewId);
        await caster.WaitForViewUpdateAsync(smith.ViewId, NpcDeathUpdate, TimeSpan.FromSeconds(15));
        await caster.WaitForViewUpdateAsync(frost.ViewId, NpcDeathUpdate, TimeSpan.FromSeconds(15));
        await Task.Delay(3000);
        Check.Equal(1, CountViewUpdate(caster, smith.ViewId, NpcDeathUpdate), "One death for the secondary target");
        Check.Equal(1, CountViewUpdate(caster, frost.ViewId, NpcDeathUpdate), "One death for the primary target");
        Check.Equal(0, CountViewUpdate(caster, below.ViewId, NpcDeathUpdate), "No kill on another floor");
        Check.That(await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={caster.CharId}") > 0,
            "Independent ability kills award experience");
        Check.Equal(0, caster.World.Problems.Count, "Area protocol problems");
    }
}
