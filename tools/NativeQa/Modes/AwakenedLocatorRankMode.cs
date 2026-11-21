using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedLocatorRankMode : NativeQaMode
{
    private const int Level = 10, X = 17000, Y = 495, Z = 6400, Near = 17400, Far = 17600;
    private const ushort Locate = 215, LocateSkill = 703;
    private const string FarStand = "?tp 17600 495 6400 4e2 12 0";
    private const string NearStand = "?tp 17400 495 6400 4e2 12 1";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Locate).Order().ToArray();
    private static readonly BytePattern On = BytePattern.Parse("80 bd 05 d7 00 00 00 00 00 00 01");
    private static readonly BytePattern Off = BytePattern.Parse("80 bd 05 d7 00 00 00 00 00 00 00");
    private static readonly BytePattern Marker = BytePattern.Parse("80 cd 00 00 62 04 00 28");

    public override string Name => "ability-parity-awakened-locator-rank";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "UPDATE char_abilities SET level=4 WHERE char_id=110 AND ability_id=2147703808",
        "DELETE FROM inventory WHERE charId=110 AND (slot IN (1,8) OR goid IN (422,423,38208,38209))",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,422,1,1,0),(110,423,8,1,0)"
    }, new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
        $"rotation={MobAggro.YawTowards(X,Z,Far,Z)},cash=100000,innerStrC=200,innerStrM=200 WHERE charId=110" },
        DevCommands: true, testMobFixture: "martial-player-controls.csv",
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[]
        {
            new(8, 423, "Data Node Goggles"),
            new(1, 422, "Handheld Data Node Locator")
        }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "215,298", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR starts at ({X},{Y},{Z}); type ?utilityobject 606 once to place the node on that stand. The near stand ({Near},{Y},{Z}) is 4 m away and the far stand ({Far},{Y},{Z}) is 6 m away. All three are outdoor NAV floor 400.",
            $"At rank 4, expect [MODIFIER] 215/703 +4. Type {FarStand}. Open page 2 and use slot 1 Goggles from bag slot 8. [DATA-MINING] tool=423 should turn on with no node marker at 6 m. Wait two seconds, turn it off, then use slot 2 Handheld from bag slot 1. It should produce a node FX and attenuation at the same spot and clear after 10 seconds.",
            "Train Locate Data Node 4 to 5 for 5,000 Info in the Upgrade window while standing still. If the tool ability is omitted from Upgrade, report the UI gate and capture the rank-4 pair. Expect [MODIFIER] 215/703 +4 remove and +5 add. Repeat slots 1 and 2 from the same stand. The 5 m Goggles and 30 m handheld ranges should stay the same.",
            $"For the positive Goggles control, type {NearStand}, then use slot 1. The node FX should appear at 4 m and clear when toggled off. No target, bag, [PVP] rounds or maximum-vitals changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Locator rank needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, Near, Far })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Locator stand ({x},{Y},{Z}) is on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && rows[Locate].Level == 4 &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => rows.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Rank-4 locator and its Detection prerequisite fit the level-10 loadout");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await SkillAsync(0, 4, false);
        actor.SendChat("?utilityobject 606");
        await actor.WaitForTextAsync("Utility fixture 606 placed.");
        actor.SendChat(FarStand);
        await actor.WaitForTextAsync("Teleported to 4e2/12");

        await GogglesAsync(visible: false);
        var before = await HandheldAsync();
        int trainAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc($"80 b7 d7 00 {StockAbilityProfile.LittleEndianHex(rows[Locate].Slot)} 01");
        byte[] trained = ServerPackets.BuildAbilityUpgraded(Locate, 5);
        await StockAbilityProfile.ObserveAsync(actor, trainAt, frame => frame.Bytes.AsSpan().SequenceEqual(trained), "Stock locator rank trains to 5");
        await SkillAsync(trainAt, 4, true);
        await SkillAsync(trainAt, 5, false);

        await GogglesAsync(visible: false);
        var after = await HandheldAsync();
        Check.That(before.Bytes.AsSpan().SequenceEqual(after.Bytes), "The same tool uses identical attenuation after the Locate Skill changes");
        actor.SkipReceived();
        actor.SendChat(NearStand);
        await actor.WaitForTextAsync("Teleported to 4e2/12");
        await GogglesAsync(visible: true);
        Check.Equal(0, actor.World.Problems.Count, "Locator rank control framing");
        Console.WriteLine($"LOCATOR RANK PASS: same 6 m handheld attenuation {after.Hex} at ranks four and five. Goggles find the node only at 4 m.");
        return Array.Empty<byte>();

        Task<RpcRecord> SkillAsync(int start, int value, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, start, frame => StockAbilityProfile.Bonus(frame, Locate, LocateSkill, value, 0, remove),
                $"Locator skill 703 {value}, remove={remove}");

        async Task GogglesAsync(bool visible)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(AwakenedDataNodeToolsMode.Goggles);
            await StockAbilityProfile.ObserveAsync(actor, start, frame => On.IsMatch(frame.Bytes), "Goggles are active");
            if (visible)
                await StockAbilityProfile.ObserveAsync(actor, start, frame => Marker.IsMatch(frame.Bytes), "Goggles find the nearby node");
            else
                await Task.Delay(TimeSpan.FromSeconds(2.2));
            int offAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc(AwakenedDataNodeToolsMode.Goggles);
            await StockAbilityProfile.ObserveAsync(actor, offAt, frame => Off.IsMatch(frame.Bytes), "Goggles toggle off");
            Check.Equal(visible, actor.World.RpcSnapshot().Skip(start).Any(frame => Marker.IsMatch(frame.Bytes)),
                "Only the node inside the stored Goggles range is marked");
        }

        async Task<RpcRecord> HandheldAsync()
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(AwakenedDataNodeToolsMode.Handheld);
            await StockAbilityProfile.ObserveAsync(actor, start, frame => On.IsMatch(frame.Bytes), "Handheld starts");
            await StockAbilityProfile.ObserveAsync(actor, start, frame => Marker.IsMatch(frame.Bytes), "Handheld finds the 6 m node");
            var attenuation = await StockAbilityProfile.ObserveAsync(actor, start, frame => BytePattern.Parse("80 cb").IsMatch(frame.Bytes),
                "Handheld attenuation from the same stand");
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(start).Any(frame => Off.IsMatch(frame.Bytes)),
                "Handheld expires normally", TimeSpan.FromSeconds(15));
            return attenuation;
        }
    }
}
