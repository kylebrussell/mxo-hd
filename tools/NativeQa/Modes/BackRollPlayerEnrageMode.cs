using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class BackRollPlayerEnrageMode : NativeQaMode
{
    private const ushort BackRoll = CombatSpecials.BackRollEscape, Sneak = Stealth.SneakGrantAbility;
    private const int X = 17000, Y = 495, Z = 6400, EnemyZ = 6600, FriendX = 17400, Level = 50;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, BackRoll, 151, CombatStyles.SpyStyle)
        .Order().ToArray();

    public override string Name => "ability-parity-back-roll-player-enrage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { "DELETE FROM inventory WHERE charId IN (110,6114) AND slot=107" }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0 WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation=0,level=16,exp={PlayerProgression.ExperienceForLevel(16)} WHERE charId=6114",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=192 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6114",
            $"INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES (6114,400,{AbilityMessages.AbilityGameObject(2)},16,1),(6114,401,{AbilityMessages.AbilityGameObject(17)},16,1)"
        }, DevCommands: true, hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, Sneak, BackRoll },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "122", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"Verified HOME {Home} only if displaced. QAEnemy two metres ahead facing away and QAFriend four metres east. Page 2 Spy / Sneak / Back Roll. /target QAEnemy if needed, or strafe and single-click. No bag or NPC targeting. QAEnemy level 16 with clean Self Defense/Speed controls.",
            "Select Spy, Sneak and press Back Roll at rest. Expect stock combat-only suppression, or refusal with no debit, cooldown or change to active Sneak. Toggle Sneak off, select QAEnemy and use radial Close Combat. Reselect Spy and Speed. Press Back Roll and save the first full stock 80 b9 7a 00. An ordinary missed special may retry after the 12 s timer.",
            "Expect 25 IS and group 1200 at 12 s with no own timer, accepted interlock special, zero special damage, target [MODIFIER] char=6114 source=122 stat=93 add for 10 s and a matching removal, ordinary combat exit and both players visible at standing. QAFriend gets no 122 modifier; there is no 1116, 1117 or 1118 transition. Record the visible Enrage cue and stock recipient record. After expiry the same helper remains a normal target again."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (X, EnemyZ), (FriendX, Z) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified clear Back Roll NAV ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) &&
            (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4), "HOME matches NAV");

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
        var enemy = context.Helpers["QAEnemy"];
        var friend = context.Helpers["QAFriend"];
        await friend.WaitForViewNamedAsync(actor.Handle);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handleId = DuelPackets.Handle(target.ViewId, target.SpawnId);
        string handleHex = WireFormat.ToHex(BitConverter.GetBytes(handleId));
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int at = actor.World.RpcSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await StockAbilityProfile.ObserveAsync(actor, at, frame => StockAbilityProfile.Bonus(frame, Sneak, 115, -200, 0, false),
            "Known ordinary Sneak succeeds with the witness facing away");
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await StockAbilityProfile.ObserveAsync(actor, at, frame => StockAbilityProfile.Bonus(frame, Sneak, 115, -200, 0, true),
            "Ordinary Sneak clears before the real interlock");
        at = actor.World.RpcSnapshot().Count;
        actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handleId),
            WireFormat.FromHex($"40 {handleHex} 01 6b"));
        await StockAbilityProfile.ObserveAsync(actor, at, frame => frame.Bytes is [0x46, ..] &&
            frame.Bytes.Length >= 6 && frame.Bytes[5] == 1, "Actual stock Close Combat against the clean hostile recipient");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Back Roll stock setup framing");
        return Array.Empty<byte>();
    }
}
