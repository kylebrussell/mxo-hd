using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class MartialArtsGrandmasterBatchMode : NativeQaMode
{
    private const ushort Karate = CombatStyles.Karate;
    private const ushort AikidoRedirection = 405, AikidoGrandmaster = 410;
    private const ushort KarateFocus = 531, KarateGrandmaster = 533, BerserkerAttack = 535;
    private const ushort KarateGrandmasterUpgrade = 1066, AikidoGrandmasterUpgrade = 1069;

    private static readonly ushort[] ReloadOrder = { KarateFocus, KarateGrandmaster, AikidoRedirection, AikidoGrandmaster };
    private static readonly ushort[] UnloadOrder = ReloadOrder.Reverse().ToArray();
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Karate, BerserkerAttack }
        .Concat(ReloadOrder)
        .Concat(ReloadOrder.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Concat(AbilityPrerequisites.Current.Closure(BerserkerAttack).Keys)
        .Distinct().Order().ToArray();

    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (KarateFocus, 0, new (ushort, int)[] { (995, 10), (1030, 12), (1142, 6), (1144, 5), (1068, 5) }),
        (KarateGrandmaster, KarateGrandmasterUpgrade, new (ushort, int)[] { (995, 2), (1142, 4), (1046, 1) }),
        (AikidoRedirection, 0, new (ushort, int)[] { (1068, 5), (1142, 1), (1143, 5), (1042, 10), (1030, 6), (1148, 12) }),
        (AikidoGrandmaster, AikidoGrandmasterUpgrade, new (ushort, int)[] { (1143, 5), (1068, 2), (1046, 1), (1144, 5) })
    };

    private static readonly (ushort Stat, int Value)[] KarateGmStats = { (106, 110), (522, 7), (159, 6), (1009, 6) };
    private static readonly (ushort Stat, int Value)[] AikidoGmStats = { (106, 150), (522, 5), (159, 3), (1009, 8) };
    private const string SelfCastRequest = "80b925040000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-ma-grandmaster-karate-aikido";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=2000,healthM=2000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { Karate, BerserkerAttack, KarateGrandmasterUpgrade, AikidoGrandmasterUpgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "405,410,531,533,535",
            ["NativeCloseableRows"] = 1,
            ["BehaviorTestRows"] = "405,410,531,533,535",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(50)
        },
        readyMessages: new[]
        {
            "Step A (Karate/Aikido Grandmaster passives: 533 Karate Grandmaster [closeable], 531 Karate Focus, 410 Aikido Grandmaster, 405 Aikido Redirection). Inspect Status Bonuses on login and verify 1066 (Karate GM) and 1069 (Aikido GM) appear once in Actions.",
            "Step B (Unload/Reload cycle). Unload leaf-to-root: ?qaunload 410, 405, 533, 531. Reload root-to-leaf: ?qaload 531, 533, 405, 410 confirming exact login Status and both upgrades return once.",
            "Step C (Upgrades on page 2). Clear selection. Press slot 3 (1066 Karate GM: +110 Max HP, +7 Melee Acc, +6 Melee Dmg, +6 Melee Def, 20 IS, 30s reuse), then slot 4 (1069 Aikido GM: replaces 1066 with +150 Max HP, +5 Acc, +3 Dmg, +8 Def, 30s reuse), then slot 4 again at +3-5s for clean toggle-off.",
            "Step D (Berserker Attack 535). Unarmed, enter ordinary Close Combat on Melee Dummy (3 m west), press slot 1 (Karate 132), then press slot 2 (Berserker Attack 535): expect 40 IS, 60.0 s own reuse + 5.0 s group 687 reuse, choreography 5939 ('Beserker Attack'), and on landing '[BUFF] 535 on for 21 s' (caster +11 Melee Dmg 159, +50 Melee Res 180, +50 Ballistic Res 181)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        foreach (ushort grant in new[] { KarateGrandmasterUpgrade, AikidoGrandmasterUpgrade })
            Check.That(!archive.ContainsKey(grant), $"Grant {grant} is absent from owned Archive");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "MA Grandmaster batch fits level-50 memory");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives, 0, remove: false);

        int cursor = await ChangeLoadoutAsync(actor, archive, UnloadOrder, load: false);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: true);

        cursor = await ChangeLoadoutAsync(actor, archive, ReloadOrder, load: true);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: false);

        cursor = await ActivateUpgradeAsync(actor, KarateGrandmasterUpgrade, KarateGmStats);
        cursor = await ActivateUpgradeAsync(actor, AikidoGrandmasterUpgrade, AikidoGmStats);
        foreach (var (stat, value) in KarateGmStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, KarateGrandmasterUpgrade, stat, value, 0, remove: true),
                $"Aikido GM Upgrade removes Karate GM stat {stat}");

        await Task.Delay(650);
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildSelfRequest(AikidoGrandmasterUpgrade));
        byte[] off = AbilityBuffs.BuildExclusive(AikidoGrandmasterUpgrade, false);
        await ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(off),
            "Aikido GM Upgrade toggles off during reuse");

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "ma-grandmaster-karate-aikido.json"), JsonSerializer.Serialize(proof));
        Check.Equal(0, actor.World.Problems.Count, "MA Grandmaster batch framing");
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
        byte[] request = Convert.FromHexString(SelfCastRequest);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        return request;
    }
}
