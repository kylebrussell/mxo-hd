using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CombatInsightMode : NativeQaMode
{
    private const ushort Insight = 113, Evade = 217, Blur = 49;
    private const int X = 17000, Y = 495, Z = 2300, EnemyX = 17120, Level = 50;
    private const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string BlurUse = "80b93100000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] ActorLoadout = DisablingShotMode.Closure(2, 17, Insight).Order().ToArray();
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Evade, Blur).Order().ToArray();

    public override string Name => "combat-insight-engagement";
    public override NativeQaModePlan Plan { get; } = new(ActorLoadout, X, Z,
        Array.Empty<string>(), new[]
        {
            $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Insight)}"
        }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET rotation=64 WHERE charId=110",
            $"UPDATE characters SET rotation=192 WHERE charId=6114",
            "DELETE FROM inventory WHERE charId IN (110,6114) AND slot BETWEEN 105 AND 107",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, slot) => $"(6114,{400 + slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, FixedCallbackRolls: false, hotbarAbilities: new ushort[] { 17 }, hotbarWithdraw: true,
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "113", ["NativeCloseableRows"] = 1, ["Native113InitiallyLoaded"] = false,
            ["CombatRngOverride"] = false, ["EngagementPolicy"] = "Server entry thresholds and shipped shield damage"
        },
        readyMessages: new[]
        {
            "CSR starts on outdoor HOME facing QAEnemy at 1.2 m. No teleport is needed. /target QAEnemy. Double-click Power in Actions > Awakened and use right-click Close Combat on the selected player. QAEnemy already has ordinary Evade Combat; 113 is archived.",
            "First request: [COMBAT-INSIGHT OURS] force100 evade115 allowedFalse, reason 14, no [PVP] round. [EVASION] equal-level damage=35 and shield 255->166 on QAEnemy. ?qaload 113 adds [MODIFIER] char=110 source=113 stat=1048 +10. Repeat from the same stand. Expect force110 evade115 allowedFalse, damage=38 and shield 166->69. The helper has the legal Execute Program prerequisite, which adds five to the baseline evade score.",
            """Double-click Grab in Actions > Awakened and repeat Close Combat. Expect force220 evade115 allowedTrue and ordinary [PVP] rounds with no further shield debit. To end the fight, press the preseeded page-2 Withdraw and send {"actor":"QAEnemy","action":"stop"}. Wait until both leave the interlock, reselect with /target QAEnemy if needed, and double-click Power again in Actions > Awakened before the next attempt.""",
            """Send {"actor":"QAEnemy","action":"cast","ability":49,"target":"QAEnemy"}. Wait for [MODIFIER] char=6114 source=49 stat=520 +25 for 600 s. Close Combat in Power with 113 now gives force110 evade141 allowedFalse, damage=38 and shield 69->0, removing 217/624. Use ?qaunload 113 for the +10 removal.""",
            """Wait for [EVASION] state off after the shield depletes. Blur stays up. The next Power Close Combat is admitted with no [COMBAT-INSIGHT OURS] gate, confirming the gate requires live Evade. Withdraw with helper stop again.""",
            "Setup aids: seeded levels, HP and IS, ordinary helper self-casts, and disposable dev load/unload for 113. No RNG control. Entry normalization remains a server policy; the shipped shield damage consumer is tested separately on refused entry."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 64, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(EnemyX, Y, Z, 192, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Unobstructed entry lane");
        var server = context.ActualServer!;
        var target = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(target.Handle, target);
        StarterHackerPassiveFixture.VerifyProfile(target, EnemyLoadout);
        await PvpOpponentsFixture.EnterAsync(target);
        NativeQaReadiness.RequireNoAmbientAggro(server, target);
        int from = target.World.RpcSnapshot().Count;
        SendShield(target, Evade);
        var state = await StockAbilityProfile.ObserveAsync(target, from,
            frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, false),
            "Ordinary helper Evade Combat state");
        if (context.StockProbe is not { } actor) return state.Bytes;

        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, ActorLoadout.Where(id => id != Insight).ToArray(), Insight);
        string handle = await HandleAsync(actor, target);
        string actorHandle = await HandleAsync(target, actor);
        actor.SendRpc("42 01 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 f9 03 00 00 f4 03");
        await EnterAsync("force100 evade115 allowedFalse", "shield 255->166 damage=35", false);
        await LoadAsync(true);
        await EnterAsync("force110 evade115 allowedFalse", "shield 166->69 damage=38", false);
        actor.SendRpc("42 02 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 f8 03 00 00 f4 03");
        await EnterAsync("force220 evade115 allowedTrue", null, true);
        actor.SendRpc("42 01 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 f9 03 00 00 f4 03");
        from = target.World.RpcSnapshot().Count;
        SendShield(target, Blur);
        await StockAbilityProfile.ObserveAsync(target, from, frame => StockAbilityProfile.Bonus(frame, Blur, 520, 25, 600, false),
            "Ordinary Gaussian Blur with Evade active");
        await EnterAsync("force110 evade141 allowedFalse", "shield 69->0 damage=38", false);
        await LoadAsync(false);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[EVASION] QAEnemy state off", StringComparison.Ordinal),
            "Shield depletion ends Evade Combat", HeadlessPlayer.DefaultTimeout);
        await EnterAsync(null, null, true);
        Console.WriteLine("COMBAT INSIGHT BOOTSTRAP PASS: unloaded/loaded refusal, Grab entry, Blur depletion and Blur-only entry.");
        return state.Bytes;

        async Task LoadAsync(bool load)
        {
            int cursor = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Insight }, load);
            await StockAbilityProfile.ObserveAsync(actor, cursor,
                frame => StockAbilityProfile.Bonus(frame, Insight, 1048, 10, 0, !load), "Normal archive 113 round trip");
        }

        async Task EnterAsync(string? scores, string? shield, bool admitted)
        {
            int cursor = server.DebugLog().Length;
            actor.SkipReceived();
            target.SkipReceived();
            actor.SendRpc($"40 {handle} 01 6b");
            if (admitted)
            {
                await actor.WaitForRpcAsync($"46 {handle} 01");
                await actor.WaitForRpcAsync("45");
            }
            else await actor.WaitForRpcAsync($"4d 0e 00 01 00 {handle}");

            if (scores != null)
                await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains(scores, StringComparison.Ordinal),
                    $"Current entry score {scores}", HeadlessPlayer.DefaultTimeout);
            if (shield != null)
                await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains(shield, StringComparison.Ordinal),
                    $"Current shield debit {shield}", HeadlessPlayer.DefaultTimeout);
            if (!admitted) return;
            if (scores == null)
                Check.That(!server.DebugLog()[cursor..].Contains("[COMBAT-INSIGHT OURS]", StringComparison.Ordinal), "Blur alone leaves ordinary entry");
            actor.SendRpc("44 00");
            target.SendRpc("44 00");
            await actor.WaitForRpcAsync($"46 {handle} 00");
            await target.WaitForRpcAsync($"46 {actorHandle} 00");
            await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains("ends: Withdrew", StringComparison.Ordinal),
                "Ordinary interlock withdrawal", HeadlessPlayer.DefaultTimeout);
        }
    }

    internal static void SendShield(HeadlessPlayer actor, ushort ability) =>
        actor.SendRpc(ability switch
        {
            Evade => EvadeUse,
            Blur => BlurUse,
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        });

    private static async Task<string> HandleAsync(HeadlessPlayer actor, HeadlessPlayer target)
    {
        var view = await actor.WaitForViewNamedAsync(target.Handle);
        return WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
    }
}
