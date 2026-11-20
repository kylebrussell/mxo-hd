using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class StarterLowBlowMode : NativeQaMode
{
    private const byte Level = 19;
    private const ushort LowBlow = 605;
    private const ushort CoderStyle = 1008;

    public override string Name => "ability-parity-starter-low-blow";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, CoderStyle, LowBlow }, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Select Melee Dummy about 3 m west and open ordinary unarmed Close Combat. Select Coder Style while interlocked and keep the dummy selected.",
            "Use Low Blow from Actions or its hotbar slot. Capture the outgoing request, 30 IS, 6 s reuse, and a landed LowBlow exchange. At level 19 this fixture expects 19 landed physical damage from the stored base 17 and Coder Style’s +12% influence. Retry after reuse if the round misses."
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "605", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Low Blow server.");
        await caster.EnterWorldAsync();
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        await StarterInterlockDamageMode.Open(caster, dummy);
        await StarterInterlockDamageMode.Select(caster, dummy, CoderStyle);
        int cursor = caster.World.RpcSnapshot().Count;
        await StarterInterlockDamageMode.Land(caster, dummy, LowBlow, server, 19, "LowBlow", pvp: false);
        await EvidenceRetestWire.TimerAsync(caster, cursor, LowBlow, 4, 6);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 10 ca 03");
        Check.Equal(0, caster.World.Problems.Count, "Low Blow packet framing");
        return Array.Empty<byte>();
    }
}
