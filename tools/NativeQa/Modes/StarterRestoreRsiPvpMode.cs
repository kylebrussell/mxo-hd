using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterRestoreRsiPvpMode : NativeQaMode
{
    private const ushort Hacker = 1006, QuickRecovery = 70, Repair = 77, Restore = 325;
    private const int Level = 15, X = 17000, Y = 495, Z = 2300, FriendX = 17200;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, QuickRecovery, Repair, Restore).Order().ToArray();

    public override string Name => "ability-parity-starter-restore-rsi-pvp";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={Level} WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,FriendX,Z)},alignment=1,pvpflag=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},x={FriendX},y={Y},z={Z}," +
                "alignment=1,pvpflag=0,healthC=500,healthM=500,innerStrC=200,innerStrM=300 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, Restore, Repair },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "70,77,325", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces QAFriend ({FriendX},{Y},{Z}), 2 m away, same organization. CSR starts unflagged; level-50 QAFriend starts flagged. Capture Quick Recovery 70/1052 +3 and 70/1057 +7 at login.",
            "Select Hacker Style (page 2 slot 1) and /target QAFriend. Have QAFriend enter ?die through helper chat. Press slot 2 for Restore RSI. The 8 s cast spends 75 IS and offers 325 with 120 s reuse. Wait briefly with the offer pending and confirm CSR is still unflagged.",
            "Approve the offer on QAFriend. Expect an in-place stand, HP=1 IS=1, QAFriend retaining its flag, and [PVP] CSR_Tester PvP flag on only on approval. Capture CSR's own condition bit and the visible result. Take note of any fighting, which this plan does not initiate.",
            "With QAFriend still selected, press page 2 slot 3 Repair RSI 1.0. Expect 10 IS, 2 s cast and 4 s reuse. QAFriend receives the ordinary +10 heal (1 to 11 HP) and remains standing. This check demonstrates QAFriend is a live support target after the restore."
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
        await PvpOpponentsFixture.EnterAsync(friend);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Complete ranked support loadout fits");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, QuickRecovery, 1052, 3, 0, false), "Quick Recovery sweep stat at login");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, QuickRecovery, 1057, 7, 0, false), "Quick Recovery health stat at login");

        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)))}");
        friend.SendChat("?die");
        await friend.WaitForViewUpdateAsync(2, "01 28 ?? 40 05 00 00 10");

        int restoreCursor = actor.World.RpcSnapshot().Count;
        actor.SkipReceived();
        EvidenceRetestWire.Cast(actor, Restore, target);
        await EvidenceRetestWire.CastBarAsync(actor, Restore, 8);
        await EvidenceRetestWire.TimerAsync(actor, restoreCursor, Restore, AbilityCooldowns.OwnTimer, 120);
        await friend.WaitForRpcAsync("81 88 45 01 00 00");
        byte[] ownFlag = PvpFlag.BuildSelfFlags(PvpFlag.PvpBit);
        var priorViews = actor.World.ViewSnapshot();
        Check.That(!priorViews.Any(view => BytePattern.IndexOf(view, ownFlag, 0) >= 0), "The caster stays unflagged while offer is pending");

        byte[] reply = Scenarios.NativeRejuvenateConsent();
        BitConverter.GetBytes((uint)Restore).CopyTo(reply, 2);
        friend.SendRpc(reply);
        await friend.WaitForViewUpdateAsync(2, "02 80 80 80 d1 00 01 00 01 00 80 80 01 00 00 00 01");
        await actor.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        await actor.WaitForViewUpdateAsync(target.ViewId, "01 2a ?? 40 00 3f 45 01 00");
        Check.Equal((ushort)1, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(friend), "Restore leaves one HP before the heal");

        int repairCursor = actor.World.RpcSnapshot().Count;
        actor.SkipReceived();
        EvidenceRetestWire.Cast(actor, Repair, target);
        await EvidenceRetestWire.CastBarAsync(actor, Repair, 2);
        await EvidenceRetestWire.TimerAsync(actor, repairCursor, Repair, AbilityCooldowns.OwnTimer, 4);
        var revived = await AbilityEvidenceDirectHealsMode.ReadHealthAsync(friend);
        Check.Equal((ushort)11, revived, "Repair raises the live restored ally from 1 to 11 HP");
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Flagged restore and heal wire framing");
        Console.WriteLine($"RESTORE PVP PASS: 325 consent flags CSR, target stands at 1/1, 77 heals to {revived} HP.");
        return Array.Empty<byte>();
    }
}

