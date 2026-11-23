using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class ShadowReadyPresentationMode : NativeQaMode
{
    private const ushort Parent = 290, Child = 1117, HardlineId = 49;
    private const int Level = 50, X = 7800, Y = 95, Z = 13795, Memory = 46;
    private const string OwnedRows = "SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id=110 ORDER BY slot";
    private static readonly ushort[] Loadout = ShadowReadyWireFixture.Parents;

    public override string Name => ShadowReadyWireFixture.Name;

    // The exact 175/290 prerequisite closure. The 1116 and 1117 grants are never seeded as owned abilities.
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,alignment=1,pvpflag=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110 AND (slot=40 OR goid=460); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
        }, DevCommands: true,
        environmentOverrides: new() { ["MXO_PACKET_LOG"] = "1", ["MXO_MOB_WANDER"] = "0", ["MXO_AGGRO"] = "0" },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "290", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "290",
            ["Parent"] = Parent, ["Child"] = Child, ["NoOwnedShadowSeed"] = true, ["ExpectedMemory"] = Memory
        }, readyMessages: new[]
        {
            $"Level {Level}, {Memory}/100 memory, CSR at Mara Northwest hardline ({X},{Y},{Z}). If needed, use the printed PICK command to return there. No NPC selection is needed.",
            "On the first ordinary world entry, open Actions > Abilities > Shadow. Shadow Upgrade should be listed exactly once before touching the loadout. Stop if it is absent. Drag the visible action to a free slot and press it once normally. Expect [ABILITY] 1117, [BUFF] 1117 on and a 20 IS debit as a grant usability check.",
            "Right-click the Mara Northwest hardline and choose Abilities. In the loaded abilities, unload Shadow (290) normally. Memory drops from 46 to 43. Reopen Actions and confirm Shadow Upgrade disappeared. Reload Shadow from the Archive through the same hardline. Memory returns to 46 and Actions shows one Shadow Upgrade again. Do not use a loadout chat command or upload any code.",
            "Relog normally with Shadow loaded. Confirm one Shadow Upgrade on the first Actions open after relog. Save the Archive changes, grant removal/add and the first-entry and relog Actions views. Only parent 290 is in scope."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        HardlineNetwork.Load(Path.Combine(root, "data", "hardline_network.csv"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
            "The Shadow Archive stand is on outdoor NAV by the Mara Northwest hardline");
        var booth = HardlineNetwork.Get(1, HardlineId) ?? throw new InvalidOperationException("Mara Northwest hardline 49 is missing.");
        Check.That(Math.Abs(booth.x - X) < 400 && Math.Abs(booth.z - Z) < 400,
            "Mara Northwest hardline 49 is within interaction distance");
        byte yaw = MobAggro.YawTowards(X, Z, booth.x, booth.z);
        Console.WriteLine($"PICK ?tp {X} {Y} {Z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {yaw:x2} (Mara Northwest hardline)");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var db = context.OwnedDatabase!;
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(loaded.Keys.ToHashSet().SetEquals(Loadout),
            $"Stock Margin loads exactly the fixture rows: {string.Join(',', loaded.Keys.Order())}");
        Check.Equal(Memory, loaded.Keys.Sum(AbilityMemory.Cost), "The fixture uses 46/100 memory");
        foreach (ushort id in Loadout)
        foreach (var need in AbilityPrerequisites.Current.Closure(id))
            Check.That(loaded.TryGetValue(need.Key, out var row) && row.Level >= need.Value,
                $"{id} has loaded prerequisite {need.Key} at rank {need.Value} or higher");
        Check.That(!archive.ContainsKey(1116) && !archive.ContainsKey(Child), "Stock Margin has no owned Invisibility or Shadow Upgrade");
        string originalRows = await db.ExecuteAsync(OwnedRows);
        await CheckReadyAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { X, Y, Z }), "CSR enters at the verified Mara Northwest stand");

        int unload = await ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: false);
        var losesChild = GrantRecord(Child, remove: true);
        await ObserveAsync(actor, unload, losesChild, "The ordinary 290 unload withdraws the child base");
        await BarrierAsync(actor);
        var removed = actor.World.RpcSnapshot().Skip(unload).ToArray();
        int off = Array.FindIndex(removed, frame => ShadowReadyWireFixture.Member(frame.Bytes, off: true));
        int childUnload = Array.FindIndex(removed, losesChild.Invoke);
        Check.That(off >= 0 && childUnload > off, "Shadow's Type45 OFF precedes the one child unload");
        Check.That(removed.Count(losesChild) == 1 &&
            removed.Count(frame => ShadowReadyWireFixture.AnyMember(frame.Bytes)) == 1, "Exactly one child unload and membership OFF");
        Check.Equal(0L, await db.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id=110 AND slot={archive[Parent].Slot}"),
            "The ordinary request commits the owned parent row unloaded");

        int reload = await ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: true);
        await CheckAdditionAsync(actor, reload);
        Check.Equal(originalRows, await db.ExecuteAsync(OwnedRows), "The parent's original rows, slots and training return without owning the child");

        actor.SendRpc("80 fe 00");
        await actor.WaitForRpcAsync("80 fd");
        actor.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(33));
        using var relog = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        await CheckReadyAsync(relog);
        Check.Equal(originalRows, await db.ExecuteAsync(OwnedRows), "World reentry retains the same owned Archive rows");
        Check.Equal(0, actor.World.Problems.Count + relog.World.Problems.Count, "Both stock grant sessions decode cleanly");
        Console.WriteLine("SHADOW PASS: initial/relog late grant, duplicate-ready idempotence, exact ordinary Archive OFF/restore and unchanged owned rows.");
        return Array.Empty<byte>();
    }

    private static async Task CheckReadyAsync(HeadlessPlayer actor)
    {
        await actor.EnterWorldAsync(loadingComplete: false);
        await BarrierAsync(actor);
        var before = actor.World.RpcSnapshot();
        Check.That(!before.Any(frame => ShadowReadyWireFixture.ChildBase(frame.Bytes) || ShadowReadyWireFixture.AnyMember(frame.Bytes)),
            "Shadow's unowned action is held until the stock world-ready request");
        actor.SendRpc("0c 01");
        await CheckAdditionAsync(actor, before.Count);
        int duplicate = actor.World.RpcSnapshot().Count;
        actor.SendRpc("0c 01");
        await BarrierAsync(actor);
        Check.That(!actor.World.RpcSnapshot().Skip(duplicate).Any(frame =>
            ShadowReadyWireFixture.ChildBase(frame.Bytes) || ShadowReadyWireFixture.AnyMember(frame.Bytes)),
            "The same ready request does not duplicate the grant");
    }

    private static async Task CheckAdditionAsync(HeadlessPlayer actor, int cursor)
    {
        await ObserveAsync(actor, cursor, frame => ShadowReadyWireFixture.Member(frame.Bytes), "The parent grants Shadow Upgrade");
        await BarrierAsync(actor);
        var frames = actor.World.RpcSnapshot().Skip(cursor).ToArray();
        int loaded = Array.FindIndex(frames, frame => ShadowReadyWireFixture.Base(frame.Bytes));
        int member = Array.FindIndex(frames, frame => ShadowReadyWireFixture.Member(frame.Bytes));
        Check.That(loaded >= 0 && member > loaded && frames.Count(frame => ShadowReadyWireFixture.ChildBase(frame.Bytes)) == 1 &&
            frames.Count(frame => ShadowReadyWireFixture.AnyMember(frame.Bytes)) == 1,
            "One zero-rank hidden base precedes one 290 to 1117 membership ADD");
    }

    private static async Task BarrierAsync(HeadlessPlayer actor)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc("81 48");
        await ObserveAsync(actor, cursor, frame => frame.Bytes is [0x81, 0x49, ..], "Current-session feature response");
    }
}
