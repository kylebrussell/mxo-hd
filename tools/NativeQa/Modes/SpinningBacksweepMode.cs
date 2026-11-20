using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class SpinningBacksweepMode : NativeQaMode
{
    private const ushort SpinningBacksweep = 152, SpyStyle = 1112, SneakPassive = 151, SneakGrant = 1111;
    private const byte Level = 4, Rank = 4;
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, SneakPassive, SneakGrant, SpinningBacksweep };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-spinning-backsweep";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, BehindDummyX, BehindDummyZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=1000,healthM=1000,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Level 4, rank 4, unarmed, spawned 2.5 m directly behind Melee Dummy (facing away, within 6 m range and outside FOV). Rank-4 closure has Spy Style 1112 (+6% thrown damage).",
            "Select Spy Style (1112), activate Sneak (1111 from Actions > Spy), select Melee Dummy and use Spinning Backsweep (152) outside interlock. Expect instant cast (no 80 ac cast bar), 20 IS spent (500 -> 480), Sneak breaks on action ('1111 off (used ability 152)'), landed thrown damage 109 (fixed base 28 + 75 stealth opener = 103, scaled +6% by rank-4 Spy Style), 30.0 s own reuse and 15.0 s shared family 691 reuse."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "152", ["NativeCloseableRows"] = 0,
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

        // Empty selection refuses natively (24) without spending IS or starting timers.
        int refuseStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(SpinningBacksweep, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[refuseStart..].Contains("[ABILITY] Denied 152: native refusal 24", StringComparison.Ordinal),
            "Spinning Backsweep refuses empty target selection natively", TimeSpan.FromSeconds(5));

        int sneakStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(SneakGrant, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[sneakStart..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
            "Sneak (1111) activates behind Melee Dummy", TimeSpan.FromSeconds(5));

        int castStart = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(SpinningBacksweep, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 152 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Spinning Backsweep lands on Melee Dummy", TimeSpan.FromSeconds(10));

        string log = server.DebugLog()[castStart..];
        Check.Equal(109, int.Parse(landed.Match(log).Groups[1].Value), "Spinning Backsweep lands for 109 thrown damage");
        byte[] ownTimer = AbilityCooldowns.BuildTimer(SpinningBacksweep, AbilityCooldowns.OwnTimer, 30, true);
        byte[] familyTimer = AbilityCooldowns.BuildTimer(691, AbilityCooldowns.OwnTimer, 15, true);
        var sentRpcs = caster.World.RpcSnapshot().Skip(rpcCursor).ToList();
        Check.That(sentRpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)), "Spinning Backsweep starts 30 s own reuse");
        Check.That(sentRpcs.Any(r => r.Bytes.AsSpan().SequenceEqual(familyTimer)), "Spinning Backsweep starts 15 s shared family 691 reuse");
        Console.WriteLine("  Spinning Backsweep refused empty target, broke Sneak, landed for 109 thrown damage with 30 s / 15 s reuse");
        return Array.Empty<byte>();
    }
}
