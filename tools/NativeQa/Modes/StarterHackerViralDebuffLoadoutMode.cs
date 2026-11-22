using System.Diagnostics;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerPassiveFixture;

namespace NativeQa.Modes;

public sealed class StarterHackerViralDebuffLoadoutMode : NativeQaMode
{
    private const int EnemyX = X + 400, StartingIs = 700;
    private const float Reuse = 43;
    private const ushort Blind = 43, Pacify = 48, Lag = 69;
    private sealed record Bonus(ushort Source, ushort Stat, int Value, float Seconds);
    private sealed record Sample(ushort Id, ushort Cost, string Request, Bonus[] Effects);
    private static readonly Sample[] Samples =
    {
        new(Blind, 10, EarlyHackerControlRecoveryMode.BlindRequest, new[] { new Bonus(Blind, 90, 0, 16) }),
        new(Pacify, 26, EarlyHackerControlRecoveryMode.PacifyRequest, new[]
        {
            new Bonus(Pacify, 85, 0, 6), new Bonus(Pacify, 157, -20, 12), new Bonus(734, 681, 0, 12)
        }),
        new(Lag, 10, StarterHackerDotResistanceMode.LagUse, new[] { new Bonus(Lag, 1157, 35, 30) })
    };
    private static readonly ushort[] Rows = Samples.Select(sample => sample.Id).ToArray();
    private static readonly ushort[] Abilities = Loadout(Rows);

    public override string Name => "ability-parity-starter-hacker-viral-debuff-loadout";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { CapRanks }, new[]
        {
            PutActor(EnemyX),
            PutHelper(6114, EnemyX, 2, 3000, 3000),
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, hotbarAbilities: Rows.Prepend(Style).ToArray(), testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "43,48,69", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) on verified clear outdoor NAV, away from the ice. Select Hacker Style in page 2 slot 1 and /target QAEnemy.",
            "Enter ?qaunload 43, ?qaunload 48 and ?qaunload 69 as three separate commands. Try page 2 slots 2–4. Stock may locally gate or clear the shortcut. If sent, expect a single 80 ba reason 7 with no cast, use timer or IS debit. No [EFFECTS] or [MODIFIER] for 43/48/69.",
            "Enter ?qaload 43, ?qaload 48 and ?qaload 69 separately. If stock cleared the shortcut, use the restored Actions entry. Slot 2 Disrupt Inputs: 2 s cast, 43 s reuse, [ABILITY-COST] 10 IS (700 to 690), [MODIFIER] 6114 source=43 stat=90 value=0 for 16 s. After the add, enter ?qaunload 43. The Blind record removes at expiry.",
            "Slot 3 Freeze System: 2 s cast, 43 s reuse, 26 IS (690 to 664). On char=6114, source=48 stat=85 value=0 for 6 s and stat=157 value=-20 for 12 s, source=734 stat=681 value=0 for 12 s. After the adds, enter ?qaunload 48 while Pacify is still up. The three records remove on their own timers.",
            "After 48's resistance modifier expires, slot 4 Processor Lag: 2 s cast, 43 s reuse, 10 IS (664 to 654), source=69 stat=1157 value=+35 for 30 s. After it adds, enter ?qaunload 69. Expect five [EFFECTS] ticks for 6 total during that DOT lifetime, followed by removal. Reload the three again to restore the fixture slots."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        CheckNav(context, EnemyX);
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var profile = VerifyProfile(caster, Abilities);
        uint handle = await StartAsync(caster, enemy, server, pvp: true);
        int targetStart = enemy.World.RpcSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, Rows, load: false);
        foreach (Sample sample in Samples)
            await RefuseAsync(sample);
        Check.That(!Samples.SelectMany(sample => sample.Effects).Any(bonus => HasBonus(targetStart, bonus, remove: false)),
            "An unloaded cast applies none of the selected effects");
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, Rows, load: true);

        int expectedIs = StartingIs;
        foreach (Sample sample in Samples)
        {
            int castCursor = caster.World.RpcSnapshot().Count;
            int logStart = server.DebugLog().Length;
            targetStart = enemy.World.RpcSnapshot().Count;
            caster.SkipReceived();
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Request, handle));
            await EvidenceRetestWire.CastBarAsync(caster, sample.Id, 2);
            await EvidenceRetestWire.TimerAsync(caster, castCursor, sample.Id, AbilityCooldowns.OwnTimer, Reuse);
            expectedIs -= sample.Cost;
            await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Contains((ushort)expectedIs), $"The reloaded {sample.Id} spends {sample.Cost} IS", HeadlessPlayer.DefaultTimeout);
            foreach (Bonus bonus in sample.Effects)
                await StockAbilityProfile.ObserveAsync(enemy, targetStart, frame =>
                    StockAbilityProfile.Bonus(frame, bonus.Source, bonus.Stat, bonus.Value, bonus.Seconds, false),
                    $"Reloaded {sample.Id} adds {bonus.Source}/{bonus.Stat}");
            var lifetime = Stopwatch.StartNew();

            await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, new[] { sample.Id }, load: false);
            await RefuseAsync(sample);
            int barrier = enemy.World.RpcSnapshot().Count;
            enemy.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(enemy, barrier, frame => frame.Opcode == 0x149, "The target stream advances past unload");
            Check.That(!sample.Effects.Any(bonus => HasBonus(targetStart, bonus, remove: true)),
                $"Unloading {sample.Id} leaves each previously applied effect active");

            foreach (Bonus bonus in sample.Effects)
            {
                await enemy.WaitUntilAsync(() => HasBonus(targetStart, bonus, remove: true), $"Natural expiry of {bonus.Source}/{bonus.Stat}",
                    TimeSpan.FromSeconds(bonus.Seconds + 3));
                Check.That(Math.Abs(lifetime.Elapsed.TotalSeconds - bonus.Seconds) < 1.5,
                    $"{bonus.Source}/{bonus.Stat} expires at its own {bonus.Seconds} s boundary despite the unload");
            }
            if (sample.Id == Lag)
            {
                var tick = new Regex(@"\[EFFECTS\] 69 ticks on QAEnemy for (\d+)");
                int[] damage = tick.Matches(server.DebugLog()[logStart..]).Select(match => int.Parse(match.Groups[1].Value)).ToArray();
                Check.That(damage.SequenceEqual(Enumerable.Repeat(6, 5)), "The 30 s DOT keeps all five six-point ticks after unload");
            }
        }
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, Rows, load: true);
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Viral debuff loadout wire framing");
        Console.WriteLine("VIRAL DEBUFF LOADOUT PASS 43/48/69");
        return Array.Empty<byte>();

        Task<RpcRecord> RefuseAsync(Sample sample) => StarterHackerControlTargetMode.RefusedAsync(caster, sample.Id,
            StarterHackerControlTargetMode.WithHandle(sample.Request, handle), AbilityRefusals.NotLoaded);

        bool HasBonus(int cursor, Bonus bonus, bool remove) => enemy.World.RpcSnapshot().Skip(cursor).Any(frame =>
            StockAbilityProfile.Bonus(frame, bonus.Source, bonus.Stat, bonus.Value, bonus.Seconds, remove));
    }
}
