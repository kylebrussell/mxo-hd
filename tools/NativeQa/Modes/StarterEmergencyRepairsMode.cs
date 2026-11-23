using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterEmergencyRepairsMode : NativeQaMode
{
    private const ushort RepairOne = 77, RepairTwo = 234, Emergency = 169, Hacker = 1006;
    private const ushort StartingHealth = 100;
    private const int X = 24800, Y = 495, Z = -1498, Rank = 28;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Hacker, RepairOne, RepairTwo, Emergency).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    // PR316 CSEQ 215/255, PR333 CSEQ 121 and PR556 Hacker Style. Patch only the live friendly view.
    private const string RepairOneStock = "80b94d000d000b000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    internal const string EmergencyStock = "80b9a9000d000b000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string RepairTwoStock = "80b9ea00040002000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    internal const string HackerStock = "80b9ee0300000000000000000000000000000000000000000000000000000084d75741086b0000000000";

    public override string Name => "ability-parity-starter-emergency-repairs";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Rank},exp={PlayerProgression.ExperienceForLevel(Rank)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+200,Z)},innerStrC=500,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={X + 200},y={Y},z={Z},healthC={StartingHealth},healthM=500,alignment=1,pvpflag=0 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, RepairOne, RepairTwo, Emergency },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "77,234,169", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Rank {Rank}, {Memory}/{AbilityMemory.ForLevel(Rank)} memory, full prerequisite tree. CSR starts outdoors facing QAFriend 2 m east. QAFriend is at 100 HP with natural regeneration held. Select Hacker Style in slot 1 and select QAFriend. Use Enter for any chat, keep the target selected.",
            "Slot 2, Repair RSI 1.0, costs 10 IS and raises QAFriend from 100 to 110 HP. It has a 2 s cast and 4 s own and shared reuse. Wait until the shared timer clears.",
            "Slot 3, Repair RSI 2.0, costs 15 IS and raises QAFriend from 110 to 133 HP. It also has a 2 s cast and 4 s own and shared reuse.",
            "Slot 4, Emergency Repairs 1.0, costs 10 IS. Expect a 2 s cast, 13 s own and 4 s family reuse. The landed heal-over-time stores 16 for 15 s with no immediate HP change. QAFriend rises to 136, 139, 142, 145 and 148 on five 2.5 s pulses. Watch for the final record removal and no sixth pulse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var ally = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(ally.Handle, ally);
        await ally.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, ally);
        Check.Equal(StartingHealth, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(ally),
            "The native target starts wounded and regeneration is held");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(requirement =>
                archive.TryGetValue(requirement.Key, out ushort rank) && rank >= requirement.Value)),
            "The three heals and their ranked prerequisite tree arrive in Margin");
        Check.That(Memory <= AbilityMemory.ForLevel(Rank), "The complete healing profile fits");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var target = await actor.WaitForViewNamedAsync(ally.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc(HackerStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        await DirectAsync(RepairOne, RepairOneStock, 110, waitForFamily: true);
        await DirectAsync(RepairTwo, RepairTwoStock, 133, waitForFamily: false);

        int timerAt = actor.World.RpcSnapshot().Count;
        int recipientAt = ally.World.RpcSnapshot().Count;
        int viewAt = ally.World.ViewSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(EmergencyStock, handle));
        await EvidenceRetestWire.CastBarAsync(actor, Emergency, 2);
        await EvidenceRetestWire.TimerAsync(actor, timerAt, Emergency, AbilityCooldowns.OwnTimer, 13);
        await EvidenceRetestWire.TimerAsync(actor, timerAt, 685, 0, 4);
        var added = await StockAbilityProfile.ObserveAsync(ally, recipientAt, frame =>
            StockAbilityProfile.Bonus(frame, Emergency, TempMods.DOTPlaceHolderAbility, 16, 15, false),
            "Emergency Repairs snapshots sixteen healing for fifteen seconds");
        Check.Equal((ushort)133, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(ally), "Landing gives no instant heal");

        foreach (ushort hp in new ushort[] { 136, 139, 142, 145, 148 })
            await EvidenceRetestWire.ViewSinceAsync(ally, viewAt, 2, "02 80 80 80 40 " + EvidenceRetestWire.Le(hp));

        ManageBonusRecord.TryDecode(added.Bytes, out var bonus);
        await StockAbilityProfile.ObserveAsync(ally, recipientAt, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == Emergency &&
            record.RecordId == bonus!.RecordId && record.AddRemoveType == 1, "Emergency Repairs expires after its fifth tick");
        Check.Equal((ushort)148, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(ally), "No sixth pulse at expiry");
        Check.Equal(0, ally.World.Problems.Count + actor.World.Problems.Count, "Starter heal packet framing");
        return Array.Empty<byte>();

        async Task DirectAsync(ushort id, string request, ushort health, bool waitForFamily)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            int viewCursor = ally.World.ViewSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(request, handle));
            await EvidenceRetestWire.CastBarAsync(actor, id, 2);
            await EvidenceRetestWire.TimerAsync(actor, cursor, id, AbilityCooldowns.OwnTimer, 4);
            await EvidenceRetestWire.TimerAsync(actor, cursor, 686, 0, 4);
            await EvidenceRetestWire.ViewSinceAsync(ally, viewCursor, 2, "02 80 80 80 40 " + EvidenceRetestWire.Le(health));
            if (waitForFamily)
                await EvidenceRetestWire.TimerAsync(actor, cursor, 686, 0, 4, ended: true);
        }
    }
}

