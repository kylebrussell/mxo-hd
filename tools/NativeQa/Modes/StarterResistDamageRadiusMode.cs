using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterResistDamageRadiusMode : NativeQaMode
{
    private const int Level = 19, X = 24800, Y = 495, Z = -1498;
    private const ushort Resist = 74, GroupRepairs = 56, TeamPatcher = 92, Upgrade = 1089;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, StarterHackerViralSightMode.HackerStyle,
        Resist, GroupRepairs, TeamPatcher).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-resist-damage-radius";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "healthC=100,healthM=500,innerStrC=700,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={X + 250},y={Y},z={Z},healthC=100,healthM=500,alignment=1,pvpflag=0 WHERE charId=6111",
            $"UPDATE characters SET x={X + 625},y={Y},z={Z},healthC=100,healthM=500,alignment=1,pvpflag=0 WHERE charId=6112",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112)"
        }, DevCommands: true, hotbarAbilities: new[] { StarterHackerViralSightMode.HackerStyle, GroupRepairs, Upgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "56,74,1089", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAFriend ({X + 250},{Y},{Z}) and QAFar ({X + 625},{Y},{Z}), clear of the ice FX. Accept QAFriend's mission-team invite and confirm both helpers are on the team. Stand still and leave selection clear.",
            "Page 2 slots 1–3 are Hacker Style, Group Repairs 56 and Team Patcher Upgrade 1089. Select Hacker Style. At login expect [MODIFIER] char=110 source=74 stat=127 +9 and stat=1052 +1. Use slot 2. Expect 4 s cast/reuse and [ABILITY] Heal 56 for 9 on CSR_Tester, QAFriend and QAFar at 6.25 m.",
            "Enter ?qaunload 74 and check the matching 127/+9 and 1052/+1 removals. Use slot 2 again after reuse. CSR_Tester and QAFriend receive 9; QAFar is out of the 6 m base radius.",
            "With 74 still unloaded, use slot 3. Expect [MODIFIER] char=110 source=1089 stat=127 +7. Use slot 2 after reuse; QAFar again receives 9. Enter ?dismissbuff 1089 and check removal; repeat slot 2 and expect QAFar excluded again.",
            "Enter ?qaload 74 and check the two original records re-add. A final slot 2 reaches QAFar again. Read the Heal lines for the amounts because natural HP regeneration continues. No maximum [VITALS] change or interlock [PVP] rounds. This batch observes 127's actual consumer; it records 1052 without asserting a Sweep Bonus effect."
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
            _ = StarterTeamPatcherUpgradeRadiusMode.InviteNativeAsync(near, server);
            return Array.Empty<byte>();
        }

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Every ranked prerequisite fits the stock loadout");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await actor.WaitForViewNamedAsync(near.Handle);
        await actor.WaitForViewNamedAsync(far.Handle);
        await TeamBreadthMode.InviteAsync(near, actor);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        await PassiveAsync(0, false);
        await HealAsync(expectFar: true);
        int off = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Resist }, load: false);
        await PassiveAsync(off, true);
        await HealAsync(expectFar: false);

        int upgrade = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterTeamPatcherUpgradeRadiusMode.UpgradeStock);
        await RecordAsync(upgrade, Upgrade, 127, 7, false);
        await HealAsync(expectFar: true);
        int dismiss = actor.World.RpcSnapshot().Count;
        actor.SendChat("?dismissbuff 1089");
        await RecordAsync(dismiss, Upgrade, 127, 7, true);
        await HealAsync(expectFar: false);

        int on = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Resist }, load: true);
        await PassiveAsync(on, false);
        await HealAsync(expectFar: true);
        Check.Equal(0, actor.World.Problems.Count + near.World.Problems.Count + far.World.Problems.Count, "Resist Damage framing");
        Console.WriteLine("RESIST DAMAGE PASS: 6.25 m teammate reached by 74 alone / neither / 1089 alone / neither / 74 restored.");
        return Array.Empty<byte>();

        async Task PassiveAsync(int from, bool removed)
        {
            await RecordAsync(from, Resist, 127, 9, removed);
            await RecordAsync(from, Resist, 1052, 1, removed);
        }

        Task RecordAsync(int from, ushort id, ushort stat, int value, bool removed) =>
            StockAbilityProfile.ObserveAsync(actor, from,
                frame => StockAbilityProfile.Bonus(frame, id, stat, value, 0, removed),
                $"Source {id} stat {stat}, removed={removed}");

        async Task HealAsync(bool expectFar)
        {
            int log = server.DebugLog().Length;
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(StarterTeamPatcherUpgradeRadiusMode.GroupStock);
            await EvidenceRetestWire.CastBarAsync(actor, GroupRepairs, 4);
            await EvidenceRetestWire.TimerAsync(actor, cursor, GroupRepairs, AbilityCooldowns.OwnTimer, 4, ended: true);
            string segment = server.DebugLog()[log..];
            foreach (string target in new[] { actor.Handle, near.Handle, far.Handle })
                Check.Equal(target != far.Handle || expectFar, segment.Contains($"[ABILITY] Heal 56 -> {target} for 9"),
                    $"Group Repairs reach at the fixed stand for {target}");
        }
    }
}
