using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerPassiveFixture;

namespace NativeQa.Modes;

public sealed class StarterGuardianSightMode : NativeQaMode
{
    internal const int CasterX = 21750, ClearX = 21550, FloorY = 1095, StandZ = 16150;
    private const int BlockedX = 22350;
    private const int StartingIs = 700;
    private const ushort FastHealing = 46, Firewall = 68, Enhancement = 173;
    private static readonly ushort[] Abilities = Loadout(FastHealing, Firewall, Enhancement);
    internal static readonly (ushort Id, ushort Cost, float Duration, (ushort Stat, int Value)[] Bonuses)[] Samples =
    {
        (FastHealing, 35, 600, new[] { ((ushort)112, 40) }),
        (Firewall, 40, 300, new[] { ((ushort)157, 40), ((ushort)180, 10), ((ushort)181, 10), ((ushort)1033, 10) }),
        (Enhancement, 40, 300, new[] { ((ushort)522, 24), ((ushort)524, 24), ((ushort)1032, 24) }),
    };

    public override string Name => "ability-parity-starter-guardian-sight";

    public override NativeQaModePlan Plan { get; } = new(Abilities, CasterX, StandZ, Array.Empty<string>(),
        new[] { CapRanks }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={CasterX},y={FloorY},z={StandZ}," +
                $"rotation={MobAggro.YawTowards(CasterX,StandZ,ClearX,StandZ)},innerStrC={StartingIs},innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={ClearX},y={FloorY},z={StandZ},alignment=1,pvpflag=0,healthC=100,healthM=500 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Style, FastHealing, Firewall, Enhancement },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "46,68,173", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({CasterX},{FloorY},{StandZ}) faces QAFriend ({ClearX},{FloorY},{StandZ}), 2 m west. Both helper stands and the CSR stand are on verified outdoor NAV clear of the ice.",
            "Choose Hacker Style on page 2 slot 1 and type /target QAFriend. Move QAFriend to BLOCKED without clearing selection. Press slots 2/3/4. Save whether each was refused locally or sent. Any sent attempt must get 80 ba reason 9 with no [ABILITY-COST], cast or reuse and no [MODIFIER] or ability [VITALS].",
            $$"""BLOCKED helper request: {"actor":"QAFriend","action":"move","x":{{BlockedX}},"y":{{FloorY}},"z":{{StandZ}}}. CLEAR helper request: {"actor":"QAFriend","action":"move","x":{{ClearX}},"y":{{FloorY}},"z":{{StandZ}}}. CSR stays on the original stand. BLOCKED is 6 m from CSR across the mapped gap, still inside the 10 m ability range.""",
            "Move QAFriend to CLEAR, reselect if needed, and cast slots 2/3/4. All cast in 2 s with 13 s own reuse. Look for [ABILITY-COST] 35/40/40 (IS 700 to 665 to 625 to 585) and [MODIFIER] char=6111 source=46 stat=112 +40 for 600 s, source=68 stats 157 +40 and 180/181/1033 +10 for 300 s, then source=173 stats 522/524/1032 +24 for 300 s. None belongs to CSR. No PvP round or maximum [VITALS] change.",
            "For a timed sight check, wait for reuse, then use slot 2 from CLEAR and move QAFriend to BLOCKED during its 2 s cast bar. Expect [ABILITY] 46 fizzles: sight lost to QAFriend, normal 35 IS and reuse, and no fresh bonus. The headless bootstrap verifies this transition for all three. Save first stock ability request frames."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { CasterX, ClearX, BlockedX })
            Check.That(NavMesh.Locate(1, x, FloorY, StandZ, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - FloorY) <= 100, $"Verified outdoor NAV stand ({x},{FloorY},{StandZ})");
        Check.That(NavMesh.HasSightPath(1, CasterX, FloorY, StandZ, ClearX, FloorY, StandZ) &&
            !NavMesh.HasSightPath(1, CasterX, FloorY, StandZ, BlockedX, FloorY, StandZ),
            "Both stands are within range and only the close stand has mapped sight");

        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await PvpOpponentsFixture.EnterAsync(friend);
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        VerifyProfile(actor, Abilities);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await actor.WaitForViewNamedAsync(friend.Handle);
        actor.SendRpc(StyleFrame);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)))}");

        await MoveAsync(BlockedX);
        foreach (var sample in Samples)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            // PR316 captured this full targeted support layout. Substitute the ability and live view/spawn only.
            EvidenceRetestWire.Cast(actor, sample.Id, view);
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame =>
                AbilityRefusals.TryDecode(frame.Bytes, out var denial) && denial.AbilityID == sample.Id &&
                denial.ErrorID == AbilityRefusals.TargetNotInSight, $"Sight refusal for {sample.Id}");
            await StreamAdvancedAsync(actor, cursor);
            Check.That(!actor.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Opcode is 0xac or 0xbd),
                $"Blocked cast {sample.Id} sends no cast bar or reuse");
            Check.That(!actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(isValue => isValue != StartingIs),
                $"Blocked cast {sample.Id} spends no IS");
        }

        int expectedIs = StartingIs;
        foreach (var sample in Samples)
        {
            await MoveAsync(ClearX);
            int logs = server.DebugLog().Length;
            int receiverCursor = friend.World.RpcSnapshot().Count;
            int castCursor = await BeginAsync(sample.Id);
            await MoveAsync(BlockedX);
            await actor.WaitUntilAsync(() => server.DebugLog()[logs..].Contains($"[ABILITY] {sample.Id} fizzles: sight lost to QAFriend"),
                $"Accepted {sample.Id} fizzles when the selected helper goes out of sight", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, castCursor, sample.Id, AbilityCooldowns.OwnTimer, 13);
            await AssertIsAsync(expectedIs -= sample.Cost);
            await StreamAdvancedAsync(friend, receiverCursor);
            Check.That(!friend.World.RpcSnapshot().Skip(receiverCursor).Any(frame => sample.Bonuses.Any(bonus =>
                StockAbilityProfile.Bonus(frame, sample.Id, bonus.Stat, bonus.Value, sample.Duration, false))),
                $"Lost sight prevents {sample.Id} from modifying the recipient");
        }

        await MoveAsync(ClearX);
        int clearHealingCast = 0;
        foreach (var sample in Samples)
        {
            await EvidenceRetestWire.TimerAsync(actor, 0, sample.Id, AbilityCooldowns.OwnTimer, 13, ended: true);
            int receiverCursor = friend.World.RpcSnapshot().Count;
            int castCursor = await BeginAsync(sample.Id);
            if (sample.Id == FastHealing) clearHealingCast = castCursor;
            await EvidenceRetestWire.TimerAsync(actor, castCursor, sample.Id, AbilityCooldowns.OwnTimer, 13);
            await AssertIsAsync(expectedIs -= sample.Cost);
            foreach (var (stat, value) in sample.Bonuses)
                await StockAbilityProfile.ObserveAsync(friend, receiverCursor, frame =>
                    StockAbilityProfile.Bonus(frame, sample.Id, stat, value, sample.Duration, false), $"Clear {sample.Id}/{stat} on QAFriend");
        }

        await MoveAsync(BlockedX);
        // A null target still casts Fast Healing on self even with QAFriend across the gap.
        actor.SendRpc("81 51 00 00 00 00");
        await EvidenceRetestWire.TimerAsync(actor, clearHealingCast, FastHealing, AbilityCooldowns.OwnTimer, 13, ended: true);
        int selfCursor = actor.World.RpcSnapshot().Count;
        actor.SkipReceived();
        EvidenceRetestWire.Cast(actor, FastHealing);
        await EvidenceRetestWire.CastBarAsync(actor, FastHealing, 2);
        await StockAbilityProfile.ObserveAsync(actor, selfCursor, frame =>
            StockAbilityProfile.Bonus(frame, FastHealing, 112, 40, 600, false), "Unset selection still resolves to self");
        await AssertIsAsync(expectedIs - 35);
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Guardian sight regression wire framing");
        Console.WriteLine("GUARDIAN SIGHT PASS 46/68/173");
        return Array.Empty<byte>();

        async Task<int> BeginAsync(ushort id)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            EvidenceRetestWire.Cast(actor, id, view);
            await EvidenceRetestWire.CastBarAsync(actor, id, 2);
            return cursor;
        }

        async Task MoveAsync(int x)
        {
            byte[] position = new[] { (float)x, (float)FloorY, (float)StandZ }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = actor.World.ViewSnapshot().Count;
            friend.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(actor, cursor, view.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }

        Task AssertIsAsync(int expected) => actor.WaitUntilAsync(() =>
            actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)expected),
            $"IS reaches {expected}", HeadlessPlayer.DefaultTimeout);

        static async Task StreamAdvancedAsync(HeadlessPlayer player, int cursor)
        {
            player.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(player, cursor, frame => frame.Opcode == 0x149,
                "Recipient stream has advanced beyond the request");
        }
    }
}
