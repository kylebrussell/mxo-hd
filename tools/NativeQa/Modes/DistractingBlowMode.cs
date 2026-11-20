using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DistractingBlowMode : NativeQaMode
{
    private const ushort DistractingBlow = 1128, EffectId = 1129, SneakParent = 151, SneakGrant = 1111, SpyStyle = 1112;
    private const byte Level = 20, Rank = 20;
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;
    private static readonly ushort[] Roots = { 2, 17, SneakParent, SpyStyle, DistractingBlow };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-distracting-blow";

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
            "Level 20, rank 20, unarmed, spawned 2.5 m directly behind Melee Dummy (facing away, within 4 m range and outside FOV). Select Spy Style (1112), activate Sneak (1111 from Actions > Spy), select Melee Dummy and cast Distracting Blow (1128) outside interlock.",
            "Expect 35 IS spent (1000 -> 965), 2.0 s cast bar (80 ac) with Sneak retained during cast and broken on landing, landed thrown damage 77 (ValueFunction 3 base 68 at level 20 + 14% rank-20 Spy Style), ApplyAuraEffect 1129 granting caster Stat 1005 (Contested Withdraw) +50 for 12 s ('[BUFF] 1129 on for 12 s' and 80 bc add/remove after 12 s), 45.0 s own reuse and 15.0 s shared family 691 reuse."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1128", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-20 memory");
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
        caster.SendRpc(new AbilityUseRecord(DistractingBlow, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var landed = new Regex($@"\[ABILITY\] 1128 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[castStart..]),
            "Distracting Blow lands on Melee Dummy from stealth", TimeSpan.FromSeconds(10));

        string log = server.DebugLog()[castStart..];
        Check.Equal(77, int.Parse(landed.Match(log).Groups[1].Value),
            "Distracting Blow lands for 77 (ValueFunction 3 base 68 at level 20 + 14% rank-20 Spy Style)");
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(rpcCursor).Any(rpc =>
            ManageBonusRecord.TryDecode(rpc.Bytes, out var m) && m!.SourceId == EffectId &&
            m.TargetId == 1005 && m.SignedValue == 50 && m.Duration == 12 && m.AddRemoveType == 0),
            "Distracting Blow applies 1129 (+50 stat 1005 for 12 s)", TimeSpan.FromSeconds(5));
        Console.WriteLine("  Distracting Blow landed from Sneak for 77 and applied 1129 (+50 Contested Withdraw for 12 s)");
        return Array.Empty<byte>();
    }
}
