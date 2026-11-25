using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class CripplingThrowMode : NativeQaMode
{
    private const ushort CripplingThrow = 280, SpyStyle = 1112;
    private const byte Level = 25, Rank = 25;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, CripplingThrow };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-crippling-throw";

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
            "Level 25, rank 25, Spy Style (1112) active, unarmed, outside interlock. First clear selection and try Crippling Throw (280): expect native InvalidTarget refusal (24) or stock suppression, no IS spent, no cast bar and no reuse.",
            "Select Melee Dummy (3 m west) and cast Crippling Throw (280) outside interlock. Expect 20 IS spent (1000 -> 980), 2.0 s cast bar (80 ac), landed thrown damage 1, '[EFFECTS] 280 on Melee Dummy: State 99 0 15.75 s (15.749999 s), Stat 519 -40 15 s' (Rooted state 99, 15 s base scaled +5% by rank-25 Wicked Aim 278 Root Duration Bonus = 15.75 s, break Chance; Force Combat stat 519 -40 for 15 s), 10.0 s own reuse and 3.0 s shared family 1215 reuse."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "280", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-25 memory");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);

        // Empty selection must refuse cleanly without charging IS or starting cast/reuse.
        int refuseStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(CripplingThrow, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[refuseStart..].Contains("[ABILITY] Denied 280: native refusal 24", StringComparison.Ordinal),
            "Crippling Throw rejects empty target selection", TimeSpan.FromSeconds(5));

        int castStart = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(CripplingThrow, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 280 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Crippling Throw lands on Melee Dummy", TimeSpan.FromSeconds(10));

        string log = server.DebugLog()[castStart..];
        Check.Equal(1, int.Parse(landed.Match(log).Groups[1].Value), "Crippling Throw lands for shipped fixed thrown base 1");
        Check.That(log.Contains("[EFFECTS] 280 on Melee Dummy:", StringComparison.Ordinal) &&
            log.Contains("State 99 0 15.7", StringComparison.Ordinal) &&
            log.Contains("Stat 519 -40 15 s", StringComparison.Ordinal),
            "Crippling Throw applies Rooted state 99 (15.75 s with rank-25 Wicked Aim) and Force Combat -40 for 15 s");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(CripplingThrow, AbilityCooldowns.OwnTimer, 10, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpcCursor).Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)),
            "Crippling Throw starts 10 s own reuse");
        Console.WriteLine("  Crippling Throw refused empty target, then landed for 1 thrown damage with Rooted 15 s and Force Combat -40 for 15 s");
        return Array.Empty<byte>();
    }
}
