using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerWithdrawBonusMode : NativeQaMode
{
    private const int Level = 50, ConcealmentRank = 40, X = 17000, Y = 495, Z = 2300, DummyX = 17200, DirectWithdraw = 65;
    private const ushort Virologist = 98, Concealment = 503, SelfDefense = 17;
    private static readonly (ushort Ability, int Bonus)[] Sources = { (Virologist, 1), (Concealment, 4) };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, SelfDefense, Virologist, Concealment)
        .Order().ToArray();

    public override string Name => "ability-parity-starter-hacker-withdraw-bonus";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={ConcealmentRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Concealment)}" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=64,healthC=4000,healthM=4000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, hotbarAbilities: new[] { SelfDefense }, hotbarWithdraw: true,
        testMobFixture: "awakened-power-shot-cap.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "98,503", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces Melee Dummy ({DummyX},{Y},{Z}) east, clear of ice FX. No helpers or bag. Page 2 slot 1 Self Defense, slot 2 stock Withdraw.",
            "Record both Status entries. Self Defense contributes direct 1005 +65. [MODIFIER] has permanent 1045 adds from Virologist 98 +1 and rank-40 Concealment 503 +4. Expect Contested Withdraw Bonus +5%.",
            "Select Melee Dummy and open ordinary Close Combat unarmed. Keep Self Defense. Press slot 2 for stock Withdraw. Expect [WITHDRAW INPUT OURS] direct=65 input=68 bonus1045=5. The random outcome may vary. Between samples use ?combatdepth fixture-reset, wait for combat HUD clear, then strafe and reselect the dummy without Escape.",
            "Between fights type ?qaunload 98. Expect [MODIFIER] source=98 stat=1045 remove value=+1; repeat Withdraw for direct=65 input=67 bonus1045=4. Reset and type ?qaload 98. Expect stat 1045 add value=+1; repeat for direct=65 input=68 bonus1045=5.",
            "Reset and type ?qaunload 503. Expect [MODIFIER] source=503 stat=1045 remove value=+4; repeat Withdraw for direct=65 input=65 bonus1045=1. Reset and type ?qaload 503. Expect stat 1045 add value=+4; repeat for direct=65 input=68 bonus1045=5. No [PVP] rounds or max [VITALS] changes."
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
            "The archive has all qualified prerequisites and fits memory");
        Check.Equal((ushort)ConcealmentRank, archive[Concealment].Level, "Concealment rank at the 4% to 5% breakpoint");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        foreach (var (ability, bonus) in Sources)
            await BonusAsync(0, ability, bonus, remove: false);

        await SampleAsync(5, 68);
        foreach (var (ability, ownBonus, withoutBonus, withoutInput) in new[]
        {
            (Virologist, 1, 4, 67),
            (Concealment, 4, 1, 65)
        })
        {
            int removal = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load: false);
            await BonusAsync(removal, ability, ownBonus, remove: true);
            await SampleAsync(withoutBonus, withoutInput);
            int addition = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load: true);
            await BonusAsync(addition, ability, ownBonus, remove: false);
            await SampleAsync(5, 68);
        }
        Check.Equal(0, actor.World.Problems.Count, "Hacker withdrawal comparison framing");
        return Array.Empty<byte>();

        Task<RpcRecord> BonusAsync(int from, ushort ability, int bonus, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, from, frame => StockAbilityProfile.Bonus(frame, ability, 1045, bonus, 0, remove),
                $"Passive {ability} stat 1045 value {bonus} remove={remove}");

        async Task SampleAsync(int bonus, int expectedInput)
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
                    $"[WITHDRAW INPUT OURS] CSR_Tester direct={DirectWithdraw} input={expectedInput} bonus1045={bonus}", StringComparison.Ordinal),
                "Ordinary Withdraw applies the current linked percentage to the unchanged direct points", HeadlessPlayer.DefaultTimeout);
            actor.SendChat("?combatdepth fixture-reset");
            await actor.WaitForTextAsync($"Fixture ready: Melee Dummy view {dummy.ViewId}, spawn {dummy.SpawnId}");
        }
    }
}
