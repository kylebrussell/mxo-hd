using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerCombatHackingMode : NativeQaMode
{
    private const ushort CombatHacking = 344, Sprint = 184, Style = CombatStyles.HackerStyle;
    private const int Level = 22, X = 17000, Y = 495, Z = 6400, FriendX = 17400;
    // abilities_usecombathacking.log.txt lines 2–3. Replace only the live selected view and spawn.
    private const string CombatHackingStock = "80b958011b00860000000000000000000000000000000000000000008050652800000000586b0000000000";
    private static readonly (ushort Stat, int Value)[] Bonuses = { (1010, 22), (117, 24), (1009, 22) };
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Style, CombatHacking, Sprint }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-hacker-combat-hacking";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "rotation=64,alignment=1,pvpflag=0,innerStrC=600,innerStrM=600 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Style, CombatHacking, Sprint },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "344", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV CSR ({X},{Y},{Z}) faces friendly QAFriend ({FriendX},{Y},{Z}). Page 2 Hacker Style / Combat Hacking / Hyper-Sprint.",
            "Choose Hacker Style. The required Dedicated Code 346 gives stat 1053 +6 throughout. Press slot 3 for Hyper-Sprint at 24 IS from base 25. Wait one second and press again to end before the first drain. Wait for its own reuse.",
            "Use /target QAFriend and slot 2 to match the captured selected-target route. Expect [ABILITY-COST] ability=344 paid=19 from base 20, 2 s cast and 298 s reuse. On CSR only, [MODIFIER] source 344 gives stats 1010/+22, 117/+24 and 1009/+22, each for 32 s. These are the values in the stock capture.",
            "While 344 is active, slot 3 now costs 18 IS. Wait one second and toggle off before the first drain. After 32 s, expect all three source-344 removes, then another slot 3 activation costs 24 IS. No max [VITALS] changes or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Combat Hacking needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Verified Combat Hacking stand ({x},{Y},{Z})");

        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(parent => archive.TryGetValue(parent.Key, out ushort rank) && rank >= parent.Value)),
            "The level-22 Combat Hacking profile fits and all prerequisites qualify");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, 346, 1053, 6, 0, false),
            "Required Dedicated Code contributes six baseline points");
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        int? remainingIs = null;
        await SprintAsync(24);
        int rpcAt = actor.World.RpcSnapshot().Count;
        int friendAt = friend.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(CombatHackingStock, DuelPackets.Handle(target.ViewId, target.SpawnId)));
        await EvidenceRetestWire.CastBarAsync(actor, CombatHacking, 2);
        await EvidenceRetestWire.TimerAsync(actor, rpcAt, CombatHacking, AbilityCooldowns.OwnTimer, 298);
        await CostAsync(logAt, CombatHacking, 19);
        foreach (var (stat, value) in Bonuses)
            await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame => StockAbilityProfile.Bonus(frame, CombatHacking, stat, value, 32, false),
                $"Combat Hacking adds stat {stat}");
        await SprintAsync(18);
        await actor.WaitUntilAsync(() => Bonuses.All(bonus => actor.World.RpcSnapshot().Skip(rpcAt).Any(frame =>
                StockAbilityProfile.Bonus(frame, CombatHacking, bonus.Stat, bonus.Value, 32, true))),
            "All Combat Hacking records expire together", TimeSpan.FromSeconds(35));
        await SprintAsync(24);
        Check.That(!friend.World.RpcSnapshot().Skip(friendAt).Any(frame => Bonuses.Any(bonus =>
            StockAbilityProfile.Bonus(frame, CombatHacking, bonus.Stat, bonus.Value, 32, false))), "Combat Hacking remains self-only");
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Combat Hacking stock packets");
        return Array.Empty<byte>();

        async Task SprintAsync(int expected)
        {
            int castAt = server.DebugLog().Length;
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(AwakenedStockStealthSpeedMode.SprintUse);
            await CostAsync(castAt, Sprint, expected);
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Sprint, 877, 15, 0, false), "Sprint is on");
            await Task.Delay(TimeSpan.FromSeconds(1));
            cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(AwakenedStockStealthSpeedMode.SprintUse);
            await EvidenceRetestWire.TimerAsync(actor, cursor, Sprint, AbilityCooldowns.OwnTimer, 5, ended: true);
        }

        async Task CostAsync(int from, ushort source, int paid)
        {
            var pattern = new Regex($@"\[ABILITY-COST\] char=110 ability={source} paid=(\d+) IS=(\d+)->(\d+)");
            Match cost = Match.Empty;
            await actor.WaitUntilAsync(() => (cost = pattern.Match(server.DebugLog()[from..])).Success,
                $"Ability {source} paid {paid} IS", HeadlessPlayer.DefaultTimeout);
            int before = int.Parse(cost.Groups[2].Value);
            int after = int.Parse(cost.Groups[3].Value);
            Check.Equal(paid, int.Parse(cost.Groups[1].Value), $"Ability {source} debit");
            Check.Equal(paid, before - after, $"Ability {source} current IS delta");
            if (remainingIs.HasValue)
                Check.Equal(remainingIs.Value, before, "IS changes only at the measured activations");
            remainingIs = after;
        }
    }
}
