using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;

namespace NativeQa.Modes;

public sealed class SeverArteryRecipientMode : NativeQaMode
{
    private const ushort Spy = CombatStyles.SpyStyle, Sever = CombatSpecials.SeverArtery;
    private const int X = 21550, EnemyX = 21750, WallX = 22350, FarX = 19600, Y = 1095, Z = 16150;
    private const int FriendX = 21650, FriendZ = 16250, Level = 50;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, Sever);

    public override string Name => "ability-parity-sever-artery-recipient";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},y={Y},z={Z},rotation=192 WHERE charId=6114",
        $"UPDATE characters SET x={FriendX},y={Y},z={FriendZ},rotation=192 WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id=6114",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sever }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SEVER_STATE_QA"] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "341", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Outdoor HOME faces QAEnemy 2 m east. No initial tp or NPC selection. Page 2 Spy Style then Sever Artery. Start at rest. /target QAFriend and try slot 2 for invalid target 24. Use printed BLOCK/FAR with /target QAEnemy for 9/34 or stock suppression. All are before IS, cast and timer. Return to HOME before casting.",
            "Type ?severstate 0 0 (15 chars), /target QAEnemy and press slot 2. Save the first complete stock 341 request. Expect a 4 s cast, 35 IS, 15 s own and 3 s family 1216. A landed cast gives matching 30 s source341 movement -50, sneak -50, invisibility -50, force -10, ThrownDOT ticks and a 10 s Daze (199). A miss is ordinary; retry after reuse.",
            "After Daze appears type ?severstate 99 0 (16 chars), before the next 2.5 s callback if possible. This stops new children and leaves current records active. Observe the last Daze removal and then the 30 s parent records removing together. Check no later tick and no effect on QAFriend. QA controls only the random-state callback, with the normal cast and hit path."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (label, x, z) in new[] { ("HOME", X, Z), ("BLOCK", WallX, Z), ("FAR", FarX, Z),
            ("QAEnemy", EnemyX, Z), ("QAFriend", FriendX, FriendZ) })
        {
            string[] words = Stand(x, z).Command.Split(' ');
            Console.WriteLine($"READY {label} type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");
        }
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z) &&
            NavMesh.HasSightPath(1, FarX, Y, Z, EnemyX, Y, Z) &&
            !NavMesh.HasSightPath(1, WallX, Y, Z, EnemyX, Y, Z), "Near and far have sight; office wall blocks the other path");
        var source = AbilityCatalog.Find(Sever);
        Check.Equal(16d, source.Number("Ability/SelectionRange"), "Shipped Sever Artery range");
        Check.Equal(1d, source.Number("Ability/CastWhenUsed"), "Shipped Sever Artery is an at-rest opener");
        var server = context.ActualServer!;
        foreach (string handle in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await actor.WaitForViewNamedAsync("QAEnemy");
        await actor.WaitForViewNamedAsync("QAFriend");
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Spy, 1034, 29, 0, false),
            "The retained Spy Style activation reaches the stock recipient mode");
        // The stock first cast is part of this run. Bootstrap verifies the exact retained style request and geometry.
        return Array.Empty<byte>();
    }
}
