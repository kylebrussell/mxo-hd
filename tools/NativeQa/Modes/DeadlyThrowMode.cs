using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DeadlyThrowMode : NativeQaMode
{
    private const ushort DeadlyThrow = 279, SpyStyle = 1112;
    private const byte Level = 20, Rank = 20;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, DeadlyThrow };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-deadly-throw";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_CONDITIONAL_THROW_QA"] = "1" },
        readyMessages: new[]
        {
            "Level 20, rank 20, Spy Style (1112) active, unarmed, outside interlock. Clear target selection and try Deadly Throw (279): expect one orange 'Select a living enemy before using this ability.' notice, no IS spent, no cast bar and no reuse.",
            "Select Melee Dummy (3 m west) and cast Deadly Throw (279). Expect 18 IS spent (1000 -> 982), 2.0 s cast bar (80 ac), landed thrown damage 37 (ValueFunction 3 base 30 at level 20 + 24% from rank-20 Spy Style 1112 +14%, Wicked Aim 278 +6% and prerequisite Weak Points 164 +4%), '[EFFECTS] 279 on Melee Dummy' with Stat 877 -85 / Stat 519 -15 / Stat 878 -72 / Stat 115 -57 for 15 s (plus optional 20% Dazed/Staggered 8 s procs), 10.0 s own reuse and 3.0 s shared family 1216 reuse.",
            "Optional conditional check after 10 s reuse: with Melee Dummy selected, run '?combatdepth offbalance 30' and cast 279 again to also capture the separate Off-Balance bonus hit of 38 (raw 31 + 24%)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "279", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-20 memory");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);

        int refuseStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(DeadlyThrow, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[refuseStart..].Contains("[ABILITY] Denied 279: Select a living enemy", StringComparison.Ordinal),
            "Deadly Throw rejects empty target selection", TimeSpan.FromSeconds(5));

        int castStart = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(DeadlyThrow, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 279 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Deadly Throw lands on Melee Dummy", TimeSpan.FromSeconds(10));

        string log = server.DebugLog()[castStart..];
        Check.Equal(37, int.Parse(landed.Match(log).Groups[1].Value), "Deadly Throw lands for 37 at level 20 (base 30 + 24% Spy Style/Wicked Aim/Weak Points)");
        Check.That(log.Contains("[EFFECTS] 279 on Melee Dummy:", StringComparison.Ordinal) &&
            log.Contains("Stat 877 -85 15 s", StringComparison.Ordinal) &&
            log.Contains("Stat 519 -15 15 s", StringComparison.Ordinal) &&
            log.Contains("Stat 878 -72 15 s", StringComparison.Ordinal) &&
            log.Contains("Stat 115 -57 15 s", StringComparison.Ordinal),
            "Deadly Throw retains all four 15 s numeric debuffs");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(DeadlyThrow, AbilityCooldowns.OwnTimer, 10, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpcCursor).Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)),
            "Deadly Throw starts 10 s own reuse");
        Console.WriteLine("  Deadly Throw refused empty target, then landed for 36 thrown damage with all four 15 s debuffs");
        return Array.Empty<byte>();
    }
}
