using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerSelectedDebuffsMode : NativeQaMode
{
    private const ushort CodeRot = 392, Overheat = 394, InfectionOne = 350, InfectionTwo = 351, TransmitVirus = 95;
    private const int Level = 26, X = 21550, Y = 1095, Z = 16150, TargetX = 21750, DeflectZ = 16350;
    private const int AllyX = 21600;
    private static readonly ushort[] Loadout = new[] { (ushort)2, (ushort)17, CombatStyles.HackerStyle, CodeRot, Overheat, InfectionOne, InfectionTwo }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-hacker-selected-debuffs";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            $"UPDATE char_abilities SET level=2 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(CombatStyles.HackerStyle)}",
            $"UPDATE char_abilities SET level=6 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(TransmitVirus)}"
        }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "rotation=64,alignment=1,pvpflag=0,innerStrC=600,innerStrM=600 WHERE charId=110",
            $"UPDATE characters SET x={AllyX},y={Y},z={DeflectZ},alignment=1,pvpflag=0 WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, CodeRot, Overheat, InfectionOne, InfectionTwo },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "0",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_HACKER_DEBUFF_QA"] = "1", ["MXO_CODE_INFECTION_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "16384",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "16384", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "350,351,392,394", ["NativeCloseableRows"] = 4 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Start on verified NAV ({X},{Y},{Z}) facing east. Choose Hacker Style (page 2 slot 1). Try slots 2–5 with no target and again after /target QAFriend at ({AllyX},{Y},{DeflectZ}). Expect no contest, debit or reuse.",
            "Type ?cd sp. Select QA Sight Target straight ahead at 2 m. Type ?cd gf to retain that target from the clear 21.5 m stand. Try slots 2–5. If stock sends the request, expect reason 34 with no contest, debit or reuse. Type ?cd sp to return.",
            "Select QA Sight Target and use slots 2/3. Save the full stock 392 and 394 requests. Each casts for 2 s and costs 20 IS. Expect [FREECAST-VIRAL] hit=True and source392 stat342 -39 for 18 s with 30 s reuse. Source394 stats180/181 -52 last 28 s with 60 s reuse. Neither cast changes HP.",
            "Type ?cd cip. From the verified open street stand, cast slots 4/5 on QA Infection Neutral (17400,495,6400), at least 4 s apart. Save both stock requests. Each cast is 2 s, 12 s own reuse and 4 s shared reuse. 350 costs 8 IS and 351 costs 10. Their 30 s DOTs snapshot 104 and 135, with a 2.5 s cadence and eleven 9- and 11-point ticks respectively. No tick at expiry.",
            "After those DOTs expire, repeat both on QA Infection Resisted (17300,495,6600). Expect [TYPED-RESISTANCE OURS] type2 total75, raw/final 9/6 and 11/7. The neutral NPC receives neither resisted cast.",
            $"Return with ?cd sp and select QA Deflect Target at ({TargetX},{Y},{DeflectZ}), about 2.8 m away. This NPC has +250 Viral Defense and the fixture uses fixed middle draws. Try slots 2–5 when ready with 4 s between the two Infection casts. Expect [FREECAST-VIRAL] hit=False and stock deflect feedback, with no debuff, DOT or HP loss.",
            "Select ordinary QA Sight Target from ?cd sp, then use ?cd sb to retain it behind the obstruction from the printed blocked NAV stand. Try slots 2–5 when ready, respecting the 4 s family reuse. Sent casts can start their reuse and debit but log [ABILITY] Sight blocked with no contest or recipient effect. Save stock feedback. No player [VITALS] or [PVP] rounds throughout."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Debuff targeting needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach ((int x, int z) in new[] { (X, Z), (TargetX, Z), (TargetX, DeflectZ), (AllyX, DeflectZ), (19600, Z), (22350, Z) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Verified outdoor debuff stand ({x},{Y},{z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, Z) &&
            NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, DeflectZ) &&
            NavMesh.HasSightPath(1, 19600, Y, Z, TargetX, Y, Z) &&
            !NavMesh.HasSightPath(1, 22350, Y, Z, TargetX, Y, Z),
            "The close, clear far and blocked comparisons are distinct");
        foreach ((int x, int z) in new[] { (17000, 6400), (17400, 6400), (17300, 6600) })
            Check.That(NavMesh.Locate(1, x, 495, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - 495) < 1 && NavMesh.HasSightPath(1, 17000, 495, 6400, x, 495, z),
                $"Code Infection outdoor NAV and clear sight at ({x},495,{z})");

        var ally = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(ally.Handle, ally);
        await ally.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, ally);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "The debuff profile fits memory");
        Check.That(archive.Keys.ToHashSet().SetEquals(Loadout),
            $"The stock profile matches the seed, absent {string.Join(',', Loadout.Except(archive.Keys))}, extra {string.Join(',', archive.Keys.Except(Loadout))}");
        foreach (ushort id in Loadout)
            foreach (var parent in AbilityPrerequisites.Current.Closure(id))
                Check.That(archive.TryGetValue(parent.Key, out ushort rank) && rank >= parent.Value,
                    $"Ability {id} needs in-memory {parent.Key} rank {parent.Value}, starts with {rank}");
        Check.Equal((ushort)2, archive[CombatStyles.HackerStyle], "The comparison holds the known low style influence");
        Check.Equal((ushort)6, archive[TransmitVirus], "The comparison retains the known +5% viral influence");
        await actor.EnterWorldAsync();
        await actor.WaitForViewNamedAsync(ally.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendChat("?cd sp");
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");
        await actor.WaitForViewNamedAsync("QA Sight Target");
        await actor.WaitForViewNamedAsync("QA Deflect Target");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendChat("?cd cip");
        await actor.WaitForTextAsync("Code Infection targets ready");
        await actor.WaitForViewNamedAsync("QA Infection Neutral");
        await actor.WaitForViewNamedAsync("QA Infection Resisted");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        return Array.Empty<byte>();
    }
}
