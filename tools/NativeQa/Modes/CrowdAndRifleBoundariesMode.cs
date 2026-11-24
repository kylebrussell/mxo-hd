using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CrowdAndRifleBoundariesMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, Level = 27;
    private const ushort Multi = 140, Sharp = 148;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Multi, Sharp).Order().ToArray();
    private static readonly (string Handle, int Id, int X, int Z, int Alignment)[] Stands =
    {
        ("QAEnemy", 6114, 17299, Z, 2), ("QAOther", 6113, 17120, 2420, 2), ("QAFriend", 6111, 17210, 2350, 1)
    };

    public override string Name => "ability-parity-crowd-rifle-boundaries";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id IN ({AbilityMessages.AbilityGameObject(Multi)},{AbilityMessages.AbilityGameObject(Sharp)})"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql, CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64,alignment=1 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=20000,healthM=20000 WHERE charId IN (110,6111,6113,6114)",
        "DELETE FROM inventory WHERE charId IN (6111,6113,6114) AND slot BETWEEN 105 AND 107"
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Alignment} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "140,148", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "CSR starts with Rifles equipped on verified outdoor HOME. No movement or bag. /target QAEnemy, then Actions > Weapons > Automatic Rifle on the Free Fire button. Click again if the first click only readies the stance. Use ?cd stop after one shot. Leave 140/148 unloaded. The rifle starts at 2.99 m; both other players remain close but unlinked.",
            "Use the printed QAEnemy helper teleports to 2.99, 3.00 and 3.01 m. Re-target QAEnemy and fire once with the Free Fire button per stand with ?cd stop between. Expect [SHARPSHOOTER OURS] opponents1, metres2.990/3.000/3.010, modifier -15/-15/+15. Then ?qaload 148 and repeat, now 0/0/+15 and countSampledFalse.",
            """Stop Fire!, send the printed QAEnemy CLOSE teleport (1.20 m), then {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. QAOther stands in melee range but does not fight yet. No second opponent in the first rounds. Send {"actor":"QAOther","action":"melee","target":"CSR_Tester"} and wait for the two direct links. Expect [MULTIFIGHTING OURS] opponents2 loadedFalse penalty-10 for both inputs. Type ?qaload 140: both now 0 with exactly two direct opponents; the shipped rule starts its remaining -5 penalty at three. QAFriend remains nearby and unlinked throughout.""",
            "This is a native boundary and direct-link check of current policy. Exact retail CombatMultiOnOneCount and ranged caller binding remain unverified; do not mark either row done from these packets alone."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Boundary NAV data required.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Stand(x, z, 0);
        Console.WriteLine($"READY HOME {Stand(X, Z, 64)}");
        foreach (int x in new[] { 17299, 17300, 17301, 17120 })
            Console.WriteLine($$"""QAEnemy x={{x}} {"actor":"QAEnemy","action":"chat","text":"{{Stand(x, Z, 192)}}"}""");

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => owned.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id)
                .All(parent => owned.TryGetValue(parent.Key, out var row) && row.Level >= parent.Value)),
            "Both passive profiles fit stock rank and memory");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands) await actor.WaitForViewNamedAsync(stand.Handle);
        var enemy = context.Helpers["QAEnemy"];
        await SampleAsync(17299, "2.990", -15);
        await SampleAsync(17300, "3.000", -15);
        await SampleAsync(17301, "3.010", 15);
        await InvisibilityBeltWatcherMode.WarpAsync(enemy, 17120, Y, Z, 192, inside: false);
        int crowd = server.DebugLog().Length;
        foreach (var opponent in new[] { enemy, context.Helpers["QAOther"] })
        {
            var view = await opponent.WaitForViewNamedAsync(actor.Handle);
            string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
            opponent.SendRpc($"40 {handle} 01 6b");
            await opponent.WaitForRpcAsync($"46 {handle} 01");
        }
        await CrowdAsync(crowd, false, -10);
        crowd = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { Multi }, load: true);
        await CrowdAsync(crowd, true, 0);
        return Array.Empty<byte>();

        Task CrowdAsync(int cursor, bool loaded, int penalty) => actor.WaitUntilAsync(() =>
        {
            string log = server.DebugLog()[cursor..];
            string prefix = $"[MULTIFIGHTING OURS] {actor.Handle} opponents2 loaded{loaded} penalty{penalty}";
            return log.Contains(prefix + " attack", StringComparison.Ordinal) && log.Contains(prefix + " defence", StringComparison.Ordinal);
        }, "Exactly two direct attackers with an unlinked witness", TimeSpan.FromSeconds(16));

        async Task SampleAsync(int x, string distance, int expected)
        {
            await InvisibilityBeltWatcherMode.WarpAsync(enemy, x, Y, Z, 192, inside: false);
            var target = await actor.WaitForViewNamedAsync(enemy.Handle);
            string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
            int cursor = server.DebugLog().Length;
            actor.SkipReceived();
            actor.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {handle}"),
                WireFormat.FromHex($"40 {handle} 00 69"));
            await actor.WaitForRpcAsync("4e 00");
            await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains(
                $"defenderGunFalse metres{distance} opponents1 countSampledTrue loaded148False modifier{expected}", StringComparison.Ordinal),
                $"Exact unlinked rifle boundary at {distance} metres", HeadlessPlayer.DefaultTimeout);
            actor.SendChat("?cd stop");
            await actor.WaitForTextAsync("Fixture combat ended.");
            actor.SendRpc("3d 00");
        }
    }

    private static string Stand(int x, int z, byte yaw)
    {
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Mapped open rifle lane at ({x},{Y},{z})");
        return InvisibilityBeltWatcherMode.Warp(x, Y, z, yaw, inside: false);
    }
}
