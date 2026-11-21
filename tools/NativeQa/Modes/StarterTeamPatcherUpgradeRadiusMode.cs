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
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, GroupRepairs, Upgrade }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "71,74,1089", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full prerequisite profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Stay on the outdoor spawn facing the two helpers 2.5 and 6.7 m east. Accept QAFriend's mission-team invite. Keep selection clear. Natural HP regeneration is held for all three players.",
            "Enter ?qaunload 71 then ?qaload 71. Expect [MODIFIER] char=110 source=71 stat=1052 and stat=1141, both value=+1 duration=0s, remove then add. Source=74 remains loaded (stat=1052 +1, stat=127 +9). It raises the group heal radius to 6.54 m before the upgrade.",
            "Use Hacker Style on page 2 slot 1, then Group Repairs on slot 2. Expect two '[ABILITY] Heal 56 -> <handle> for 9' lines for CSR and QAFriend. QAFar at 6.7 m is untouched. Expect 4 s cast and 4 s reuse. Wait until slot 2 is ready.",
            "Use Team Patcher Upgrade (slot 3). Expect [MODIFIER] char=110 source=1089 stat=127/+7, 174/+3, 520/+10 and 564/+7, all duration=0s. No such adds on helpers. Cast slot 2 again. Radius is now 6.96 m and '[ABILITY] Heal 56 -> QAFar for 9' joins the CSR/QAFriend lines. Enter ?dismissbuff 1089. Expect all four matching removals, then a third group heal again excludes QAFar."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
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
        int unload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Repair }, load: false);
        await RecordsAsync(unload, Repair, RepairStats, remove: true);
        int reload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Repair }, load: true);
        await RecordsAsync(reload, Repair, RepairStats, remove: false);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, remove: false), "Team Patcher grants 1089");
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        await HealAsync(nearAfter: 109, farAfter: null);

        var helperCursors = new[] { near, far }.ToDictionary(player => player, player => player.World.RpcSnapshot().Count);
        int apply = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UpgradeStock);
        await RecordsAsync(apply, Upgrade, UpgradeStats, remove: false);
        await EvidenceRetestWire.TimerAsync(actor, apply, Upgrade, AbilityCooldowns.OwnTimer, 30);
        await HealAsync(nearAfter: 118, farAfter: 109);
        foreach (var entry in helperCursors)
            Check.That(!entry.Key.World.RpcSnapshot().Skip(entry.Value).Any(frame => UpgradeStats.Any(stat =>
                StockAbilityProfile.Bonus(frame, Upgrade, stat.Stat, stat.Value, 0, false))), "Upgrade modifiers are caster-only");

        int remove = actor.World.RpcSnapshot().Count;
        actor.SendChat("?dismissbuff 1089");
        await RecordsAsync(remove, Upgrade, UpgradeStats, remove: true);
        await HealAsync(nearAfter: 127, farAfter: null);
        Check.Equal(0, actor.World.Problems.Count + near.World.Problems.Count + far.World.Problems.Count,
            "Team Patcher modifier and group heal records frame cleanly");
        return Array.Empty<byte>();

        async Task HealAsync(ushort nearAfter, ushort? farAfter)
        {
            int logAt = server.DebugLog().Length;
            int castAt = actor.World.RpcSnapshot().Count;
            int nearAt = near.World.ViewSnapshot().Count;
            int farAt = far.World.ViewSnapshot().Count;
            actor.SendRpc(GroupStock);
            await EvidenceRetestWire.CastBarAsync(actor, GroupRepairs, 4);
            await EvidenceRetestWire.ViewSinceAsync(near, nearAt, 2, $"02 80 80 80 40 {EvidenceRetestWire.Le(nearAfter)}");
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
