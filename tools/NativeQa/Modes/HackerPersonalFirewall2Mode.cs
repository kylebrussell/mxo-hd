using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterGuardianSightMode;

namespace NativeQa.Modes;

public sealed class HackerPersonalFirewall2Mode : NativeQaMode
{
    private const ushort Firewall = 319, OriginalFirewall = 68;
    // Firewall requires Potency rank 24. Its Upgrade Artist discipline needs Potency rank 30.
    private const int Level = 30, BlockedX = 22350, StartIs = 700;
    private const float Duration = 300, Reuse = 13;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle,
        Firewall, OriginalFirewall).Order().ToArray();
    private static readonly (ushort Stat, int Value)[] ControlResistance = { (157, 40), (180, 10), (181, 10), (1033, 10) };
    // PR773 captured the complete targeted 68 request. Only its live handle is rebound.
    private const string StockFirewall = "80b944000400020000000000000000000000000000000000000000001060bf0001000000086b0000000000";

    public override string Name => "ability-parity-hacker-personal-firewall-2";

    public override NativeQaModePlan Plan { get; } = new(Abilities, CasterX, StandZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={CasterX},y={FloorY},z={StandZ}," +
                $"rotation={MobAggro.YawTowards(CasterX,StandZ,ClearX,StandZ)},innerStrC={StartIs},innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={ClearX},y={FloorY},z={StandZ}," +
                "alignment=1,pvpflag=0,healthC=100,healthM=1000 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Firewall, OriginalFirewall },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "319,68", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({CasterX},{FloorY},{StandZ}) faces QAFriend ({ClearX},{FloorY},{StandZ}), 2 m west. Both stands are on verified outdoor NAV away from the WinterFrost ice.",
            "Select Hacker Style with page 2 slot 1, then /target QAFriend. While QAFriend is blocked 6 m away, try slot 2. A local sight block is acceptable. If a request is sent, expect 80 ba reason 9 with no [ABILITY-COST], cast bar, reuse or modifier.",
            $$"""Blocked helper request: {"actor":"QAFriend","action":"move","x":{{BlockedX}},"y":{{FloorY}},"z":{{StandZ}}}. Clear helper request: {"actor":"QAFriend","action":"move","x":{{ClearX}},"y":{{FloorY}},"z":{{StandZ}}}.""",
            "Move QAFriend back to clear, reselect, and use slot 2. Record the first stock 319 request. Expect a 2 s cast, 45 IS (700 to 655), 13 s reuse, and [MODIFIER] char=6111 source=319 stat=157 +80 plus stats 180/181/1033 +20, all for 300 s. Armor bit 0x0100 reaches QAFriend and the observer. CSR gets no modifiers.",
            "Send QAFriend helper chat ?health 100 followed by ?dismissbuff 319. Expect the four remove records, visible armor clear, and [ABILITY] Healed 100 -> 350. If a regen tick slips between the commands, compare the before and after values in the heal line for +250.",
            "Use slot 3 on the same QAFriend as a rank control. Expect 2 s, 40 IS and 13 s reuse, source=68 stat=157 +40 and 180/181/1033 +10 for 300 s. Repeat helper chat ?health 100 and ?dismissbuff 68 and look for +75. Neither rank changes maximum [VITALS] or starts a [PVP] round."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { CasterX, ClearX, BlockedX })
            Check.That(NavMesh.Locate(1, x, FloorY, StandZ, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - FloorY) <= 100, $"Verified outdoor NAV ({x},{FloorY},{StandZ})");
        Check.That(NavMesh.HasSightPath(1, CasterX, FloorY, StandZ, ClearX, FloorY, StandZ) &&
            !NavMesh.HasSightPath(1, CasterX, FloorY, StandZ, BlockedX, FloorY, StandZ),
            "Clear and blocked helper stands have the required sight relation");

        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(caster, Abilities);
        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var target = await caster.WaitForViewNamedAsync(friend.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        byte[] controlRequest = StarterHackerControlTargetMode.WithHandle(StockFirewall, handle);
        await MoveAsync(BlockedX);
        await StarterHackerControlTargetMode.RefusedAsync(caster, OriginalFirewall, controlRequest, AbilityRefusals.TargetNotInSight);
        await MoveAsync(ClearX);
        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.1));
        int friendRpc = friend.World.RpcSnapshot().Count;
        int friendView = friend.World.ViewSnapshot().Count;
        int casterView = caster.World.ViewSnapshot().Count;
        int castRpc = caster.World.RpcSnapshot().Count;
        caster.SkipReceived();
        caster.SendRpc(controlRequest);
        await EvidenceRetestWire.CastBarAsync(caster, OriginalFirewall, 2);
        await EvidenceRetestWire.TimerAsync(caster, castRpc, OriginalFirewall, AbilityCooldowns.OwnTimer, Reuse);
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)660),
            "Original Firewall spends exactly 40 IS", HeadlessPlayer.DefaultTimeout);
        await BonusesAsync(friendRpc, remove: false);
        await ArmorAsync(0x100, friendView, casterView);
        Check.That(!caster.World.RpcSnapshot().Skip(castRpc).Any(rpc => ControlResistance.Any(stat =>
            StockAbilityProfile.Bonus(rpc, OriginalFirewall, stat.Stat, stat.Value, Duration, false))), "Original Firewall affects the selected friend only");

        friendRpc = friend.World.RpcSnapshot().Count;
        friendView = friend.World.ViewSnapshot().Count;
        casterView = caster.World.ViewSnapshot().Count;
        int termLog = server.DebugLog().Length;
        friend.SendChat("?health 100");
        await EvidenceRetestWire.ViewSinceAsync(friend, friendView, 2, Convert.ToHexString(ServerPackets.BuildSelfHealthUpdate(100)[2..]));
        friend.SendChat("?dismissbuff 68");
        await BonusesAsync(friendRpc, remove: true);
        await ArmorAsync(0, friendView, casterView);
        await friend.WaitUntilAsync(() => System.Text.RegularExpressions.Regex.Matches(server.DebugLog()[termLog..],
            @"\[ABILITY\] Healed (\d+) -> (\d+)").Any(match =>
                int.Parse(match.Groups[2].Value) - int.Parse(match.Groups[1].Value) == 75),
            "Original Firewall term heals the wounded recipient for 75", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, caster.World.Problems.Count + friend.World.Problems.Count, "Firewall wire framing");
        Console.WriteLine("PERSONAL FIREWALL BOOTSTRAP PASS: stock 68 control; 319 awaits its first stock capture.");
        return Array.Empty<byte>();

        async Task MoveAsync(int x)
        {
            byte[] position = new[] { (float)x, (float)FloorY, (float)StandZ }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = caster.World.ViewSnapshot().Count;
            friend.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(caster, cursor, target.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }

        async Task BonusesAsync(int cursor, bool remove)
        {
            foreach (var (stat, value) in ControlResistance)
                await StockAbilityProfile.ObserveAsync(friend, cursor,
                    rpc => StockAbilityProfile.Bonus(rpc, OriginalFirewall, stat, value, Duration, remove), $"68/{stat} {value}, remove={remove}");
        }

        async Task ArmorAsync(ushort bits, int ownerCursor, int observerCursor)
        {
            await EvidenceRetestWire.ViewSinceAsync(friend, ownerCursor, 2,
                Convert.ToHexString(PlayerViewAttributes.BuildSelf((36, PlayerViewAttributes.U16(bits)))[2..]));
            await EvidenceRetestWire.ViewSinceAsync(caster, observerCursor, target.ViewId,
                Convert.ToHexString(PlayerViewAttributes.Build((27, PlayerViewAttributes.U16(bits)))));
        }
    }
}
