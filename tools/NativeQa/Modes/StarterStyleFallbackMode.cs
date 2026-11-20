using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterStyleFallbackMode : NativeQaMode
{
    private const ushort SelfDefense = CombatStyles.SelfDefense, Hacker = CombatStyles.HackerStyle, Coder = CombatStyles.CoderStyle;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, Hacker, Coder).Order().ToArray();
    // PR529 1201–1202 and PR521 1059–1060, 4760–4761, respectively.
    private const string StockSelfDefense = "80b911000000000000000000000000000000000000000000000000000000000084d75741086b0000000000";
    private const string StockHacker = "80b9ee030000000000000000000000000000000000000000000000000000000084d75741086b0000000000";
    private const string StockCoder = "80b9f0030400020000000000000000000000000000000000000000000000000084d75741086b0000000000";
    private static readonly (ushort Stat, int Value)[] SelfStats =
    {
        (159, 24), (1009, 47), (522, 50), (171, 12), (1010, 47),
        (524, 50), (1011, 47), (183, 47), (1005, 65)
    };
    private static readonly (ushort Stat, int Value)[] HackerStats =
    {
        (174, 27), (1009, 47), (1010, 47), (1011, 47),
        (342, 70), (172, 27), (183, 47), (1005, 64)
    };
    private static readonly (ushort Stat, int Value)[] CoderStats =
    {
        (522, 64), (159, 26), (1009, 47), (1010, 47), (1011, 47),
        (342, 64), (172, 26), (183, 47), (1005, 66)
    };

    public override string Name => "ability-parity-starter-style-fallback";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 17000, 2300,
        Array.Empty<string>(), new[] { "UPDATE char_abilities SET level=50 WHERE char_id=110" }, Array.Empty<string>(),
        DevCommands: true, hotbarAbilities: new[] { SelfDefense, Hacker, Coder },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "2,17,1006,1008", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Stay at spawn and clear selection. Page 2 slots 1–3 are Self Defense, Hacker Style and Coder Style. Awakened rank 50 grants Self Defense rank 50. Check its startup +12 Awareness and the nine active Self Defense records.",
            "Press slot 2. Hacker Style replaces Self Defense and adds the eight source-1006 records. Enter ?qaunload 1006. Self Defense should immediately replace Hacker Style, with no stale 1006 bonuses. Enter ?qaload 1006. Verify it stays inactive. Press slot 2 to select it again.",
            "Press slot 3. Check that Coder Style replaces Hacker Style. Unload and reload 1008. It should also fall back to Self Defense and stay inactive after reload. End by selecting slot 3 again, then slot 1. Enter submits fixture commands."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Starter style memory fits");
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(!archive.ContainsKey(SelfDefense), "Self Defense comes from Awakened rather than a fixture archive row");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(SelfDefense, false, 50),
            "Awakened supplies the rank-50 hidden Self Defense grant");
        await StockAbilityProfile.ObserveAsync(actor, 0,
            frame => StockAbilityProfile.Bonus(frame, 2, 204, 12, 0, false), "Rank-50 Awakened Awareness");
        await RecordsAsync(0, SelfDefense, SelfStats, remove: false);

        await SelectAsync(StockHacker, SelfDefense, SelfStats, Hacker, HackerStats);
        await CycleAsync(Hacker, HackerStats);
        await SelectAsync(StockHacker, SelfDefense, SelfStats, Hacker, HackerStats);
        await SelectAsync(StockCoder, Hacker, HackerStats, Coder, CoderStats);
        await CycleAsync(Coder, CoderStats);
        await SelectAsync(StockCoder, SelfDefense, SelfStats, Coder, CoderStats);
        await SelectAsync(StockSelfDefense, Coder, CoderStats, SelfDefense, SelfStats);
        Check.Equal(0, actor.World.Problems.Count, "Style updates have valid framing");
        return Array.Empty<byte>();

        async Task CycleAsync(ushort style, (ushort Stat, int Value)[] stats)
        {
            int views = actor.World.ViewSnapshot().Count;
            int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { style }, load: false);
            await TransitionAsync(unloaded, views, style, stats, SelfDefense, SelfStats);
            int reloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { style }, load: true);
            actor.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(actor, reloaded, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes),
                "The memory load without style selection is complete");
            Check.That(!actor.World.RpcSnapshot().Skip(reloaded).Any(frame =>
                frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(style, true)) ||
                stats.Any(stat => StockAbilityProfile.Bonus(frame, style, stat.Stat, stat.Value, 0, false))),
                $"Reloading {style} does not select it or restore its active bonuses");
            await Task.Delay(600); // Keep the two captured style requests outside the duplicate-use window.
        }

        async Task SelectAsync(string request, ushort oldStyle, (ushort Stat, int Value)[] oldStats,
            ushort nextStyle, (ushort Stat, int Value)[] nextStats)
        {
            int views = actor.World.ViewSnapshot().Count;
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(request);
            await TransitionAsync(cursor, views, oldStyle, oldStats, nextStyle, nextStats);
        }

        async Task TransitionAsync(int cursor, int views, ushort oldStyle, (ushort Stat, int Value)[] oldStats,
            ushort nextStyle, (ushort Stat, int Value)[] nextStats)
        {
            byte[] selected = PlayerViewAttributes.BuildSelf((39, PlayerViewAttributes.U32(nextStyle)));
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views)
                .Any(view => BytePattern.IndexOf(view, selected, 0) >= 0), $"Own selection shows style {nextStyle}",
                HeadlessPlayer.DefaultTimeout);
            await RecordsAsync(cursor, oldStyle, oldStats, remove: true);
            await RecordsAsync(cursor, nextStyle, nextStats, remove: false);
            byte[] oldOff = AbilityBuffs.BuildExclusive(oldStyle, false);
            byte[] nextOn = AbilityBuffs.BuildExclusive(nextStyle, true);
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => frame.Bytes.AsSpan().SequenceEqual(nextOn),
                $"Style {nextStyle} switches on");
            var updates = actor.World.RpcSnapshot().Skip(cursor).ToList();
            int off = updates.FindIndex(frame => frame.Bytes.AsSpan().SequenceEqual(oldOff));
            int on = updates.FindIndex(frame => frame.Bytes.AsSpan().SequenceEqual(nextOn));
            Check.That(off >= 0 && off < on, "Old style switches off before the new one switches on");
        }

        async Task RecordsAsync(int cursor, ushort style, (ushort Stat, int Value)[] stats, bool remove)
        {
            foreach (var stat in stats)
                await StockAbilityProfile.ObserveAsync(actor, cursor,
                    frame => StockAbilityProfile.Bonus(frame, style, stat.Stat, stat.Value, 0, remove),
                    $"Style {style} stat {stat.Stat} {stat.Value} remove={remove}");
        }
    }
}
