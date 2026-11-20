using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class MartialArtsPassivesAndStylesMode : NativeQaMode
{
    private const ushort Aikido = 101, Karate = 132, KungFu = 133;
    private const ushort MaExpertise = 136, MaInitiate = 137, KungFuMaster = 257, KungFuProficiency = 258;
    private const ushort KarateExpertise = 264, AikidoProwess = 269, KungFuPerfection = 431;
    private const ushort InitiateUpgrade = 1062, KungFuMasterUpgrade = 1063;

    // Reload order (root-to-leaf): 136 -> 137 -> 264 -> 269 -> 258 -> 257 -> 431. Unload in reverse (leaf-to-root).
    private static readonly ushort[] ReloadOrder = { MaExpertise, MaInitiate, KarateExpertise, AikidoProwess, KungFuProficiency, KungFuMaster, KungFuPerfection };
    private static readonly ushort[] UnloadOrder = ReloadOrder.Reverse().ToArray();
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Aikido, Karate, KungFu }
        .Concat(ReloadOrder)
        .Concat(ReloadOrder.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (MaExpertise, 0, new (ushort, int)[] { (997, 5), (1142, 5) }),
        (MaInitiate, InitiateUpgrade, new (ushort, int)[] { (1046, 2) }),
        (KungFuMaster, KungFuMasterUpgrade, new (ushort, int)[] { (1046, 1), (1142, 1) }),
        (KungFuProficiency, 0, new (ushort, int)[] { (1057, 5), (997, 7), (1142, 3) }),
        (KarateExpertise, 0, new (ushort, int)[] { (1057, 5), (997, 7), (1142, 3) }),
        (AikidoProwess, 0, new (ushort, int)[] { (997, 5), (1057, 15), (1142, 3) }),
        (KungFuPerfection, 0, new (ushort, int)[] { (1142, 1), (1143, 10), (995, 10), (1030, 6) })
    };

    private static readonly (ushort Stat, int Value)[] InitiateStats = { (106, 70), (522, 4), (159, 2), (1009, 4) };
    private static readonly (ushort Stat, int Value)[] MasterStats = { (106, 90), (522, 6), (159, 2), (1009, 4) };
    // Stock view-zero class-upgrade / style request template (PR381 0291e4b5eae0:1880-1881).
    private const string SelfAbilityRequest = "80b925040000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-ma-passives-and-styles";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        },
        DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { Aikido, Karate, KungFu, InitiateUpgrade, KungFuMasterUpgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "101,132,133,136,137,257,258,264,269,431",
            ["NativeCloseableRows"] = 7,
            ["BehaviorTestRows"] = "101,132,133",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(50)
        },
        readyMessages: new[]
        {
            "Step A (7 closeable MA passives: 136/137/257/258/264/269/431). Inspect Status Bonuses on login (59/100 memory) and verify Martial Arts Initiate Upgrade (1062) and Kung Fu Master Upgrade (1063) each appear once in Actions.",
            "Unload in leaf-to-root order: ?qaunload 431, 257, 258, 269, 264, 137, 136. Confirm removals: 431 (-1 Melee Dmg, -10 Melee Def, -10 Stun Dur, -6 Viral Def), 257 (-1 Init, -1 Melee Dmg, 1063 leaves Actions), 258 (-5 Max HP, -7 Melee Acc, -3 Melee Dmg), 269 (-5 Melee Acc, -15 Max HP, -3 Melee Dmg), 264 (-5 Max HP, -7 Melee Acc, -3 Melee Dmg), 137 (-2 Init, 1062 leaves Actions), 136 (-5 Melee Acc, -5 Melee Dmg).",
            "Reload in root-to-leaf order: ?qaload 136, 137, 264, 269, 258, 257, 431 (137 requires 136 at rank 15; 257 requires 258 at rank 30). Confirm all passive rows and both upgrades (1062, 1063) return once.",
            "Step B (3 starter MA styles: 101/132/133). Press page 2 slot 1 (Aikido 101: +64 Melee Acc, +26 Melee Dmg, +57 Defs, +74 Withdraw), slot 2 (Karate 132: +64 Acc, +29 Dmg, +45 Defs, +72 Withdraw), slot 3 (Kung-Fu 133: +74 Acc, +27 Dmg, +49 Defs, +70 Withdraw).",
            "Step C (Granted upgrades: 1062/1063). Clear selection. Press page 2 slot 4 (1062: +70 Max HP, +4 Melee Acc, +2 Melee Dmg, +4 Melee Def, 30s standalone reuse). Press slot 5 (1063: replaces 1062 with +90 Max HP, +6 Melee Acc, +2 Melee Dmg, +4 Melee Def, 30s reuse). Press slot 5 again within 3-5 s to verify clean toggle-off during reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(!archive.ContainsKey(InitiateUpgrade) && !archive.ContainsKey(KungFuMasterUpgrade),
            "Granted martial upgrades are absent from owned Archive");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50),
            "Seven martial passive chains and three starter styles fit level-50 memory");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives, 0, remove: false);

        int cursor = await ChangeLoadoutAsync(actor, archive, UnloadOrder, load: false);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: true);

        cursor = await ChangeLoadoutAsync(actor, archive, ReloadOrder, load: true);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: false);

        // Verify all three starter styles via 80 b9.
        var styleChecks = new (ushort Style, int Acc, int Dmg, int Def, int Withdraw)[]
        {
            (Aikido, 64, 26, 57, 74),
            (Karate, 64, 29, 45, 72),
            (KungFu, 74, 27, 49, 70)
        };
        foreach (var (style, acc, dmg, def, withdraw) in styleChecks)
        {
            cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(BuildSelfRequest(style));
            foreach (var (stat, val) in new (ushort, int)[] { (522, acc), (159, dmg), (1009, def), (1010, def), (183, def), (1011, def), (1005, withdraw) })
                await ObserveAsync(actor, cursor, packet => Bonus(packet, style, stat, val, 0, remove: false),
                    $"Style {style} publishes stat {stat}={val}");
        }

        // Verify granted upgrades 1062 -> replaced by 1063 -> 1063 toggle-off during 30 s reuse.
        cursor = await ActivateUpgradeAsync(actor, InitiateUpgrade, InitiateStats);
        cursor = await ActivateUpgradeAsync(actor, KungFuMasterUpgrade, MasterStats);
        foreach (var (stat, value) in InitiateStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, InitiateUpgrade, stat, value, 0, remove: true),
                $"Kung Fu Master Upgrade removes Initiate Upgrade stat {stat}");

        await Task.Delay(650);
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildSelfRequest(KungFuMasterUpgrade));
        byte[] off = AbilityBuffs.BuildExclusive(KungFuMasterUpgrade, false);
        await ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(off),
            "Kung Fu Master Upgrade toggles off during reuse");
        foreach (var (stat, value) in MasterStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, KungFuMasterUpgrade, stat, value, 0, remove: true),
                $"Kung Fu Master Upgrade removes stat {stat} on toggle-off");

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "ma-passives-and-styles.json"), JsonSerializer.Serialize(proof));
        Check.Equal(0, actor.World.Problems.Count, "Martial Arts passives and styles batch framing");
        return Array.Empty<byte>();
    }

    private static async Task<int> ActivateUpgradeAsync(HeadlessPlayer actor, ushort ability, (ushort Stat, int Value)[] stats)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildSelfRequest(ability));
        await EvidenceRetestWire.TimerAsync(actor, cursor, ability, AbilityCooldowns.OwnTimer, 30);
        foreach (var (stat, value) in stats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, ability, stat, value, 0, remove: false),
                $"Upgrade {ability} publishes stat {stat}={value}");
        return cursor;
    }

    private static byte[] BuildSelfRequest(ushort ability)
    {
        byte[] request = Convert.FromHexString(SelfAbilityRequest);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        return request;
    }
}
