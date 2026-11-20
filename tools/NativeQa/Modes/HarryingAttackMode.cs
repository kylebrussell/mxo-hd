using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HarryingAttackMode : NativeQaMode
{
    private const ushort HarryingAttack = 1127, SneakParent = 151, SneakGrant = 1111, SpyStyle = 1112;
    private const byte Level = 11, Rank = 11;
    // 2.52 m directly behind Melee Dummy (24499, 495, -1498, rotation 0 facing +Z): within 4 m SelectionRange and outside 50 deg FOV.
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;
    private static readonly ushort[] Roots = { 2, 17, SneakParent, SpyStyle, HarryingAttack };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-harrying-attack";

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
            "Level 11, rank 11, unarmed, spawned 2.5 m directly behind Melee Dummy (facing away, within 4 m range and outside FOV). Select Spy Style (1112), activate Sneak (1111 from Actions > Spy), select Melee Dummy and cast Harrying Attack (1127) outside interlock.",
            "Expect 25 IS spent (1000 -> 975), 2.0 s cast bar (80 ac) while Sneak stays active during cast then breaks on landing, landed thrown damage 53 (ValueFunction 3 base 49 at level 11 + 9% rank-11 Spy Style), optional independent 30% procs of Dazed (state 199), Off-Balance (state 200) and Staggered (state 201) for 12 s each, 45.0 s own reuse and 15.0 s shared family 691 (aaSneakAttack) reuse."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1127", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-11 memory");
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
        caster.SendRpc(new AbilityUseRecord(HarryingAttack, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 1127 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Harrying Attack lands on Melee Dummy from stealth", TimeSpan.FromSeconds(10));

        string log = server.DebugLog()[castStart..];
        Check.Equal(53, int.Parse(landed.Match(log).Groups[1].Value),
            "Harrying Attack lands for 53 (ValueFunction 3 base 49 at level 11 + 9% rank-11 Spy Style)");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(HarryingAttack, AbilityCooldowns.OwnTimer, 45, true);
        byte[] groupTimer = AbilityCooldowns.BuildTimer(691, AbilityCooldowns.GroupTimer, 15, true);
        var rpcs = caster.World.RpcSnapshot().Skip(rpcCursor).ToArray();
        Check.That(rpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)), "Harrying Attack starts 45 s own reuse");
        Check.That(rpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(groupTimer)), "Harrying Attack starts 15 s shared family 691 reuse");
        Console.WriteLine("  Harrying Attack landed from Sneak for 53 after 2 s cast with 45 s own / 15 s shared reuse");
        return Array.Empty<byte>();
    }
}
