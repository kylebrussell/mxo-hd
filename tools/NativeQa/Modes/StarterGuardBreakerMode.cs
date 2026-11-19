using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class StarterGuardBreakerMode : NativeQaMode
{
    private const ushort GuardBreaker = 138;
    private const ushort KungFu = 133;

    public override string Name => "ability-parity-starter-guard-breaker";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, KungFu, GuardBreaker }, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=19,exp={PlayerProgression.ExperienceForLevel(19)},y=495," +
                "healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=15 WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        readyMessages: new[]
        {
            "Select Melee Dummy about 3 m west and enter ordinary unarmed Close Combat. Reselect Kung Fu on the hotbar while interlocked.",
            "Keep the live dummy selected. In chat enter ?combatdepth offbalance 60 and confirm the reply says OffBalance fixture applied 885/200 to Melee Dummy for 60 seconds. This prepares only the Off Balance prerequisite.",
            "With the Guard Breaker slot enabled, click it and capture the normal outgoing cast and a landed round. If the round misses, retry when the slot is ready. Expect 40 physical in this fixture and a -10 target melee-defense penalty for 15 seconds."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "138", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster)
            return Array.Empty<byte>();

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        await caster.EnterWorldAsync();
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await StarterInterlockDamageMode.Open(caster, dummy);
        await StarterInterlockDamageMode.Select(caster, dummy, KungFu);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(dummy.ViewId))} {dummy.SpawnId:x2}");
        int viewStart = caster.World.ViewSnapshot().Count;
        caster.SendChat("?combatdepth offbalance 60");
        await caster.WaitForTextAsync("OffBalance fixture applied 885/200 to Melee Dummy for 60 seconds");
        await StarterInterlockDamageMode.AwaitOffBalance(caster, dummy, viewStart);

        await StarterInterlockDamageMode.Land(caster, dummy, GuardBreaker, server, 40, "KarateAbility GuardBreaker", pvp: false);
        Check.That(server.DebugLog().Contains("[EFFECTS] 138 on Melee Dummy: Stat 1009 -10 15 s", StringComparison.Ordinal),
            "Guard Breaker applies its ordinary target-only defense penalty");
        Console.WriteLine("  Target view confirmed Off Balance 04 before Guard Breaker landed for 40");
        return Array.Empty<byte>();
    }
}
