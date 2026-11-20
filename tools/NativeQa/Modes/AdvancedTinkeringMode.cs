using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AdvancedTinkeringMode : NativeQaMode
{
    private const int Level = 11, Rank = 4;
    private const ushort AdvancedTinkering = 514, CoderStyle = 1008;
    // Native Tinkering from PR389, PacketLog d3b7112643d9 lines 2786–2787.
    private const string StockTinkering = "80b924000400020000000000000000000000000000000000000000007802c50001000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, AdvancedTinkering, CoderStyle }
        .Concat(AbilityPrerequisites.Current.Closure(AdvancedTinkering).Keys)
        .Concat(AbilityPrerequisites.Current.Closure(CoderStyle).Keys)
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-advanced-tinkering";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,innerStrC=200,innerStrM=200,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END WHERE charId IN (110,6114)",
            $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110",
            "UPDATE characters SET x=25200,z=-1498 WHERE charId=6114"
        },
        DevCommands: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        readyMessages: new[]
        {
            "Level 11, 21/41 memory, rank-4 prerequisites. Select QAEnemy at 4 m and choose Coder Style, then use Advanced Tinkering (514). Capture the stock request, 6 s cast, fixed viral base 6, and 30 s reuse.",
            "The fixture controls the viral contest only. The stock ability request and broader area/scaling checks remain open."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "514", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(loaded.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout),
            "Margin loaded all explicit Advanced Tinkering and Coder style prerequisites");
        Check.Equal(21, Loadout.Sum(AbilityMemory.Cost), "The level-11 profile uses 21 of 41 memory");
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        await enemy.WaitForViewNamedAsync(caster.Handle);
        await StarterFixedDamageMode.SelectStyle(caster, self, CoderStyle, 172);
        byte[] request = await StarterFixedDamageMode.Land(caster, enemy, target, AdvancedTinkering, 6, 6, 30, StockTinkering);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, caster);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "advanced-tinkering.json"),
            JsonSerializer.Serialize(new { Request = Convert.ToHexString(request), Damage = 6, CastSeconds = 6, ReuseSeconds = 30 }));
        Console.WriteLine("  Advanced Tinkering delivered viral 6 with the six-second cast and 30-second reuse");
        return Array.Empty<byte>();
    }
}
