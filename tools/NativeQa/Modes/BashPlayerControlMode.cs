using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class BashPlayerControlMode : NativeQaMode
{
    private const ushort Coder = CombatStyles.CoderStyle, LowBlow = 605, Bash = 618;
    private const int Level = 50, CoderRank = 19, X = 17000, Y = 495, CasterZ = 6400, EnemyZ = 6600;
    private static readonly ushort[] CasterLoadout = Profile(2, 17, Coder, LowBlow, Bash).Order().ToArray();
    private static readonly ushort[] EnemyLoadout = Profile(2, 17, 198).Order().ToArray();
    private static readonly byte CasterYaw = MobAggro.YawTowards(X, CasterZ, X, EnemyZ);
    private static readonly byte EnemyYaw = MobAggro.YawTowards(X, EnemyZ, X, CasterZ);
    // PR330 retained Self Defense. PR405 stock PacketLog 8745737faaee, lines 1190–1191, retained Low Blow.
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockLowBlow = "80b95d020400020000000000000000000000000000000000000000005d020000006b0000086b0000000000";

    public override string Name => "ability-parity-coder-bash-player-control";
    public override NativeQaModePlan Plan { get; } = new(CasterLoadout, X, CasterZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET healthC=30000,healthM=30000,innerStrC=800,innerStrM=1000 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={X},y={Y},z={CasterZ},rotation={CasterYaw} WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation={EnemyYaw} WHERE charId=6114",
            $"UPDATE char_abilities SET level={CoderRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Coder)}",
            $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) =>
                    $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Math.Min(Level, AbilityCatalog.Find(id).Number("Ability/AbilityLevelCap", Level))},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Bash }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "618", ["PlayerRecipient"] = "QAEnemy (6114)", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, Coder Style {CoderRank}, {CasterLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{CasterZ}) faces visible QAEnemy ({X},{Y},{EnemyZ}) 2 m away. Page 2 slot 1 Coder Style and slot 2 Bash.",
            "Select QAEnemy and open ordinary Close Combat. Reselect Coder Style with page 2 slot 1. Keep QAEnemy selected. Enter ?cd stagger 30 and wait for 883/201 on QAEnemy, then press slot 2. Save the complete first stock 618 request and expect 40 IS, 10 s own and 5 s group 687 reuse. A landed Bash uses [PVP] move 289. If it misses, wait 10 s for reuse, reapply Stagger and retry.",
            "Stay in interlock. QAEnemy logs 618/102 Stun for 4 s, ordinary 734/681 immunity for 4 s and 618/93 Enraged for 8 s, then matched removals. Its exact Cheap Shot is refused during Stun, pays nothing, then queues when Stun releases. Wait for BASH RECIPIENT PASS 618. Keep bash-player-controls.jsonl, the native and QAEnemy PacketLogs, and DebugLog. Runtime's 734/681 uses the shared default; GOB 618 supplies the 4 s Stun and 8 s Enrage.",
            "The bootstrap uses retained Self Defense, Coder Style, Low Blow and Close Combat controls. The first complete Bash request is a native capture. Bash uses its ordinary proc and duration. Projected DONE +1 (618) after the recipient pass."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { CasterZ, EnemyZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Grounded outdoor NAV ({X},{Y},{z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, CasterZ, X, Y, EnemyZ), "CSR faces a visible QAEnemy at 2 m");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        VerifyLoadout(enemy, EnemyLoadout);
        await PvpOpponentsFixture.EnterAsync(enemy);
        VerifyStand(enemy, EnemyZ, EnemyYaw);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster)
        {
            _ = MartialPlayerControlMode.WatchBashAsync(context, enemy, server);
            return Array.Empty<byte>();
        }

        VerifyLoadout(caster, CasterLoadout);
        await PvpOpponentsFixture.EnterAsync(caster);
        VerifyStand(caster, CasterZ, CasterYaw);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var opponent = await caster.WaitForViewNamedAsync(enemy.Handle);
        PvpOpponentsFixture.Observe(opponent, 2, enemy.CharId);
        await enemy.WaitForViewNamedAsync(caster.Handle);
        uint handle = DuelPackets.Handle(opponent.ViewId, opponent.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockSelfDefense, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
        await caster.WaitForRpcAsync($"46 {hex} 01");
        caster.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        caster.SendRpc($"81 51 {hex}");
        caster.SendChat("?cd stagger 30");
        await caster.WaitForTextAsync("Stagger fixture applied 883/201 to QAEnemy");
        int frames = caster.World.RpcSnapshot().Count, log = server.DebugLog().Length;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(StockLowBlow, handle));
        await EvidenceRetestWire.TimerAsync(caster, frames, LowBlow, AbilityCooldowns.OwnTimer, 6);
        Check.That(server.DebugLog()[log..].Contains("[PVP] LowBlowAbility is CSR_Tester's next move", StringComparison.Ordinal),
            "Exact retained Low Blow reaches the same Coder player-interlock queue");
        Check.That(!enemy.World.RpcSnapshot().Any(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record) &&
            record!.SourceId is LowBlow or Bash), "The Low Blow control applies neither Bash state");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Coder interlock bootstrap framing");
        Console.WriteLine("BASH PLAYER CONTROL BOOTSTRAP PASS: exact retained controls, legal player interlock, real QA stagger; 618 waits for stock native input.");
        return Array.Empty<byte>();
    }

    private static void VerifyLoadout(HeadlessPlayer player, ushort[] loadout)
    {
        var owned = AbilityMemoryUiFixture.Decode(player.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(owned.Keys.ToHashSet().SetEquals(loadout) && loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                owned.TryGetValue(need.Key, out ushort level) && level >= need.Value)), $"{player.Handle} has the full ranked legal profile");
    }

    private static void VerifyStand(HeadlessPlayer player, int z, byte yaw)
    {
        var self = player.World.CreationSnapshot().Last(view => view.ViewId == 2);
        byte[] position = self.Attributes["Position"];
        Check.That(BitConverter.ToDouble(position) == X && BitConverter.ToDouble(position, 8) == Y &&
            BitConverter.ToDouble(position, 16) == z && self.Attributes["Yaw Interval"][0] == yaw,
            $"{player.Handle} loads at its verified stand and faces the other participant");
    }
}
