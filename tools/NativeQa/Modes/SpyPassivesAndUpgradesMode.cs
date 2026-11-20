using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class SpyPassivesAndUpgradesMode : NativeQaMode
{
    private const ushort Infiltrator = 131, KnifeThrower = 283, Cutthroat = 118, ImprovedSneakAttack = 284, Assassin = 285;
    private const ushort InfiltratorUpgrade = 1115, CutthroatUpgrade = 1119, KnifeThrowerUpgrade = 1120, AssassinUpgrade = 1122;

    // Root-to-leaf reload order verified by SpyPassivesAndUpgradesTests; unload in reverse.
    private static readonly ushort[] ReloadOrder = { Infiltrator, Cutthroat, KnifeThrower, ImprovedSneakAttack, Assassin };
    private static readonly ushort[] UnloadOrder = ReloadOrder.Reverse().ToArray();
    private static readonly ushort[] Loadout = new ushort[] { 2, 17 }
        .Concat(ReloadOrder)
        .Concat(ReloadOrder.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (Infiltrator, InfiltratorUpgrade, Array.Empty<(ushort, int)>()),
        (KnifeThrower, KnifeThrowerUpgrade, Array.Empty<(ushort, int)>()),
        (Cutthroat, CutthroatUpgrade, Array.Empty<(ushort, int)>()),
        (ImprovedSneakAttack, 0, new (ushort, int)[] { (1042, 25), (1043, 25), (1039, 12) }),
        (Assassin, AssassinUpgrade, Array.Empty<(ushort, int)>())
    };

    private static readonly (ushort Stat, int Value)[] CutthroatStats = { (1034, 3), (1032, 5), (520, 10) };
    private static readonly (ushort Stat, int Value)[] KnifeThrowerStats = { (1034, 4), (1032, 15), (520, 10) };
    private static readonly (ushort Stat, int Value)[] AssassinStats = { (1034, 6), (1032, 5), (209, 5) };
    private static readonly (ushort Stat, int Value)[] InfiltratorStats = { (1009, 3), (1032, 3), (639, 3), (877, 10) };

    // Stock view-zero class-upgrade request template (PR381 0291e4b5eae0:1880-1881).
    private const string ClassUpgradeRequest = "80b925040000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-spy-passives-and-upgrades";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        },
        DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { CutthroatUpgrade, KnifeThrowerUpgrade, AssassinUpgrade, InfiltratorUpgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "118,131,283,284,285,1119,1122",
            ["NativeCloseableRows"] = 6,
            ["BehaviorTestRows"] = "118,131,283,285",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(50)
        },
        readyMessages: new[]
        {
            "Step A (6 closeable Spy-family rows: 284, 1119, 118, 131, 283, 285 + native-family 1122). On login inspect Status Bonuses (Improved Sneak Attack 284: +25 Confuse Dur, +25 Powerless Dur, +12 Pacify Dur) and verify Cutthroat Upgrade (1119), Knife Thrower Upgrade (1120), Assassin Upgrade (1122), and Infiltrator Upgrade (1115) each appear once in Actions.",
            "Step B (Unload/Reload cycle). Unload leaf-to-root: ?qaunload 285, 284, 283, 118, 131. Confirm 1122 leaves Actions on 285, the three control-duration rows drop on 284, 1120 leaves on 283, 1119 leaves on 118, and 1115 leaves on 131. Reload root-to-leaf: ?qaload 131, 118, 283, 284, 285 (283 requires 118) and confirm all rows and all four upgrades return once.",
            "Step C (Granted Upgrades on page 2). Clear selection. Press slot 1 (1119 Cutthroat Upgrade: +3 Thrown Dmg, +5 Thrown Acc, +10 Combat Evasion, 20 IS, 30s standalone reuse). Press slot 2 (1120 Knife Thrower Upgrade: replaces 1119 with +4 Thrown Dmg, +15 Thrown Acc, +10 Combat Evasion, 30s reuse). Press slot 3 (1122 Assassin Upgrade: replaces 1120 with +6 Thrown Dmg, +5 Thrown Acc, +5 Stealth, 30s reuse), then press slot 3 again within 3-5 s to verify clean toggle-off during reuse. Optional: slot 4 (1115 Infiltrator Upgrade)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        foreach (ushort grant in new[] { InfiltratorUpgrade, CutthroatUpgrade, KnifeThrowerUpgrade, AssassinUpgrade })
            Check.That(!archive.ContainsKey(grant), $"Grant {grant} is absent from owned Archive");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Spy batch fits level-50 memory");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives, 0, remove: false);

        int cursor = await ChangeLoadoutAsync(actor, archive, UnloadOrder, load: false);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: true);

        cursor = await ChangeLoadoutAsync(actor, archive, ReloadOrder, load: true);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: false);

        cursor = await ActivateUpgradeAsync(actor, CutthroatUpgrade, CutthroatStats);
        cursor = await ActivateUpgradeAsync(actor, KnifeThrowerUpgrade, KnifeThrowerStats);
        foreach (var (stat, value) in CutthroatStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, CutthroatUpgrade, stat, value, 0, remove: true),
                $"Knife Thrower Upgrade removes Cutthroat stat {stat}");

        cursor = await ActivateUpgradeAsync(actor, AssassinUpgrade, AssassinStats);
        foreach (var (stat, value) in KnifeThrowerStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, KnifeThrowerUpgrade, stat, value, 0, remove: true),
                $"Assassin Upgrade removes Knife Thrower stat {stat}");

        await Task.Delay(650);
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildUpgradeRequest(AssassinUpgrade));
        byte[] off = AbilityBuffs.BuildExclusive(AssassinUpgrade, false);
        await ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(off),
            "Assassin Upgrade toggles off during reuse");
        foreach (var (stat, value) in AssassinStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, AssassinUpgrade, stat, value, 0, remove: true),
                $"Assassin Upgrade removes stat {stat} on toggle-off");

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "spy-passives-and-upgrades.json"), JsonSerializer.Serialize(proof));
        Check.Equal(0, actor.World.Problems.Count, "Spy passives and upgrades batch framing");
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
