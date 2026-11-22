using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerViralDebuffCastTargetMode : NativeQaMode
{
    private const int X = 17000, Y = 495, CasterZ = 8200, InZ = 6800, OutZ = 6100, StartingIs = 700;
    private const ushort Lag = 69, Blind = 43, Pacify = 48;
    private const float Reuse = 43;
    private sealed record Sample(ushort Id, ushort Cost, string Request, ushort TargetStat, int Value, float Duration);
    private static readonly Sample[] Samples =
    {
        new(Lag, 10, StarterHackerDotResistanceMode.LagUse, 1157, 35, 30),
        new(Blind, 10, EarlyHackerControlRecoveryMode.BlindRequest, 90, 0, 16),
        new(Pacify, 26, EarlyHackerControlRecoveryMode.PacifyRequest, 85, 0, 6)
    };
    private static readonly ushort[] Abilities = StarterHackerPassiveFixture.Loadout(Lag, Blind, Pacify);

    public override string Name => "ability-parity-starter-hacker-viral-debuff-cast-target";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, CasterZ, Array.Empty<string>(),
        new[] { StarterHackerPassiveFixture.CapRanks }, new[]
        {
            StarterHackerPassiveFixture.PutActor(X),
            $"UPDATE characters SET y={Y},z={CasterZ},rotation={MobAggro.YawTowards(X,CasterZ,X,InZ)} WHERE charId=110",
            StarterHackerPassiveFixture.PutHelper(6114, X, 2, 3000, 3000),
            $"UPDATE characters SET z={InZ} WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, hotbarAbilities: Samples.Select(sample => sample.Id).Prepend(CombatStyles.HackerStyle).ToArray(),
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "43,48,69", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level 19, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(19)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy IN ({X},{Y},{InZ}), 14 m ahead. OUT is ({X},{Y},{OutZ}), 21 m away. All stands are verified outdoor NAV clear of the ice.",
            "Select Hacker Style in page 2 slot 1 and /target QAEnemy. Prepare the OUT helper request and submit it during the 2 s bar for each of slots 2–4 (69/43/48). Return QAEnemy IN between casts. Expect [ABILITY] ID fizzles: the selected viral debuff target changed or left range. Each starts 43 s own reuse and spends 10/10/26 IS. No [EFFECTS], recipient [MODIFIER], or HP loss.",
            $$"""OUT helper request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{OutZ}}}. IN helper request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{InZ}}}. Leave CSR on the original stand.""",
            "Return QAEnemy IN and wait for the slot timers. Slot 2 gives [MODIFIER] char=6114 source=69 stat=1157 value=+35 for 30 s, then five [EFFECTS] ticks for 6. Slot 3 gives source=43 stat=90 value=0 for 16 s. After 69 expires, slot 4 gives source=48 stat=85 value=0 for 6 s, stat=157 value=-20 for 12 s, and source=734 stat=681 for 12 s. This ordering keeps the Pacify resistance debuff out of the DOT sample. Final IS 608."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { CasterZ, InZ, OutZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, X, Y, z), $"Verified outdoor NAV and sight at ({X},{Y},{z})");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(actor, Abilities);
        uint handle = await StarterHackerPassiveFixture.StartAsync(actor, enemy, server, pvp: true);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        int expectedIs = StartingIs;
        var attempts = new Dictionary<ushort, int>();
        foreach (var sample in Samples)
        {
            int logStart = server.DebugLog().Length;
            int cursor = await BeginAsync(sample);
            attempts.Add(sample.Id, cursor);
            await MoveAsync(OutZ);
            await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
                $"[ABILITY] {sample.Id} fizzles: the selected viral debuff target changed or left range"),
                $"Accepted {sample.Id} fizzles when the original recipient moves beyond range", HeadlessPlayer.DefaultTimeout);
            await AssertCostAndReuseAsync(sample, cursor);
            Check.That(!server.DebugLog()[logStart..].Contains($"[EFFECTS] {sample.Id} on QAEnemy:"),
                $"No {sample.Id} effect is added after the fizzle");
            await MoveAsync(InZ);
        }

        int dotLogStart = 0;
        int dotRecipientStart = 0;
        foreach (var sample in Samples)
        {
            await ReuseEndsAsync(sample.Id, attempts[sample.Id]);
            if (sample.Id == Pacify)
            {
                await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(dotRecipientStart).Any(
                    frame => StockAbilityProfile.Bonus(frame, Lag, 1157, 35, 30, true)),
                    "Processor Lag's 30 s record is removed", TimeSpan.FromSeconds(32));
                var tick = new Regex(@"\[EFFECTS\] 69 ticks on QAEnemy for (\d+)");
                int[] damage = tick.Matches(server.DebugLog()[dotLogStart..]).Select(match => int.Parse(match.Groups[1].Value)).ToArray();
                Check.That(damage.SequenceEqual(Enumerable.Repeat(6, 5)), "Processor Lag ticks five times for 6 and then expires");
            }
            int logStart = server.DebugLog().Length;
            int targetStart = enemy.World.RpcSnapshot().Count;
            int cursor = await BeginAsync(sample);
            await AssertCostAndReuseAsync(sample, cursor);
            await StockAbilityProfile.ObserveAsync(enemy, targetStart, frame =>
                StockAbilityProfile.Bonus(frame, sample.Id, sample.TargetStat, sample.Value, sample.Duration, false),
                $"The {sample.Id} positive control has its normal effect and duration");
            if (sample.Id == Lag)
            {
                dotLogStart = logStart;
                dotRecipientStart = targetStart;
            }
            if (sample.Id == Pacify)
            {
                await StockAbilityProfile.ObserveAsync(enemy, targetStart,
                    frame => StockAbilityProfile.Bonus(frame, Pacify, 157, -20, 12, false), "48 sets viral resistance -20 for 12 s");
                await StockAbilityProfile.ObserveAsync(enemy, targetStart,
                    frame => StockAbilityProfile.Bonus(frame, 734, 681, 0, 12, false), "48 applies 12 s control immunity");
            }
        }
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Starter viral debuff targeting wire framing");
        Console.WriteLine("VIRAL DEBUFF CAST TARGET PASS 43/48/69");
        return Array.Empty<byte>();

        async Task<int> BeginAsync(Sample sample)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Request, handle));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, 2);
            return cursor;
        }

        async Task MoveAsync(int z)
        {
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = actor.World.ViewSnapshot().Count;
            enemy.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(actor, cursor, view.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }

        async Task AssertCostAndReuseAsync(Sample sample, int cursor)
        {
            expectedIs -= sample.Cost;
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Contains((ushort)expectedIs), $"Stock {sample.Id} spends {sample.Cost} IS", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, cursor, sample.Id, AbilityCooldowns.OwnTimer, Reuse);
        }

        Task ReuseEndsAsync(ushort id, int cursor)
        {
            byte[] ready = AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, Reuse, start: false);
            return actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Bytes.AsSpan().SequenceEqual(ready)),
                $"{id} reuse ends", TimeSpan.FromSeconds(45));
        }
    }
}
