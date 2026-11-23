using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerHighRankPacifyMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, CentreZ = 6700;
    private const ushort Hacker = CombatStyles.HackerStyle, FreezeSystem3 = 473, FreezeArea2 = 479, Freeze1 = 48, Blast = 58;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, FreezeSystem3, FreezeArea2, Freeze1, Blast)
        .Order().ToArray();
    private static readonly (string Name, int Character, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, CentreZ, 2),
        ("QAOther", 6113, X, 7300, 2),
        ("QAFar", 6112, X, 7301, 2),
        ("QAFriend", 6111, X + 100, CentreZ, 1)
    };

    public override string Name => "ability-parity-hacker-high-rank-pacify";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,CentreZ)},innerStrC=700,innerStrM=1000 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Character}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Hacker, FreezeSystem3, FreezeArea2, Blast },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "473,479", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/100 memory. Outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{CentreZ}). QAOther at z7300 is the 6.00 m edge, QAFar at z7301 is 6.01 m, and same-org QAFriend stands x17100,z6700.",
            "On page 2 choose Hacker Style in slot 1, /target QAEnemy, then use Freeze Area 2.0 in slot 3. Keep CSR at the spawn stand. Save the complete 479 request. Expect a 2 s cast, 37 IS (700→663) and 15 s own reuse.",
            "On QAEnemy and QAOther, expect [MODIFIER] source=479 stat=85 for 6.90 s (6 s base with +15% Pacify duration), source=479 stat=157 -80 for 12 s and source=734 stat=681 for 12 s. QAFriend and QAFar stay clear. Let the state and linked state record expire first, then watch for the resistance and immunity removals at twelve seconds.",
            "Once 479 reuse and both recipients' immunity clear, repeat slot 3 on QAEnemy. After landing, use Logic Blast in slot 4. QAEnemy's [TYPED-RESISTANCE OURS] 58 type2 total-80 raw15 final17 consumes the vulnerability. The damage removes 479/85 and 479/157 immediately. Original 734/681 still expires after 12 s, and QAOther keeps its independent records through their own natural expiries."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, x, Y, z) && NavMesh.HasSightPath(1, X, Y, CentreZ, x, Y, z),
                $"Clear sight to ({x},{Y},{z})");
        }

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) &&
                rank >= need.Value)), "Full ranked Archive profile");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        await StockAbilityProfile.ObserveAsync(caster, 0, packet => StockAbilityProfile.Bonus(packet, 391, 1039, 15, 0, false),
            "Effective Code adds the exact 15-point Pacify duration bonus");
        await StockAbilityProfile.ObserveAsync(caster, 0, packet => StockAbilityProfile.Bonus(packet, 471, 1053, 5, 0, false),
            "Attack Programming adds the exact five-point cost reduction bonus");
        var enemy = context.Helpers["QAEnemy"];
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        await HackerSelectedPacifyRanksMode.VerifyRankOnePacifyAsync(caster, enemy,
            DuelPackets.Handle(target.ViewId, target.SpawnId), 6.9f);
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "High-rank Pacify mode and retained 48/58 framing");
        Console.WriteLine("HIGH-RANK PACIFY BOOTSTRAP PASS: legal ranked profile and exact 48/58 control. Native player 479 retest pending.");
        return Array.Empty<byte>();
    }
}
