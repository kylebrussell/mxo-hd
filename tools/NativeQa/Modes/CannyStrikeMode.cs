using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class CannyStrikeMode : NativeQaMode
{
    private const ushort CannyStrike = 295, SpyStyle = 1112;
    private const byte Level = 22, Rank = 22;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, CannyStrike };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-canny-strike";

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
            "Level 22, rank 22, unarmed, with Melee Dummy 3 m west. Full rank-22 closure loads Spy Style 1112 (+15%), Weak Points 164 (+4%) and Wicked Aim 278 (+6%) = +25% thrown damage.",
            "Enter ordinary unarmed Close Combat on Melee Dummy, reselect Spy Style (1112), and use Canny Strike (295) during interlock. Expect 70 IS spent (1000 -> 930), 30.0 s own reuse (80 bd 01 27 01 00 30.0f), queued move '[COMBAT] CannyStrikeAbility is the next move', and when the round lands: thrown damage 11 (ValueFunction 4 base 9 from 8..10 at level 22 + 25% closure, replacing generic 50..80)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "295", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-22 memory");
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        // Outside close combat: refuses natively (24) with no IS spend or reuse.
        int refuseStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(CannyStrike, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[refuseStart..].Contains("[ABILITY] Denied 295: native refusal 24", StringComparison.Ordinal),
            "Canny Strike refuses outside interlock", TimeSpan.FromSeconds(5));

        await StarterInterlockDamageMode.Open(caster, dummy);
        await StarterInterlockDamageMode.Select(caster, dummy, SpyStyle);

        int castStart = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(CannyStrike, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 295 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Canny Strike lands on Melee Dummy", TimeSpan.FromSeconds(15));

        string log = server.DebugLog()[castStart..];
        Check.Equal(11, int.Parse(landed.Match(log).Groups[1].Value), "Canny Strike lands for 11 thrown damage (base 9 + 25%)");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(CannyStrike, AbilityCooldowns.OwnTimer, 30, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpcCursor).Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)),
            "Canny Strike starts 30 s own reuse");
        Console.WriteLine("  Canny Strike refused outside interlock, then landed in close combat for 11 thrown damage with 30 s reuse");
        return Array.Empty<byte>();
    }
}
