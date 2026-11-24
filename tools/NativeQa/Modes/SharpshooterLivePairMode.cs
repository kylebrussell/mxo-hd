using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class SharpshooterLivePairMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, Level = 27;
    private const ushort Sharp = 148;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Sharp).Order().ToArray();
    private static readonly (string Handle, int Id, int X, int Z, byte Yaw, int Alignment)[] Helpers =
    {
        ("QAEnemy", 6114, 17400, Z, 64, 2), ("QAOther", 6113, 17520, Z, 192, 1)
    };

    public override string Name => "ability-parity-sharpshooter-live-pair";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        StarterHackerPassiveFixture.CapRanks,
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Sharp)}"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql, CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64,alignment=1 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=20000,healthM=20000 WHERE charId IN (110,6113,6114)",
        "DELETE FROM inventory WHERE charId IN (6113,6114) AND slot BETWEEN 105 AND 107"
    }.Concat(Helpers.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},rotation={stand.Yaw},alignment={stand.Alignment} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new ushort[] { 17 }, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "148", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "No teleport needed. CSR starts on outdoor HOME facing QAEnemy, 4 m away, with rifle 692 equipped. Both helpers are visible. /target QAEnemy. Use Actions > Weapons > Automatic Rifle on the Free Fire button. If the first click only readies the stance, click again. Avoid radial Fire! and Close Combat. Stop each sample with ?cd stop and wait for the HUD to clear.",
            "Fire before the helpers fight. [SHARPSHOOTER OURS] actor=CSR_Tester class55 defenderGunFalse metres4.000 opponents1 countSampledTrue loaded148False modifier15. Shot hit or miss is immaterial.",
            """Send {"actor":"QAOther","action":"melee","target":"QAEnemy"}. Wait for their [PVP] round. Fire from the same stand on QAEnemy. Expect opponents2 and modifier0. Type ?qaload 148 and repeat during the same helper fight: countSampledFalse loaded148True modifier15. Then ?qaunload 148 and repeat for opponents2 modifier0.""",
            """Send {"actor":"QAOther","action":"stop"} and {"actor":"QAEnemy","action":"stop"}. Wait for their interlock to end. Both helpers stay visible. Repeat the same Free Fire shot: opponents1 and modifier15 again.""",
            "Setup aids are pre-equipped rifle, seeded helper HP, disabled ambient aggro, dev loadout changes and fixture stop after each sample. Both helpers open and withdraw normally. No hit-roll aid is needed. The original engine's distance/classifier bridge remains unknown."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 64, inside: false)}");
        var server = context.ActualServer!;
        foreach (var stand in Helpers)
        {
            Console.WriteLine($"READY {stand.Handle} {InvisibilityBeltWatcherMode.Warp(stand.X, Y, stand.Z, stand.Yaw, inside: false)}");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, stand.X, Y, stand.Z), $"Open sight to {stand.Handle}");
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout.Where(id => id != Sharp).ToArray(), Sharp);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Sharpshooter profile fits memory");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        var enemy = context.Helpers["QAEnemy"];
        var other = context.Helpers["QAOther"];
        string target = await GetHandleAsync(actor, enemy);
        string opponent = await GetHandleAsync(other, enemy);
        await actor.WaitForViewNamedAsync(other.Handle);
        await ShotAsync(1, true, false, 15);
        other.SendRpc($"40 {opponent} 01 6b");
        await other.WaitForRpcAsync($"46 {opponent} 01");
        await ShotAsync(2, true, false, 0);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Sharp }, load: true);
        await ShotAsync(0, false, true, 15);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Sharp }, load: false);
        await ShotAsync(2, true, false, 0);
        int from = server.DebugLog().Length;
        other.SendRpc("44 00");
        enemy.SendRpc("44 00");
        await actor.WaitUntilAsync(() => server.DebugLog()[from..].Contains("Close combat between QAOther and QAEnemy ends:", StringComparison.Ordinal),
            "Ordinary helper interlock teardown", TimeSpan.FromSeconds(35));
        await ShotAsync(1, true, false, 15);
        Console.WriteLine("SHARPSHOOTER BOOTSTRAP PASS: visible bystander, live unrelated interlock, load/unload and ordinary teardown.");
        return Array.Empty<byte>();

        async Task ShotAsync(int count, bool sampled, bool loaded, int modifier)
        {
            int cursor = server.DebugLog().Length;
            actor.SkipReceived();
            actor.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {target}"),
                WireFormat.FromHex($"40 {target} 00 69"));
            await actor.WaitForRpcAsync("4e 00");
            string record = $"actor={actor.Handle} class55 defenderGunFalse metres4.000 opponents{count} countSampled{sampled} loaded148{loaded} modifier{modifier} ";
            await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains(record, StringComparison.Ordinal),
                $"Rifle shot input: {record}", HeadlessPlayer.DefaultTimeout);
            actor.SendChat("?cd stop");
            await actor.WaitForTextAsync("Fixture combat ended.");
            actor.SendRpc("3d 00");
        }
    }

    private static async Task<string> GetHandleAsync(HeadlessPlayer observer, HeadlessPlayer target)
    {
        var view = await observer.WaitForViewNamedAsync(target.Handle);
        return WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
    }
}
