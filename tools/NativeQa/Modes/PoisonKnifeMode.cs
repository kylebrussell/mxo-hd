using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class PoisonKnifeMode : NativeQaMode
{
    private const ushort PoisonKnife = 146, SpyStyle = 1112;
    private const byte Level = 4, Rank = 4;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, PoisonKnife };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-poison-knife";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=1000,healthM=1000,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Level 4, rank 4, Spy Style (1112) active, unarmed, outside interlock. First clear selection and try Poison Knife (146): expect native refusal 24 (one yellow 'Target is not valid.'), no IS spent, no cast bar and no reuse.",
            "Select Melee Dummy (3 m west) and cast Poison Knife (146) outside interlock. Expect 15 IS spent (500 -> 485), 2.0 s cast bar (80 ac), 0 direct hit ('[ABILITY] 146 lands on view 4 for 0'), ThrownDOT applied ('[EFFECTS] 146 on Melee Dummy: Dot 1157 18 25 s', base 17 + 6% rank-4 Spy Style = 18), ticks every 2.5 s for 2 damage ('[EFFECTS] 146 ticks on Melee Dummy for 2', 9 ticks total), 20.0 s own reuse and 3.0 s shared family 1216 reuse."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "146,281", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-4 memory");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);

        int refuseStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(PoisonKnife, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[refuseStart..].Contains("[ABILITY] Denied 146: native refusal 24", StringComparison.Ordinal),
            "Poison Knife rejects empty target with native refusal 24", TimeSpan.FromSeconds(5));

        int castStart = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(PoisonKnife, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[castStart..].Contains("[EFFECTS] 146 ticks on Melee Dummy for 2", StringComparison.Ordinal),
            "Poison Knife applies ThrownDOT and delivers its first 2-damage tick", TimeSpan.FromSeconds(10));

        string log = server.DebugLog()[castStart..];
        Check.That(log.Contains("[EFFECTS] 146 on Melee Dummy: Dot 1157 18 25 s", StringComparison.Ordinal),
            "Poison Knife applies scaled 18-total 25 s ThrownDOT");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(PoisonKnife, AbilityCooldowns.OwnTimer, 20, true);
        byte[] groupTimer = AbilityCooldowns.BuildTimer(1216, AbilityCooldowns.GroupTimer, 3, true);
        var rpcs = caster.World.RpcSnapshot().Skip(rpcCursor).ToArray();
        Check.That(rpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)), "Poison Knife starts 20 s own reuse");
        Check.That(rpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(groupTimer)), "Poison Knife starts 3 s shared family 1216 reuse");
        Console.WriteLine("  Poison Knife refused empty target (native 24), landed 18-total 25 s ThrownDOT and ticked for 2");
        return Array.Empty<byte>();
    }
}
