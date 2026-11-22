using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerViralBaseMode : NativeQaMode
{
    private const int Level = StarterHackerPassiveFixture.Level;
    private const int X = StarterHackerPassiveFixture.X, Y = StarterHackerPassiveFixture.Y, Z = StarterHackerPassiveFixture.Z;
    private const int EnemyX = X + 400, SplashX = X + 600;
    private const int InitialIs = 700;
    private const ushort Style = CombatStyles.HackerStyle, TransmitVirus = 95, Family = 682;
    private const ushort StyleRank = 2, TransmitRank = 6;

    internal sealed record Sample(ushort Id, int BaseDamage, int Cost, int Cast, int Reuse, int SharedReuse, string Capture);

    internal static readonly Sample[] Samples =
    {
        new(57, 8, 5, 2, 4, 4, StarterHackerViralSightMode.Samples[0].Capture),
        new(53, 12, 18, 4, 8, 4, HarmfulCodeMode.StockViralUse),
        new(58, 11, 6, 2, 4, 4, StarterHackerViralSightMode.Samples[1].Capture),
        new(59, 17, 7, 2, 4, 4, StarterHackerViralSightMode.Samples[2].Capture),
        new(60, 7, 11, 4, 2, 2, StarterHackerViralSightMode.Samples[3].Capture)
    };

    private static readonly ushort[] Loadout = StarterHackerPassiveFixture.Loadout(Samples.Select(sample => sample.Id).ToArray());
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-hacker-viral-base";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            StarterHackerPassiveFixture.CapRanks,
            $"UPDATE char_abilities SET level={StyleRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Style)}",
            $"UPDATE char_abilities SET level={TransmitRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(TransmitVirus)}"
        },
        new[]
        {
            StarterHackerPassiveFixture.PutActor(EnemyX),
            StarterHackerPassiveFixture.PutHelper(6114, EnemyX, 2, 3000, 3000),
            StarterHackerPassiveFixture.PutHelper(6112, SplashX, 2, 3000, 3000),
            "DELETE FROM char_abilities WHERE char_id IN (6112,6114)"
        }, DevCommands: true, hotbarAbilities: Samples.Select(sample => sample.Id).Prepend(Style).ToArray(),
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "57,53,58,59,60", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}; {Memory}/{AbilityMemory.ForLevel(Level)} memory with all prerequisites. On outdoor NAV floor 400 CSR stands at ({X},{Y},{Z}) facing QAEnemy ({EnemyX},{Y},{Z}). QAFar ({SplashX},{Y},{Z}) is 2 m past QAEnemy. The fixture holds IS regen and fixes only the eligible hit contest.",
            "Select Hacker Style on page 2 slot 1 and /target QAEnemy. [MODIFIER] source=1006 stat=172 adds +4 and source=95 stat=1031 adds +1, both 0 s. Their combined +5% does not change the five rounded test values.",
            "Press page 2 slots 2 through 6 in order, leaving QAEnemy selected. Wait for group 682 to finish before each next cast. [PVP] landed damage on QAEnemy is 8, 12, 11, 17 and 7. Only 60 hits QAFar too, also for 7. No interlock rounds.",
            "[ABILITY-COST] spends 5, 18, 6, 7 and 11 IS, giving 700 to 695 to 677 to 671 to 664 to 653. Cast / own / shared seconds are 2/4/4, 4/8/4, 2/4/4, 2/4/4 and 4/2/2. This isolates the sampled fixed base; the original retail resistance and contest coefficients are outside this check."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerPassiveFixture.CheckNav(context, EnemyX);
        Check.That(NavMesh.Locate(1, SplashX, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
            NavMesh.HasSightPath(1, EnemyX, Y, Z, SplashX, Y, Z), "Code Nuke's secondary helper is on clear outdoor NAV");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        var splash = await HeadlessPlayer.LoginAsync(context.Address, "QAFar", "QAFar", stockCharacterLoad: true);
        context.Helpers.Add(splash.Handle, splash);
        await PvpOpponentsFixture.EnterAsync(splash);
        NativeQaReadiness.RequireNoAmbientAggro(server, splash);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        Check.Equal(StyleRank, archive[Style].Level, "The complete stock profile keeps Hacker Style at rank 2");
        Check.Equal(TransmitRank, archive[TransmitVirus].Level, "The complete stock profile keeps Transmit Virus at rank 6");
        int styleAt = actor.World.RpcSnapshot().Count;
        uint handle = await StarterHackerPassiveFixture.StartAsync(actor, enemy, server, pvp: true);
        await actor.WaitForViewNamedAsync(splash.Handle);
        await StarterHackerPassiveFixture.RecordsAsync(actor, styleAt, Style, new (ushort, int)[] { (172, 4) });
        await StarterHackerPassiveFixture.RecordsAsync(actor, 0, TransmitVirus, new (ushort, int)[] { (1031, 1) });

        int strength = InitialIs;
        foreach (var sample in Samples)
        {
            int castAt = actor.World.RpcSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            int mainAt = enemy.World.RpcSnapshot().Count;
            int splashAt = splash.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, handle));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, sample.Cast);
            await EvidenceRetestWire.TimerAsync(actor, castAt, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            await EvidenceRetestWire.TimerAsync(actor, castAt, Family, AbilityCooldowns.GroupTimer, sample.SharedReuse);
            await HitAsync(enemy, mainAt, sample);
            if (sample.Id == 60) await HitAsync(splash, splashAt, sample);
            strength -= sample.Cost;
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewAt)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)strength),
                $"The stock cast of {sample.Id} spends {sample.Cost} IS", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, castAt, Family, AbilityCooldowns.GroupTimer, sample.SharedReuse, ended: true);
            if (sample.Id != 60)
            {
                byte[] damagePrefix = AbilityMessages.Damage(false, sample.Id, actor.Handle, splash.Handle, 0, 0)[..^8];
                Check.That(!splash.World.RpcSnapshot().Skip(splashAt).Any(frame => frame.Bytes.AsSpan().StartsWith(damagePrefix)),
                    $"Single-target cast {sample.Id} leaves the secondary helper unharmed");
            }
        }
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count + splash.World.Problems.Count,
            "The starter viral base comparison retains stock packet framing");
        return Array.Empty<byte>();

        Task<RpcRecord> HitAsync(HeadlessPlayer victim, int from, Sample sample)
        {
            byte[] expected = AbilityMessages.Damage(false, sample.Id, actor.Handle, victim.Handle, sample.BaseDamage, 0);
            return StockAbilityProfile.ObserveAsync(victim, from, frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                $"The low-influence {sample.Id} hit on {victim.Handle} is {sample.BaseDamage}");
        }
    }
}
