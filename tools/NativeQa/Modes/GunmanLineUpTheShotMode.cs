using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class GunmanLineUpTheShotMode : NativeQaMode
{
    private const int Level = 20;
    private const ushort Rifles = CombatStyles.Rifles, Conceal = Stealth.ConcealmentGrantAbility;
    private const ushort LineUp = AbilityHealthCallbacks.LineUpAbility;
    // PR330 stock Rifles and PR429 PacketLog 7483f644ff39:1600–1601 stock Concealment.
    private const string RiflesFrame = "80b99300040002000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string ConcealFrame = "80b97104000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Rifles, LineUp }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-gunman-line-up-the-shot";

    public override NativeQaModePlan Plan { get; } = new(Loadout, 17000, 6400, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,rotation=64," +
                "healthC=500,healthM=1000,innerStrC=1000,innerStrM=1000,pvpflag=0 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Rifles, Conceal, LineUp },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "506", ["NativeCloseableRows"] = 1,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryAvailable"] = AbilityMemory.ForLevel(Level)
        },
        readyMessages: new[]
        {
            $"CSR level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, verified outdoor NAV (17000,495,6400), rifle in hand. No helper or target needed. Page 2 is Rifles, Concealment (1137), Line Up The Shot (506). Choose slot 1. Press slot 3 before concealing and record the local gate or reason 23 (mask 0x00040000). No debit, cast or reuse on refusal.",
            "Press slot 2, wait for [STEALTH] 1137 on, then press slot 3. Save the complete first outgoing 506 request. Concealment stays active during the four-second cast. Expect [ABILITY-COST] ability=506 paid=30, own reuse 15 s, [HEALTH-EFFECT] LineUp506 subject=1 reapply1137 and [MODIFIER] source=506 adds stat=524 +25 for 20 s and stat=171 +10 for 12 s. No stat=710 modifier or extra IS debit for reapplication.",
            "As soon as the modifiers appear, type ?health 1000. Healing leaves both records active. After the 12 s stat=171 removal and before 20 s, type ?health 400. The health loss clears 1137. Stat=524 stays until its 20 s removal.",
            "Once both modifiers and reuse end, press slot 2 and wait for Concealment, then press slot 3 again. Immediately after the [MODIFIER] adds, type ?health 200 while the 12 s damage component is still active. This time both 506 records and 1137 end together. The health commands send self-health updates, with no max [VITALS] change or [PVP] round."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerPassiveFixture.CheckNav(context, 17400);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        uint handle = DuelPackets.Handle(self.ViewId, self.SpawnId);
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(RiflesFrame, handle));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 93 00 00 00");

        // This bootstrap verifies the captured setup. The first 506 wire frame comes from native QA.
        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc(ConcealFrame);
        await EvidenceRetestWire.CastBarAsync(actor, Conceal, 2);
        await StockAbilityProfile.ObserveAsync(actor, start, rpc => StockAbilityProfile.Bonus(rpc, Conceal, 209, Level, 0, false),
            "Stock Concealment publishes the legal rank-20 state");
        Check.Equal(0, actor.World.Problems.Count, "Rifles and Concealment stock request framing");
        return Array.Empty<byte>();
    }
}
