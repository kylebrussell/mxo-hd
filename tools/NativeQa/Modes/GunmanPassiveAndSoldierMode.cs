using System.Text.Json;
using hds;
using HeadlessClient;
using NativeQa;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class GunmanPassiveAndSoldierMode : NativeQaMode
{
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Handguns =
    {
        (125, 0, new (ushort, int)[] { (996, 5), (1139, 5) }),
        (239, 0, new (ushort, int)[] { (996, 5), (1139, 2), (1144, 6) })
    };
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Rifles =
    {
        (125, 0, new (ushort, int)[] { (996, 5), (1139, 5) }),
        (440, 1074, new (ushort, int)[] { (996, 5), (1139, 1), (1040, 2) }),
        (441, 0, new (ushort, int)[] { (995, 10), (1040, 15), (181, 20) }),
        (508, 1136, new (ushort, int)[] { (996, 2), (1139, 2) })
    };
    private static readonly (ushort Stat, int Value)[] Soldier = { (106, 50), (180, 20), (181, 20) };

    public override string Name => "ability-parity-gunman-passives";
    public override NativeQaModePlan Plan { get; } = MakePlan();

    private static HashSet<ushort> Core() => Profile(2, 17, 154);
    private static HashSet<ushort> BaseStance() => Profile(2, 17);
    private static HashSet<ushort> First() => Profile(2, 17, 125, 239);
    private static HashSet<ushort> Second() => Profile(2, 17, 440, 441, 508);

    private static IEnumerable<ushort> LoadOrder(IEnumerable<ushort> ids) =>
        ids.OrderBy(id => AbilityPrerequisites.Current.Closure(id).Count).ThenBy(id => id);

    private static NativeQaModePlan MakePlan()
    {
        var core = Core();
        var first = First();
        var second = Second();
        var firstOnly = LoadOrder(first.Except(core)).Reverse().ToArray();
        var secondOnly = LoadOrder(second.Except(core)).ToArray();
        var rifleRetest = LoadOrder(second.Except(BaseStance())).ToArray();
        string staged = string.Join(",", second.Except(first).Order().Select(id => 0x80000000u | ((uint)id << 10)));
        return new(first.Concat(second).Distinct().Order().ToArray(), CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
            Array.Empty<string>(), new[] { $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id IN ({staged})" },
            new[] { $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110" },
            DevCommands: true,
            environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
            extraQaMetadata: new()
            {
                ["InitialMemory"] = first.Sum(AbilityMemory.Cost),
                ["SecondMemory"] = second.Sum(AbilityMemory.Cost),
                ["NativeCloseableIds"] = "125,239,440,441,508,1061",
                ["RetainSoldierCore"] = string.Join(",", core.Order()),
                ["UnloadHandguns"] = string.Join(",", firstOnly),
                ["LoadRifles"] = string.Join(",", secondOnly),
                ["UnloadRifles"] = string.Join(",", rifleRetest.Reverse()),
                ["ReloadRifles"] = string.Join(",", rifleRetest)
            },
            readyMessages: new[]
            {
                "At the Hardline or Load Area, record the source-specific Firearms Expert 125 (+5 accuracy/+5 ballistic damage) and Dual Handguns 239 (+5 accuracy/+2 ballistic damage/+6 melee resistance) passives in the 66-memory initial profile.",
                "With WinterFrost selected, activate Soldier Upgrade 1061 from the Soldier Actions entry. Capture the caster's +50 Max Health and +20 each melee and ballistic resistance, plus the 30-second reuse.",
                $"Retain the Soldier prerequisites shown in RetainSoldierCore. On the stock loadout unload these entries together or in descending dependency order: {string.Join(",", firstOnly)}. Confirm that the 239 source records are removed and 125 stays loaded through the profile switch.",
                $"Load the 92-memory rifle profile at the same Hardline by selecting these entries together or in prerequisite order: {string.Join(",", secondOnly)}. Verify 440 adds +5 ballistic accuracy/+1 ballistic damage/+2 Root Duration and grant 1074, 441 adds +10 Stun Duration/+15 Root Duration/+20 ballistic resistance, and 508 adds +2 ballistic accuracy/+2 ballistic damage and grant 1136.",
                "Confirm Expert Rifleman Upgrade 1074 and Sniper Upgrade 1136 in stock Actions. Activate 1074 with WinterFrost selected and capture removal of the original 1061 records.",
                "After 1061 replacement, retain only Awakened and Self Defense and unload the entries shown in UnloadRifles. Restore those entries using ReloadRifles. Capture the 125/440/441/508 source removals and re-adds and both rifle granted actions disappearing and returning.",
                "If the stock loadout UI is unavailable, use ?qaunload or ?qaload with one ID at a time in the exact printed order. Wait for each acknowledgment before the next command."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();

        using var actor = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var core = Core();
        var first = First();
        var second = Second();
        Check.Equal(66, first.Sum(AbilityMemory.Cost), "Initial handgun memory");
        Check.Equal(92, second.Sum(AbilityMemory.Cost), "Rifle memory");
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).ToDictionary(item => AbilityCatalog.IdOf(item.Descriptor));
        Check.That(rows.Keys.ToHashSet().SetEquals(first.Concat(second)), "The archive owns both profiles without granted children");
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Select(item => AbilityCatalog.IdOf(item.Descriptor));
        Check.That(first.SetEquals(loaded), "Only the handgun profile is loaded on entry");

        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync("WinterFrost");
        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Handguns, 0, remove: false);
        var soldier = await ObserveAsync(actor, 0, GrantRecord(1061, remove: false), "Soldier grant");
        proof.Add(new { Ability = 1061, Grant = soldier.Hex });
        int cursor = actor.World.RpcSnapshot().Count;
        var cast = $"80 b9 {LittleEndianHex(1061)} {LittleEndianHex(target.ViewId)}";
        actor.SendRpc(cast);
        await CheckSoldierAsync(cursor, remove: false);
        var timer = BytePattern.Parse("80 bd 01 25 04 04 00 00 f0 41 01");
        var reuse = await ObserveAsync(actor, cursor, packet => timer.IsMatch(packet.Bytes), "Soldier 30-second reuse");
        proof.Add(new { Ability = 1061, Cast = cast, Reuse = reuse.Hex });

        var firstOnly = first.Except(core).Order().ToArray();
        var secondOnly = second.Except(core).Order().ToArray();
        cursor = await ChangeLoadoutAsync(actor, rows, firstOnly, load: false);
        await CheckPassivesAsync(actor, proof, Handguns[1..], cursor, remove: true);
        cursor = await ChangeLoadoutAsync(actor, rows, secondOnly, load: true);
        await CheckPassivesAsync(actor, proof, Rifles[1..], cursor, remove: false);

        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc($"80 b9 {LittleEndianHex(1074)} {LittleEndianHex(target.ViewId)}");
        await CheckSoldierAsync(cursor, remove: true);
        var replacement = await ObserveAsync(actor, cursor, packet => Bonus(packet, 1074, 106, 120, 0, remove: false),
            "Expert Rifleman replaces the Soldier maximum");
        proof.Add(new { Ability = 1074, Replacement = replacement.Hex });

        var rifleRetest = second.Except(BaseStance()).Order().ToArray();
        cursor = await ChangeLoadoutAsync(actor, rows, rifleRetest, load: false);
        await CheckPassivesAsync(actor, proof, Rifles, cursor, remove: true);
        cursor = await ChangeLoadoutAsync(actor, rows, rifleRetest, load: true);
        await CheckPassivesAsync(actor, proof, Rifles, cursor, remove: false);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "gunman-passive-and-soldier.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();

        async Task CheckSoldierAsync(int start, bool remove)
        {
            foreach (var (stat, value) in Soldier)
            {
                var record = await ObserveAsync(actor, start, packet => Bonus(packet, 1061, stat, value, 0, remove),
                    $"Soldier bonus {stat} remove={remove}");
                proof.Add(new { Ability = 1061, Stat = stat, Remove = remove, Record = record.Hex });
            }
        }
    }
}
