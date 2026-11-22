using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerWithdrawBonusMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300, DummyX = 17200, DirectWithdraw = 25;
    private const ushort Virologist = 98, ResistViruses = 76, Phage = 83, SelfDefense = 17;
    private static readonly (ushort Ability, int Bonus)[] LoadOrder = { (Virologist, 1), (ResistViruses, 4), (Phage, 1) };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, SelfDefense, Phage, ResistViruses, Virologist)
        .Order().ToArray();

    public override string Name => "ability-parity-starter-hacker-withdraw-bonus";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=64,healthC=4000,healthM=4000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, hotbarAbilities: new[] { SelfDefense }, hotbarWithdraw: true,
        testMobFixture: "awakened-power-shot-cap.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "76,83,98", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces Melee Dummy ({DummyX},{Y},{Z}) east, clear of ice FX. No helpers or bag. Page 2 slot 1 Self Defense, slot 2 stock Withdraw.",
            "Record the Status Bonuses entries for Contested Withdraw and Contested Withdraw Bonus, if shown. [MODIFIER] stat 1045 has three zero-duration sources: 98 +1, 76 +4, 83 +1. Self Defense at rank 19 contributes direct 1005 +25.",
            "Select Melee Dummy and open ordinary Close Combat unarmed. Keep Self Defense. Press slot 2. Capture [WITHDRAW INPUT OURS] direct=25 input=25 bonus1045=6. The roll may fail. Use ?combatdepth fixture-reset, wait for the HUD to clear, then strafe and reselect the dummy for the next comparison. Keep the target selected without Escape.",
            "Out of combat type ?qaunload 83, ?qaunload 76 and ?qaunload 98 in that order. Check the three 1045 removals and repeat the Status and Withdraw sample. Expect direct=25 input=25 bonus1045=0. Reset again and type ?qaload 98, ?qaload 76 and ?qaload 83. Check restoration and repeat. Expect direct=25 input=25 bonus1045=6. No max [VITALS] changes or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor NAV at ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "The known clear withdrawal stand has a mapped path to the dummy");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The level-19 archive has all qualified prerequisites and fits memory");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        foreach (var (ability, bonus) in LoadOrder)
            await BonusAsync(0, ability, bonus, remove: false);

        await SampleAsync(6);
        foreach (bool load in new[] { false, true })
        {
            var rows = load ? LoadOrder : LoadOrder.Reverse();
            foreach (var (ability, bonus) in rows)
            {
                int from = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load);
                await BonusAsync(from, ability, bonus, remove: !load);
            }
            await SampleAsync(load ? 6 : 0);
        }
        Check.Equal(0, actor.World.Problems.Count, "Hacker withdrawal comparison framing");
        return Array.Empty<byte>();

        Task<RpcRecord> BonusAsync(int from, ushort ability, int bonus, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, from, frame => StockAbilityProfile.Bonus(frame, ability, 1045, bonus, 0, remove),
                $"Passive {ability} stat 1045 value {bonus} remove={remove}");

        async Task SampleAsync(int bonus)
        {
            actor.SkipReceived();
            actor.SendRpc($"81 51 {handle}");
            actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen,
                DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)), WireFormat.FromHex($"40 {handle} 01 6b"));
            await actor.WaitForRpcAsync($"40 {handle} 00 6b");
            await actor.WaitForRpcAsync("47");
            await StockAbilityProfile.ObserveAsync(actor, 0,
                frame => StockAbilityProfile.Bonus(frame, SelfDefense, 1005, DirectWithdraw, 0, false),
                "Self Defense's direct withdrawal value");
            int from = server.DebugLog().Length;
            actor.SendRpc("44 01");
            await actor.WaitUntilAsync(() => server.DebugLog()[from..].Contains(
                    $"[WITHDRAW INPUT OURS] CSR_Tester direct={DirectWithdraw} input={DirectWithdraw} bonus1045={bonus}", StringComparison.Ordinal),
                "Ordinary Withdraw keeps its direct input while the separate bonus changes", HeadlessPlayer.DefaultTimeout);
            actor.SendChat("?combatdepth fixture-reset");
            await actor.WaitForTextAsync($"Fixture ready: Melee Dummy view {dummy.ViewId}, spawn {dummy.SpawnId}");
        }
    }
}
