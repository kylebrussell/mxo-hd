using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class EmergencyRepairsCapMode : NativeQaMode
{
    private const ushort Hacker = 1006, EmergencyOne = 169, EmergencyTwo = 332;
    private const ushort StartingHealth = 499, WoundedHealth = 450;
    private const int Rank = 50, X = 17000, Y = 495, Z = 6400, FriendZ = 6600;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Hacker, EmergencyOne, EmergencyTwo).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-emergency-repairs-two-cap";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Rank},exp={PlayerProgression.ExperienceForLevel(Rank)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X,FriendZ)},innerStrC=600,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={FriendZ},healthC={StartingHealth},healthM=500,alignment=1,pvpflag=0 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, EmergencyTwo, EmergencyOne },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "332", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Rank {Rank}, {Memory}/{AbilityMemory.ForLevel(Rank)} memory, full prerequisite tree. CSR ({X},{Y},{Z}) faces QAFriend ({X},{Y},{FriendZ}) at 499/500 HP with natural regeneration held. Select Hacker Style on page 2 slot 1 and select QAFriend.",
            "Use slot 2 Emergency Repairs 2.0 and save the full first stock 80 b9 for ability 332. Expect a 4 s cast, paid 24 IS (25 base with Efficiency +5%), 300 s own and 4 s family reuse, and no health change on landing. [MODIFIER] char=6111 source=332 stat=1157 add value=+96 duration=50s (76 base with Hacker Style +27 healing).",
            "The first scheduled 2.5 s pulse logs [VITALS] char=6111 ability=332 HOT raw=5 HP 499->500 max=500. Leave QAFriend full for two subsequent pulse times. There must be no other source=332 health delta at full and the effect should remain active.",
            "About 10 s after landing, send QAFriend helper chat ?health 450 once. The next scheduled pulse logs HP 450->455, then +5 until cap or expiry. The original source=332 record removes at 50 s, before a twentieth pulse. No CSR heal, max-health change or [PVP] round. Slot 3 is the known rank-1 control after the original record ends."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, friend);
        Check.Equal(StartingHealth, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(friend),
            "QAFriend starts one HP below the cap");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.Keys.ToHashSet().SetEquals(Abilities), "The complete Ballista profile arrives in Margin");
        foreach (ushort id in Abilities)
            foreach (var (required, rank) in AbilityPrerequisites.Current.Closure(id))
                Check.That(archive.TryGetValue(required, out ushort loaded) && loaded >= rank,
                    $"Ability {id} prerequisite {required} reaches rank {rank}");
        Check.That(Memory <= AbilityMemory.ForLevel(Rank), "The complete ranked profile fits");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc(StarterEmergencyRepairsMode.HackerStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        // Tier two receives its first direct 80 b9 from the real client. Replay the retained tier-one request here.
        int timersFrom = actor.World.RpcSnapshot().Count;
        int effectFrom = friend.World.RpcSnapshot().Count;
        int viewsFrom = friend.World.ViewSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterEmergencyRepairsMode.EmergencyStock, handle));
        await EvidenceRetestWire.CastBarAsync(actor, EmergencyOne, 2);
        await EvidenceRetestWire.TimerAsync(actor, timersFrom, EmergencyOne, AbilityCooldowns.OwnTimer, 13);
        await StockAbilityProfile.ObserveAsync(friend, effectFrom, frame =>
            StockAbilityProfile.Bonus(frame, EmergencyOne, TempMods.DOTPlaceHolderAbility, 17, 15, false),
            "Emergency Repairs 1.0 lands its known HOT record");
        await EvidenceRetestWire.ViewSinceAsync(friend, viewsFrom, 2, "02 80 80 80 40 f4 01");
        friend.SendChat($"?health {WoundedHealth}");
        await EvidenceRetestWire.ViewSinceAsync(friend, viewsFrom, 2, "02 80 80 80 40 c5 01");
        Check.Equal(0, friend.World.Problems.Count + actor.World.Problems.Count, "Selected-friendly HOT framing");
        return Array.Empty<byte>();
    }
}
