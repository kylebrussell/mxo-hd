using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterAwakenedToggleUnloadMode : NativeQaMode
{
    private const int Level = 18, X = 24800, Y = 495, Z = -1498;
    private const ushort Punishing = 16, Sprint = 184, Evade = 217;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Punishing, Sprint, Evade).Order().ToArray();
    // Stock captures 356285d5e8e9:699–700, 11701b9dd84e at 18:40:50, and PR524's Evade toggle.
    private const string PunishingUse = "80b91000000000000000000000000000000000000000000000000000e05bc40001000000086b0000000000";
    private const string SprintUse = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";

    public override string Name => "ability-parity-starter-awakened-toggle-unload";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "innerStrC=800,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={X + 300},y={Y},z={Z - 300} WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Punishing, Sprint, Evade },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "16,184,217", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Stay on the clear outdoor stand, with no selection and no combat. QAFriend watches from southeast. Use page 2 slots 1 and 2 for Punishing Blows and Hyper-Sprint. CSR has 800 IS and natural regen is held. Expect [MODIFIER] source=16 stat=212 +15 and source=184 stats 115/877/878 +15, plus [BUFF-DRAIN] amount1 per second for 16 and amount25 every five seconds for 184.",
            "While both are on, enter ?qaunload 16. Confirm the 212 removal and no further ability16 debit. Keep Sprint active through at least one more five-second drain. Enter ?qaload 16 and confirm it stays off.",
            "Enter ?qaunload 184. Expect three +15 movement records removed, speed reset on owner and observer, no later ability184 debit, and 5 s own reuse. Reload 184 and confirm speed stays at baseline.",
            "Use slot 3 for Evade Combat at rest. Expect shield on and a 40 IS debit. Enter ?qaunload 217 and expect Evade state and shield removal plus 30 s own reuse. Reload 217 and confirm it stays off. Enter submits each command."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && rows.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                rows.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Every ranked prerequisite fits stock memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        ushort observed = (await observer.WaitForViewNamedAsync(actor.Handle)).ViewId;

        int punishingAt = await ActivateAsync(Punishing, PunishingUse);
        await BonusAsync(punishingAt, Punishing, 212, 15, false);
        int observerAt = observer.World.ViewSnapshot().Count;
        int sprintAt = await ActivateAsync(Sprint, SprintUse);
        foreach (ushort stat in new ushort[] { 115, 877, 878 }) await BonusAsync(sprintAt, Sprint, stat, 15, false);
        await ObserveSpeedAsync(observerAt, .15f, 0x0400);
        await DrainAsync(0, Punishing, 1);
        await DrainAsync(0, Sprint, 25);

        int removed = await UnloadAsync(Punishing);
        await BonusAsync(removed, Punishing, 212, 15, true);
        int afterPunishing = server.DebugLog().Length;
        await ReloadAsync(Punishing);
        await DrainAsync(afterPunishing, Sprint, 25);
        Check.That(!server.DebugLog()[afterPunishing..].Contains("[BUFF-DRAIN] char110 ability16 "),
            "Unloaded Punishing Blows stops while Sprint continues spending IS");

        observerAt = observer.World.ViewSnapshot().Count;
        removed = await UnloadAsync(Sprint);
        foreach (ushort stat in new ushort[] { 115, 877, 878 }) await BonusAsync(removed, Sprint, stat, 15, true);
        await ObserveSpeedAsync(observerAt, 0, 0);
        await EvidenceRetestWire.TimerAsync(actor, removed, Sprint, AbilityCooldowns.OwnTimer, 5);
        int afterSprint = server.DebugLog().Length;
        await ReloadAsync(Sprint);
        await EvidenceRetestWire.TimerAsync(actor, removed, Sprint, AbilityCooldowns.OwnTimer, 5, ended: true);
        Check.That(!server.DebugLog()[afterSprint..].Contains("[BUFF-DRAIN] char110 ability184 "),
            "Unloaded Sprint does not spend IS after its next former tick");

        int evadeAt = await ActivateAsync(Evade, EvadeUse);
        await BonusAsync(evadeAt, Evade, EvasionShield.StateAbility, 0, false);
        removed = await UnloadAsync(Evade);
        await BonusAsync(removed, Evade, EvasionShield.StateAbility, 0, true);
        await EvidenceRetestWire.TimerAsync(actor, removed, Evade, AbilityCooldowns.OwnTimer, 30);
        await ReloadAsync(Evade);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Awakened unload framing");
        return Array.Empty<byte>();

        async Task<int> ActivateAsync(ushort id, string capture)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(capture);
            await ExclusiveAsync(start, id, true);
            return start;
        }

        async Task<int> UnloadAsync(ushort id)
        {
            int start = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { id }, load: false);
            await ExclusiveAsync(start, id, false);
            return start;
        }

        async Task ReloadAsync(ushort id)
        {
            int start = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { id }, load: true);
            Check.That(!actor.World.RpcSnapshot().Skip(start).Any(frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(id, true))),
                $"Reloading {id} does not reactivate it");
        }

        Task<RpcRecord> ExclusiveAsync(int from, ushort id, bool on) => StockAbilityProfile.ObserveAsync(actor, from,
            frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(id, on)), $"Toggle {id} on={on}");

        Task<RpcRecord> BonusAsync(int from, ushort id, ushort stat, int value, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, from, frame => StockAbilityProfile.Bonus(frame, id, stat, value, 0, remove),
                $"Modifier {id}/{stat} remove={remove}");

        Task DrainAsync(int from, ushort id, int cost) => actor.WaitUntilAsync(() => server.DebugLog()[from..].Split('\n')
            .Any(line => line.Contains($"[BUFF-DRAIN] char110 ability{id} ") && line.Contains($" amount{cost} ")),
            $"Toggle {id} spends {cost} IS", TimeSpan.FromSeconds(9));

        Task ObserveSpeedAsync(int from, float scale, ushort bits)
        {
            byte[] update = PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(scale)), (27, PlayerViewAttributes.U16(bits)));
            var packet = BytePattern.Parse(Convert.ToHexString(BitConverter.GetBytes(observed).Concat(update).ToArray()));
            return observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(from).Any(packet.Occurs),
                $"Observer sees movement {scale} and flags {bits:x4}", HeadlessPlayer.DefaultTimeout);
        }
    }
}
