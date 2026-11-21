using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterTeamPatcherUpgradeRadiusMode : NativeQaMode
{
    private const int Level = 19, X = 24800, Y = 495, Z = -1498;
    private const ushort Hacker = 1006, Upgrade = 1089, GroupRepairs = 56, Repair = 71, Resist = 74;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Hacker, 92, Repair, Resist, GroupRepairs)
        .Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private static readonly (ushort Stat, int Value)[] RepairStats = { (1052, 1), (1141, 1) };
    private static readonly (ushort Stat, int Value)[] ResistStats = { (1052, 1), (127, 9) };
    private static readonly (ushort Stat, int Value)[] UpgradeStats = { (127, 7), (174, 3), (520, 10), (564, 7) };
    // Native PR446 9e937c5aa11c:4351 and stock fa273b663f5e:7976, no explicit targets.
    internal const string UpgradeStock = "80b94104000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    internal const string GroupStock = "80b93800000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-starter-team-patcher-upgrade-radius";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "healthC=100,healthM=500,innerStrC=700,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={X+250},y={Y},z={Z},healthC=100,healthM=500,alignment=1,pvpflag=0 WHERE charId=6111",
            $"UPDATE characters SET x={X+670},y={Y},z={Z},healthC=100,healthM=500,alignment=1,pvpflag=0 WHERE charId=6112",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112)"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, GroupRepairs, Upgrade }, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "56,71,74,1089", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAFriend ({X + 250},{Y},{Z}) and QAFar ({X + 670},{Y},{Z}), all outdoor NAV floor 400. Accept the team invite and keep selection clear. All three begin at 100 HP with natural HP regen held.",
            "Select Hacker Style on page 2 slot 1, then type ?qaunload 71. Expect [MODIFIER] 71/1052 and 71/1141 +1 removals. With 74 still loaded, 56 heals CSR and QAFriend for 8 (100 to 108) and leaves QAFar out of range. Wait for slot 2 reuse.",
            "Type ?qaload 71 and confirm both +1 adds. Cast the same 56 on slot 2. It now heals CSR and QAFriend for 9 (108 to 117), from the same stands. Same 4 s cast, 4 s reuse and no heal on QAFar.",
            "Use Team Patcher Upgrade (slot 3). Expect only CSR [MODIFIER] 1089/127 +7, 174 +3, 520 +10, 564 +7. Cast slot 2 again. QAFar first receives 9 (100 to 109), and CSR/QAFriend reach 126. Type ?dismissbuff 1089 and confirm the four removals. The last 56 reaches 135 on CSR/QAFriend and leaves QAFar at 109."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Team Patcher needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, X + 250, X + 670 })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Team Patcher stand ({x},{Y},{Z}) is on outdoor NAV");
        var server = context.ActualServer!;
        foreach (string handle in new[] { "QAFriend", "QAFar" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var near = context.Helpers["QAFriend"];
        var far = context.Helpers["QAFar"];
        await TeamBreadthMode.InviteAsync(near, far);
        if (context.StockProbe is not { } actor)
        {
            _ = InviteNativeAsync(near, server);
            return Array.Empty<byte>();
        }

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The level-19 fixture carries all ranked prerequisites while the upgrade comes only from its grant");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await actor.WaitForViewNamedAsync(near.Handle);
        await actor.WaitForViewNamedAsync(far.Handle);
        await TeamBreadthMode.InviteAsync(near, actor);

        await RecordsAsync(0, Resist, ResistStats, remove: false);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        int unload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Repair }, load: false);
        await RecordsAsync(unload, Repair, RepairStats, remove: true);
        await HealAsync(nearAfter: 108, farAfter: null);
        int reload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Repair }, load: true);
        await RecordsAsync(reload, Repair, RepairStats, remove: false);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, remove: false), "Team Patcher grants 1089");
        await HealAsync(nearAfter: 117, farAfter: null);

        var helperCursors = new[] { near, far }.ToDictionary(player => player, player => player.World.RpcSnapshot().Count);
        int apply = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UpgradeStock);
        await RecordsAsync(apply, Upgrade, UpgradeStats, remove: false);
        await EvidenceRetestWire.TimerAsync(actor, apply, Upgrade, AbilityCooldowns.OwnTimer, 30);
        await HealAsync(nearAfter: 126, farAfter: 109);
        foreach (var entry in helperCursors)
            Check.That(!entry.Key.World.RpcSnapshot().Skip(entry.Value).Any(frame => UpgradeStats.Any(stat =>
                StockAbilityProfile.Bonus(frame, Upgrade, stat.Stat, stat.Value, 0, false))), "Upgrade modifiers are caster-only");

        int remove = actor.World.RpcSnapshot().Count;
        actor.SendChat("?dismissbuff 1089");
        await RecordsAsync(remove, Upgrade, UpgradeStats, remove: true);
        await HealAsync(nearAfter: 135, farAfter: null);
        Check.Equal(0, actor.World.Problems.Count + near.World.Problems.Count + far.World.Problems.Count,
            "Team Patcher modifier and group heal records frame cleanly");
        return Array.Empty<byte>();

        async Task HealAsync(ushort nearAfter, ushort? farAfter)
        {
            int logAt = server.DebugLog().Length;
            int castAt = actor.World.RpcSnapshot().Count;
            int ownerAt = actor.World.ViewSnapshot().Count;
            int nearAt = near.World.ViewSnapshot().Count;
            int farAt = far.World.ViewSnapshot().Count;
            actor.SendRpc(GroupStock);
            await EvidenceRetestWire.CastBarAsync(actor, GroupRepairs, 4);
            await EvidenceRetestWire.ViewSinceAsync(near, nearAt, 2, $"02 80 80 80 40 {EvidenceRetestWire.Le(nearAfter)}");
            await EvidenceRetestWire.ViewSinceAsync(actor, ownerAt, 2, $"02 80 80 80 40 {EvidenceRetestWire.Le(nearAfter)}");
            if (farAfter.HasValue)
                await EvidenceRetestWire.ViewSinceAsync(far, farAt, 2, $"02 80 80 80 40 {EvidenceRetestWire.Le(farAfter.Value)}");
            else
                Check.That(!server.DebugLog()[logAt..].Contains("Heal 56 -> QAFar"), "Outside teammate receives no group heal");
            await EvidenceRetestWire.TimerAsync(actor, castAt, GroupRepairs, AbilityCooldowns.OwnTimer, 4, ended: true);
        }

        async Task RecordsAsync(int cursor, ushort source, (ushort Stat, int Value)[] stats, bool remove)
        {
            foreach (var (stat, value) in stats)
                await StockAbilityProfile.ObserveAsync(actor, cursor, packet => StockAbilityProfile.Bonus(packet, source, stat, value, 0, remove),
                    $"{source}/{stat} value {value}, remove={remove}");
        }
    }

    internal static async Task InviteNativeAsync(HeadlessPlayer leader, ServerInstance server)
    {
        while (!server.HasExited)
        {
            if (leader.World.CreationSnapshot().Any(view => view.Name == "CSR_Tester"))
            {
                TeamBreadthMode.SendInvite(leader, "CSR_Tester");
                return;
            }
            await Task.Delay(200);
        }
    }
}
