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
        ("QAOther", 6113, X, 7100, 2),
        ("QAFar", 6112, X, 7101, 2),
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
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/100 memory. Outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{CentreZ}). QAOther at z7100 is the 4.00 m edge, QAFar at z7101 is 4.01 m, and same-org QAFriend stands x17100,z6700. No ambient aggro.",
            "Select Hacker Style on page 2. Try slots 2 and 3 empty and targeting QAFriend; expect local refusal or reason 24 with no debit. Move QAEnemy to z7801 and try both for reason 34. At z7601 try only slot 2 (12.01 m). Send IN and reselect QAEnemy.",
            $$"""Helper move example {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":7801}. IN is z{{CentreZ}}. Keep CSR at the spawn stand.""",
            "Slot 2 Freeze System 3.0 (473), first full stock request. Expect 2 s, 40 IS (700→660) and 15 s own reuse. QAEnemy and QAOther only get [MODIFIER] source473 stat85 for 4.60 s and stat157 -80 for 8 s. Independent source734/stat681 lasts 8 s; record the natural state remove before the two 8 s removals.",
            "After both 8 s records clear, move QAOther to z7300 and QAFar to z7301 (6.00/6.01 m). Slot 3 Freeze Area 2.0 (479), first full stock request. Expect 2 s, 37 IS (660→623) and 15 s own reuse. Current archived-value behavior adds stat85 for about 11.523 s, stat157 -100 for 12 s and independent source734/stat681 for 12 s on QAEnemy and QAOther only, then the three matched removes. Authored base Pacify is 6 s and its resistance is -80; the captured live magnitude context remains open.",
            "When 473 is ready again and recipient immunity has cleared, repeat slot 2 on QAEnemy and use Logic Blast (slot 4) as soon as it lands. On QAEnemy the actual damage breaks 473/85 and its linked 473/157 while the original 734/681 retains the 8 s lifetime. Wait on the same stand without entering interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z))
            .Concat(new[] { 7300, 7301, 7601, 7801 }.Select(z => (X, z))))
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
        Console.WriteLine("HIGH-RANK PACIFY BOOTSTRAP PASS: legal ranked profile and exact 48/58 control. Native 473 and 479 activations pending.");
        return Array.Empty<byte>();
    }
}
