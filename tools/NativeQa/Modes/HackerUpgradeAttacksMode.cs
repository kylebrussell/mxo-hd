using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerUpgradeAttacksMode : NativeQaMode
{
    private const ushort UpgradeAttacks = 314, Style = CombatStyles.HackerStyle;
    private const int Level = 22, X = 17000, Y = 495, Z = 6400, FriendX = 17400;
    // Vector PvP, July 29, lines 9080–9081. Rebind only the selected player's view and spawn.
    private const string StockCast = "80b93a010d005a0000000000000000000000000000000000000000000000000000000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Style, UpgradeAttacks }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-hacker-upgrade-attacks";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "rotation=64,alignment=1,pvpflag=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, hotbarAbilities: new[] { Style, UpgradeAttacks }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "314", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAFriend ({FriendX},{Y},{Z}). Page 2 Hacker Style / Upgrade Attacks.",
            "Choose Hacker Style (slot 1). Open Status > Bonuses and record the Viral Accuracy baseline. /target QAFriend, then press slot 2. Expect 2 s cast, 90 s own reuse, and [ABILITY-COST] char=110 ability=314 paid=35 IS=508->473 with the loaded passives.",
            "[MODIFIER] char=110 source=314 stat=342 add value=52 for 20 s. Confirm the Status delta and the matching removal after 20 s. QAFriend gets no 314 record and there are no max [VITALS] changes or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Upgrade Attacks needs the repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Verified outdoor Upgrade Attacks stand ({x},{Y},{Z})");

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
            "The level-22 Upgrade Attacks loadout fits memory with all prerequisites loaded");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        int cursor = actor.World.RpcSnapshot().Count;
        int friendCursor = friend.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockCast, DuelPackets.Handle(target.ViewId, target.SpawnId)));
        await EvidenceRetestWire.CastBarAsync(actor, UpgradeAttacks, 2);
        await EvidenceRetestWire.TimerAsync(actor, cursor, UpgradeAttacks, AbilityCooldowns.OwnTimer, 90);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[ABILITY-COST] char=110 ability=314 paid=35 IS=508->473"),
            "Upgrade Attacks spends the ordinary 35 IS", HeadlessPlayer.DefaultTimeout);
        await StockAbilityProfile.ObserveAsync(actor, cursor, packet => StockAbilityProfile.Bonus(packet, UpgradeAttacks, 342, 52, 20, false),
            "Level 22 adds +52 Viral Accuracy to the caster");
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(packet =>
            StockAbilityProfile.Bonus(packet, UpgradeAttacks, 342, 52, 20, true)),
            "The same +52 source record expires after 20 seconds", TimeSpan.FromSeconds(24));
        Check.That(!friend.World.RpcSnapshot().Skip(friendCursor).Any(packet =>
            StockAbilityProfile.Bonus(packet, UpgradeAttacks, 342, 52, 20, false)), "The selected friendly player gets no buff");
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Upgrade Attacks stock protocol");
        return Array.Empty<byte>();
    }
}
