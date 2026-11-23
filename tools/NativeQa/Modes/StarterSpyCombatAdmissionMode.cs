using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSpyCombatAdmissionMode : NativeQaMode
{
    private const ushort SpyStyle = 1112, Sneak = 1111, Poison = 146, Sense = 149, Backsweep = 152;
    private const int Level = 50, Rank = 14;
    private const string StockOpen = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";
    // Complete stock casts from PR423, PR424, PR434 and PR438.
    private const string StyleCast = "80b958040400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string SneakCast = "80b95704040002000000000000000000000000000000000000000000585cbf0001000000086b0000000000";
    internal const string PoisonCast = "80b99200000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string SenseCast = "80b99500000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private const string BacksweepCast = "80b99800040002000000000000000000000000000000000000000000585cbf0001000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 151, SpyStyle, Poison, Sense, Backsweep)
        .Order().ToArray();

    public override string Name => "ability-parity-starter-spy-combat-admission";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new[] { SpyStyle, Sneak, Poison, Sense, Backsweep },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.ResetMode] = "1", ["MXO_POISON_KNIFE_QA"] = "dummy"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "146,149,152", ["NativeCloseableRows"] = 0, ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level) },
        readyMessages: new[]
        {
            "Rank-14 fixture at level 50 to fit both trees. Use Sixth Sense (page 2 slot 4) at rest and capture the radar ping. Open ordinary Close Combat on Melee Dummy and reselect Spy Style (slot 1). Try Sense and Backsweep (slot 5). The client can block them locally. A sent request must refuse without reuse.",
            "Use ?combatdepth fixture-reset, wait, strafe to reveal the fresh dummy, and click it and reselect Spy Style. Enter submits chat, and Escape can holster the active style. Use Sneak (slot 2) from behind, then Spinning Backsweep (slot 5). Capture the hit, Sneak ending and the 30/15 s timers.",
            "Reset, strafe and select the new dummy again. Reselect Spy Style. From rest, use Poison Knife (slot 3). Check the ordinary 2 s cast, its ThrownDOT, and 20/3 s reuse. Headless also checks the retired handle."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), $"Legal level-{Level} memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        int pingAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(SenseCast, 0));
        await StockAbilityProfile.ObserveAsync(actor, pingAt, frame => BytePattern.Parse("80 c5 05 00 0a").IsMatch(frame.Bytes),
            "Sixth Sense emits the regular ten-second radar marker");

        uint original = Handle(dummy);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(original));
        actor.SkipReceived();
        actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockOpen, original), WireFormat.FromHex($"40 {hex} 01 6b"));
        await actor.WaitForRpcAsync($"40 {hex} 00 6b");
        await actor.WaitForRpcAsync("47");
        await StyleAsync(original);
        await StarterHackerControlTargetMode.RefusedAsync(actor, Sense, StarterHackerControlTargetMode.WithHandle(SenseCast, 0),
            AbilityRefusals.MustBeOutsideCombat);
        await StarterHackerControlTargetMode.RefusedAsync(actor, Backsweep, StarterHackerControlTargetMode.WithHandle(BacksweepCast, original),
            AbilityRefusals.MustBeOutsideCombat);

        dummy = await ResetAsync();
        uint current = Handle(dummy);
        Check.That(current != original, "A restored dummy has a fresh handle");
        await StyleAsync(current);
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(SneakCast, 0));
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("CSR_Tester 1111 on", StringComparison.Ordinal),
            "Normal Sneak activates behind the current dummy", HeadlessPlayer.DefaultTimeout);
        int sweepAt = actor.World.RpcSnapshot().Count;
        int before = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BacksweepCast, current));
        await EvidenceRetestWire.TimerAsync(actor, sweepAt, Backsweep, AbilityCooldowns.OwnTimer, 30);
        await EvidenceRetestWire.TimerAsync(actor, sweepAt, 691, AbilityCooldowns.GroupTimer, 15);
        await actor.WaitUntilAsync(() => server.DebugLog()[before..].Contains("152 lands on view", StringComparison.Ordinal) &&
            server.DebugLog()[before..].Contains("1111 off (used ability 152)", StringComparison.Ordinal),
            "The active Sneak ends on the landed Backsweep", HeadlessPlayer.DefaultTimeout);

        dummy = await ResetAsync();
        await StyleAsync(Handle(dummy));
        await StarterHackerControlTargetMode.RefusedAsync(actor, Poison, StarterHackerControlTargetMode.WithHandle(PoisonCast, current));
        int poisonAt = actor.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(PoisonCast, Handle(dummy)));
        await EvidenceRetestWire.CastBarAsync(actor, Poison, 2);
        await EvidenceRetestWire.TimerAsync(actor, poisonAt, Poison, AbilityCooldowns.OwnTimer, 20);
        await EvidenceRetestWire.TimerAsync(actor, poisonAt, 1216, AbilityCooldowns.GroupTimer, 3);
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("146 ticks on Melee Dummy", StringComparison.Ordinal),
            "Poison Knife ticks on the fresh selected dummy", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count, "Starter Spy admission framing");
        return Array.Empty<byte>();

        async Task StyleAsync(uint handle)
        {
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StyleCast, handle));
            await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        }

        async Task<ViewCreation> ResetAsync()
        {
            int at = actor.World.CreationSnapshot().Count;
            actor.SendChat("?combatdepth fixture-reset");
            await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Skip(at)
                .Any(view => view.Name == CombatFixtureLoadout.DummyName), "A fresh Melee Dummy is visible", HeadlessPlayer.DefaultTimeout);
            return await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        }
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
