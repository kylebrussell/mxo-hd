using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class MartialArtsMasterDisciplinesMode : NativeQaMode
{
    private const ushort KarateMaster = 263, AikidoMaster = 270, KungFuGrandmaster = 426;
    private const ushort KungFuGrandmasterUpgrade = 1064, KarateMasterUpgrade = 1065, AikidoMasterUpgrade = 1067;

    // Three independent Master/Grandmaster branches (fit comfortably under level-50 100 memory).
    private static readonly ushort[] ReloadOrder = { KarateMaster, AikidoMaster, KungFuGrandmaster };
    private static readonly ushort[] UnloadOrder = ReloadOrder.Reverse().ToArray();
    private static readonly ushort[] Loadout = new ushort[] { 2, 17 }
        .Concat(ReloadOrder)
        .Concat(ReloadOrder.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (KarateMaster, KarateMasterUpgrade, new (ushort, int)[] { (1046, 1), (997, 1) }),
        (AikidoMaster, AikidoMasterUpgrade, new (ushort, int)[] { (1046, 2), (1030, 1) }),
        (KungFuGrandmaster, KungFuGrandmasterUpgrade, new (ushort, int)[] { (1046, 1), (1042, 2), (997, 5), (1142, 1) })
    };

    private static readonly (ushort Stat, int Value)[] AikidoMasterStats = { (106, 148), (522, 4), (159, 2), (1009, 7) };
    private static readonly (ushort Stat, int Value)[] KungFuGmStats = { (106, 137), (522, 13), (159, 6), (1009, 8) };
    // Stock view-zero class-upgrade request template (PR381 0291e4b5eae0:1880-1881).
    private const string ClassUpgradeRequest = "80b925040000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-ma-master-disciplines";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        },
        DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { AikidoMasterUpgrade, KungFuGrandmasterUpgrade, KarateMasterUpgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "263,270,426,1064,1067",
            ["NativeCloseableRows"] = 5,
            ["BehaviorTestRows"] = "263,270,426,533,1064,1067",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(50)
        },
        readyMessages: new[]
        {
            "Step A (5 closeable MA Master/Grandmaster rows: 263/270/426/1064/1067). On login inspect Status Bonuses and confirm 1065 (Karate Master), 1067 (Aikido Master), and 1064 (Kung Fu Grandmaster) upgrades each appear once in Actions.",
            "Step B (Unload/Reload cycle). Unload: ?qaunload 426, 270, 263 (426 drops Init -1, Powerless Dur -2, Melee Acc -5, Melee Dmg -1, 1064; 270 drops Init -2, Viral Def -1, 1067; 263 drops Init -1, Melee Acc -1, 1065). Reload: ?qaload 263, 270, 426 and confirm all rows and upgrades return once.",
            "Step C (Granted Upgrades on page 2). Clear selection. Press slot 1 (1067 Aikido Master Upgrade: +148 Max HP, +4 Melee Acc, +2 Melee Dmg, +7 Melee Def, 20 IS, 30s standalone reuse). Press slot 2 (1064 Kung Fu Grandmaster Upgrade: replaces 1067 with +137 Max HP, +13 Melee Acc, +6 Melee Dmg, +8 Melee Def, 30s reuse), then press slot 2 again within 3-5 s to verify clean toggle-off during reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        foreach (ushort grant in new[] { KungFuGrandmasterUpgrade, KarateMasterUpgrade, AikidoMasterUpgrade })
            Check.That(!archive.ContainsKey(grant), $"Grant {grant} is absent from owned Archive");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "MA Master batch fits level-50 memory");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives, 0, remove: false);

        int cursor = await ChangeLoadoutAsync(actor, archive, UnloadOrder, load: false);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: true);

        cursor = await ChangeLoadoutAsync(actor, archive, ReloadOrder, load: true);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: false);

        cursor = await ActivateUpgradeAsync(actor, AikidoMasterUpgrade, AikidoMasterStats);
        cursor = await ActivateUpgradeAsync(actor, KungFuGrandmasterUpgrade, KungFuGmStats);
        foreach (var (stat, value) in AikidoMasterStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, AikidoMasterUpgrade, stat, value, 0, remove: true),
                $"Kung Fu Grandmaster Upgrade removes Aikido Master stat {stat}");

        await Task.Delay(650);
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildUpgradeRequest(KungFuGrandmasterUpgrade));
        byte[] off = AbilityBuffs.BuildExclusive(KungFuGrandmasterUpgrade, false);
        await ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(off),
            "Kung Fu Grandmaster Upgrade toggles off during reuse");
        foreach (var (stat, value) in KungFuGmStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, KungFuGrandmasterUpgrade, stat, value, 0, remove: true),
                $"Kung Fu Grandmaster Upgrade removes stat {stat} on toggle-off");

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "ma-master-disciplines.json"), JsonSerializer.Serialize(proof));
        Check.Equal(0, actor.World.Problems.Count, "MA Master disciplines batch framing");
        return Array.Empty<byte>();
    }

    private static async Task<int> ActivateUpgradeAsync(HeadlessPlayer actor, ushort ability, (ushort Stat, int Value)[] stats)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildUpgradeRequest(ability));
        await EvidenceRetestWire.TimerAsync(actor, cursor, ability, AbilityCooldowns.OwnTimer, 30);
        foreach (var (stat, value) in stats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, ability, stat, value, 0, remove: false),
                $"Upgrade {ability} publishes stat {stat}={value}");
        return cursor;
    }

    private static byte[] BuildUpgradeRequest(ushort ability)
    {
        byte[] request = Convert.FromHexString(ClassUpgradeRequest);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        return request;
    }
}
