using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterOperativeSittingMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300;
    private const ushort Operative = 143, Upgrade = 1060, Sitting = 671;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Operative }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly (ushort Stat, int Value)[] StanceStats =
        { (1009, -200), (114, 200), (1010, -200), (112, 300), (1011, -200), (183, -200) };

    public override string Name => "ability-parity-starter-operative-sitting";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=64,healthC=500,healthM=2000,innerStrC=100,innerStrM=5000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Operative }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "143,1060", ["NativeCloseableRows"] = 1,
            ["NativeCloseableIds"] = "143", ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, CSR ({X},{Y},{Z}) on verified outdoor NAV, no target or helper. Start below the HP and IS caps. Page 2 identifies Operative. At login expect [MODIFIER] 143/526 +50 and Actions > Operative Upgrade 1060. Standing updates are +40 HP and +8 IS per two seconds.",
            "Type /sit. Expect [EMOTE] stance 0a, six 671 modifier adds, including 112 +300 Health Regen and 114 +200 IS Regen. Observe +180 HP and +24 IS per tick.",
            "While still sitting, type ?qaunload 143. Expect only the 526 removal and 1060 grant removal. The 671 records stay, and recovery becomes +160 HP and +24 IS. Use ?health 500 between samples if near max HP.",
            "Type ?qaload 143 while seated. Expect 526 +50 and 1060 restored, with +180 HP and +24 IS per tick. Type /stand. Expect six 671 removals, then +40 HP and +8 IS. This fixture changes no maximum vitals while switching posture and starts no interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var point) && !point.indoors && Math.Abs(point.floorY + 95 - Y) < 1,
            "CSR stands outside on the verified NAV floor");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "The complete Operative prerequisite profile is loaded");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await OperativeRecordsAsync(0, remove: false);
        await RecoveryAsync(40, 8);

        int seated = actor.World.RpcSnapshot().Count;
        actor.SendRpc("34 0a"); // Stock /sit, ProxyM2_m2_packets6 line 69362.
        await StanceRecordsAsync(seated, remove: false);
        await RecoveryAsync(180, 24);

        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Operative }, load: false);
        await OperativeRecordsAsync(unloaded, remove: true);
        await RecoveryAsync(160, 24);

        int reloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Operative }, load: true);
        await OperativeRecordsAsync(reloaded, remove: false);
        await RecoveryAsync(180, 24);

        int standing = actor.World.RpcSnapshot().Count;
        actor.SendRpc("33 0a"); // Stock /stand continuation of stance 0a.
        await StanceRecordsAsync(standing, remove: true);
        await RecoveryAsync(40, 8);
        Check.Equal(0, actor.World.Problems.Count, "Stock sitting and loadout framing");
        return Array.Empty<byte>();

        async Task OperativeRecordsAsync(int cursor, bool remove)
        {
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Operative, 526, 50, 0, remove),
                $"Operative sitting bonus, remove={remove}");
            await StockAbilityProfile.ObserveAsync(actor, cursor, StockAbilityProfile.GrantRecord(Upgrade, remove),
                $"Operative Upgrade grant, remove={remove}");
        }

        async Task StanceRecordsAsync(int cursor, bool remove)
        {
            foreach (var (stat, value) in StanceStats)
                await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Sitting, stat, value, 0, remove),
                    $"Sitting modifier {stat}, remove={remove}");
        }

        async Task RecoveryAsync(int hp, int strength)
        {
            int viewAt = actor.World.ViewSnapshot().Count;
            actor.SendChat("?health 500");
            await actor.WaitUntilAsync(() =>
            {
                var frames = actor.World.ViewSnapshot();
                var health = PvpOpponentsFixture.HealthHistory(frames, viewAt, 2, self: true);
                var inner = AwakenedPowerShotMode.InnerStrengthUpdates(frames.Skip(viewAt));
                return TwoSteps(health, hp) && TwoSteps(inner, strength);
            }, $"Two ordinary updates of +{hp} HP and +{strength} IS", TimeSpan.FromSeconds(10));
        }

        static bool TwoSteps(IEnumerable<ushort> values, int step) =>
            values.Zip(values.Skip(1), (before, after) => after - before).Count(delta => delta == step) >= 2;
    }
}
