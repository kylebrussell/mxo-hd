using hds;
using HeadlessClient;
using static NativeQa.Modes.AwakenedDataNodeToolsMode;

namespace NativeQa.Modes;

public sealed class AwakenedLocatorLifecycleMode : NativeQaMode
{
    private const int Level = 10, X = 17000, Y = 495, Z = 6400, NearX = 17400;
    private const string MoveNear = "?tp 17400 495 6400 4e2 12 1";
    private const ushort Locate = UtilityAbilities.LocateDataNode;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Locate }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly string On = WireFormat.ToHex(AbilityBuffs.BuildExclusive(Locate, true));
    private static readonly string Off = WireFormat.ToHex(AbilityBuffs.BuildExclusive(Locate, false));

    public override string Name => "ability-parity-awakened-locator-lifecycle";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=CASE WHEN ability_id={AbilityMessages.AbilityGameObject(Locate)} THEN 5 ELSE LEAST(level,{Level}) END WHERE char_id=110",
        "DELETE FROM inventory WHERE charId=110 AND (slot IN (1,8) OR goid IN (422,423,38208,38209))",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,422,1,1,0),(110,423,8,1,0)"
    }, new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},innerStrC=20,innerStrM=200 WHERE charId=110" },
        DevCommands: true, testMobFixture: "martial-player-controls.csv",
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(8, 423, "Data Node Goggles"), new(1, 422, "Handheld Data Node Locator") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "215", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "215" },
        readyMessages: new[]
        {
            $"CSR level {Level}, Locate Data Node rank 5, full prerequisites. Start ({X},{Y},{Z}) and test stand ({NearX},{Y},{Z}) are outdoor NAV floor 400. Type ?utilityobject 606 then {MoveNear}. Clear selection. Page 2 slot 1 Goggles (bag 8), slot 2 Handheld (bag 1).",
            "Turn on Goggles. [DATA-MINING] 215/423 on, node FX 28000462, IS 20->15. After the marker is visible press slot 1 again. Search and FX end immediately without another cast bar, IS debit or tactic refusal.",
            "Press Handheld. 215/422 on, node FX and 80 cb, IS 15->10. Its FX and search clear after ten seconds.",
            "Turn on Goggles again (10->5). Type ?qaunload 215 while the marker is present. Expect [MODIFIER] source=215 stat=703 remove +5, search off and marker gone. Try slot 1. Expect stock suppression or reason 7, and no cast, marker or IS change.",
            "Type ?qaload 215. The +5 record returns and search stays off. Activate Goggles (5->0) and, after the marker is visible, press slot 1 again. It must still stop without a cast, IS charge or tactic refusal. No [PVP] rounds or maximum [VITALS] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, NearX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor locator stand at ({x},{Y},{Z})");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(archive[Locate].Level == 5 && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Complete level-10 locator profile");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Locate, 703, 5, 0, false),
            "Locate supplies the shipped +5 rank record");
        actor.SendChat("?utilityobject 606");
        await actor.WaitForTextAsync("Utility fixture 606 placed.");
        actor.SendChat(MoveNear);
        await actor.WaitForTextAsync("Teleported to 4e2/12");

        await StartAsync(Goggles, 15);
        await EndAsync(Goggles);

        await StartAsync(Handheld, 10);
        await actor.WaitForRpcAsync("80 cb");
        await actor.WaitForRpcAsync(Off, TimeSpan.FromSeconds(15));

        await StartAsync(Goggles, 5);
        int unload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Locate }, load: false);
        await OffAfterAsync(unload);
        await StockAbilityProfile.ObserveAsync(actor, unload, frame => StockAbilityProfile.Bonus(frame, Locate, 703, 5, 0, true),
            "Unloading also removes +5 Locate Skill");
        await StarterHackerControlTargetMode.RefusedAsync(actor, Locate, WireFormat.FromHex(Goggles), AbilityRefusals.NotLoaded);

        int reload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Locate }, load: true);
        await StockAbilityProfile.ObserveAsync(actor, reload, frame => StockAbilityProfile.Bonus(frame, Locate, 703, 5, 0, false),
            "Reload restores the rank record");
        Check.That(!actor.World.RpcSnapshot().Skip(reload).Any(frame => frame.Bytes.AsSpan().SequenceEqual(WireFormat.FromHex(On))),
            "Reload does not reopen the search");
        await StartAsync(Goggles, 0);
        await EndAsync(Goggles);
        Check.Equal(0, actor.World.Problems.Count, "Locator lifecycle framing");
        return Array.Empty<byte>();

        async Task StartAsync(string request, ushort isAfter)
        {
            int views = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(request);
            await actor.WaitForRpcAsync(On);
            await actor.WaitForRpcAsync("80 cd 00 00 62 04 00 28");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(isAfter),
                $"Starting search costs five IS, leaving {isAfter}", HeadlessPlayer.DefaultTimeout);
        }

        async Task EndAsync(string request)
        {
            int rpcs = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await OffAfterAsync(rpcs);
            actor.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(actor, rpcs, frame => frame.Bytes is [0x81, 0x49, ..], "Locator switch-off barrier");
            Check.That(!actor.World.RpcSnapshot().Skip(rpcs).Any(frame => frame.Bytes is [0x80, 0xac or 0xba, ..]),
                "Stopping the search neither casts nor uses a refusal");
            Check.That(!actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(),
                "Stopping the search never spends IS");
        }

        async Task OffAfterAsync(int from)
        {
            await StockAbilityProfile.ObserveAsync(actor, from, frame => frame.Bytes.AsSpan().SequenceEqual(DataMiningPackets.Remove(0)),
                "The marker is removed");
            await StockAbilityProfile.ObserveAsync(actor, from, frame => frame.Bytes.AsSpan().SequenceEqual(WireFormat.FromHex(Off)),
                "The native search switch goes off");
        }
    }
}
