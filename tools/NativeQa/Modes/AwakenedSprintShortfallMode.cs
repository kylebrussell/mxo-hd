using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedSprintShortfallMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, Level = 25;
    private const ushort Speed = 12, Sprint = 184, Evade = 217;
    // Native 11701b9dd84e Speed (3289–3290) and Sprint, PR524 Evade and retail Speed dismissal.
    private const string SpeedUse = "80b90c00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string SprintUse = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Speed, Sprint, Evade).Order().ToArray();

    public override string Name => "ability-parity-awakened-sprint-shortfall";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "innerStrC=140,innerStrM=140 WHERE charId=110",
            $"UPDATE characters SET x={X + 300},y={Y},z={Z} WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Speed, Evade, Sprint }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "12,184,217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Level 25 supplies the rank-25 Hyper-Jump prerequisite for Hyper-Speed. Stay at CSR (17000,495,2300) with no target or combat. QAFriend observes from (17300,495,2300). Page 2 slots 1–3 are Hyper-Speed, Evade Combat and Hyper-Sprint. IS starts at 140 with regen held.",
            "Press slots 1, 2, then 3. IS goes 140 → 100 → 60 → 35. Expect 12 movement scale +0.45, [EVASION] state on, then 184 scale +0.15 with both movement flags set. Expect source=12 stat=115 value=+45 duration=600s and source=184 stats 115/877/878 +15 duration=0s.",
            "Wait. First Sprint tick spends 25 to leave 10 IS. The next tick cannot pay, emits one shortfall of 15, removes just 184's records and starts 5 s reuse. Owner and witness fall back to +0.45 with flags 0x1000. 217 remains on and there is no second debit.",
            "Type ?dismissbuff 12 to recover zero movement scale while Evade remains on. Then toggle 217 off and check its shield removal and 30 s reuse. No maximum [VITALS] change or interlock [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var levels = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => levels.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => levels.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The complete Awakened profile fits stock memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        ushort observerView = (await observer.WaitForViewNamedAsync(actor.Handle)).ViewId;

        int speedAt = actor.World.RpcSnapshot().Count;
        await UseAsync(SpeedUse, 100);
        await BonusAsync(speedAt, Speed, 115, 45, 600, false);
        int evadeAt = actor.World.RpcSnapshot().Count;
        await UseAsync(EvadeUse, 60);
        await BonusAsync(evadeAt, Evade, EvasionShield.StateAbility, 0, 0, false);

        int sprintView = observer.World.ViewSnapshot().Count;
        int sprintOwn = actor.World.ViewSnapshot().Count;
        int sprintAt = actor.World.RpcSnapshot().Count;
        await UseAsync(SprintUse, 35);
        await ObserverMovementAsync(sprintView, .15f, 0x1400);
        foreach (ushort stat in new ushort[] { 115, 877, 878 })
            await BonusAsync(sprintAt, Sprint, stat, 15, 0, false);
        await WaitForIsAsync(sprintOwn, 10);
        var shortage = BytePattern.Parse("81 67 ?? ?? ?? ?? b4 20 00 c6 01 b8 00");
        await StockAbilityProfile.ObserveAsync(actor, sprintAt,
            frame => shortage.IsMatch(frame.Bytes) && BitConverter.ToInt32(frame.Bytes, frame.Bytes.Length - 4) == 15,
            "Sprint emits the exact shortfall once, preserving the last 10 IS");
        await EvidenceRetestWire.TimerAsync(actor, sprintAt, Sprint, AbilityCooldowns.OwnTimer, 5);
        await ObserverMovementAsync(sprintView, .45f, 0x1000);
        foreach (ushort stat in new ushort[] { 115, 877, 878 })
            await BonusAsync(sprintAt, Sprint, stat, 15, 0, true);
        Check.That(!actor.World.RpcSnapshot().Skip(evadeAt).Any(frame =>
            StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, true)),
            "Sprint shortfall cannot terminate the Evade shield");

        int speedOff = actor.World.RpcSnapshot().Count;
        int observerOff = observer.World.ViewSnapshot().Count;
        actor.SendRpc("80 bb 0c 00");
        await BonusAsync(speedOff, Speed, 115, 45, 600, true);
        await ObserverMovementAsync(observerOff, 0, 0, includeFlags: false);
        int evadeOff = actor.World.RpcSnapshot().Count;
        actor.SendRpc(EvadeUse);
        await BonusAsync(evadeOff, Evade, EvasionShield.StateAbility, 0, 0, true);
        await EvidenceRetestWire.TimerAsync(actor, evadeOff, Evade, AbilityCooldowns.OwnTimer, 30);
        Check.Equal(1, actor.World.RpcSnapshot().Skip(sprintAt).Count(frame => shortage.IsMatch(frame.Bytes)), "One Sprint shortfall");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Awakened shortfall framing");
        Console.WriteLine("AWAKENED SHORTFALL PASS: 140 -> 100 -> 60 -> 35 -> 10, Sprint releases and Speed/Evade remain.");
        return Array.Empty<byte>();

        async Task UseAsync(string capture, ushort isAfter)
        {
            int cursor = actor.World.ViewSnapshot().Count;
            actor.SendRpc(capture);
            await WaitForIsAsync(cursor, isAfter);
        }

        Task WaitForIsAsync(int from, ushort value)
        {
            string isWire = EvidenceRetestWire.Le(value);
            var speedCost = BytePattern.Parse($"02 00 02 88 05 8c 1e 66 66 e6 3e 80 90 {isWire}");
            var sprintCost = BytePattern.Parse($"02 00 02 80 88 9a 99 19 3e 80 90 {isWire}");
            return actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(from).Any(frame =>
                AbilityParityCoderSweepMode.InnerStrengthUpdates(frame).Contains(value) ||
                speedCost.Occurs(frame) || sprintCost.Occurs(frame)), $"Current IS {value}", TimeSpan.FromSeconds(13));
        }

        Task BonusAsync(int from, ushort id, ushort stat, int value, int duration, bool removed) =>
            StockAbilityProfile.ObserveAsync(actor, from, frame => StockAbilityProfile.Bonus(frame, id, stat, value, duration, removed),
                $"Source {id} stat {stat}, removed={removed}");

        Task ObserverMovementAsync(int from, float scale, ushort flags, bool includeFlags = true)
        {
            byte[] update = includeFlags
                ? PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(scale)), (27, PlayerViewAttributes.U16(flags)))
                : PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(scale)));
            var pattern = BytePattern.Parse(Convert.ToHexString(BitConverter.GetBytes(observerView).Concat(update).ToArray()));
            return observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(from).Any(pattern.Occurs),
                $"Observer movement {scale}, flags {flags:x4}", TimeSpan.FromSeconds(13));
        }
    }
}
