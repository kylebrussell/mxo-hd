using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class MartialArtsStarterAdmissionMode : NativeQaMode
{
    private const ushort Aikido = 101, KungFu = 133, GuardBreaker = 138, IronGuard = 139;
    private const byte Level = 12, Rank = 12;
    private static readonly ushort[] Roots = { 2, 17, Aikido, KungFu, GuardBreaker, IronGuard };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-ma-starter-admission";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=2000,healthM=2000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_CONDITIONAL_THROW_QA"] = "1" },
        readyMessages: new[]
        {
            "Level 12, rank 12, unarmed, with Melee Dummy 3 m west (Aikido 101 + Kung-Fu 133 + Guard Breaker 138 + Iron Guard 139 + MA Expertise 136 = +10% melee damage).",
            "Enter ordinary unarmed Close Combat on Melee Dummy, reselect Aikido (101), and use Iron Guard (139): expect 25 IS, 10.0 s own reuse, landed physical damage 13 (base 12 + 10%), and caster +10 MeleeDef (1009) & +10 BallisticDef (1010) for 12 s.",
            "Optional in same fight: select Melee Dummy, run '?combatdepth offbalance 30', reselect Kung-Fu (133), and use Guard Breaker (138): expect 20 IS, 10.0 s own reuse, landed physical damage 39 (base 36 + 10%) and target '[EFFECTS] 138 on Melee Dummy: Stat 1009 -10 15 s'."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "138,139", ["NativeCloseableRows"] = 0,
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

        // Outside close combat: both 138 and 139 refuse natively (24) with no IS spend or timers.
        foreach (ushort id in new[] { GuardBreaker, IronGuard })
        {
            int refuseStart = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(id, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            await caster.WaitUntilAsync(() => server.DebugLog()[refuseStart..].Contains($"[ABILITY] Denied {id}: native refusal 24", StringComparison.Ordinal),
                $"Starter interlock move {id} refuses outside close combat", TimeSpan.FromSeconds(5));
        }

        await StarterInterlockDamageMode.Open(caster, dummy);
        await StarterInterlockDamageMode.Select(caster, dummy, Aikido);

        int castStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(IronGuard, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 139 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Iron Guard lands in close combat", TimeSpan.FromSeconds(15));
        Check.Equal(13, int.Parse(landed.Match(server.DebugLog()[castStart..]).Groups[1].Value),
            "Iron Guard lands for 13 physical damage at rank 12");

        Console.WriteLine("  Guard Breaker (138) and Iron Guard (139) refused outside interlock, and Iron Guard landed in close combat for 13");
        return Array.Empty<byte>();
    }
}
