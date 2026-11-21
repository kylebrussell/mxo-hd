using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class AwakenedResistanceThresholdMode : NativeQaMode
{
    private const ushort CalmMind = 5, EmptyMind = 8, Desperation = 1149;
    private static readonly ushort[] Loadout = Profile(2, 17, CalmMind, EmptyMind, Desperation).Order().ToArray();

    // Stock PR382 7bc903fb3dd5:5886,7264 and PR453 a91de5605bf5:738.
    private const string CalmCast = "80b90500070005000000000000000000000000000000000000000000585cc60001000000086b0000000000";
    internal const string EmptyCast = "80b90800070005000000000000000000000000000000000000000000a8fb7fbf00000000086b0000000000";
    private const string DesperationCast = "80b97d040000000000000000000000000000000000000000000000e05bc50001000000086b0000000000";

    public override string Name => "ability-parity-awakened-resistance-threshold";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,healthC=500,healthM=1000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { CalmMind, EmptyMind, Desperation }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "5,8,1149", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Test with no target. Verify HP is exactly half of maximum (500/1000 in this disposable profile). Enter confirms a command. Keep selection clear without hitting Escape between steps.",
            "Page 2 slot 1 Calm Mind gives +75 Thrown Resistance for 20 s and 60 s reuse. Immediately use slot 2 Empty Mind. Verify the +75 Thrown record clears before +75 Viral Resistance appears. The two buffs share the shipped stacking family.",
            "While Empty Mind is active, use slot 3 Desperation at half health. Expect +50 Initiative for 20 s from the half-life branch. Empty Mind stays active. Raise HP to 850 with ?health 850. The sampled Initiative remains +50 until its own expiry. Save both removals and the independent reuse records."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Awakened resistance profile fits in memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        Check.Equal((ushort)500, BitConverter.ToUInt16(actor.World.CreationSnapshot()
            .Last(view => view.ViewId == 2).Attributes["Health"]), "Exact half health at entry");

        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(CalmCast, 0));
        await ObserveAsync(actor, cursor, frame => Bonus(frame, CalmMind, 1033, 75, 20, false), "Calm Mind adds thrown resistance");
        await EvidenceRetestWire.TimerAsync(actor, cursor, CalmMind, AbilityCooldowns.OwnTimer, 60);
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(EmptyCast, 0));
        var calmOff = await ObserveAsync(actor, cursor, frame => Bonus(frame, CalmMind, 1033, 75, 20, true), "The first resistance is removed");
        var emptyOn = await ObserveAsync(actor, cursor, frame => Bonus(frame, EmptyMind, 157, 75, 20, false), "Empty Mind adds viral resistance");
        var frames = actor.World.RpcSnapshot().ToList();
        Check.That(frames.IndexOf(calmOff) < frames.IndexOf(emptyOn), "The shared-family resistance is removed before its replacement applies");
        await EvidenceRetestWire.TimerAsync(actor, cursor, EmptyMind, AbilityCooldowns.OwnTimer, 60);

        int resistanceAt = cursor;
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(DesperationCast));
        await ObserveAsync(actor, cursor, frame => Bonus(frame, Desperation, 1044, 50, 20, false),
            "Exact half health uses the lower branch of Desperation");
        await EvidenceRetestWire.TimerAsync(actor, cursor, Desperation, AbilityCooldowns.OwnTimer, 90);
        Check.That(!actor.World.RpcSnapshot().Skip(resistanceAt).Any(frame => Bonus(frame, EmptyMind, 157, 75, 20, true)),
            "Desperation does not remove the unrelated resistance");
        actor.SendChat("?health 850");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 40 52 03");
        actor.SendChat("?combatdepth initiative");
        await actor.WaitForTextAsync("bonus=50");
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(frame => Bonus(frame, Desperation, 1044, 50, 20, true)) &&
            actor.World.RpcSnapshot().Skip(resistanceAt).Any(frame => Bonus(frame, EmptyMind, 157, 75, 20, true)),
            "The separate effect records expire naturally", TimeSpan.FromSeconds(25));
        Check.Equal(0, actor.World.Problems.Count, "Awakened resistance threshold framing");
        return Array.Empty<byte>();
    }
}
