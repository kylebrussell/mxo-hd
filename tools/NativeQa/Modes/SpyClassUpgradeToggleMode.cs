using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class SpyClassUpgradeToggleMode : NativeQaMode
{
    private const ushort SpyDiscipline = 155, InfiltratorDiscipline = 131, SpyUpgrade = 1113, InfiltratorUpgrade = 1115;
    private const byte Level = 19, Rank = 15;
    private static readonly ushort[] Roots = { 2, 17, SpyDiscipline, InfiltratorDiscipline };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-spy-class-toggles";

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
            "Level 19, rank 15, unarmed, outside interlock. Spy (155) grants Spy Upgrade (1113) and Infiltrator (131) grants Infiltrator Upgrade (1115) in Actions. Clear selection (or select Melee Dummy).",
            "Activate Spy Upgrade (1113): expect 20 IS, 30.0 s own reuse (80 bd 01 59 04 04 30.0f) sent before toggle-on (80 bd 05 59 04 ... 01), and 4 adds (Stat 1009 +3, Stat 1032 +3, Stat 1034 +2, Stat 209 +8). Click 1113 at +4 s: expect clean toggle-off ('[BUFF] 1113 off', 4 removals, no denial, no new timer). Click inactive 1113 at +6 s: client holds original 30 s timer. Reactivate at +31 s: expect fresh activation.",
            "Activate Infiltrator Upgrade (1115) while 1113 is active: expect 1113 replaced ('[BUFF] 1113 off (replaced by 1115)'), 1115 on with 4 adds (Stat 1009 +3, Stat 1032 +3, Stat 639 +3, Stat 877 +10), clean toggle-off at +4 s, inactive hold at +6 s, and reactivation after 30 s."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "131,1113,1115,1117", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-19 memory");

        foreach (ushort id in new[] { SpyUpgrade, InfiltratorUpgrade })
        {
            int start = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(id, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            await caster.WaitUntilAsync(() => server.DebugLog()[start..].Contains($"[BUFF] {id} on", StringComparison.Ordinal),
                $"Class upgrade {id} activates", TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromSeconds(1));
            int offStart = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(id, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            await caster.WaitUntilAsync(() => server.DebugLog()[offStart..].Contains($"[BUFF] {id} off", StringComparison.Ordinal),
                $"Class upgrade {id} toggles off during activation reuse", TimeSpan.FromSeconds(5));
        }
        Console.WriteLine("  Spy Upgrade (1113) and Infiltrator Upgrade (1115) activated and toggled off cleanly during 30 s reuse");
        return Array.Empty<byte>();
    }
}
