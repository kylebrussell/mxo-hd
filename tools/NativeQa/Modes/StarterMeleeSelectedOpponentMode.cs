using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterMeleeSelectedOpponentMode : NativeQaMode
{
    private const int Level = 35, Rank = 19, X = 17000, Y = 495, Z = 2300, DummyX = 17200;
    private const ushort LowBlow = 605, OverhandSmash = 145, CheapShot = 198;

    // PR405 stock PacketLog 8745737faaee lines 1190Ã¢â‚¬â€œ1191.
    private const string LowBlowRequest = "80b95d020400020000000000000000000000000000000000000000005d020000006b0000086b0000000000";
    private static readonly StarterSoldierResistanceMode.Sample Smash =
        StarterSoldierResistanceMode.Samples.Single(sample => sample.Id == OverhandSmash);
    private static readonly Sample[] Samples =
    {
        new(LowBlow, CombatStyles.CoderStyle, StarterCoderPetBuffsAndDetonateMode.CoderStock, LowBlowRequest, 6, 288),
        new(OverhandSmash, CombatStyles.Karate, Smash.StockStyle, Smash.StockUse, 12, 277),
        new(CheapShot, CombatStyles.SelfDefense, AwakenedInterlockAdmissionMode.StockOpen,
            AwakenedInterlockAdmissionMode.Samples.Single(sample => sample.Ability == CheapShot).Capture, 20, 285)
    };
    private static readonly ushort[] Loadout = Samples.SelectMany(sample => new[] { sample.Id, sample.Style })
        .Concat(new ushort[] { 2, 18, 154, 604 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-melee-selected-opponent";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
            "healthC=5000,healthM=5000,innerStrC=500,innerStrM=1000 WHERE charId=110",
        $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, hotbarAbilities: Samples.SelectMany(sample => new[] { sample.Style, sample.Id }).ToArray(),
        hotbarWithdraw: true, testMobFixture: "awakened-power-shot-cap.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "605,145,198", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR ({X},{Y},{Z}) faces the isolated Melee Dummy ({DummyX},{Y},{Z}). Both outdoor NAV stands are verified. Page 2 has Coder Style/Low Blow, Karate/Overhand Smash, Self Defense/Cheap Shot, then Withdraw. Characters are level {Level} for the combined memory requirement. All ability ranks are capped at {Rank}.",
            "At rest select the dummy. Pick each style before trying its move. If the client sends a move despite the at-rest gate, expect reason 24 and no [ABILITY-COST], cast or reuse. Restore Self Defense before opening Close Combat through the target's right-click menu.",
            "During interlock reselect the matching style before each move. Keep the same dummy selected. Low Blow should queue in Coder Style for 30 IS and 6 s reuse, move 288. A landed neutral hit should be 19. Overhand Smash should queue for 20 IS and 12 s reuse, move 277 and two -10 melee defense/accuracy [EFFECTS] records for 10 s on a hit. Cheap Shot is a regression control in Self Defense. It queues on the same opponent and starts 20 s reuse.",
            "If a round misses, wait for reuse before trying it again. Keep selection and use slot 7 to withdraw after the controls. Stale handle refusal is verified headlessly. No helpers or bag actions."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Melee stand {x},{Y},{Z} is on outdoor NAV");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "The dummy is visible across the outdoor stand");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "All ranked melee prerequisites fit the stock profile");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));

        foreach (var sample in Samples)
            await RefusedAsync(actor, sample.Id, WithHandle(sample.Use, handle));

        actor.World.SendRpcs(WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
        await actor.WaitForRpcAsync($"40 {hex} 00 6b");
        await actor.WaitForRpcAsync("47");
        foreach (var sample in Samples)
        {
            int viewStart = actor.World.ViewSnapshot().Count;
            var style = BytePattern.Parse($"02 00 02 80 80 80 80 80 10 {StockAbilityProfile.LittleEndianHex(sample.Style)} 00 00");
            actor.SendRpc(WithHandle(sample.StyleUse, handle));
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewStart).Any(style.Occurs),
                $"Style {sample.Style} selected", HeadlessPlayer.DefaultTimeout);

            await RefusedAsync(actor, sample.Id, WithHandle(sample.Use, handle ^ 0x10000));
            int timerStart = actor.World.RpcSnapshot().Count;
            int logStart = server.DebugLog().Length;
            actor.SendRpc(WithHandle(sample.Use, handle));
            await EvidenceRetestWire.TimerAsync(actor, timerStart, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            string next = $"[COMBAT] {AbilityCatalog.Find(sample.Id).Name} is the next move";
            await actor.WaitUntilAsync(() =>
            {
                string log = server.DebugLog()[logStart..];
                int start = log.IndexOf(next, StringComparison.Ordinal);
                return start >= 0 && log[start..].Contains($"animation {sample.Animation} (", StringComparison.Ordinal);
            }, $"Stock {sample.Id} queues a move against the live opponent", TimeSpan.FromSeconds(15));
        }
        Check.Equal(0, actor.World.Problems.Count, "Normal melee requests frame cleanly");
        return Array.Empty<byte>();
    }

    private sealed record Sample(ushort Id, ushort Style, string StyleUse, string Use, int Reuse, int Animation);
}
