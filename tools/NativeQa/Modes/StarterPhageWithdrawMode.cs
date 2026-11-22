using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerWithdrawBonusMode;

namespace NativeQa.Modes;

public sealed class StarterPhageWithdrawMode : NativeQaMode
{
    private const int ResistRank = 19;
    private const ushort Resist = 76, Phage = 83, Virologist = 98, Doctor = 236, Upgrade = 1082;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, SelfDefense, Resist, Phage, Doctor).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Sources =
    {
        (Resist, 0, new (ushort, int)[] { (1030, 4), (1045, 4), (1049, 2) }),
        (Phage, Upgrade, new (ushort, int)[] { (1030, 1), (1045, 1), (1049, 1) })
    };

    public override string Name => "ability-parity-starter-phage-withdraw";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level={ResistRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Resist)}"
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=64,healthC=4000,healthM=4000 WHERE charId=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, hotbarAbilities: new[] { SelfDefense }, hotbarWithdraw: true,
        testMobFixture: "awakened-power-shot-cap.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "76,83", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory with legal rank-{ResistRank} Resist Viruses. Outdoor NAV CSR ({X},{Y},{Z}) faces Melee Dummy ({DummyX},{Y},{Z}) east, clear of ice. No helpers or bag. Page 2 slot 1 Self Defense, slot 2 stock Withdraw.",
            "At entry [MODIFIER] stat 1045 has fixed 98 +1 and 236 +2, plus 76 +4 and 83 +1. Source 76 also gives stat 1030 +4 and 1049 +2. Source 83 gives stat 1030 and 1049 +1 each and grants Selective Phage Upgrade 1082 in Actions. Self Defense gives direct 1005 +65.",
            "Select Melee Dummy, open ordinary Close Combat unarmed, keep Self Defense, and press slot 2. [WITHDRAW INPUT OURS] direct=65 input=70 bonus1045=8. Natural outcome can vary. Between samples use ?combatdepth fixture-reset, wait for combat HUD clear, then strafe and reselect without Escape.",
            "Between fights type ?qaunload 83. Expect the three source-83 +1 removals and 1082 out of Actions. Repeat stock Withdraw: direct=65 input=69 bonus1045=7. Reset and type ?qaload 83. All three and the grant return. Repeat for input=70 bonus1045=8.",
            "Reset and type ?qaunload 76. Expect source 76 removals 1030 +4, 1045 +4 and 1049 +2. 83 and 1082 remain available. Repeat Withdraw: direct=65 input=67 bonus1045=4. Reset and type ?qaload 76. Repeat for input=70 bonus1045=8. Sources 98 and 236 stay fixed. No [PVP] or max [VITALS]. PR721 and PR694 already cover the live Evade checks."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        VerifyStands(context.RepositoryRoot!);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The learned Resist Viruses rank qualifies all loaded prerequisites");
        Check.Equal((ushort)ResistRank, archive[Resist].Level, "Resist Viruses uses its ordinary +4% learned-rank source");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);

        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Virologist, 1045, 1, 0, false),
            "Required Virologist contributes +1 to the fixed baseline");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Doctor, 1045, 2, 0, false),
            "Doctor supplies a fixed +2 to make Phage's +1 cross the integer breakpoint");
        var proof = new List<object>();
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, Sources, 0, remove: false);
        await SampleAsync(actor, server, dummy, 8, 70);

        foreach (var (ability, percent, input) in new[] { (Phage, 7, 69), (Resist, 4, 67) })
        {
            var source = Sources.Where(row => row.Parent == ability).ToArray();
            int removal = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load: false);
            await StockAbilityProfile.CheckPassivesAsync(actor, proof, source, removal, remove: true);
            await SampleAsync(actor, server, dummy, percent, input);

            int restored = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { ability }, load: true);
            await StockAbilityProfile.CheckPassivesAsync(actor, proof, source, restored, remove: false);
            await SampleAsync(actor, server, dummy, 8, 70);
        }

        Check.Equal(0, actor.World.Problems.Count, "Phage withdrawal and grant packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "phage-withdraw-modifiers.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();
    }
}
