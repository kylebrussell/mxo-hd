using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class SuplexMode : NativeQaMode
{
    private const ushort Suplex = 287, SpyStyle = 1112, SneakPassive = 151, SneakGrant = 1111;
    private const byte Level = 27, Rank = 27;
    // Melee Dummy sits at (24499, 495, -1498) facing +Z (rotation 0).
    // Standing 2.52 m south at (24499, 495, -1750) puts CSR directly behind it:
    // Sneak (1111) passes EnemyWatching, CastOutsideTargetFOVAngle (50 deg) and 6 m SelectionRange are satisfied.
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, SneakPassive, SneakGrant, Suplex };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-suplex";

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
            "Level 27, rank 27, unarmed, spawned 2.5 m directly behind Melee Dummy (facing away, within 6 m range and outside FOV). Full rank-27 closure loads Spy Style 1112 (+17%), Weak Points 164 (+5%) and Wicked Aim 278 (+8%) = +30% thrown damage.",
            "Select Spy Style (1112), activate Sneak (1111 from Actions > Spy), select Melee Dummy and use Suplex (287) outside interlock. Expect instant cast (no 80 ac cast bar), 50 IS spent (1000 -> 950), Sneak breaks on action ('1111 off (used ability 287)'), landed thrown damage 505 (ValueFunction 3 base 89 at level 27 + 300 stealth opener = 389, scaled +30%), '[EFFECTS] 287 on Melee Dummy: State 200 0 15 s, State 201 0 15 s, State 199 0 15 s' (Off-Balance, Staggered, Dazed for 15 s each), 38.0 s own reuse and 15.0 s shared family 691 reuse."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "287", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-27 memory");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);

        // Empty selection refuses natively (24) without spending IS or starting timers.
        int refuseStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(Suplex, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[refuseStart..].Contains("[ABILITY] Denied 287: native refusal 24", StringComparison.Ordinal),
            "Suplex refuses empty target selection natively", TimeSpan.FromSeconds(5));

        int sneakStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(SneakGrant, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[sneakStart..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
            "Sneak (1111) activates behind Melee Dummy", TimeSpan.FromSeconds(5));

        int castStart = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(Suplex, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 287 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Suplex lands on Melee Dummy", TimeSpan.FromSeconds(10));

        string log = server.DebugLog()[castStart..];
        Check.Equal(505, int.Parse(landed.Match(log).Groups[1].Value), "Suplex lands for 505 thrown damage");
        Check.That(log.Contains("[EFFECTS] 287 on Melee Dummy:", StringComparison.Ordinal) &&
            log.Contains("State 200 0 15 s", StringComparison.Ordinal) &&
            log.Contains("State 201 0 15 s", StringComparison.Ordinal) &&
            log.Contains("State 199 0 15 s", StringComparison.Ordinal),
            "Suplex applies Off-Balance (200), Staggered (201) and Dazed (199) for 15 s each");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(Suplex, AbilityCooldowns.OwnTimer, 38, true);
        byte[] familyTimer = AbilityCooldowns.BuildTimer(691, AbilityCooldowns.OwnTimer, 15, true);
        var sentRpcs = caster.World.RpcSnapshot().Skip(rpcCursor).ToList();
        Check.That(sentRpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)), "Suplex starts 38 s own reuse");
        Check.That(sentRpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(familyTimer)), "Suplex starts 15 s shared family 691 reuse");
        Console.WriteLine("  Suplex refused empty target, broke Sneak, landed for 505 thrown damage with 15 s Off-Balance/Staggered/Dazed and 38 s / 15 s reuse");
        return Array.Empty<byte>();
    }
}
