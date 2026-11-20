using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class GunmanRiflemanScattershotMode : NativeQaMode
{
    private const ushort Scattershot = 245, DualSmgs = 246, Rifleman = 251, AdvancedRiflery = 252;
    private const ushort ScattershotUpgrade = 1071, RiflemanUpgrade = 1073;
    private static readonly ushort[] Loadout = Profile(2, 17, Scattershot, DualSmgs, Rifleman, AdvancedRiflery).Order().ToArray();
    private static readonly ushort[] Cycle = { AdvancedRiflery, Rifleman, DualSmgs };
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (DualSmgs, 0, new (ushort, int)[] { (1144, 6), (1139, 6) }),
        (AdvancedRiflery, 0, new (ushort, int)[] { (996, 5), (1139, 6) }),
        (Rifleman, RiflemanUpgrade, new (ushort, int)[] { (1140, 1) })
    };
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] ScattershotGrant =
        { (Scattershot, ScattershotUpgrade, Array.Empty<(ushort, int)>()) };
    private static readonly (ushort Stat, int Value)[] ScattershotStats = { (106, 90), (524, 6), (171, 4), (1010, 4) };
    private static readonly (ushort Stat, int Value)[] RiflemanStats = { (106, 90), (524, 7), (171, 2), (1010, 3) };
    // Stock view-zero class-upgrade request, PR381 0291e4b5eae0:1880–1881. These grants use that layout.
    private const string ClassUpgradeRequest = "80b925040000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-rifleman-scattershot";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=500,innerStrM=500 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        }, DevCommands: true, loadAbilityAncestors: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        hotbarAbilities: new ushort[] { ScattershotUpgrade, RiflemanUpgrade },
        extraQaMetadata: new() { ["ScopedStockRows"] = "246,251,252,1071,1073", ["NativeCloseableRows"] = 5 },
        readyMessages: new[]
        {
            "Step A. With both upgrades off, inspect the source rows in Status. Dual Submachine Guns (246) gives +6 melee resistance and +6 ballistic damage. Advanced Riflery (252) gives +5 ballistic accuracy and +6 ballistic damage. Rifleman (251) gives +1 ballistic resistance and grants Rifleman Upgrade.",
            "Step B. Unload 252, 251, and 246 in that order at the Hardline (or use ?qaunload one at a time). Capture their removed rows and Rifleman Upgrade leaving Actions. Reload 246, 251, 252 and confirm the rows and action return once.",
            "Step C. Clear selection. Verify Scattershot Upgrade and Rifleman Upgrade each appear once in Actions. Use page 2 slot 1 then slot 2. Both add +90 max HP. Scattershot adds +6 accuracy, +4 damage and +4 defense. Rifleman replaces it with +7 accuracy, +2 damage and +3 defense. Capture both stock presses and 30s reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(!archive.ContainsKey(ScattershotUpgrade) && !archive.ContainsKey(RiflemanUpgrade),
            "Both tested upgrades are grants rather than owned Archive rows");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= 100, "Both chains fit level-50 memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives, 0, remove: false);
        await CheckPassivesAsync(actor, proof, ScattershotGrant, 0, remove: false);
        int cursor = await ChangeLoadoutAsync(actor, archive, Cycle, load: false);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: true);
        cursor = await ChangeLoadoutAsync(actor, archive, Cycle.Reverse().ToArray(), load: true);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: false);

        cursor = await ActivateAsync(actor, ScattershotUpgrade);
        await CheckUpgradeAsync(actor, ScattershotUpgrade, ScattershotStats, cursor);
        cursor = await ActivateAsync(actor, RiflemanUpgrade);
        await CheckUpgradeAsync(actor, RiflemanUpgrade, RiflemanStats, cursor);
        foreach (var (stat, value) in ScattershotStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, ScattershotUpgrade, stat, value, 0, remove: true),
                $"Rifleman removes Scattershot stat {stat}");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "rifleman-scattershot-passives.json"), JsonSerializer.Serialize(proof));
        Check.Equal(0, actor.World.Problems.Count, "Class upgrade replay framing");
        return Array.Empty<byte>();
    }

    private static async Task<int> ActivateAsync(HeadlessPlayer actor, ushort ability)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        byte[] request = Convert.FromHexString(ClassUpgradeRequest);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        actor.SendRpc(request);
        await EvidenceRetestWire.TimerAsync(actor, cursor, ability, AbilityCooldowns.OwnTimer, 30);
        return cursor;
    }

    private static async Task CheckUpgradeAsync(HeadlessPlayer actor, ushort ability, (ushort Stat, int Value)[] stats, int cursor)
    {
        foreach (var (stat, value) in stats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, ability, stat, value, 0, remove: false),
                $"Upgrade {ability} adds stat {stat}");
    }
}
