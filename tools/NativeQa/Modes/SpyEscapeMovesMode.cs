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

    public override string Name => "ability-parity-spy-escapes";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Level 12, rank 12, unarmed with Melee Dummy (3 m west). Enter ordinary unarmed Close Combat on Melee Dummy, reselect Spy Style (1112), and use Side Roll Escape (121). Expect 15 IS, 12.0 s shared family 1200 (aaEscapeMoveAbility) reuse (no 0 s own timer), 0 damage, caster Stat 520 (Combat Evasion) +20 for 30 s ('[BUFF] 121 on for 30 s'), and leaving close combat (RPC 46 / '[SPECIAL] Side Roll Escape').",
            "After the 12 s family reuse expires, re-enter Close Combat on Melee Dummy, reselect Spy Style (1112), and use Cartwheel Escape (292). Expect 15 IS, 30.0 s own reuse and 12.0 s shared family 1200 reuse, 0 damage, target Confuse ('[EFFECTS] 292 on Melee Dummy: State 91 0 4 s'), caster Stat 520 +25 for 30 s ('[BUFF] 292 on for 30 s'), and leaving close combat (RPC 46 / '[SPECIAL] Cartwheel Escape')."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "121,292", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-12 memory");
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));

        await StarterInterlockDamageMode.Open(caster, dummy);
        await StarterInterlockDamageMode.Select(caster, dummy, SpyStyle);

        int rpcCursor = caster.World.RpcSnapshot().Count;
        int logStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(SideRollEscape, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitForRpcAsync($"46 {handle} 00");
        byte[] familyTimer = AbilityCooldowns.BuildTimer(1200, AbilityCooldowns.GroupTimer, 12, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpcCursor).Any(r => r.Bytes.AsSpan().SequenceEqual(familyTimer)),
            "Side Roll Escape starts 12 s family 1200 reuse");
        Check.That(server.DebugLog()[logStart..].Contains("[BUFF] 121 on for 30 s", StringComparison.Ordinal),
            "Side Roll Escape applies caster +20 Combat Evasion for 30 s");

        await Task.Delay(TimeSpan.FromSeconds(13));
        await StarterInterlockDamageMode.Open(caster, dummy);
        await StarterInterlockDamageMode.Select(caster, dummy, SpyStyle);

        int cartStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(CartwheelEscape, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitForRpcAsync($"46 {handle} 00");
        string cartLog = server.DebugLog()[cartStart..];
        Check.That(cartLog.Contains("[EFFECTS] 292 on Melee Dummy: State 91 0 4 s", StringComparison.Ordinal),
            "Cartwheel Escape confuses target for 4 s");
        Check.That(cartLog.Contains("[BUFF] 292 on for 30 s", StringComparison.Ordinal),
            "Cartwheel Escape applies caster +25 Combat Evasion for 30 s");
        Console.WriteLine("  Side Roll Escape (121) and Cartwheel Escape (292) both landed, buffed caster, started family 1200 reuse, and left close combat");
        return Array.Empty<byte>();
    }
}
