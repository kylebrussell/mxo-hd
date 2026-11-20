using System.Buffers.Binary;
using HeadlessClient;
using hds;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class ZenMasterBalancePurityMode : NativeQaMode
{
    private const ushort Balance = 861, Purity = 868;
    private const int FixtureLevel = 99;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Balance, Purity }
        .Concat(AbilityPrerequisites.Current.Closure(Balance).Keys)
        .Concat(AbilityPrerequisites.Current.Closure(Purity).Keys)
        .Distinct().Order().ToArray();
    // Stock self-cast request template (PR382 7bc903fb3dd5:5886-5887, target cleared).
    private const string SelfCastTemplate = "80b905000000000000000000000000000000000000000000000000585cc60001000000086b0000000000";

    public override string Name => "ability-parity-zen-master-balance-purity";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(PlayerProgression.MaxLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=1000,healthM=1000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        },
        DevCommands: true, HealthCallbackQa: true, loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { Balance, Purity },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_BUFF_DISPEL_QA_ROLL100"] = "0"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "861,862,868",
            ["NativeCloseableRows"] = 3,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(50)
        },
        readyMessages: new[]
        {
            "Level 99 disposable diagnostic because Balance (861) and Purity (868) carry stored ReqCharLevel=99. MXO_BUFF_DISPEL_QA_ROLL100=0 makes Purity's 25% cleanse deterministic in this disposable DB.",
            "Step A (Balance 861 + Balance Effect 862). Clear selection. Press page 2 slot 1 (Balance 861): expect 50 IS spent, 2.0 s cast (80 ac), '[BUFF] 861 on for 30 s', and 60.0 s own reuse (80 bd 01 5d 03 ... 60.0f). Promptly type '?health 400' and '?health 460': expect '[HEALTH-EFFECT] 862 heals CSR_Tester ...' (+50..99 each, no healing influence). Wait for '[BUFF] 861 off' (30 s), then type '?health 420' and confirm no '[HEALTH-EFFECT] 862' heal fires.",
            "Step B (Purity 868). Type '?combatdepth debuff' (applies cleansable Destroy Health 476 for 40 s: Stat 106 -520, Stat 112 -20). Clear selection and press page 2 slot 2 (Purity 868): expect 20 IS spent, instant cast (no 80 ac), 15.0 s own reuse (80 bd 01 64 03 ... 15.0f), '[SWEEP] 868 removed 476 from CSR_Tester', and no lasting 868 buff."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        // Step A: Balance 861 + 862 health-change trigger.
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(SelfRequest(Balance));
        await EvidenceRetestWire.TimerAsync(actor, cursor, Balance, AbilityCooldowns.OwnTimer, 60);
        await ObserveAsync(actor, cursor, packet => Bonus(packet, Balance, Balance, 0, 30, remove: false) ||
            MxoHd.PacketResearch.ManageBonusRecord.TryDecode(packet.Bytes, out var mod) && mod!.SourceId == Balance && mod.TargetId == Balance && mod.AddRemoveType == 0,
            "Balance 861 activates for 30 s");

        int healLog = server.DebugLog().Length;
        actor.SendChat("?health 400");
        await actor.WaitUntilAsync(() => server.DebugLog()[healLog..].Contains("[HEALTH-EFFECT] 862 heals CSR_Tester 400 -> ", StringComparison.Ordinal),
            "Balance 862 heals CSR_Tester after ?health 400", HeadlessPlayer.DefaultTimeout);

        // Step B: Purity 868 cleanses Destroy Health 476 deterministically.
        int debuffLog = server.DebugLog().Length;
        actor.SendChat("?combatdepth debuff");
        await actor.WaitUntilAsync(() => server.DebugLog()[debuffLog..].Contains("476", StringComparison.Ordinal),
            "Cleansable debuff 476 applied", HeadlessPlayer.DefaultTimeout);

        int purityCursor = actor.World.RpcSnapshot().Count;
        int sweepLog = server.DebugLog().Length;
        actor.SendRpc(SelfRequest(Purity));
        await EvidenceRetestWire.TimerAsync(actor, purityCursor, Purity, AbilityCooldowns.OwnTimer, 15);
        await actor.WaitUntilAsync(() => server.DebugLog()[sweepLog..].Contains("[SWEEP] 868 removed 476 from CSR_Tester", StringComparison.Ordinal),
            "Purity 868 cleanses 476", HeadlessPlayer.DefaultTimeout);

        Check.Equal(0, actor.World.Problems.Count, "Zen Master Balance and Purity framing");
        return Array.Empty<byte>();
    }

    private static byte[] SelfRequest(ushort ability)
    {
        byte[] request = Convert.FromHexString(SelfCastTemplate);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        return request;
    }
}
