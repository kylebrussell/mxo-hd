using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DazzlingThrowMode : NativeQaMode
{
    private const ushort DazzlingThrow = 1132, SneakParent = 151, SneakGrant = 1111, SpyStyle = 1112;
    private const byte Level = 23, Rank = 23;
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;
    private static readonly ushort[] Roots = { 2, 17, SneakParent, SpyStyle, DazzlingThrow };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-dazzling-throw";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, BehindDummyX, BehindDummyZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Level 23, rank 23, unarmed, spawned 2.5 m directly behind Melee Dummy (facing away, within 6 m range and outside FOV). Select Spy Style (1112), activate Sneak (1111 from Actions > Spy), select Melee Dummy and cast Dazzling Throw (1132) outside interlock.",
            "Expect 35 IS spent (1000 -> 965), 2.0 s cast bar (80 ac) with Sneak retained during cast and broken on landing, landed thrown damage 53 (shipped fixed base 43 + 25% from full rank-23 closure: Spy Style 1112 +15%, Weak Points 164 +4%, Wicked Aim 278 +6%), optional 50% Dazed proc (state 199 for 12 s), 45.0 s own reuse and 15.0 s shared family 691 reuse."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1132", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-23 memory");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);

        int sneakStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(SneakGrant, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[sneakStart..].Contains("[STEALTH]", StringComparison.Ordinal),
            "Sneak activates behind Melee Dummy", TimeSpan.FromSeconds(8));

        int castStart = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(DazzlingThrow, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 1132 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Dazzling Throw lands on Melee Dummy from stealth", TimeSpan.FromSeconds(10));

        string log = server.DebugLog()[castStart..];
        Check.Equal(53, int.Parse(landed.Match(log).Groups[1].Value),
            "Dazzling Throw lands for 53 (fixed base 43 + 25% full rank-23 closure)");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(DazzlingThrow, AbilityCooldowns.OwnTimer, 45, true);
        byte[] groupTimer = AbilityCooldowns.BuildTimer(691, AbilityCooldowns.GroupTimer, 15, true);
        var rpcs = caster.World.RpcSnapshot().Skip(rpcCursor).ToArray();
        Check.That(rpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)), "Dazzling Throw starts 45 s own reuse");
        Check.That(rpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(groupTimer)), "Dazzling Throw starts 15 s shared family 691 reuse");
        Console.WriteLine("  Dazzling Throw landed from Sneak for 53 with 45 s own / 15 s shared reuse");
        return Array.Empty<byte>();
    }
}
