using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSharpshooterCloseInterlockMode : NativeQaMode
{
    private const int Level = 19, X = PvpOpponentsFixture.X, Y = PvpOpponentsFixture.Y, Z = PvpOpponentsFixture.Z;
    private const ushort Sharpshooter = 148;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Sharpshooter }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-sharpshooter-close-interlock";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Sharpshooter)}"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql, CombatFixtureLoadout.SeedWeapons,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},rotation=64 WHERE charId IN (110,6114)",
        "DELETE FROM inventory WHERE charId=6114 AND slot IN (105,107)"
    }, DevCommands: true, hotbarAbilities: new[] { Sharpshooter }, hotbarWithdraw: true,
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "148", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR ({X},{Y},{Z}) faces unarmed QAEnemy ({PvpOpponentsFixture.EnemyX},{Y},{Z}) at 1.2 m on verified outdoor NAV. CSR starts with a pistol equipped. Leave both weapons and tactics alone. Sharpshooter 148 is owned and starts unloaded.",
            """Send {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. Let one ordinary exchange follow Round 0 with no abilities or Withdraw. Expect [PVP] and [SHARPSHOOTER INTERLOCK] loaded148=False attack=-5 defence=-5 for CSR only.""",
            "Type ?cd stop and wait for the combat HUD to clear. Type ?qaload 148 and repeat the same helper melee. Expect loaded148=True attack=0 defence=0. Then stop, ?qaunload 148, repeat and confirm -5/-5 returns. No max [VITALS] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, PvpOpponentsFixture.EnemyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1,
                $"Verified outdoor stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, PvpOpponentsFixture.EnemyX, Y, Z), "Verified unobstructed short-range path");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            owned.ContainsKey(Sharpshooter) && !loaded.ContainsKey(Sharpshooter) &&
            Loadout.Where(id => id != Sharpshooter).All(loaded.ContainsKey) &&
            AbilityPrerequisites.Current.Closure(Sharpshooter).All(need =>
                loaded.TryGetValue(need.Key, out var row) && row.Level >= need.Value), "Sharpshooter is archived with every qualified prerequisite loaded");

        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await enemy.WaitForViewNamedAsync(actor.Handle);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));

        await ExchangeAsync(false, -5);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { Sharpshooter }, load: true);
        await ExchangeAsync(true, 0);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { Sharpshooter }, load: false);
        await ExchangeAsync(false, -5);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Ordinary close-round and loadout request framing");
        return Array.Empty<byte>();

        async Task ExchangeAsync(bool loadedNow, int penalty)
        {
            int from = server.DebugLog().Length;
            enemy.SkipReceived();
            enemy.SendRpc($"40 {handle} 01 6b");
            await enemy.WaitForRpcAsync($"46 {handle} 01");
            await enemy.WaitUntilAsync(() => server.DebugLog()[from..].Contains(
                $"[SHARPSHOOTER INTERLOCK] {actor.Handle} loaded148={loadedNow} attack={penalty} defence={penalty}", StringComparison.Ordinal),
                "The normal round reads the current passive and both short-range inputs", TimeSpan.FromSeconds(12));
            actor.SendChat("?cd stop");
            await actor.WaitForTextAsync("Fixture combat ended.");
            await enemy.WaitForRpcAsync($"46 {handle} 00");
        }
    }
}
