using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSoldierCrowdModifiersMode : NativeQaMode
{
    private const int Level = 19, X = PvpOpponentsFixture.X, Y = PvpOpponentsFixture.Y, Z = PvpOpponentsFixture.Z;
    private const ushort MultiFighting = 140, Sharpshooter = 148;
    private static readonly (string Name, uint Id, int X, int Z)[] Opponents =
    {
        ("QAEnemy", 6114, X + 120, Z),
        ("QAFriend", 6111, X + 120, Z + 140),
        ("QAFar", 6112, X + 200, Z)
    };
    private static readonly ushort[] Probes = { MultiFighting, Sharpshooter };
    private static readonly ushort[] Loadout = Probes.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-soldier-crowd-modifiers";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id IN ({string.Join(",", Probes.Select(AbilityMessages.AbilityGameObject))})"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql,
        CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
        $"UPDATE characters SET alignment=2,x={X},y={Y},z={Z},rotation=64 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=20000,healthM=20000 WHERE charId IN (110,6114,6111,6112)",
        "DELETE FROM inventory WHERE charId IN (6114,6111,6112) AND slot IN (105,107)"
    }.Concat(Opponents.Select(p =>
        $"UPDATE characters SET alignment=1,x={p.X},y={Y},z={p.Z},rotation=192 WHERE charId={p.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: Probes, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "140,148", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR ({X},{Y},{Z}) faces unarmed QAEnemy ({Opponents[0].X},{Y},{Opponents[0].Z}). QAFriend ({Opponents[1].X},{Y},{Opponents[1].Z}) and QAFar ({Opponents[2].X},{Y},{Opponents[2].Z}) stand beside and behind QAEnemy. All four stands are verified outdoor NAV. CSR starts with a rifle and both passives unloaded. Do not double-click a target, which starts free fire.",
            """Send {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. After Round 0, expect [SHARPSHOOTER INTERLOCK] attack=-15 defence=-15 opponents=1. Then have QAFriend and QAFar send the same helper action, directly on CSR_Tester. No CSR targeting click is required.""",
            "With all three still fighting CSR, expect [MULTIFIGHTING OURS] opponents3 loadedFalse penalty-15 on attack and defence, and [SHARPSHOOTER INTERLOCK] loaded148=False attack=-15 defence=-15 opponents=1. The opponents of each of the three helpers consist of CSR alone.",
            "Type ?qaload 140. Expect multi penalty -5 in the same fight and unchanged rifle penalty. Type ?qaload 148. Expect the rifle penalty 0/0 and multi penalty still -5. Then type ?qaunload 140 and ?qaunload 148. Expect -15 for both again. No max [VITALS] changes. Stop the fixture when finished."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Opponents.Select(p => (p.X, p.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified outdoor Soldier stand ({x},{Y},{z})");

        var server = context.ActualServer!;
        foreach (var opponent in Opponents)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, opponent.Name, opponent.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var owned = AbilityMemoryUiFixture.Decode(caster.CharacterData[7]).Where(e => AbilityMemory.IsAbility(e.Descriptor))
            .ToDictionary(e => AbilityCatalog.IdOf(e.Descriptor));
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(e => AbilityMemory.IsAbility(e.Descriptor))
            .ToDictionary(e => AbilityCatalog.IdOf(e.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Probes.All(id => owned.ContainsKey(id) && !loaded.ContainsKey(id)) &&
            Loadout.Except(Probes).All(id => loaded.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id)
                .All(need => loaded.TryGetValue(need.Key, out var row) && row.Level >= need.Value)) &&
            Probes.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => loaded.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Both passives fit alongside all loaded prerequisites");

        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        int cursor = server.DebugLog().Length;
        foreach (var helper in context.Helpers.Values)
        {
            await caster.WaitForViewNamedAsync(helper.Handle);
            var target = await helper.WaitForViewNamedAsync(caster.Handle);
            string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
            helper.SendRpc($"40 {handle} 01 6b");
            await helper.WaitForRpcAsync($"46 {handle} 01");
        }
        await RoundAsync(cursor, false, -15, false, -15);
        cursor = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(caster, owned, new[] { MultiFighting }, load: true);
        await RoundAsync(cursor, true, -5, false, -15);
        cursor = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(caster, owned, new[] { Sharpshooter }, load: true);
        await RoundAsync(cursor, true, -5, true, 0);
        cursor = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(caster, owned, Probes, load: false);
        await RoundAsync(cursor, false, -15, false, -15);
        Check.Equal(0, context.Helpers.Values.Append(caster).Sum(player => player.World.Problems.Count), "Crowded rifle interlock framing");
        return Array.Empty<byte>();

        Task RoundAsync(int from, bool multiLoaded, int multi, bool sharpLoaded, int sharp) =>
            caster.WaitUntilAsync(() =>
            {
                string log = server.DebugLog()[from..];
                string prefix = $"[MULTIFIGHTING OURS] {caster.Handle} opponents3 loaded{multiLoaded} penalty{multi}";
                return log.Contains(prefix + " attack", StringComparison.Ordinal) &&
                    log.Contains(prefix + " defence", StringComparison.Ordinal) &&
                    log.Contains($"[SHARPSHOOTER INTERLOCK] {caster.Handle} loaded148={sharpLoaded} attack={sharp} defence={sharp} opponents=1",
                        StringComparison.Ordinal);
            }, $"Separate Multi-Fighting ({multi}) and Sharpshooter ({sharp}) inputs", TimeSpan.FromSeconds(16));
    }
}
