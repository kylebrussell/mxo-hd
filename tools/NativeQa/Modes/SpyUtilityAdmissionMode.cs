using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class SpyUtilityAdmissionMode : NativeQaMode
{
    private const byte Level = 14, Rank = 14;
    private static readonly ushort[] Roots = { 2, 17, SixthSense.Ability };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-spy-utility-admission";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=2000,healthM=2000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Level 14, rank 14, unarmed, with Melee Dummy 3 m west (rank-14 Sixth Sense 149 grants +56 Detection, guaranteeing detection of nearby hostile NPCs even on roll 0).",
            "Outside interlock, activate Sixth Sense (149) from Actions > Infiltrator (no target needed): expect instant cast, 10 IS spent (1000 -> 990), and radar map packet 80 c5 (05 00 0a 01 00 ..., 10 s display with Melee Dummy pinged 3 m west).",
            "Enter ordinary unarmed Close Combat on Melee Dummy and try Sixth Sense (149): held client-side / refused natively with MustBeOutsideCombat (21), no IS spent and no 80 c5 radar packet."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "149,1110,1125", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-14 memory");
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(SixthSense.Ability, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(rpcCursor).Any(r =>
            r.Bytes.Length >= 7 && r.Bytes[0] == 0x80 && r.Bytes[1] == 0xc5 && r.Bytes[2] == 0x05 && r.Bytes[4] == 10),
            "Sixth Sense emits 80 c5 radar ping for Melee Dummy", TimeSpan.FromSeconds(5));

        await StarterInterlockDamageMode.Open(caster, dummy);
        int refuseStart = server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(SixthSense.Ability, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await caster.WaitUntilAsync(() => server.DebugLog()[refuseStart..].Contains("[ABILITY] Denied 149: native refusal 21", StringComparison.Ordinal),
            "Sixth Sense refuses in close combat with MustBeOutsideCombat (21)", TimeSpan.FromSeconds(5));

        Console.WriteLine("  Sixth Sense (149) emitted 80 c5 radar ping outside combat and refused natively (21) inside close combat");
        return Array.Empty<byte>();
    }
}
