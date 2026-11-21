using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedDetectionDisguiseMode : NativeQaMode
{
    private const int Level = 50, Y = 495, X = 17000, Z = 2300, DisguiseRank = 22;
    private const ushort Detect = 298, Mask = 1114, Shadow = 1118;
    // The full stock Countermeasure frame is from PR413 and the two mask setup frames are from PR435c.
    private const string Countermeasure = "80b92a0100000000000000000000000000000000000000000000000064f11a00b030893f08080000000000";
    private const string SpyUse = "80b95804000000000000000000000000000000000000000000000000585cc50001000000086b0000000000";
    private const string MaskUse = "80b95a0400000000000000000000000000000000000000000000000064f11a00101a0c3d08280000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Detect).Order().ToArray();
    private static readonly ushort[] MaskLoadout = StockAbilityProfile.Profile(2, 17, CombatStyles.SpyStyle, 120).Order().ToArray();
    private static readonly (string Handle, int Id, int X, int Z, int Org)[] Witnesses =
    {
        ("QAEnemy", 6114, 17900, 1900, 2),
        ("QAFriend", 6111, 16100, 2500, 1),
        ("QAOther", 6113, 17000, 3300, 2)
    };

    public override string Name => "ability-parity-awakened-detection-disguise";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "UPDATE char_abilities SET level=50 WHERE char_id=110",
        "DELETE FROM inventory WHERE charId=110 AND (slot=8 OR goid=2388)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,2388,8,1,0)"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET innerStrC=200,innerStrM=200,rotation={MobAggro.YawTowards(X,Z,Witnesses[0].X,Witnesses[0].Z)} WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6113,6114)",
        "DELETE FROM inventory WHERE charId IN (6111,6114) AND (slot=40 OR goid=1268)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(6111,1268,40,1,0),(6114,1268,40,1,0)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", new[] { 6111, 6114 }.SelectMany(id => MaskLoadout.Select((ability, index) =>
                $"({id},{400 + index},{0x80000000u | ((uint)ability << 10)},{DisguiseRank},1)")))
    }.Concat(Witnesses.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Org} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, testMobFixture: "martial-player-controls.csv",
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(8, 2388, "Concealment Countermeasure Tool") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "298,1114,1118", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "All four stands are outdoor NAV on floor 400. CSR (17000,495,2300) faces hostile QAEnemy (17900,495,1900), 9.85 m away. Same-org QAFriend (16100,495,2500) and hostile QAOther (17000,495,3300) are also within 12 m. Keep CSR on the start stand, with no selected target and no bag clicks.",
            """After CSR enters, send {"actor":"QAEnemy","action":"detect-disguises"}. The helper replay uses the complete stock mask frames for QAEnemy and QAFriend and sets QAOther's Shadow as a negative control. Wait for the helper acknowledgment and both [STEALTH] 1114 on lines. Both helpers should look like Security. Then press the pre-seeded page 2 slot 1 Countermeasure while both disguises are still active.""",
            "At loaded rank 50, source 298 stat 638 +200 makes the hostile rank-22 disguise reveal deterministic. Expect stock 80 b9 2a 01 from bag 8, 200→195 IS, 10 s own reuse and [STEALTH] QAEnemy 1114 off (revealed by Detection). Confirm QAEnemy returns to the normal model with the counter FX; same-org QAFriend stays disguised and QAOther retains Shadow. No maximum [VITALS] changes or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Detection needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var stand in Witnesses.Select(witness => (witness.X, witness.Z)).Prepend((X, Z)))
            Check.That(NavMesh.Locate(1, stand.X, Y, stand.Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Detection participant at ({stand.X},{Y},{stand.Z}) starts on outdoor NAV");

        foreach (var stand in Witnesses)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            if (helper.Handle == "QAOther") continue;
            helper.SendRpc(Convert.FromHexString(SpyUse));
            await helper.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Detect, 638, 200, 0, false), "Rank-50 Detect +200");

        var enemy = context.Helpers["QAEnemy"];
        var friend = context.Helpers["QAFriend"];
        var shadow = context.Helpers["QAOther"];
        var baseView = await actor.WaitForViewNamedAsync(enemy.Handle);
        await actor.WaitForViewNamedAsync(friend.Handle);
        int views = actor.World.ViewSnapshot().Count;
        int enemyAt = enemy.World.RpcSnapshot().Count, friendAt = friend.World.RpcSnapshot().Count, shadowAt = shadow.World.RpcSnapshot().Count;
        await PrepareWitnessesAsync(enemy, context.Helpers);
        byte[] mask = PlayerViewAttributes.Build((0, PlayerViewAttributes.U8(0)), (1, PlayerViewAttributes.U32(0)), (28, PlayerViewAttributes.U32(1308623188)));
        var maskBytes = BytePattern.Parse(WireFormat.ToHex(mask));
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).Any(maskBytes.Occurs),
            "The observer receives the disguise model", HeadlessPlayer.DefaultTimeout);

        int castAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(Countermeasure));
        await StockAbilityProfile.ObserveAsync(enemy, enemyAt, frame => StockAbilityProfile.Bonus(frame, Mask, 881, 0, 42, true),
            "The hostile disguise state is removed by Detection");
        await enemy.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 9d 05 00 28 ??");
        await EvidenceRetestWire.TimerAsync(actor, castAt, Detect, AbilityCooldowns.OwnTimer, 10);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 10 c3 00");
        byte[] restored = PlayerViewAttributes.Build((0, PlayerViewAttributes.U8(1)), (1, baseView.Attributes["TitleAbility"]),
            (28, baseView.Attributes.GetValueOrDefault("Description") ?? PlayerViewAttributes.U32(0)));
        var restoredBytes = BytePattern.Parse(EvidenceRetestWire.Le(baseView.ViewId) + " " + WireFormat.ToHex(restored));
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).Any(restoredBytes.Occurs),
            "The observer receives the hostile's original model", HeadlessPlayer.DefaultTimeout);
        Check.That(!friend.World.RpcSnapshot().Skip(friendAt).Any(frame => StockAbilityProfile.Bonus(frame, Mask, 881, 0, 42, true)) &&
            !shadow.World.RpcSnapshot().Skip(shadowAt).Any(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var delta) &&
                delta!.SourceId == Shadow && delta.AddRemoveType == 1), "Detection leaves the allied disguise and hostile Shadow active");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count + shadow.World.Problems.Count,
            "Detection disguise framing");
        return Array.Empty<byte>();
    }

    internal static async Task<string> PrepareWitnessesAsync(HeadlessPlayer actor, IReadOnlyDictionary<string, HeadlessPlayer> helpers)
    {
        if (actor.Handle != "QAEnemy") throw new InvalidOperationException("Use QAEnemy for the Detection witness setup.");
        var shadow = helpers["QAOther"];
        shadow.SendChat("?stealth shadow");
        await shadow.WaitForTextAsync("Stealth shadow on.");
        await Task.WhenAll(MaskAsync(actor), MaskAsync(helpers["QAFriend"]));
        return "Hostile and allied disguise active, plus hostile Master Shadow. Use Countermeasure now.";

        static async Task MaskAsync(HeadlessPlayer player)
        {
            int from = player.World.RpcSnapshot().Count;
            player.SendRpc(Convert.FromHexString(MaskUse));
            await EvidenceRetestWire.CastBarAsync(player, Mask, 2);
            await StockAbilityProfile.ObserveAsync(player, from, packet => StockAbilityProfile.Bonus(packet, Mask, 881, 0, 42, false),
                $"Ordinary stock disguise on {player.Handle}");
        }
    }
}
