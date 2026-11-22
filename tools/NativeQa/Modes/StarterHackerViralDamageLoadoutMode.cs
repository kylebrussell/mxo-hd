using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerViralBaseMode;
using static NativeQa.Modes.StarterHackerPassiveFixture;

namespace NativeQa.Modes;

public sealed class StarterHackerViralDamageLoadoutMode : NativeQaMode
{
    private const int EnemyX = X + 400, SecondaryX = X + 600;
    private const ushort HarmfulCode = 53, CodeNuke = 60, Family = 682;
    private static readonly NativeQaModePlan BasePlan = new StarterHackerViralBaseMode().Plan;
    private static readonly ushort[] LoadOrder = Samples.Select(sample => sample.Id).ToArray();
    private static readonly ushort[] UnloadOrder = LoadOrder.Reverse().ToArray();

    public override string Name => "ability-parity-starter-hacker-viral-damage-loadout";

    public override NativeQaModePlan Plan { get; } = new(BasePlan.Abilities.ToArray(), BasePlan.X, BasePlan.Z,
        BasePlan.BeforeLoadout.ToArray(), BasePlan.AfterLoadout.ToArray(), BasePlan.AfterHelpers.ToArray(),
        DevCommands: true, hotbarAbilities: BasePlan.HotbarAbilities.ToArray(), testMobFixture: BasePlan.TestMobFixture,
        environmentOverrides: new(BasePlan.EnvironmentOverrides),
        extraQaMetadata: new() { ["ScopedStockRows"] = "53,57,58,59,60", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {BasePlan.Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}); QAFar ({SecondaryX},{Y},{Z}) is the opposing splash witness. All are on clear outdoor NAV away from the ice. Select Hacker Style from page 2 slot 1 and /target QAEnemy.",
            "Unload 60, 59, 58, 53, 57 in that order with five separate ?qaunload ID commands. Try slots 2–6. An unloaded shortcut may be disabled or clear locally. Any sent attempt should produce one 80 ba reason 7, with no [ABILITY-COST], cast, reuse, or damage.",
            "Reload 57, 53, 58, 59, 60 in that order with separate ?qaload ID commands. If stock cleared a shortcut, use the restored Actions entry. For the timed check, start Harmful Code (slot 3) and type ?qaunload 53 during its 4 s cast. The accepted cast spends 18 IS (700 to 682), starts 8 s own / 4 s shared reuse and fizzles before the hit. Reload 53 before the positive checks.",
            "With QAEnemy selected, use slots 2–6 in order after each shared timer. The normal 8/12/11/17/7 [PVP] hits land on QAEnemy and only slot 6 splashes QAFar for 7. Normal [ABILITY-COST] is 5/18/6/7/11 IS. Final IS 635 with the timed fizzle, 653 without it. No maximum [VITALS] or interlock-round changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        CheckNav(context, EnemyX);
        Check.That(NavMesh.Locate(1, SecondaryX, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
            NavMesh.HasSightPath(1, EnemyX, Y, Z, SecondaryX, Y, Z), "The splash witness stands on clear outdoor NAV");
        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var enemy = context.Helpers["QAEnemy"];
        var splash = context.Helpers["QAFar"];
        var profile = VerifyProfile(caster, BasePlan.Abilities.ToArray());
        uint handle = await StartAsync(caster, enemy, server, pvp: true);
        await caster.WaitForViewNamedAsync(splash.Handle);

        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, UnloadOrder, load: false);
        foreach (Sample sample in Samples)
            await StarterHackerControlTargetMode.RefusedAsync(caster, sample.Id, Request(sample), AbilityRefusals.NotLoaded);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, LoadOrder, load: true);

        Sample pending = Samples.Single(sample => sample.Id == HarmfulCode);
        int logStart = server.DebugLog().Length;
        int enemyStart = enemy.World.RpcSnapshot().Count;
        int castStart = await BeginAsync(pending);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, new[] { HarmfulCode }, load: false);
        await caster.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
            "[ABILITY] 53 fizzles: the starter viral ability was unloaded before landing"),
            "Unloading Harmful Code during the cast prevents its pending hit", HeadlessPlayer.DefaultTimeout);
        await CheckTimersAsync(pending, castStart);
        await AssertIsAsync(682);
        await AssertNoHitAsync(enemy, enemyStart, pending);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, new[] { HarmfulCode }, load: true);
        await EvidenceRetestWire.TimerAsync(caster, castStart, pending.Id, AbilityCooldowns.OwnTimer, pending.Reuse, ended: true);

        int expectedIs = 682;
        foreach (Sample sample in Samples)
        {
            enemyStart = enemy.World.RpcSnapshot().Count;
            int splashStart = splash.World.RpcSnapshot().Count;
            castStart = await BeginAsync(sample);
            await CheckTimersAsync(sample, castStart);
            await AssertIsAsync(expectedIs -= sample.Cost);
            await AssertHitAsync(enemy, enemyStart, sample);
            if (sample.Id == CodeNuke) await AssertHitAsync(splash, splashStart, sample);
            else await AssertNoHitAsync(splash, splashStart, sample);
            await EvidenceRetestWire.TimerAsync(caster, castStart, Family, AbilityCooldowns.GroupTimer, sample.SharedReuse, ended: true);
        }
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + splash.World.Problems.Count, "Starter viral loadout wire framing");
        Console.WriteLine("STARTER VIRAL DAMAGE LOADOUT PASS 53/57/58/59/60");
        return Array.Empty<byte>();

        byte[] Request(Sample sample) => StarterHackerControlTargetMode.WithHandle(sample.Capture, handle);

        async Task<int> BeginAsync(Sample sample)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SkipReceived();
            caster.SendRpc(Request(sample));
            await EvidenceRetestWire.CastBarAsync(caster, sample.Id, sample.Cast);
            return cursor;
        }

        async Task CheckTimersAsync(Sample sample, int cursor)
        {
            await EvidenceRetestWire.TimerAsync(caster, cursor, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            await EvidenceRetestWire.TimerAsync(caster, cursor, Family, AbilityCooldowns.GroupTimer, sample.SharedReuse);
        }

        Task AssertIsAsync(int expected) => caster.WaitUntilAsync(() => caster.World.ViewSnapshot()
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)expected),
            $"Ordinary cost leaves {expected} IS", HeadlessPlayer.DefaultTimeout);

        Task<RpcRecord> AssertHitAsync(HeadlessPlayer receiver, int cursor, Sample sample)
        {
            byte[] damage = AbilityMessages.Damage(false, sample.Id, caster.Handle, receiver.Handle, sample.BaseDamage, 0);
            return StockAbilityProfile.ObserveAsync(receiver, cursor, frame => frame.Bytes.AsSpan().SequenceEqual(damage),
                $"Reloaded {sample.Id} lands on {receiver.Handle} for {sample.BaseDamage}");
        }

        async Task AssertNoHitAsync(HeadlessPlayer receiver, int cursor, Sample sample)
        {
            receiver.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(receiver, cursor, frame => frame.Opcode == 0x149, "The recipient stream advances beyond the cast");
            byte[] prefix = AbilityMessages.Damage(false, sample.Id, caster.Handle, receiver.Handle, 0, 0)[..^8];
            Check.That(!receiver.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Bytes.AsSpan().StartsWith(prefix)),
                $"{sample.Id} applies no damage to {receiver.Handle}");
        }
    }
}
