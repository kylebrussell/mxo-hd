using System.Text;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterDisarmingShotAccuracyMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300, EnemyX = 17200;
    private const ushort Disarm = 126, Handguns = 129, BallisticAccuracy = 524;
    private const string OpenStyle = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";
    private static readonly StarterSoldierResistanceMode.Sample Stock =
        StarterGunmanResistanceMode.Samples.Single(sample => sample.Id == Disarm);
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, 128, Handguns, Disarm }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly Regex TotalsPattern = new(@"\[ATTACK-TOTALS\] melee=(-?\d+) ballistic=(-?\d+) thrown=-?\d+",
        RegexOptions.CultureInvariant);
    private static readonly TimeSpan ManualRun = TimeSpan.FromMinutes(30);

    public override string Name => "ability-parity-starter-disarming-shot-accuracy";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { StarterHackerPassiveFixture.CapRanks }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},rotation=64 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},rotation=192 WHERE charId=6114",
            $"DELETE FROM inventory WHERE charId=6114 AND slot={CombatFixtureWeapons.HandSlot}; INSERT INTO inventory(charId,slot,goid,count,purity) " +
                $"VALUES (6114,{CombatFixtureWeapons.HandSlot},{CombatFixtureWeapons.Handgun},1,0)",
            "DELETE FROM char_abilities WHERE char_id=6114",
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                $"(6114,400,{AbilityMessages.AbilityGameObject(2)},{Level},1),(6114,401,{AbilityMessages.AbilityGameObject(17)},{Level},1)"
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Disarm }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "126", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) on clear outdoor NAV. Both have handguns and stay on the same stands. The helper takes one baseline shot and stops. Wait for DISARM ACCURACY baseline.",
            "Use /target QAEnemy or select the helper, right-click Close Combat, then choose Handguns on page 2 slot 1 and Disarming Shot on slot 2. If it misses, wait 8 s and retry. When it lands, type ?cd stop immediately, within the 10 s effect. Keep the same gun equipped.",
            "Expect [MODIFIER] char=6114 source=126 stat=524 value=-34 through -18 duration=10 s, then its matching removal. The helper fires again during and after the effect. Its real [SHARPSHOOTER OURS] attackBonus before the range rule must drop by exactly the record value and then recover. The [ATTACK-TOTALS] melee control remains unchanged. Wait for DISARM ACCURACY PASS."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, EnemyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Disarming Shot stand ({x},{Y},{Z}) is on outdoor NAV");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Both armed players have clear sight");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(enemy, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor)
        {
            _ = WatchNativeAsync(server, enemy);
            return Array.Empty<byte>();
        }

        StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        Check.That(Memory <= AbilityMemory.ForLevel(Level), "The full handgun profile fits starter memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        var shooterTarget = await enemy.WaitForViewNamedAsync(actor.Handle);
        await VerifyAsync(server, enemy, shooterTarget, async () =>
        {
            uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
            string targetHex = WireFormat.ToHex(BitConverter.GetBytes(handle));
            actor.SkipReceived();
            actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(OpenStyle, handle),
                WireFormat.FromHex($"40 {targetHex} 01 6b"));
            await actor.WaitForRpcAsync($"40 {targetHex} 00 6b");
            await actor.WaitForRpcAsync("47");
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(Stock.StockStyle, handle));
            await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {LittleEndianHex(Handguns)} 00 00");
            for (int attempt = 0; attempt < 6; attempt++)
            {
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(Stock.StockUse, handle));
                try
                {
                    await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Any(packet => Effect(packet) != null),
                        "The affected player receives the ten-second stat 524 record", TimeSpan.FromSeconds(8));
                    actor.SendChat("?cd stop");
                    return;
                }
                catch (TimeoutException) when (attempt < 5) { }
            }
        });
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Stock PvP and modifier framing");
        return Array.Empty<byte>();
    }

    private static async Task WatchNativeAsync(ServerInstance server, HeadlessPlayer enemy)
    {
        try
        {
            var target = await enemy.WaitForViewNamedAsync("CSR_Tester", ManualRun);
            await VerifyAsync(server, enemy, target);
        }
        catch (Exception error)
        {
            Console.WriteLine("DISARM ACCURACY INCOMPLETE: " + error.Message);
        }
    }

    private static async Task VerifyAsync(ServerInstance server, HeadlessPlayer enemy, ViewCreation victim,
        Func<Task>? cast = null)
    {
        string target = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(victim.ViewId, victim.SpawnId)));
        enemy.SendRpc("3f 69 00 10 00 00 00");
        var baseline = await SampleAsync("baseline");
        int start = enemy.World.RpcSnapshot().Count;
        if (cast != null) await cast();
        ManageBonusRecord? record = null;
        await enemy.WaitUntilAsync(() => (record = enemy.World.RpcSnapshot().Skip(start)
            .Select(packet => Effect(packet)).FirstOrDefault(candidate => candidate != null)) != null,
            "Disarming Shot applies typed Ballistic Accuracy", cast == null ? ManualRun : HeadlessPlayer.DefaultTimeout);
        int delta = record!.SignedValue;
        var left = BytePattern.Parse($"46 {target} 00");
        await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(start).Any(packet =>
            left.IsMatch(packet.Bytes)),
            "CSR exits the interlock while the modifier is active", TimeSpan.FromSeconds(9));
        var during = await SampleAsync("debuff");
        var expiry = record with { AddRemoveType = 1 };
        await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(start).Any(packet => Effect(packet, remove: true) == expiry),
            "The affected player receives matching expiry by record ID", TimeSpan.FromSeconds(15));
        var recovered = await SampleAsync("recovered");
        Check.Equal(baseline.Melee, during.Melee, "The typed accuracy input leaves melee unchanged");
        Check.Equal(baseline.Ballistic + delta, during.Ballistic, "The actual outgoing shot consumes the signed 524 record");
        Check.Equal(baseline, recovered, "The same weapon inputs recover after expiry");
        Console.WriteLine($"DISARM ACCURACY PASS 126: ballistic {baseline.Ballistic}->{during.Ballistic}->{recovered.Ballistic}; " +
            $"stat524={delta} for 10s; melee={baseline.Melee} throughout.");
        async Task<(int Ballistic, int Melee)> SampleAsync(string stage)
        {
            enemy.SkipReceived();
            enemy.SendChat("?cd totals");
            var diagnostic = await enemy.WaitForTextAsync("[ATTACK-TOTALS]");
            var totals = TotalsPattern.Match(Encoding.Latin1.GetString(diagnostic.Bytes));
            Check.That(totals.Success, "The affected player returns the typed attack totals");
            int logStart = server.DebugLog().Length;
            enemy.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {target}"),
                WireFormat.FromHex($"40 {target} 00 69"));
            await enemy.WaitForRpcAsync("4e 00");
            await enemy.WaitForRpcAsync("81 68");
            var shot = server.DebugLog()[logStart..].Split('\n')
                .Select(line => SharpshooterFixture.ReadInput(line.TrimEnd('\r'), enemy.Handle)).OfType<SharpshooterFixture.Input>().FirstOrDefault();
            Check.That(shot is { DefenderGun: true, Modifier: 0 }, "The actual PvP shot uses the same armed target without a range penalty");
            Check.Equal(int.Parse(totals.Groups[2].Value), shot!.Before, "The typed input is the actual outgoing shot's input");
            int stopAt = server.DebugLog().Length;
            enemy.SendRpc("3d 00");
            await enemy.WaitUntilAsync(() => server.DebugLog()[stopAt..].Contains($"[PVP] {enemy.Handle} stops firing"),
                "The fixture helper holsters between samples", HeadlessPlayer.DefaultTimeout);
            int melee = int.Parse(totals.Groups[1].Value);
            Console.WriteLine($"DISARM ACCURACY {stage}: QAEnemy ballistic={shot.Before} melee={melee}");
            return (shot.Before, melee);
        }
    }

    private static ManageBonusRecord? Effect(RpcRecord packet, bool remove = false) =>
        ManageBonusRecord.TryDecode(packet.Bytes, out var record) &&
        record is { SourceId: Disarm, TargetId: BallisticAccuracy, Duration: 10, SignedValue: >= -34 and <= -18 } &&
        record.AddRemoveType == (remove ? 1 : 0) ? record : null;
}
