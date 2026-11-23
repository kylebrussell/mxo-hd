using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerReactiveCodeMode : NativeQaMode
{
    private const ushort ReactiveCode = 941, CodeConversion = 943, TrojanHorse = 945;
    private const int X = 17000, Y = 495, Z = 2300;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, ReactiveCode, CodeConversion, TrojanHorse).Order().ToArray();
    // Complete stock PR545 requests: 943 on/off at 1797/2454, 941 at 3344/4865, 945 at 6379/6727.
    private const string StockConversionOn = "80b9af030000000000000000000000000000000000000000000000000000000084d75741086b0000000000";
    private const string StockConversionOff = "80b9af03000000000000000000000000000000000000000000000000585cbc0001000000086b0000000000";
    private const string StockReactive = "80b9ad03000000000000000000000000000000000000000000000000585cbc0001000000086b0000000000";
    private const string StockTrojanHorse = "80b9b103000000000000000000000000000000000000000000000000585cbc0001000000086b0000000000";

    public override string Name => "ability-parity-starter-hacker-reactive-code";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET x=CASE charId WHEN 6111 THEN {X - 120} WHEN 6114 THEN {X + 220} ELSE {X} END,y={Y},z={Z},alignment=CASE charId WHEN 6114 THEN 2 ELSE 1 END,level=50,exp={PlayerProgression.ExperienceForLevel(50)},pvpflag=0,healthC=CASE charId WHEN 6111 THEN 250 WHEN 6114 THEN 1500 ELSE 500 END,healthM=CASE charId WHEN 6114 THEN 2000 ELSE 500 END,innerStrC=500,innerStrM=500 WHERE charId IN (110,6111,6114)",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)"
        }, DevCommands: true, FixedCallbackRolls: true, HealthCallbackQa: true, hotbarAbilities: new[] { CodeConversion, ReactiveCode, TrojanHorse },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "941,943,945", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            "Page 2 slots 1–3 are Code Conversion, Reactive Code and Trojan Horse. Clear selection and stay at spawn. QAFriend is 1.2 m west at 250/500 HP. QAEnemy is 2.2 m east at 1500/2000 HP. This disposable fixture holds natural HP regen and fixes callback draws to zero.",
            "Stage A. Press slot 1 and wait for a 6-IS drain tick. Enter ?health 350 on CSR. Expect one 944 pulse. CSR goes to 410 and QAFriend to 310. QAEnemy stays at 1500. Press slot 1 again and check that the 15 s reuse starts on deactivation. Then enter ?health 300. No new 944 pulse. Wait 3 s before slot 2.",
            "Stage B. Press slot 2 and wait for a 7-IS drain tick. Use ?health 250, then ?health 450. QAEnemy takes exactly 50 from each change and stays alive at 1400. QAFriend stays at 310. Press slot 2 again for the 15 s reuse, then enter ?health 300 and confirm 942 stops. Wait 3 s before slot 3.",
            "Stage C. Press slot 3. The fixture fixes the 946 draw to a hit. Wait for one pulse at 4 s, one hostile recipient and 20 IS debited from both QAEnemy and CSR. Turn it off after the pulse. Enter submits fixture commands."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var friend = await EnterHelperAsync("QAFriend");
        var enemy = await EnterHelperAsync("QAEnemy");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The three chains fit in memory");
        await actor.WaitForViewNamedAsync(friend.Handle);
        await actor.WaitForViewNamedAsync(enemy.Handle);
        await friend.WaitForViewNamedAsync(actor.Handle);
        await enemy.WaitForViewNamedAsync(actor.Handle);

        int debugAt = server.DebugLog().Length;
        int before = actor.World.RpcSnapshot().Count;
        int friendViews = friend.World.ViewSnapshot().Count;
        int enemyViews = enemy.World.ViewSnapshot().Count;
        actor.SendRpc(StockConversionOn);
        await ExclusiveAsync(before, CodeConversion, true);
        await WaitForDrainAsync(CodeConversion, 6, debugAt);
        await SetActorHealthAsync(350, 410);
        await WaitForHealthAsync(friend, friendViews, 310);
        await actor.WaitUntilAsync(() => server.DebugLog()[debugAt..].Contains(
            "[HEALTH-CALLBACK] 943 on CSR_Tester delta=150", StringComparison.Ordinal),
            "One external health change reaches 943", HeadlessPlayer.DefaultTimeout);
        await ToggleOffAsync(CodeConversion, StockConversionOff);
        await SetActorHealthAsync(300, 300);
        string conversionLog = server.DebugLog()[debugAt..];
        Check.Equal(1, conversionLog.Split("[HEALTH-CALLBACK] 943 ", StringSplitOptions.None).Length - 1,
            "944 healing does not re-enter 943 in the same transaction or fire after toggle-off");
        Check.That(!conversionLog.Contains("[HEALTH-EFFECT] 944 heals QAEnemy", StringComparison.Ordinal) &&
            PvpOpponentsFixture.HealthHistory(enemy.World.ViewSnapshot(), enemyViews, 2, self: true).Length == 0,
            "Code Conversion excludes the hostile");

        debugAt = server.DebugLog().Length;
        before = actor.World.RpcSnapshot().Count;
        byte[] enemyDamage = AbilityMessages.Damage(false, 942, actor.Handle, enemy.Handle, 50, 0);
        byte[] friendDamage = AbilityMessages.Damage(false, 942, actor.Handle, friend.Handle, 50, 0);
        friendViews = friend.World.ViewSnapshot().Count;
        int friendRpcs = friend.World.RpcSnapshot().Count;
        actor.SendRpc(StockReactive);
        await ExclusiveAsync(before, ReactiveCode, true);
        await WaitForDrainAsync(ReactiveCode, 7, debugAt);
        foreach (var (newHealth, enemyHealth) in new (ushort, ushort)[] { (250, 1450), (450, 1400) })
        {
            enemyViews = enemy.World.ViewSnapshot().Count;
            int enemyRpcs = enemy.World.RpcSnapshot().Count;
            await SetActorHealthAsync(newHealth, newHealth);
            await StockAbilityProfile.ObserveAsync(enemy, enemyRpcs, rpc => rpc.Bytes.AsSpan().SequenceEqual(enemyDamage),
                "Each external health change deals exactly 50 neutral damage to the hostile");
            await WaitForHealthAsync(enemy, enemyViews, enemyHealth);
        }
        await ToggleOffAsync(ReactiveCode, StockReactive);
        await SetActorHealthAsync(300, 300);
        Check.Equal(2, server.DebugLog()[debugAt..].Split("[HEALTH-CALLBACK] 941 ", StringSplitOptions.None).Length - 1,
            "No background or post-deactivation Reactive Code proc");
        Check.That(PvpOpponentsFixture.HealthHistory(friend.World.ViewSnapshot(), friendViews, 2, self: true).Length == 0 &&
            !friend.World.RpcSnapshot().Skip(friendRpcs).Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(friendDamage)),
            "Reactive Code excludes the healthy friendly recipient");

        before = actor.World.RpcSnapshot().Count;
        enemyViews = enemy.World.ViewSnapshot().Count;
        friendViews = friend.World.ViewSnapshot().Count;
        int actorViews = actor.World.ViewSnapshot().Count;
        debugAt = server.DebugLog().Length;
        actor.SendRpc(StockTrojanHorse);
        await ExclusiveAsync(before, TrojanHorse, true);
        await EvidenceRetestWire.TimerAsync(actor, before, TrojanHorse, AbilityCooldowns.OwnTimer, 15);
        await enemy.WaitUntilAsync(() => HasIS(enemy, enemyViews, 480),
            "A 946 pulse removes 20 IS from the surviving hostile", HeadlessPlayer.DefaultTimeout);
        await actor.WaitUntilAsync(() =>
        {
            var updates = InnerStrengthUpdates(actor, actorViews).ToArray();
            return updates.Zip(updates.Skip(1), (previous, after) => previous - after).Contains(20);
        }, "Caster pays the 20-IS pulse separately from regeneration", HeadlessPlayer.DefaultTimeout);
        Check.That(!HasIS(friend, friendViews, 480), "The same pulse leaves the nearby friendly untouched");
        Check.That(server.DebugLog()[debugAt..].Contains("[AURA] 945 pulse 946 reaches 1 targets", StringComparison.Ordinal),
            "The pulse reached exactly one target");
        before = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockTrojanHorse);
        await ExclusiveAsync(before, TrojanHorse, false);
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count + enemy.World.Problems.Count,
            "Reactive-area fixture packet framing");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> EnterHelperAsync(string handle)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            return helper;
        }

        Task<RpcRecord> ExclusiveAsync(int after, ushort id, bool on) => StockAbilityProfile.ObserveAsync(actor, after,
            frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(id, on)), $"Ability {id} on={on}");

        async Task ToggleOffAsync(ushort id, string request)
        {
            int after = actor.World.RpcSnapshot().Count;
            actor.SendRpc(request);
            await ExclusiveAsync(after, id, false);
            await EvidenceRetestWire.TimerAsync(actor, after, id, AbilityCooldowns.OwnTimer, 15);
            await Task.Delay(TimeSpan.FromSeconds(2.2)); // Toggle-off leaves the two-second cast guard in force.
        }

        Task WaitForDrainAsync(ushort id, int amount, int after) => actor.WaitUntilAsync(() => server.DebugLog()[after..]
            .Split('\n').Any(line => line.Contains($"[BUFF-DRAIN] char110 ability{id} ", StringComparison.Ordinal) &&
                line.Contains($" amount{amount} ", StringComparison.Ordinal)),
            $"Ability {id} pays its {amount}-IS recurring drain", HeadlessPlayer.DefaultTimeout);

        async Task SetActorHealthAsync(ushort value, ushort expected)
        {
            int after = actor.World.ViewSnapshot().Count;
            actor.SendChat($"?health {value}");
            await WaitForHealthAsync(actor, after, expected);
        }

        static Task WaitForHealthAsync(HeadlessPlayer player, int after, ushort expected) => player.WaitUntilAsync(() =>
            PvpOpponentsFixture.HealthHistory(player.World.ViewSnapshot(), after, 2, self: true).Contains(expected),
            $"Own health becomes {expected}", HeadlessPlayer.DefaultTimeout);
    }

    private static bool HasIS(HeadlessPlayer player, int after, ushort expected) =>
        InnerStrengthUpdates(player, after).Contains(expected);

    private static IEnumerable<ushort> InnerStrengthUpdates(HeadlessPlayer player, int after)
    {
        foreach (byte[] view in player.World.ViewSnapshot().Skip(after))
            if (BuffDrainLifetimeFixture.TryInnerStrength(view, out ushort value))
                yield return value;
    }
}
