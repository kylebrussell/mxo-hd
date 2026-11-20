using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterCoderInventoryLifecycleMode : NativeQaMode
{
    private const ushort Decompile = 276, Compile = CraftingSession.Ability, Write = CraftingSession.WriterAbility;
    private const byte CompilerSlot = 1, WriterSlot = 2, PillSlot = 5, RevolverSlot = 6;
    private const uint Pill = 2399, Revolver = 700;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 37, Decompile, 277).Order().ToArray();

    // The 7.6005 constructor at 621d3b00 takes ability ID, bag slot, tool slot and the recipe-known hint.
    // Retain the inventory-target request used by the native-crafting-requests replay.
    private const string UseOnPill = "81 31 14 01 00 00 05 6b 00";
    private const string UseOnRevolver = "81 31 14 01 00 00 06 6b 00";
    private const string ReuseStart = "80 bd 01 14 01 04 00 00 20 41 01";
    private const string ReuseEnd = "80 bd 01 14 01 04 00 00 20 41 00";

    public override string Name => "ability-parity-starter-coder-inventory-lifecycle";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, 24751, -1498, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{CraftingSession.Tool},{CompilerSlot},1,0),"
                + $"(110,{CraftingSession.WriterTool},{WriterSlot},1,0),(110,{Pill},{PillSlot},1,0),(110,{Revolver},{RevolverSlot},1,0)",
            "CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY(charId,goid))",
            $"DELETE FROM char_recipes WHERE charId=110 AND goid IN ({Pill},{Revolver})"
        }, Array.Empty<string>(), DevCommands: true, hotbarAbilities: new[] { Decompile },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "276,969,970", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Use Activate Item on the Code Analyser in bag slot 2. Verify Writer opens and the avatar begins typing. Close the window and verify the active record clears and the avatar stands.",
            "Repeat with the Code Compiler in bag slot 1. Opening and closing both tools should leave the bag and Info unchanged.",
            "Use Decompile Items (page 2 slot 1) on the Health Pill in bag slot 5. Check 81 31, the 10 s reuse timer, 81 30 and the bag slot clearing.",
            "Try to decompile the revolver in bag slot 6 before reuse ends. It should remain in the bag. After the timer clears, decompile the revolver and verify that recipe is learned."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("The Coder fixture requires an owned database.");
        Check.Equal(4L, await Items(), "The fixture starts with both tools and two decompile inputs");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        long startingInfo = await Info();

        await CheckToolAsync(Write, active => Scenarios.WriterTool(actor, WriterSlot, active));
        await CheckToolAsync(Compile, active => Scenarios.CompilerTool(actor, CompilerSlot, active));
        Check.Equal(4L, await Items(), "Opening or closing either tool leaves the bag intact");
        Check.Equal(startingInfo, await Info(), "Opening and closing either tool is free");

        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UseOnPill);
        await Observe(cursor, ReuseStart, "Decompile starts exactly one 10-second reuse");
        await Observe(cursor, "81 30 5f 09 00 00 01", "The Health Pill recipe is returned");
        await Observe(cursor, "81 2f 01 05 5f 09 00 00", "The first item is decompiled");
        Check.Equal(1L, await Learned(Pill), "The Health Pill recipe persists");

        int repeated = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UseOnRevolver);
        await Observe(repeated, "81 2f 00 06 bc 02 00 00", "An early repeat receives a result without spending the input");
        Check.Equal(0L, await Learned(Revolver), "The early repeat did not learn the second recipe");
        Check.Equal(3L, await Items(), "The early repeat leaves the revolver in the bag");
        Check.Equal(1, actor.World.RpcSnapshot().Skip(cursor).Count(frame => BytePattern.Parse(ReuseStart).IsMatch(frame.Bytes)),
            "The early repeat does not restart the timer");

        await Observe(cursor, ReuseEnd, "Decompile reuse ends");
        int finished = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UseOnRevolver);
        await Observe(finished, "81 2f 01 06 bc 02 00 00", "The same request succeeds after reuse");
        Check.Equal(1L, await Learned(Revolver), "The revolver recipe persists");
        Check.Equal(2L, await Items(), "Only the two tools remain");
        Check.Equal(startingInfo, await Info(), "Decompile leaves Info unchanged");
        Check.Equal(0, actor.World.Problems.Count, "Starter Coder replay framing");
        return Array.Empty<byte>();

        async Task CheckToolAsync(ushort ability, Action<bool> send)
        {
            foreach (bool active in new[] { true, false })
            {
                int rpc = actor.World.RpcSnapshot().Count;
                int views = actor.World.ViewSnapshot().Count;
                send(active);
                await StockAbilityProfile.ObserveAsync(actor, rpc, frame =>
                    ManageBonusRecord.TryDecode(frame.Bytes, out var bonus) && bonus!.SourceId == ability &&
                    bonus.Type == AbilityBuffs.KindLinked && bonus.TargetId == CraftingSession.Attribute &&
                    bonus.AddRemoveType == (active ? 0 : 1), $"Tool {ability} publishes its linked record (active={active})");
                byte[] exclusive = AbilityBuffs.BuildExclusive(ability, active);
                await StockAbilityProfile.ObserveAsync(actor, rpc, frame => frame.Bytes.AsSpan().SequenceEqual(exclusive),
                    $"Tool {ability} publishes its exclusive state (active={active})");
                var stance = BytePattern.Parse(active ? "02 00 01 28 ?? 40 03 00 00 10" : "02 00 01 28 ?? 40 00 00 00 10");
                await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).Any(stance.Occurs),
                    $"Tool {ability} updates the owner's stance (active={active})", HeadlessPlayer.DefaultTimeout);
            }
        }

        Task<RpcRecord> Observe(int start, string hex, string description) =>
            StockAbilityProfile.ObserveAsync(actor, start, frame => BytePattern.Parse(hex).IsMatch(frame.Bytes), description);
        Task<long> Items() => db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96");
        Task<long> Info() => db.LongAsync("SELECT cash FROM characters WHERE charId=110");
        Task<long> Learned(uint goid) => db.LongAsync($"SELECT COUNT(*) FROM char_recipes WHERE charId=110 AND goid={goid}");
    }
}
