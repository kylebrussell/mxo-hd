using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSharpshooterCrowdedInterlockMode : NativeQaMode
{
    private const int Level = 19, X = PvpOpponentsFixture.X, Y = PvpOpponentsFixture.Y, Z = PvpOpponentsFixture.Z;
    private const ushort Sharpshooter = 148;
    private const int FriendZ = Z + 140;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Sharpshooter }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-sharpshooter-crowded-interlock";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Sharpshooter)}"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql, CombatFixtureLoadout.SeedWeapons,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},rotation=64 WHERE charId IN (110,6111,6114)",
        $"UPDATE characters SET x={PvpOpponentsFixture.EnemyX},y={Y},z={FriendZ} WHERE charId=6111",
        "DELETE FROM inventory WHERE charId IN (6111,6114) AND slot IN (105,107)"
    }, DevCommands: true, hotbarAbilities: new[] { Sharpshooter }, hotbarWithdraw: true,
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "148", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR ({X},{Y},{Z}) faces unarmed QAEnemy ({PvpOpponentsFixture.EnemyX},{Y},{Z}) at 1.2 m. QAFriend ({PvpOpponentsFixture.EnemyX},{Y},{FriendZ}) joins from the side. All stands are verified outdoor NAV. Pistol pre-equipped on CSR and 148 starts unloaded.",
            """Send {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. Let one ordinary exchange follow Round 0 with no abilities or Withdraw. Expect [PVP] and [SHARPSHOOTER INTERLOCK] loaded148=False attack=-5 defence=-5 for CSR only.""",
            """Keep the fight active. Send {"actor":"QAFriend","action":"melee","target":"QAEnemy"} to join it. Expect opponents=2, attack=-20 defence=-20 on the same CSR pair, including the extra -15 for firing into a melee.""",
            "While all three are still interlocked, type ?qaload 148. Expect attack=0 defence=0 with opponents=2. Then ?qaunload 148 for -20/-20 again, still with both opposing links. Use ?cd stop after the sample. No max [VITALS] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (PvpOpponentsFixture.EnemyX, Z), (PvpOpponentsFixture.EnemyX, FriendZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, PvpOpponentsFixture.EnemyX, Y, Z, x, Y, z), $"Verified outdoor stand ({x},{Y},{z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, PvpOpponentsFixture.EnemyX, Y, Z), "Verified unobstructed short-range path");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await PvpOpponentsFixture.EnterAsync(friend);
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
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

        int from = server.DebugLog().Length;
        enemy.SendRpc($"40 {handle} 01 6b");
        await enemy.WaitForRpcAsync($"46 {handle} 01");
        await ExchangeAsync(from, false, -5, 1);
        var target = await friend.WaitForViewNamedAsync(enemy.Handle);
        await actor.WaitForViewNamedAsync(friend.Handle);
        await friend.WaitForViewNamedAsync(actor.Handle);
        string joinHandle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
        from = server.DebugLog().Length;
        friend.SendRpc($"40 {joinHandle} 01 6b");
        await friend.WaitForRpcAsync($"46 {joinHandle} 01");
        await ExchangeAsync(from, false, -20, 2);

        from = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { Sharpshooter }, load: true);
        await ExchangeAsync(from, true, 0, 2);
        from = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { Sharpshooter }, load: false);
        await ExchangeAsync(from, false, -20, 2);
        actor.SendChat("?cd stop");
        await actor.WaitForTextAsync("Fixture combat ended.");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count,
            "Ordinary crowded interlock, join and loadout request framing");
        return Array.Empty<byte>();

        Task ExchangeAsync(int from, bool loadedNow, int penalty, int opponents) =>
            enemy.WaitUntilAsync(() => server.DebugLog()[from..].Contains(
                $"[SHARPSHOOTER INTERLOCK] {actor.Handle} loaded148={loadedNow} attack={penalty} defence={penalty} opponents={opponents}",
                StringComparison.Ordinal), "The normal round reads live opposing links and both gun inputs", TimeSpan.FromSeconds(14));
    }
}
