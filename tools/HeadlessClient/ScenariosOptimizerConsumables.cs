using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task OptimizerConsumablesUse(ScenarioContext context)
    {
        var items = new[] { (5, 47111u, 2), (6, 47112u, 1), (7, 47113u, 1) };
        var players = await context.AddPlayersInWorldAsync(
            NearNpc("OptimizerOwner") with { Level = 50, Health = 5000, MaxHealth = 5000, InnerStrength = 350, MaxInnerStrength = 500,
                Items = items, OwnedLoadedAbilities = new[] { ((ushort)2, (ushort)50), ((ushort)10, (ushort)50) } },
            new CharacterSeed("BelowOptimizerLevel") { Level = 44, Items = items });
        var actor = players[0];
        var low = players[1];
        var server = context.Server;

        foreach (byte slot in new byte[] { 5, 6, 7 })
        {
            low.SendRpc($"80 c6 {slot:x2} 00");
            await low.WaitForTextAsync("character level is too low");
        }
        Check.Equal(4L, await Count(low), "All three level-45 refusals preserve the items");
        Cast(actor, 1277);
        await Refused(1277, AbilityRefusals.RequiredItemCategory);

        actor.SendRpc("80 c6 05 00");
        await ItemCast(1275, 47111, "69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync(Bonus(1275, 117, 10));
        actor.SendRpc("80 c6 05 00");
        await Refused(1275, AbilityRefusals.ReuseWait);
        Check.Equal(3L, await Count(actor), "Early retry preserves the second inhaler");
        Cast(actor, 10);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains($"[ABILITY-COST] char={actor.CharId} ability=10 paid=18 "),
            "Hyper-Dodge pays 18 of its ordinary 20 IS", HeadlessPlayer.DefaultTimeout);

        CastSweepItem(actor, 1276, 6);
        await ItemCast(1276, 47112, "5f 06 00 01");
        await actor.WaitForRpcAsync(Bonus(1276, 109, 30));
        await actor.WaitUntilAsync(() => server.DebugLog().Contains($"[VITALS] char={actor.CharId} maxIS 500 -> 530"),
            "The Hypo raises maximum IS", HeadlessPlayer.DefaultTimeout);

        // The Overdrive GOB repeats 1276 in its internal AbilityID while its owned item grants 1277.
        CastSweepItem(actor, 1276, 7);
        await ItemCast(1277, 47113, "5f 07 00 01");
        await OverdriveRecords(off: false);
        Check.Equal(1L, await Count(actor), "Each item spends one unit and uses its own timer");

        const string fx = "02 80 80 80 80 30 07 00 00 c6";
        var ownFx = BytePattern.Parse("02 00 " + fx);
        actor.SendRpc("80 bb fd 04");
        await Task.Delay(4500);
        Check.Equal(0, actor.Received(Bonus(1277, 159, 8, off: true)).Count, "Overdrive cannot be dismissed");
        Check.That(!actor.World.ViewSnapshot().Any(ownFx.Occurs), "Overdrive FX stays quiet outside interlock");

        var npc = await actor.WaitForViewNamedAsync("WinterFrost");
        actor.SendRpc($"40 {HandleOf(npc)} 00 6b");
        await WaitForCombatView(actor);
        await actor.WaitForViewUpdateAsync(2, fx, TimeSpan.FromSeconds(7));
        actor.SendRpc("44 01");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains($"[ENRAGE] withdraw suppressed char={actor.CharId}"),
            "Overdrive's Enrage suppresses ordinary Withdraw", HeadlessPlayer.DefaultTimeout);

        actor.SendChat("?die");
        await OverdriveRecords(off: true);
        await actor.WaitForRpcAsync(Bonus(1276, 109, 30, off: true));
        await actor.WaitUntilAsync(() => server.DebugLog().Contains($"[VITALS] char={actor.CharId} maxIS 530 -> 500"),
            "Death clears the boosted maximum", HeadlessPlayer.DefaultTimeout);
        int fxAtDeath = actor.World.ViewSnapshot().Count(ownFx.Occurs);
        await Task.Delay(4500);
        Check.Equal(fxAtDeath, actor.World.ViewSnapshot().Count(ownFx.Occurs), "Death cancels the Overdrive FX");
        Check.Equal(0, actor.World.Problems.Count + low.World.Problems.Count, "Optimizer frames stay readable");

        async Task ItemCast(ushort ability, ushort item, string spent)
        {
            var cast = await actor.WaitForRpcAsync("80 ac " + Le(ability));
            Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "Every consumable casts in one second");
            await actor.WaitForRpcAsync(spent);
            await actor.WaitUntilAsync(() => actor.Received(WireFormat.ToHex(
                AbilityCooldowns.BuildTimer(item, AbilityCooldowns.GroupTimer, 30, true))).Count == 1,
                "A single 30-second timer starts on the owned item", HeadlessPlayer.DefaultTimeout);
        }

        Task OverdriveRecords(bool off) => actor.WaitUntilAsync(() =>
            new ushort[] { 159, 171, 172, 1034 }.All(stat => actor.Received(Bonus(1277, stat, 8, off)).Count == 1) &&
            actor.Received(Bonus(1277, 93, 0, off, kind: "11")).Count == 1,
            $"Overdrive damage and Enrage records with off={off}", HeadlessPlayer.DefaultTimeout);

        Task<long> Count(HeadlessPlayer player) => context.Database.LongAsync(
            $"SELECT SUM(count) FROM inventory WHERE charId={player.CharId} AND goid BETWEEN 47111 AND 47113");

        async Task Refused(ushort ability, uint reason)
        {
            var denial = await actor.WaitForRpcAsync("80 ba " + Le(ability));
            Check.That(AbilityRefusals.TryDecode(denial.Bytes, out var record) && record.ErrorID == reason,
                $"Ability {ability} refusal reason is {reason}");
        }

        static string Bonus(ushort ability, ushort stat, uint value, bool off = false, string kind = "15") =>
            $"80 bc {kind} 00 ?? ?? 00 {Le((uint)ability)} {Le(stat)} {Le(value)} {(off ? "01" : "00")} 00 00 96 43";
    }
}
