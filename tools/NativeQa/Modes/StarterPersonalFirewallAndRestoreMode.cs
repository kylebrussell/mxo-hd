using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterPersonalFirewallAndRestoreMode : NativeQaMode
{
    private const ushort Hacker = 1006, Blur = 49, Firewall = 68, Restore = 325;
    private const int Level = 15, X = 17000, Y = 495, Z = 2300, FriendX = 17200;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, Blur, Firewall, Restore).Order().ToArray();
    private static readonly (ushort Stat, int Value)[] Resistance = { (157, 40), (180, 10), (181, 10), (1033, 10) };

    // Stock self activation from PR689, selected friendly activation from PR594, corpse activation from PR316.
    // Select the ability and rebind the current direct-object view and spawn where the stock request carries it.
    private const string SelfCast = "80b9b80000000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string FriendlyCast = "80b94d000d000b000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string CorpseCast = "80b9a0010d000b000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-starter-firewall-and-restore";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={Level} WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,FriendX,Z)},alignment=1,pvpflag=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},alignment=1,pvpflag=0,healthC=100,healthM=500,innerStrC=50,innerStrM=300 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, Blur, Firewall, Restore },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "49,68,325", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAFriend ({FriendX},{Y},{Z}), 2 m east on open outdoor NAV. Select Hacker Style with page 2 slot 1. Select QAFriend and keep the same selection throughout.",
            "Slot 2 Gaussian Blur. Expect a 2 s cast, 20 IS, 8 s own reuse, and [MODIFIER] char=110 source=49 stat=520 add +25 for 600 s. No copy on QAFriend. Keep Blur on while testing Firewall.",
            "Slot 3 Personal Firewall. Expect a 2 s cast, 40 IS, 13 s own reuse, four [MODIFIER] records on QAFriend only (68/157 +40 and 68/180,181,1033 +10 for 300 s). Send helper action chat to QAFriend with text ?dismissbuff 68. Expect all four removes and [ABILITY] Healed 100 -> 175. CSR still has Blur; ?dismissbuff 49 removes its +25 separately.",
            "Slot 4 Restore RSI. With QAFriend alive, expect a local guard or server refusal with no offer/cost. Send QAFriend helper action chat with text ?die. After the down pose, keep or reselect QAFriend and press slot 4. Expect an 8 s cast, 75 IS, 120 s reuse and a 325 consent offer. Approve QAFriend with helper action approve, accept=true. Expect [RESURRECTION] Restored QAFriend with 325 HP=1 IS=1, a visible stand in place, and no [PVP] round. Capture the first native activation frames."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1,
                $"Outdoor NAV stand ({x},{Y},{Z})");
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, friend);
        Check.Equal((ushort)100, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(friend), "Wounded helper ready");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Full ranked prerequisite profile fits");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        await StartAsync(Blur, SelfCast, 0, 2, 8);
        await BonusAsync(actor, Blur, 520, 25, 600, false, 0);
        await StartAsync(Firewall, FriendlyCast, handle, 2, 13);
        foreach (var (stat, value) in Resistance)
            await BonusAsync(friend, Firewall, stat, value, 300, false, 0);
        int friendCursor = friend.World.RpcSnapshot().Count;
        friend.SendChat("?dismissbuff 68");
        foreach (var (stat, value) in Resistance)
            await BonusAsync(friend, Firewall, stat, value, 300, true, friendCursor);
        Check.Equal((ushort)175, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(friend), "Firewall term heal reaches the wounded recipient");
        int actorCursor = actor.World.RpcSnapshot().Count;
        actor.SendChat("?dismissbuff 49");
        await BonusAsync(actor, Blur, 520, 25, 600, true, actorCursor);
        Check.That(!actor.World.RpcSnapshot().Any(packet => Resistance.Any(modifier =>
            StockAbilityProfile.Bonus(packet, Firewall, modifier.Stat, modifier.Value, 300, false))) &&
            !friend.World.RpcSnapshot().Any(packet => StockAbilityProfile.Bonus(packet, Blur, 520, 25, 600, false)),
            "Self Blur and friendly Firewall have distinct recipients");

        actor.SendRpc(Cast(CorpseCast, Restore, handle));
        actor.SkipReceived();
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");
        Check.That(!friend.World.RpcSnapshot().Any(packet => packet.Opcode == 0x188),
            "A living friendly player gets no resurrection offer");
        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.1));
        friend.SendChat("?die");
        await friend.WaitForViewUpdateAsync(2, "01 28 ?? 40 05 00 00 10");
        await StartAsync(Restore, CorpseCast, handle, 8, 120);
        var offer = await friend.WaitForRpcAsync("81 88 45 01 00 00");
        Check.That(BitConverter.ToUInt32(offer.Bytes, 8) != 0, "Restore has a live native approval offer");
        byte[] consent = Scenarios.NativeRejuvenateConsent();
        BitConverter.GetBytes((uint)Restore).CopyTo(consent, 2);
        friend.SendRpc(consent);
        await friend.WaitForViewUpdateAsync(2, "02 80 80 80 d1 00 01 00 01 00");
        await actor.WaitForViewUpdateAsync(target.ViewId, "01 2a ?? 40 00 3f 45 01 00");
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Stock casts and approval framing");
        Console.WriteLine("FIREWALL AND RESTORE PASS 49/68/325");
        return Array.Empty<byte>();

        async Task StartAsync(ushort id, string template, uint selected, float seconds, float reuse)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(Cast(template, id, selected));
            await EvidenceRetestWire.CastBarAsync(actor, id, seconds);
            await EvidenceRetestWire.TimerAsync(actor, cursor, id, AbilityCooldowns.OwnTimer, reuse);
        }
    }

    private static byte[] Cast(string capture, ushort id, uint selected)
    {
        byte[] request = StarterHackerControlTargetMode.WithHandle(capture, selected);
        BitConverter.GetBytes(id).CopyTo(request, 2);
        return request;
    }

    private static Task<RpcRecord> BonusAsync(HeadlessPlayer viewer, ushort source, ushort stat, int value, float duration,
        bool remove, int cursor) => StockAbilityProfile.ObserveAsync(viewer, cursor,
            packet => StockAbilityProfile.Bonus(packet, source, stat, value, duration, remove),
            $"Bonus {source}/{stat} {value} for {viewer.Handle}, remove={remove}");
}

