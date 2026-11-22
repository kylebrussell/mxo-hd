using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterInsightShieldDamageMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300, EnemyX = 17120, FriendX = 17280;
    private const ushort Insight = 113, Hindering = 130, Handguns = 129, Evade = EvasionShield.Parent;
    // PR524 stock Evade and PR554b stock Handguns/Hindering (5530/5281) from the earlier entry fixture.
    private const string EvadeFrame = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string HandgunsFrame = "80b981000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string HinderingFrame = "80b98200040002000000000000000000000000000000000000000000585cba0001000000086b0000000000";
    private static readonly ushort[] DefenderLoadout = { 2, 17, Evade };
    private static readonly ushort[] EnemyLoadout = { 2, 17 };
    private static readonly ushort[] FriendLoadout = new ushort[] { 2, 17, 128, Handguns, Hindering }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-insight-shield-damage";

    public override NativeQaModePlan Plan { get; } = new(DefenderLoadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=64,innerStrC=300,innerStrM=300 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z}," +
                "rotation=192,innerStrC=300,innerStrM=300 WHERE charId=6114",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z}," +
                "rotation=192,innerStrC=300,innerStrM=300 WHERE charId=6111",
            "DELETE FROM inventory WHERE charId IN (110,6111,6114) AND slot IN (105,107)",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (6111,{CombatFixtureWeapons.Handgun},{CombatFixtureWeapons.HandSlot},1,0)",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
            Seed(6114, EnemyLoadout.Append(Insight), archived: Insight),
            Seed(6111, FriendLoadout)
        }, DevCommands: true, hotbarAbilities: new[] { Evade }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "113,130,217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"All three characters are level 19 on verified outdoor NAV. CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}). QAFriend ({FriendX},{Y},{Z}) holds the fixture handgun.",
            "At rest, keep Speed and activate Evade Combat once in page 2 slot 1. Expect a two-second cast, 40 IS spent, source 217 state 624, and the full 255 shield. Keep still while the helpers perform the comparison.",
            "QAEnemy first attacks without Combat Insight. Expect [COMBAT-INSIGHT OURS] force100 evade110 allowedFalse and [EVASION] raw damage 35, shield 255 to 166, IS 260 to 250.",
            "The same helper loads 113 at rank 19. Expect [MODIFIER] 113/1048 +3, force103 against evade110, damage 36, shield 166 to 74, and IS 250 to 240.",
            "QAFriend casts Hindering Shot on QAEnemy (20 IS, two-second cast, source 130/519 -10 for eight seconds). The third attack is still refused with force92 against evade110. Raw damage stays 36, shield reaches 0, IS 240 to 230, and 624 clears with 30 s Evade reuse. This samples the shipped shield-damage function through the server's current entry rule."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, EnemyX, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Verified outdoor shield comparison stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, FriendX, Y, Z, EnemyX, Y, Z), "Hindering Shot has a clear path to the attacker");

        var enemy = await StartHelperAsync("QAEnemy");
        var archive = StarterHackerPassiveFixture.VerifyProfile(enemy, EnemyLoadout, Insight);
        Check.Equal((ushort)Level, archive[Insight].Level, "Combat Insight starts archived at rank 19");
        enemy.SendRpc("42 01 00 00 00 00 00");
        await StockAbilityProfile.ObserveAsync(enemy, 0,
            frame => StockAbilityProfile.Bonus(frame, 1017, 1012, 1, 0, false), "The attacker selected stock Power");

        var friend = await StartHelperAsync("QAFriend");
        StarterHackerPassiveFixture.VerifyProfile(friend, FriendLoadout);
        var friendOwn = friend.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(CombatFixtureWeapons.Handgun, BitConverter.ToUInt32(friendOwn.Attributes["EquippedItemID"]),
            "The Hindering shooter starts with a handgun");
        var friendTarget = await friend.WaitForViewNamedAsync(enemy.Handle);
        uint shotHandle = DuelPackets.Handle(friendTarget.ViewId, friendTarget.SpawnId);
        int styleAt = friend.World.ViewSnapshot().Count;
        friend.SendRpc(WithHandle(HandgunsFrame, shotHandle));
        await EvidenceRetestWire.ViewSinceAsync(friend, styleAt, 2, "02 80 80 80 80 80 10 81 00 00 00");

        if (context.StockProbe is not { } defender)
        {
            _ = NativeCompareAsync();
            return Array.Empty<byte>();
        }

        StarterHackerPassiveFixture.VerifyProfile(defender, DefenderLoadout);
        await EnterAsync(defender);
        int activation = defender.World.RpcSnapshot().Count;
        defender.SendRpc(EvadeFrame);
        await EvidenceRetestWire.CastBarAsync(defender, Evade, 2);
        await StockAbilityProfile.ObserveAsync(defender, activation,
            frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, false), "Evade publishes owner state 624");
        await CompareAsync(defender.Handle, defender);
        Check.Equal(0, defender.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count,
            "Shield comparison packet framing");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> StartHelperAsync(string name)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await EnterAsync(helper);
            return helper;
        }

        async Task EnterAsync(HeadlessPlayer player)
        {
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }

        async Task NativeCompareAsync()
        {
            try
            {
                await CompareAsync("CSR_Tester", owner: null);
                Console.WriteLine("INSIGHT SHIELD PASS raw 35/36/36, meter 255/166/74/0 and force 100/103/92");
            }
            catch (Exception error)
            {
                Console.WriteLine("INSIGHT SHIELD FAILED " + error.Message);
            }
        }

        async Task CompareAsync(string defenderName, HeadlessPlayer? owner)
        {
            var defenderView = await enemy.WaitForViewNamedAsync(defenderName, TimeSpan.FromMinutes(5));
            uint handle = DuelPackets.Handle(defenderView.ViewId, defenderView.SpawnId);
            string target = WireFormat.ToHex(BitConverter.GetBytes(handle));
            uint activeFlags = PvpFlag.PvpBit | EvasionShield.StateFlag;
            byte[] full = PlayerViewAttributes.Build((21, PlayerViewAttributes.U8(255)),
                (PlayerViewAttributes.OtherConditionStateFlags, PlayerViewAttributes.U32(activeFlags)));
            await enemy.WaitForViewUpdateAsync(defenderView.ViewId, WireFormat.ToHex(full), TimeSpan.FromMinutes(5));
            ViewCreation? own = owner?.World.CreationSnapshot().Last(view => view.ViewId == 2);

            await ResistAsync(100, 35, 255, 166, 260);
            int loaded = await StockAbilityProfile.ChangeLoadoutAsync(enemy, archive, new[] { Insight }, load: true);
            await StockAbilityProfile.ObserveAsync(enemy, loaded,
                frame => StockAbilityProfile.Bonus(frame, Insight, 1048, 3, 0, false), "The same attacker gains +3% Force Combat Bonus");
            await ResistAsync(103, 36, 166, 74, 250);

            int debuffAt = enemy.World.RpcSnapshot().Count;
            int shotAt = friend.World.RpcSnapshot().Count;
            friend.SendRpc(WithHandle(HinderingFrame, shotHandle));
            await EvidenceRetestWire.CastBarAsync(friend, Hindering, 2);
            await EvidenceRetestWire.TimerAsync(friend, shotAt, Hindering, AbilityCooldowns.OwnTimer, 8);
            await StockAbilityProfile.ObserveAsync(enemy, debuffAt,
                frame => StockAbilityProfile.Bonus(frame, Hindering, 519, -10, 8, false), "Hindering supplies the direct -10 Force Combat modifier");
            int finalAt = owner?.World.RpcSnapshot().Count ?? 0;
            await ResistAsync(92, 36, 74, 0, 240);
            if (owner != null)
            {
                await StockAbilityProfile.ObserveAsync(owner, finalAt,
                    frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, true),
                    "Meter depletion removes owner state 624");
                await EvidenceRetestWire.TimerAsync(owner, finalAt, Evade, AbilityCooldowns.OwnTimer, 30);
            }

            async Task ResistAsync(int force, int damage, byte before, byte after, ushort strength)
            {
                int logAt = server.DebugLog().Length;
                int enemyRpcAt = enemy.World.RpcSnapshot().Count;
                int enemyViewsAt = enemy.World.ViewSnapshot().Count;
                int ownerRpcAt = owner?.World.RpcSnapshot().Count ?? 0;
                int ownerViewsAt = owner?.World.ViewSnapshot().Count ?? 0;
                byte[] refused = WireFormat.FromHex($"4d 0e 00 01 00 {target}");
                enemy.SendRpc($"40 {target} 01 6b");
                await StockAbilityProfile.ObserveAsync(enemy, enemyRpcAt,
                    frame => frame.Bytes.AsSpan().SequenceEqual(refused), "Every attack receives the stock attacker refusal");

                string damageLog = $"resistance IS {strength}->{strength - 10} cost=10; shield {before}->{after} damage={damage}";
                await enemy.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(damageLog, StringComparison.Ordinal),
                    damageLog, HeadlessPlayer.DefaultTimeout);
                Check.That(server.DebugLog()[logAt..].Contains($"force{force} evade110 allowedFalse", StringComparison.Ordinal),
                    "The modeled entry score differs from the raw shield damage");
                byte[] meter = after != 0
                    ? PlayerViewAttributes.Build((21, PlayerViewAttributes.U8(after)))
                    : PlayerViewAttributes.Build((21, PlayerViewAttributes.U8(0)),
                        (PlayerViewAttributes.OtherConditionStateFlags, PlayerViewAttributes.U32(PvpFlag.PvpBit)));
                await EvidenceRetestWire.ViewSinceAsync(enemy, enemyViewsAt, defenderView.ViewId, WireFormat.ToHex(meter));

                if (owner != null)
                {
                    string self = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(2, own!.SpawnId)));
                    byte[] result = WireFormat.FromHex($"4d 0e 01 01 00 {self}");
                    await StockAbilityProfile.ObserveAsync(owner, ownerRpcAt,
                        frame => frame.Bytes.AsSpan().SequenceEqual(result), "Every attack receives the stock defender result");
                    byte[] update = PlayerViewAttributes.BuildSelf(
                        (PlayerViewAttributes.SelfInnerStrength, PlayerViewAttributes.U16((ushort)(strength - 10))),
                        (30, PlayerViewAttributes.U8(after)));
                    await EvidenceRetestWire.ViewSinceAsync(owner, ownerViewsAt, 2, WireFormat.ToHex(update[2..]));
                }
            }
        }
    }

    private static string Seed(uint character, IEnumerable<ushort> ids, ushort archived = 0) =>
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
        string.Join(",", ids.Select((id, index) =>
            $"({character},{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},{(id == archived ? 0 : 1)})"));
}
