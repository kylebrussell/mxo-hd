using System.Diagnostics;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterCraftingGrantLifetimeMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400, StartingInfo = 30000;
    private const ushort Write = 37, Compile = 277, WriterGrant = 970, CompilerGrant = 969;
    private const uint LongItem = 39413, ShortItem = 2399;
    // PR640e ordinary bag activations and the two witnessed close requests.
    private const string WriterOpen = "80b9ca03000000000000000000000000000000000000000000000000000000000000000008020100000000";
    private const string WriterClose = "80b9ca03000000000000000000000000000000000000000000000000e025b502b00c000005020000000000";
    private const string CompilerOpen = "80b9c903000000000000000000000000000000000000000000000000000000000000000005010100000000";
    private const string CompilerClose = "80b9c903000000000000000000000000000000000000000000000000000000000000006205010000000000";
    // PR479 Compile60 with the fixture's archive reference and the same Compiler bag slot 1.
    private const string CompileLong = "60 f5 99 00 00 90 01 00 01 01";
    private const string CompileShort = "60 5f 09 00 00 91 01 00 01 01";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Write, Compile).Order().ToArray();

    public override string Name => "ability-parity-starter-crafting-grant-lifetime";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,1271,1,1,0),(110,531,2,1,0)",
        $"DELETE FROM char_abilities WHERE char_id=110 AND (slot IN (400,401) OR ability_id IN ({LongItem << 7},{ShortItem << 7}))",
        $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"(110,400,{LongItem << 7},1,0),(110,401,{ShortItem << 7},1,0)",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110 AND ability_id >= 2147483648"
    }, new[]
    {
        $"UPDATE characters SET y={Y},cash={StartingInfo},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId=110"
    }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Write, Compile },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "37,277,969,970", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Level 18, CSR (17000,495,6400) on outdoor NAV. Move the game clear of the desktop overlay before using Inventory. Code Compiler bag 1, Analyser bag 2. Two different pill codes, one charge each. The bag starts with neither pill.",
            "Activate the Analyser. Confirm 970/968 on and typing. Focus chat and enter ?qaunload 37 with the window still open. Server should send grant 970 unload, 968 removal and standing. Record if the window stays drawn. Close a residual window before ?qaload 37. Loading restores the grant only. Open the Analyser again from the bag, then close it normally.",
            "Activate the Compiler and start Max Health Boost Pill 3.0, the 37.5 s cast. While the timer is running enter ?qaunload 277. Expect 969 grant unload, 968 removal, standing and 61 01 cancellation. After the original deadline, the Boost code still has one charge, no pill and Info 30,000. If the UI blocks chat, capture that separately without substituting a close.",
            "Close any residual window and ?qaload 277. Activate the Compiler and compile Health Pill 1.0, the other code. Expect the fresh 3 s job, 61 00, one pill and one code charge consumed. This checks the cancelled session releases its cast authority for the next ordinary compile."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("The crafting fixture requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
            "Crafting starts on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var db = context.OwnedDatabase!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(req => archive.TryGetValue(req.Key, out var row) && row.Level >= req.Value)),
            "Crafting parents and all their prerequisites fit level 18");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await ToolAsync(WriterGrant, WriterOpen, true);
        int lost = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Write }, false);
        await GrantAsync(lost, WriterGrant, true);
        await StateAsync(lost, WriterGrant, false);
        lost = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Write }, true);
        await GrantAsync(lost, WriterGrant, false);
        Check.That(!actor.World.RpcSnapshot().Skip(lost).Any(frame =>
            frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(WriterGrant, true))),
            "Reloading Write Code does not open a session");
        await ToolAsync(WriterGrant, WriterOpen, true);
        await ToolAsync(WriterGrant, WriterClose, false);

        await ToolAsync(CompilerGrant, CompilerOpen, true);
        int pendingAt = actor.World.RpcSnapshot().Count, logAt = server.DebugLog().Length;
        var deadline = Stopwatch.StartNew();
        actor.SendRpc(CompileLong);
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[COMPILE60] Started 39413 seconds 37.5 "),
            "Long compile has actually started", HeadlessPlayer.DefaultTimeout);
        lost = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Compile }, false);
        await StateAsync(lost, CompilerGrant, false);
        await GrantAsync(lost, CompilerGrant, true);
        await StockAbilityProfile.ObserveAsync(actor, pendingAt, frame => BytePattern.Parse("61 01").IsMatch(frame.Bytes), "The pending compile is cancelled");
        Check.Equal(1L, await CountCodeAsync(LongItem), "Cancelled Boost code keeps the charge");
        Check.Equal(0L, await CountBagAsync(LongItem), "Cancelled Boost has no result");
        Check.Equal((long)StartingInfo, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"), "Cancellation charges no Info");

        lost = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Compile }, true);
        await GrantAsync(lost, CompilerGrant, false);
        await ToolAsync(CompilerGrant, CompilerOpen, true);
        int nextAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(CompileShort);
        await StockAbilityProfile.ObserveAsync(actor, nextAt, frame => BytePattern.Parse("61 00").IsMatch(frame.Bytes), "The new short compile succeeds");
        Check.Equal(0L, await CountCodeAsync(ShortItem), "New compile consumes its own sole charge");
        Check.Equal(1L, await CountBagAsync(ShortItem), "A single Health Pill materializes");
        TimeSpan untilOldDeadline = TimeSpan.FromSeconds(38.5) - deadline.Elapsed;
        if (untilOldDeadline > TimeSpan.Zero) await Task.Delay(untilOldDeadline);
        Check.Equal(1L, await CountCodeAsync(LongItem), "The entire original deadline passes with the cancelled code intact");
        Check.Equal(0L, await CountBagAsync(LongItem), "The cancelled job does not materialize a delayed Boost");
        Check.Equal(29992L, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"), "Only the fresh short job spends 8 Info");
        Check.That(!server.DebugLog()[logAt..].Contains("[COMPILE60] Finished 39413"), "No stale completion terminates the cancelled job");
        await ToolAsync(CompilerGrant, CompilerClose, false);
        Check.Equal(0, actor.World.Problems.Count, "Crafting lifetime packet framing");
        return Array.Empty<byte>();

        Task<long> CountCodeAsync(uint item) => db.LongAsync($"SELECT COALESCE(SUM(level),0) FROM char_abilities WHERE char_id=110 AND ability_id={item << 7}");
        Task<long> CountBagAsync(uint item) => db.LongAsync($"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId=110 AND goid={item}");
        Task<RpcRecord> GrantAsync(int start, ushort id, bool remove) => StockAbilityProfile.ObserveAsync(actor, start, StockAbilityProfile.GrantRecord(id, remove), $"Grant {id}, remove={remove}");
        async Task StateAsync(int start, ushort id, bool active)
        {
            byte[] expected = AbilityBuffs.BuildExclusive(id, active);
            await StockAbilityProfile.ObserveAsync(actor, start, frame => frame.Bytes.AsSpan().SequenceEqual(expected), $"Crafting tool {id} active={active}");
            await StockAbilityProfile.ObserveAsync(actor, start, frame =>
                ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == id &&
                record.Type == AbilityBuffs.KindLinked && record.TargetId == CraftingSession.Attribute &&
                record.AddRemoveType == (active ? 0 : 1), $"Tool {id} linked stance record, active={active}");
        }

        async Task ToolAsync(ushort id, string request, bool active)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(request);
            await StateAsync(start, id, active);
        }
    }
}
