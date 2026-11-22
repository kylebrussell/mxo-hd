using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerCodeRotOverheatMode : NativeQaMode
{
    private const ushort CodeRot = 392, Overheat = 394;
    private const int Level = 26, X = 21550, Y = 1095, Z = 16150, TargetX = 21750, DeflectZ = 16350;
    private const int AllyX = 21600;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, CombatStyles.HackerStyle, CodeRot, Overheat }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-hacker-code-rot-overheat";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "rotation=64,alignment=1,pvpflag=0,innerStrC=600,innerStrM=600 WHERE charId=110",
            $"UPDATE characters SET x={AllyX},y={Y},z={DeflectZ},alignment=1,pvpflag=0 WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, CodeRot, Overheat },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "0",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_HACKER_DEBUFF_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "16384",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "16384", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "392,394", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Start on verified NAV ({X},{Y},{Z}) facing east. Choose Hacker Style (page 2 slot 1). Try slots 2/3 with no target and again after /target QAFriend at ({AllyX},{Y},{DeflectZ}). Expect no contest, debit or reuse.",
            "Type ?cd sp. Select QA Sight Target straight ahead at 2 m. Type ?cd gf to retain that target from the clear 21.5 m stand. Try both slots. If stock sends the request, expect reason 34 with no contest, debit or reuse. Type ?cd sp to return.",
            "Select QA Sight Target and use slots 2/3. Save the first full stock requests. Expect [FREECAST-VIRAL] hit=True on this NPC only. Each has a 2 s cast and 20 IS cost. 392 has 30 s reuse and source392 stat342 -39 for 18 s. 394 has 60 s reuse and source394 stats180/181 -52 for 28 s. Both expire normally. Neither cast changes HP.",
            $"After each reuse select QA Deflect Target at ({TargetX},{Y},{DeflectZ}), roughly 2.8 m away, and press the same button. The fixture adds +250 Viral Defense to this NPC and uses fixed middle draws. Expect [FREECAST-VIRAL] hit=False and stock deflect feedback with no new debuff or HP change.",
            "Return to ?cd sp, select QA Sight Target, then use ?cd sb to retain it behind the obstruction from the printed blocked NAV stand. Try each when ready. If a cast is sent, expect [ABILITY] Sight blocked with no contest or recipient modifier. Save stock feedback. No player [VITALS] or [PVP] changes throughout."
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
        await actor.EnterWorldAsync();
        await actor.WaitForViewNamedAsync(ally.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendChat("?cd sp");
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");
        await actor.WaitForViewNamedAsync("QA Sight Target");
        await actor.WaitForViewNamedAsync("QA Deflect Target");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        return Array.Empty<byte>();
    }
}
