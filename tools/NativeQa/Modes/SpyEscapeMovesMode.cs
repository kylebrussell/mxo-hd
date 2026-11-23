using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class SpyEscapeMovesMode : NativeQaMode
{
    private const ushort SideRollEscape = 121, CartwheelEscape = 292, SpyStyle = 1112;
    private const byte Level = 12, Rank = 12;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, SideRollEscape, CartwheelEscape };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    // PR423 stock style and escapes, PR517 ordinary interlock opener.
    internal const string StockOpen = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";
    internal const string StockStyle = "80b958040400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    internal const string StockSideRoll = "80b9790004000200000000000000000000000000000000000000000079000000006b0000086b0000000000";
    internal const string StockCartwheel = "80b9240104000200000000000000000000000000000000000000000024010000006b0000086b0000000000";

    public override string Name => "ability-parity-spy-escapes";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new[] { SpyStyle, SideRollEscape, CartwheelEscape },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.ResetMode] = "1" },
        readyMessages: new[]
        {
            "Level 12, rank 12. Try both escape shortcuts with Melee Dummy selected before opening combat. Capture any client suppression or target refusal. Then open ordinary unarmed Close Combat, reselect Spy Style (page 2 slot 1) and use Side Roll Escape (slot 2). Expect 15 IS, 12.0 s shared family 1200 (aaEscapeMoveAbility) reuse (no 0 s own timer), 0 damage, caster Stat 520 (Combat Evasion) +20 for 30 s ('[BUFF] 121 on for 30 s'), and leaving close combat (RPC 46 / '[SPECIAL] Side Roll Escape').",
            "After the escape use ?combatdepth fixture-reset, strafe slightly, reselect the new dummy and wait for 12 s family reuse to clear. Enter submits the command. Keep selection without Escape. Re-enter Close Combat, reselect Spy Style (slot 1), and use Cartwheel Escape (slot 3). Expect 15 IS, 30.0 s own reuse and 12.0 s shared family 1200 reuse, 0 damage, target Confuse ('[EFFECTS] 292 on Melee Dummy: State 91 0 4 s'), caster Stat 520 +25 for 30 s ('[BUFF] 292 on for 30 s'), and leaving close combat (RPC 46 / '[SPECIAL] Cartwheel Escape')."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1112,121,292", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-12 memory");
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        await RefuseAsync(SideRollEscape, StockSideRoll, Handle(dummy));
        await RefuseAsync(CartwheelEscape, StockCartwheel, Handle(dummy));

        await OpenAsync();
        await RefuseAsync(SideRollEscape, StockSideRoll, Handle(dummy) ^ 0x10000);
        int before = await EscapeAsync(SideRollEscape, StockSideRoll, 20);
        var ownSideRoll = BytePattern.Parse("80 bd 01 79 00");
        Check.That(!caster.World.RpcSnapshot().Skip(before).Any(frame => ownSideRoll.IsMatch(frame.Bytes)),
            "Side Roll uses only the shared cooldown");

        int creationAt = caster.World.CreationSnapshot().Count;
        var old = dummy;
        caster.SendChat("?combatdepth fixture-reset");
        await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(creationAt).Any(view => view.Name == old.Name),
            "A fresh hostile dummy is visible after reset", HeadlessPlayer.DefaultTimeout);
        dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        Check.That(Handle(dummy) != Handle(old), "Reset retires the prior view");
        byte[] familyOff = AbilityCooldowns.BuildTimer(1200, AbilityCooldowns.GroupTimer, 12, false);
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(before).Any(frame => frame.Bytes.AsSpan().SequenceEqual(familyOff)),
            "The real shared cooldown expires before the second escape", TimeSpan.FromSeconds(15));
        await OpenAsync();
        await RefuseAsync(CartwheelEscape, StockCartwheel, Handle(old));
        int cartLog = server.DebugLog().Length;
        int cartAt = await EscapeAsync(CartwheelEscape, StockCartwheel, 25);
        await EvidenceRetestWire.TimerAsync(caster, cartAt, CartwheelEscape, AbilityCooldowns.OwnTimer, 30);
        Check.That(server.DebugLog()[cartLog..].Contains("[EFFECTS] 292 on Melee Dummy: State 91 0 4 s", StringComparison.Ordinal),
            "Cartwheel applies its confusion to the current opponent");
        Check.Equal(0, caster.World.Problems.Count, "Spy escape replay framing");
        return Array.Empty<byte>();

        async Task OpenAsync()
        {
            uint handle = Handle(dummy);
            string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
            caster.SkipReceived();
            caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockOpen, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
            await caster.WaitForRpcAsync($"40 {hex} 00 6b");
            await caster.WaitForRpcAsync("47");
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(StockStyle, handle));
            await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        }

        Task<RpcRecord> RefuseAsync(ushort id, string capture, uint handle) =>
            StarterHackerControlTargetMode.RefusedAsync(caster, id, StarterHackerControlTargetMode.WithHandle(capture, handle));

        async Task<int> EscapeAsync(ushort id, string capture, int evasion)
        {
            uint handle = Handle(dummy);
            string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(capture, handle));
            await caster.WaitForRpcAsync($"46 {hex} 00");
            await EvidenceRetestWire.TimerAsync(caster, cursor, 1200, AbilityCooldowns.GroupTimer, 12);
            await StockAbilityProfile.ObserveAsync(caster, cursor, frame => StockAbilityProfile.Bonus(frame, id, 520, evasion, 30, false),
                $"{id} adds caster Combat Evasion +{evasion}");
            return cursor;
        }
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
