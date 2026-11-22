using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerCodeInfectionMode : NativeQaMode
{
    private const ushort TierOne = 350, TierTwo = 351, Style = CombatStyles.HackerStyle, TransmitVirus = 95;
    private const int Level = 26, X = 17000, Y = 495, Z = 6400;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Style, TierOne, TierTwo }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-hacker-code-infection-tiers";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
        {
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            $"UPDATE char_abilities SET level=2 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Style)}",
            $"UPDATE char_abilities SET level=6 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(TransmitVirus)}"
        }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=64,alignment=1,pvpflag=0,innerStrC=600,innerStrM=600 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Style, TierOne, TierTwo },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "0",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_CODE_INFECTION_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "350,351", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified NAV CSR ({X},{Y},{Z}) faces east. Choose Hacker Style on page 2 slot 1. Try slots 2 and 3 with selection cleared. No cast, cost, reuse or effect.",
            "Type ?cd cip. Select QA Infection Neutral (17400,495,6400), then cast slots 2 and 3 at least four seconds apart. Save the first complete stock 350 and 351 requests. Each has a 2 s cast, 12 s own reuse and 4 s shared reuse. 350 spends 8 IS and 351 spends 10 IS. Expect two separate 30 s DOT records; neutral ticks are 9 (350) and 11 (351), every 2.5 s through 27.5 s, with no tick at expiry.",
            "After both DOTs on Neutral expire, target QA Infection Resisted (17300,495,6600), a same-level +75 Viral Resistance control. Repeat both casts. The same raw ticks should give final 6 (350) and 7 (351), with [TYPED-RESISTANCE OURS] type2 total75 and no damage to the neutral target from this pair.",
            "For refusal checks after both are ready, select QA Sight Target at the outdoor pick stand created by ?cd sp. Use ?cd gf from the verified 21.5 m clear stand, then try both. If sent, expect reason 34 with no cost/effects. Return with ?cd sp, reselect the same target and use ?cd sb. Try both at the blocked 6 m stand. A sent cast can consume cost and reuse but should apply no DOT or contest. No [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var root = context.RepositoryRoot ?? throw new InvalidOperationException("Code Infection needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach ((int x, int z) in new[] { (X, Z), (17400, 6400), (17300, 6600) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, X, Y, Z, x, Y, z),
                $"Code Infection outdoor NAV and clear sight at ({x},{Y},{z})");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(parent => archive.TryGetValue(parent.Key, out ushort rank) && rank >= parent.Value)),
            "The complete rank-26 Code Infection profile fits memory");
        Check.Equal((ushort)2, archive[Style], "The comparison uses the same neutral Hacker Style rank as the starter base proof");
        Check.Equal((ushort)6, archive[TransmitVirus], "The comparison retains the known +5% viral influence");
        await actor.EnterWorldAsync();
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendChat("?cd cip");
        await actor.WaitForTextAsync("Code Infection targets ready");
        await actor.WaitForViewNamedAsync("QA Infection Neutral");
        await actor.WaitForViewNamedAsync("QA Infection Resisted");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        return Array.Empty<byte>();
    }
}
