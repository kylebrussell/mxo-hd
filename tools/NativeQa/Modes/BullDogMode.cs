using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class BullDogMode : NativeQaMode
{
    private const ushort BullDog = 387, SneakParent = 151, SneakGrant = 1111, SpyStyle = 1112;
    private const byte Level = 19, Rank = 19;
    // Place CSR_Tester 2.52 m directly behind Melee Dummy (24499, 495, -1498, rotation 0 facing +Z)
    // so Sneak activates cleanly and CastOutsideTargetFOVAngle (50 deg) and SelectionRange (6 m) are satisfied in place.
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;
    private static readonly ushort[] Roots = { 2, 17, SneakParent, SpyStyle, BullDog };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-bulldog";

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
            "Level 19, rank 19, unarmed, spawned 2.5 m directly behind Melee Dummy (facing away, outside its FOV). Select Spy Style (1112), activate Sneak (1111 from Actions > Spy), select Melee Dummy and use Bull Dog (387) outside interlock.",
            "Expect instant cast (no cast bar), 37 IS spent (1000 -> 963), Sneak breaks on action, landed thrown damage 1158 (shipped fixed base 75 + 950 stealth opener = 1025, scaled +13% by rank-19 Spy Style), 60.0 s own reuse (80 bd 01 83 01 04) and 15.0 s shared family 691 (aaSneakAttack) reuse."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "387", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-19 memory");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);

        // Activate Sneak (1111) behind Melee Dummy (2 s cast).
        int sneakStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(SneakGrant, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[sneakStart..].Contains("[STEALTH]", StringComparison.Ordinal),
            "Sneak activates behind Melee Dummy", TimeSpan.FromSeconds(8));

        int castStart = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(BullDog, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 387 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Bull Dog lands on Melee Dummy from stealth", TimeSpan.FromSeconds(8));

        string log = server.DebugLog()[castStart..];
        Check.Equal(1158, int.Parse(landed.Match(log).Groups[1].Value),
            "Bull Dog lands for 1158 (shipped base 75 + 950 stealth opener + 13% rank-19 Spy Style)");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(BullDog, AbilityCooldowns.OwnTimer, 60, true);
        byte[] groupTimer = AbilityCooldowns.BuildTimer(691, AbilityCooldowns.GroupTimer, 15, true);
        var rpcs = caster.World.RpcSnapshot().Skip(rpcCursor).ToArray();
        Check.That(rpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)), "Bull Dog starts 60 s own reuse");
        Check.That(rpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(groupTimer)), "Bull Dog starts 15 s shared family 691 reuse");
        Console.WriteLine("  Bull Dog landed from Sneak for 1158 with 60 s own / 15 s shared reuse");
        return Array.Empty<byte>();
    }
}
