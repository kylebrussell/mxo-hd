using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class StarterLegSweepMode : NativeQaMode
{
    private const ushort LegSweep = 134, SpyStyle = 1112;
    private const byte Level = 19, Rank = 15;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, LegSweep };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-starter-leg-sweep";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Select Melee Dummy, enter unarmed Close Combat and reselect Spy Style while interlocked. Use Leg Sweep from the hotbar and capture the ordinary cast, LegSweep animation, 14 landed damage in this rank-15 fixture, and the target's -15 melee defense for ten seconds."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "134", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(!self.Attributes.TryGetValue("EquippedItemID", out var weapon) || BitConverter.ToUInt32(weapon) == 0,
            "The live stock view starts unarmed");
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(archive.All(row => row.Level == Rank) &&
            archive.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout),
            "Exact Spy Style profile and prerequisite ranks");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-19 memory");
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        await StarterInterlockDamageMode.Open(caster, dummy);
        await StarterInterlockDamageMode.Select(caster, dummy, SpyStyle);
        await StarterInterlockDamageMode.Land(caster, dummy, LegSweep, server, 14, "LegSweep", pvp: false);
        var log = server.DebugLog();
        Check.That(log.Contains("[COMBAT-ATTACK OURS] special 134 stat 1032", StringComparison.Ordinal),
            "Leg Sweep still contests using thrown accuracy");
        Check.That(log.Contains("[EFFECTS] 134 on Melee Dummy: Stat 1009 -15 10 s", StringComparison.Ordinal),
            "Leg Sweep retains the target melee-defense penalty");
        Console.WriteLine("  Leg Sweep landed for 14 with thrown accuracy, named animation and the target defense penalty");
        return Array.Empty<byte>();
    }
}
