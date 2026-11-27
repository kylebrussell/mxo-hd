using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task BlueMarbleConsumables(ScenarioContext context)
    {
        var items = new[] { (5, 47907u, 2), (6, 47908u, 1), (7, 47909u, 1) };
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("MarbleOwner") { Level = 50, Items = items, OwnedLoadedAbilities = new[] { ((ushort)2, (ushort)50) } },
            NearNpc("MarbleControl") with { Level = 49, Health = 5000, MaxHealth = 5000, Items = items,
                OwnedLoadedAbilities = new[] { ((ushort)2, (ushort)39) } });
        var actor = players[0];
        var control = players[1];
        var stats = new ushort[] { 520, 183, 1009, 1010, 1011 };

        control.SendRpc("80 c6 07 00");
        await control.WaitForTextAsync("character level is too low");
        CastSweepItem(control, 1375, 6);
        await control.WaitForTextAsync("This Blue Marble requires Awakened rank 40.");
        var npc = await control.WaitForViewNamedAsync("WinterFrost");
        control.SendRpc($"40 {HandleOf(npc)} 00 6b");
        await WaitForCombatView(control);
        control.SendRpc("80 c6 05 00");
        await Refused(control, 1374, AbilityRefusals.MustBeOutsideCombat);
        Check.Equal(4L, await BagCount(control), "Each admission refusal keeps the item");

        Cast(actor, 1374);
        await Refused(actor, 1374, AbilityRefusals.RequiredItemCategory);
        actor.SendRpc("80 c6 05 00");
        await Applied(1374, 47907, 30, "69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 af 00 00 04");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 a8 00 00 04");

        CastSweepItem(actor, 1375, 6);
        await Refused(actor, 1375, AbilityRefusals.ReuseWait);
        Check.Equal(3L, await BagCount(actor), "Shared reuse preserves the other stage");
        await Records(1374, 30, off: true);
        actor.SendRpc("80 c6 05 00");
        await Refused(actor, 1374, AbilityRefusals.ReuseWait);
        Check.Equal(3L, await BagCount(actor), "Same item reuse continues after the buff expires");
        await actor.WaitForRpcAsync(Timer(1374, start: false), TimeSpan.FromSeconds(37));

        CastSweepItem(actor, 1375, 6);
        await Applied(1375, 47908, 40, "5f 06 00 01");
        await Records(1375, 40, off: true);
        await actor.WaitForRpcAsync(Timer(1374, start: false), TimeSpan.FromSeconds(37));

        actor.SendRpc("80 c6 07 00");
        await Applied(1376, 47909, 50, "5f 07 00 01");
        await Records(1376, 50, off: true);
        Check.Equal(1L, await BagCount(actor), "Each successful stage spends one marble");
        Check.Equal(0, actor.World.Problems.Count + control.World.Problems.Count, "Marble frames stay readable");

        async Task Applied(ushort ability, ushort item, uint value, string inventoryRecord)
        {
            await actor.WaitForRpcAsync(inventoryRecord);
            await actor.WaitForRpcAsync(Timer(1374, start: true));
            Check.That(actor.Received(Timer(item, start: true)).Count == 1, "One 30-second item timer starts");
            Check.Equal(0, actor.Received("80 ac " + Le(ability)).Count, "Marbles apply instantly");
            await Records(ability, value, off: false);
        }

        Task Records(ushort ability, uint value, bool off) =>
            actor.WaitUntilAsync(() => stats.All(stat => actor.Received(
                $"80 bc 15 00 ?? ?? 00 {Le((uint)ability)} {Le(stat)} {Le(value)} {(off ? "01" : "00")} 00 00 c0 40").Count == 1),
                $"Five stage {ability} modifier records with off={off}", TimeSpan.FromSeconds(11));

        Task<long> BagCount(HeadlessPlayer player) => context.Database.LongAsync(
            $"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={player.CharId} AND goid BETWEEN 47907 AND 47909");

        static string Timer(ushort id, bool start) =>
            WireFormat.ToHex(AbilityCooldowns.BuildTimer(id, AbilityCooldowns.GroupTimer, 30, start));

        static async Task Refused(HeadlessPlayer player, ushort ability, uint reason)
        {
            var row = await player.WaitForRpcAsync("80 ba " + Le(ability));
            Check.That(AbilityRefusals.TryDecode(row.Bytes, out var denial) && denial.ErrorID == reason,
                $"Ability {ability} refusal reason is {reason}");
        }
    }
}
