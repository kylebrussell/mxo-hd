using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AndvaranautCouplerUse(ScenarioContext context)
    {
        const ushort ability = 1379, alias = 1377, item = 47927;
        var items = new[] { (5, (uint)item, 2) };
        var awake = new[] { ((ushort)2, (ushort)1) };
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("CouplerOwner") { Level = 50, Items = items, OwnedLoadedAbilities = awake },
            new CharacterSeed("CouplerLower") { X = 17120, Level = 25, Items = items, OwnedLoadedAbilities = awake },
            NearNpc("CouplerControl") with { Level = 50, Health = 5000, MaxHealth = 5000, Items = items,
                OwnedLoadedAbilities = Array.Empty<(ushort, ushort)>() });
        var owner = players[0];
        var lower = players[1];
        var control = players[2];
        var seen = await lower.WaitForViewNamedAsync(owner.Handle);

        control.SendRpc("80 c6 05 00");
        await control.WaitForTextAsync("The Andvaranaut Coupler requires Awakened rank 1.");
        var npc = await control.WaitForViewNamedAsync("WinterFrost");
        control.SendRpc($"40 {HandleOf(npc)} 00 6b");
        await WaitForCombatView(control);
        control.SendRpc("80 c6 05 00");
        await Refused(control, ability, AbilityRefusals.MustBeOutsideCombat);
        Check.Equal(2L, await Count(control), "Rank and combat refusals preserve the stack");

        CastSweepItem(owner, alias, 5);
        await Applied(owner, damage: 12, resistance: 29);
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 b7 00 00 04");
        await lower.WaitForViewUpdateAsync(seen.ViewId, "02 80 80 80 0c b7 00 00 04");

        lower.SendRpc("80 c6 05 00");
        await Applied(lower, damage: 6, resistance: 15);
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 b5 00 00 04");
        await lower.WaitForViewUpdateAsync(seen.ViewId, "02 80 80 80 0c b5 00 00 04");
        Check.Equal(0, owner.Received("80 ac 61 05").Count, "The internal ID also applies instantly");

        owner.SendRpc("80 c6 05 00");
        await Refused(owner, ability, AbilityRefusals.ReuseWait);
        await Records(owner, 12, 29, off: true);
        await Records(lower, 6, 15, off: true);
        Cast(owner, ability);
        await Refused(owner, ability, AbilityRefusals.RequiredItemCategory);
        CastSweepItem(owner, alias, 8);
        await Refused(owner, alias, AbilityRefusals.RequiredItemCategory);

        owner.SendRpc("80 c6 05 00");
        await Refused(owner, ability, AbilityRefusals.ReuseWait);
        Check.Equal(1L, await Count(owner), "Reuse outlasts the buff and preserves the second dose");
        await owner.WaitForRpcAsync(Timer(start: false), TimeSpan.FromSeconds(68));
        owner.SendRpc("80 c6 05 00");
        await owner.WaitForRpcAsync("5f 05 00 01");
        Check.Equal(0L, await Count(owner), "Reuse clears and the next successful use spends the last dose");
        Check.Equal(0, players.Sum(player => player.World.Problems.Count), "Coupler frames stay readable");

        async Task Applied(HeadlessPlayer actor, uint damage, uint resistance)
        {
            await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
            await actor.WaitForRpcAsync(Timer(start: true));
            await Records(actor, damage, resistance, off: false);
            Check.Equal(0, actor.Received("80 ac 63 05").Count, "There is no cast bar for the instant effect");
            Check.Equal(1L, await Count(actor), "Only one Coupler in the stack was spent");
        }

        async Task Records(HeadlessPlayer actor, uint damage, uint resistance, bool off)
        {
            var stats = new (ushort Stat, uint Value)[] {
                (159, damage), (171, damage), (172, damage), (1034, damage),
                (180, resistance), (181, resistance), (157, resistance), (1033, resistance)
            };
            string[] patterns = stats.Select(mod =>
                $"80 bc 15 00 ?? ?? 00 63 05 00 00 {Le(mod.Stat)} {Le(mod.Value)} {(off ? "01" : "00")} 00 00 70 42").ToArray();
            await actor.WaitUntilAsync(() => patterns.All(pattern => actor.Received(pattern).Count > 0),
                $"all eight modifiers with off={off}", TimeSpan.FromSeconds(off ? 65 : 5));
            var records = patterns.Select(pattern => actor.Received(pattern).Single()).ToArray();
            Check.That(records.GroupBy(record => record.DatagramSequence).All(group => group.Key.HasValue && group.Count() <= 6),
                "The shared modifier cap splits the Coupler's eight records");
        }

        Task<long> Count(HeadlessPlayer actor) => context.Database.LongAsync(
            $"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={actor.CharId} AND slot=5 AND goid={item}");

        static string Timer(bool start) =>
            WireFormat.ToHex(AbilityCooldowns.BuildTimer(item, AbilityCooldowns.GroupTimer, 120, start));

        static async Task Refused(HeadlessPlayer actor, ushort id, uint reason)
        {
            var row = await actor.WaitForRpcAsync("80 ba " + Le(id));
            Check.That(AbilityRefusals.TryDecode(row.Bytes, out var refusal) && refusal.ErrorID == reason,
                $"Coupler request {id} refuses with reason {reason}");
        }
    }
}
