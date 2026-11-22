using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedDetectionBoundaryMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 2300;
    private const ushort Detect = 298, Locate = 215, Invisible = 1116;
    // PR413, 7c4a7a3b89b7, PacketLog lines 5215–5216. Keep the stock item slot and all trailing bytes.
    internal const string Countermeasure = "80 b9 2a 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
        "00 00 00 00 64 f1 1a 00 b0 30 89 3f 08 08 00 00 00 00 00";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Detect, Locate).Order().ToArray();
    private static readonly (string Handle, int Id, int X, int Z, int Org)[] Stands =
    {
        ("QAEnemy", 6114, X, Z + 1200, 2),
        ("QAOther", 6113, X, Z - 1200, 2),
        ("QAFar", 6112, X + 400, Z + 1132, 2),
        ("QAFriend", 6111, X + 500, Z, 1)
    };

    public override string Name => "ability-parity-awakened-detection-boundary";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=CASE WHEN ability_id={0x80000000L | ((long)Locate << 10)} THEN 5 ELSE LEAST(level,{Level}) END WHERE char_id=110",
        "DELETE FROM inventory WHERE charId=110 AND (slot IN (1,8) OR goid IN (422,2388))",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,2388,8,1,0),(110,422,1,1,0)"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},innerStrC=200,innerStrM=200 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET rotation={MobAggro.YawTowards(X,Z,Stands[0].X,Stands[0].Z)} WHERE charId=110"
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Org} WHERE charId={stand.Id}"))
        .Append("DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)")
        .ToArray(), DevCommands: true, testMobFixture: "martial-player-controls.csv",
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[]
        {
            new(8, 2388, "Concealment Countermeasure Tool"),
            new(1, 422, "Handheld Data Node Locator")
        },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "215,298,1116", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Stay at CSR (17000,495,2300), facing north. QAEnemy (17000,495,3500) and QAOther (17000,495,1100) are exactly 12 m ahead and behind. QAFar (17400,495,3432) is 12.006 m away; QAFriend (17500,495,2300) is allied and 5 m away. All five stands are outdoor NAV floor 400. No target selection.",
            "Use page 2 Countermeasure slot 1 and Handheld slot 2. To verify the stock bag route after clearing the desktop overlay, the same tools are in bag slots 8 and 1.",
            "Expect source 298 passive 204 +5 and 638 +72 in the owner bonus records. For each helper, send chat ?stealth invis and wait for [STEALTH] 1116 on. Press page 2 slot 1 (Countermeasure). Capture the stock 80 b9 2a 01 with bag slot 8, IS 200→195, 10 s own reuse, and [STEALTH] 1116 off (revealed by Detection) for QAEnemy AND QAOther. QAFar and QAFriend retain 1116. No maximum [VITALS] changes or [PVP] rounds.",
            "Without moving, type ?utilityobject 606 and press page 2 slot 2 (Handheld). Capture 80 b9 d7 00 with bag slot 1, [DATA-MINING] ability=215 tool=422 outcome=on, and IS 195→190. The marker appears and clears after 10 s. Neither remaining hidden helper has a reveal from 215."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var db = context.OwnedDatabase!;
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Detection needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Prepend((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Detection stand ({x},{Y},{z}) is on outdoor NAV");
        Check.Equal(2L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND count=1 AND " +
            "((slot=8 AND goid=2388) OR (slot=1 AND goid=422))"), "Both tool shortcuts have their exact stock bag slots");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && ranks.GetValueOrDefault(Detect) == Level &&
            ranks.GetValueOrDefault(Locate) == 5 && Loadout.All(id => ranks.ContainsKey(id) &&
                AbilityPrerequisites.Current.Closure(id).All(need => ranks.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The complete tool ability profile fits and arrives at valid ranks");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Detect, 204, 5, 0, false), "Detection Awareness +5");
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Detect, 638, 72, 0, false), "Detection Detect +72");

        var witnesses = new List<(HeadlessPlayer Player, int Cursor)>();
        foreach (var stand in Stands)
        {
            var helper = context.Helpers[stand.Handle];
            await actor.WaitForViewNamedAsync(stand.Handle);
            int helperAt = helper.World.RpcSnapshot().Count;
            helper.SendChat("?stealth invis");
            await StockAbilityProfile.ObserveAsync(helper, helperAt, frame => InvisibleState(frame, false), $"{helper.Handle} becomes invisible");
            witnesses.Add((helper, helperAt));
        }

        int rpcAt = actor.World.RpcSnapshot().Count, viewsAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(Countermeasure);
        foreach (var witness in witnesses.Take(2))
        {
            await StockAbilityProfile.ObserveAsync(witness.Player, witness.Cursor, frame => InvisibleState(frame, true),
                $"Detection reveals the 12 m hostile {witness.Player.Handle}");
            await witness.Player.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 9d 05 00 28 ??");
        }
        await EvidenceRetestWire.TimerAsync(actor, rpcAt, Detect, AbilityCooldowns.OwnTimer, 10);
        await WaitForIsAsync(viewsAt, 195);

        actor.SendChat("?utilityobject 606");
        await actor.WaitForTextAsync("Utility fixture 606 placed.");
        viewsAt = actor.World.ViewSnapshot().Count;
        int locatorAt = actor.World.RpcSnapshot().Count;
        actor.SkipReceived();
        actor.SendRpc(AwakenedDataNodeToolsMode.Handheld);
        await actor.WaitForRpcAsync("80 bd 05 d7 00 00 00 00 00 00 01");
        await actor.WaitForRpcAsync("80 cb");
        await WaitForIsAsync(viewsAt, 190);
        await actor.WaitForRpcAsync("80 bd 05 d7 00 00 00 00 00 00 00", TimeSpan.FromSeconds(15));
        await StockAbilityProfile.ObserveAsync(actor, locatorAt, packet => packet.Bytes.AsSpan().SequenceEqual(DataMiningPackets.Remove(0)),
            "Handheld clears its node marker on expiry");
        Check.That(witnesses.Skip(2).All(witness => !witness.Player.World.RpcSnapshot().Skip(witness.Cursor).Any(frame => InvisibleState(frame, true))),
            "Neither the out-of-area hostile nor the in-area ally is revealed");
        Check.Equal(2L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND ((slot=8 AND goid=2388) OR (slot=1 AND goid=422))"),
            "Both reusable tools remain in their slots");
        Check.Equal(0, actor.World.Problems.Count + witnesses.Sum(witness => witness.Player.World.Problems.Count), "Awakened tool framing");
        Console.WriteLine("AWAKENED TOOL PASS: 298 reveals both 12 m opponents, including behind the caster; 215 marker expires; 200 -> 195 -> 190 IS.");
        return Array.Empty<byte>();

        Task WaitForIsAsync(int cursor, ushort expected)
        {
            var update = BytePattern.Parse($"02 00 02 80 80 80 10 {EvidenceRetestWire.Le(expected)}");
            return actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(cursor).Any(update.Occurs),
                $"Own view publishes {expected} IS", HeadlessPlayer.DefaultTimeout);
        }
    }

    private static bool InvisibleState(RpcRecord packet, bool remove) =>
        ManageBonusRecord.TryDecode(packet.Bytes, out var record) && record!.SourceId == Invisible &&
        record.TargetId == 115 && record.AddRemoveType == (remove ? 1 : 0);
}
