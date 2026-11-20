using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerReactiveCodeMode : NativeQaMode
{
    private const ushort ReactiveCode = 941, CodeConversion = 943, TrojanHorse = 945;
    private const int X = 17000, Y = 495, Z = 2300;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, ReactiveCode, CodeConversion, TrojanHorse).Order().ToArray();
    // Both stock toggle requests in PR446, lines 4442–4443 and 4808–4809, are identical.
    private const string StockTrojanHorse = "80b9b103000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";

    public override string Name => "ability-parity-starter-hacker-reactive-code";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET x=CASE charId WHEN 6111 THEN {X - 120} WHEN 6114 THEN {X + 220} ELSE {X} END,y={Y},z={Z},alignment=CASE charId WHEN 6114 THEN 2 ELSE 1 END,level=50,exp={PlayerProgression.ExperienceForLevel(50)},pvpflag=0,healthC=500,healthM=500,innerStrC=500,innerStrM=500 WHERE charId IN (110,6111,6114)",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)"
        }, DevCommands: true, FixedCallbackRolls: true, HealthCallbackQa: true, hotbarAbilities: new[] { CodeConversion, ReactiveCode, TrojanHorse },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "941,943,945", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            "Page 2 slots 1–3 are Code Conversion, Reactive Code and Trojan Horse. Stay at spawn, clear selection, and use Enter for commands. QAFriend is 1.2 m west and QAEnemy is 2.2 m east. Callback chance and effect draws are fixed to zero on this disposable server, so the 30% roll always lands.",
            """Stage A. Use helper actions {"actor":"QAFriend","action":"chat","text":"?health 250"} and {"actor":"QAEnemy","action":"chat","text":"?health 250"} before activating. Press slot 1 and capture its first stock request. Run ?health 350 on CSR. Confirm child 944 heals nearby friendly players, including QAFriend, and leaves the hostile alone. Check 6 IS recurring per second and the 15 s own reuse. Press slot 1 to turn it off, then change CSR health again and check that 944 stops.""",
            "Stage B. Press slot 2 and capture its first stock request. Run ?health 350 then ?health 450 on CSR. Confirm child 942 uses the fixed 50 base on QAEnemy on each health change, including the external heal, and excludes QAFriend. Check 7 IS recurring per second and the 15 s own reuse. Press slot 2 again to turn it off and change CSR health again to check no child damage.",
            "Stage C. Press slot 3 as the known wire control. Wait for one 946 pulse to remove 20 IS from QAEnemy after 4 s, with the same 20 IS cost to CSR. Turn it off once the pulse arrives."
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

        // Stock capture of 941 and 943 is a native step. Replay only the captured control here.
        int cursor = actor.World.RpcSnapshot().Count;
        int enemyViews = enemy.World.ViewSnapshot().Count;
        int friendViews = friend.World.ViewSnapshot().Count;
        int actorViews = actor.World.ViewSnapshot().Count;
        int debugAt = server.DebugLog().Length;
        actor.SendRpc(StockTrojanHorse);
        await ExclusiveAsync(cursor, true);
        await EvidenceRetestWire.TimerAsync(actor, cursor, TrojanHorse, AbilityCooldowns.OwnTimer, 15);
        await enemy.WaitUntilAsync(() => HasIS(enemy, enemyViews, 480),
            "A 946 pulse removes 20 IS from the hostile", HeadlessPlayer.DefaultTimeout);
        await actor.WaitUntilAsync(() =>
        {
            var updates = InnerStrengthUpdates(actor, actorViews).ToArray();
            return updates.Zip(updates.Skip(1), (before, after) => before - after).Contains(20);
        }, "Caster pays the 20-IS pulse separately from regeneration", HeadlessPlayer.DefaultTimeout);
        Check.That(!HasIS(friend, friendViews, 480), "The same pulse leaves the nearby friendly untouched");
        Check.That(server.DebugLog()[debugAt..].Contains("[AURA] 945 pulse 946 reaches 1 targets", StringComparison.Ordinal),
            "The pulse reached exactly one target");
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockTrojanHorse);
        await ExclusiveAsync(cursor, false);
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

        Task<RpcRecord> ExclusiveAsync(int after, bool on) => StockAbilityProfile.ObserveAsync(actor, after,
            frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(TrojanHorse, on)), $"Trojan Horse on={on}");
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
