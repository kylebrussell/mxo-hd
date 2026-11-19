using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class HackerPassiveAndUpgradeMode : NativeQaMode
{
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (461, 0, new (ushort, int)[] { (1139, 5), (997, 5), (995, 15) }),
        (469, 1088, new (ushort, int)[] { (995, 5) }),
        (470, 1081, new (ushort, int)[] { (1056, 5), (1059, 5) }),
        (318, 1095, new (ushort, int)[] { (1144, 1), (1140, 1) }),
        (425, 1096, Array.Empty<(ushort, int)>())
    };
    private static readonly (ushort Stat, int Value)[] Upgrade =
        { (159, 25), (171, 25), (172, 25), (1034, 25), (162, 200) };

    public override string Name => "ability-parity-hacker-passive-upgrade";
    public override NativeQaModePlan Plan { get; } = MakePlan();

    private static HashSet<ushort> FirstProfile() => Profile(2, 17, 461, 469, 470);
    private static HashSet<ushort> SecondProfile() => Profile(2, 17, 318, 425);

    private static NativeQaModePlan MakePlan()
    {
        var first = FirstProfile();
        var second = SecondProfile();
        var unload = first.Except(second).Order().ToArray();
        var load = second.Except(first).Order().ToArray();
        string staged = string.Join(",", load.Select(id => 0x80000000u | ((uint)id << 10)));
        return new(first.Concat(second).Distinct().Order().ToArray(), 24800, -1498,
            Array.Empty<string>(), new[] { $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id IN ({staged})" },
            new[]
            {
                "UPDATE characters SET y=495,alignment=1,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111)",
                "UPDATE characters SET x=25200,z=13984 WHERE charId=6111"
            }, DevCommands: true,
            environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
            extraQaMetadata: new()
            {
                ["InitialMemory"] = first.Sum(AbilityMemory.Cost),
                ["SecondMemory"] = second.Sum(AbilityMemory.Cost),
                ["UnloadForSecond"] = string.Join(",", unload),
                ["LoadForSecond"] = string.Join(",", load),
                ["NativeCloseableIds"] = "461,469,470,318,1096"
            },
            readyMessages: new[]
            {
                "Once CSR is in world, send helper chat ?tp 25200 495 -1498 0 0 0 for QAFriend and wait for its fresh nearby view.",
                "At the Hardline or Load Area, capture source 461 (+5 Melee Accuracy Bonus, +5 Ballistic Damage Bonus, +15 Stun Duration Bonus), 469 (+5 Stun Duration Bonus and Destroyer grant), and 470 (+5 Max IS Bonus, +5 IS Regen Bonus and Ravager Downgrade grant). Use the individual source records to distinguish aggregate Status rows.",
                $"Switch to the second profile through the stock Loadout. Retain shared abilities and unload these first-stage entries in descending dependency order or one selection and commit: {string.Join(",", unload)}. Capture removal of the three passive sources and both granted entries.",
                $"Load the following second-stage entries in prerequisite order or one selection and commit: {string.Join(",", load)}. Confirm Upgrade Artist 318 adds +1 melee and ballistic resistance bonuses and grants 1095, and Upgrade Master 425 grants 1096.",
                "Select QAFriend and cast the granted Upgrade Master Upgrade (1096). Capture the recipient's four +25 typed damage records and +200 Max Buffer for 300 seconds. Check the caster's 10-second reuse timer.",
                "Unload the second-stage-only abilities and capture the 318 passive records and both 1095 and 1096 actions going away. Reload the stage and capture those records returning once. Require an actual Actions entry for grant presentation."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        using var actor = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var first = FirstProfile();
        var second = SecondProfile();
        Check.Equal(93, first.Sum(AbilityMemory.Cost), "First stock loadout memory");
        Check.Equal(61, second.Sum(AbilityMemory.Cost), "Second stock loadout memory");
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).ToDictionary(item => AbilityCatalog.IdOf(item.Descriptor));
        Check.That(rows.Keys.ToHashSet().SetEquals(first.Concat(second)), "Archive owns both legal profiles without granted children");
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Select(item => AbilityCatalog.IdOf(item.Descriptor));
        Check.That(first.SetEquals(loaded), "Only first profile is loaded on entry");

        await actor.EnterWorldAsync();
        await actor.WaitForViewNamedAsync("WinterFrost");
        friend.SendChat("?tp 25200 495 -1498 0 0 0");
        await friend.WaitForTextAsync("Teleported to 0/0");
        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives[..3], 0, remove: false);
        var unload = first.Except(second).Order().ToArray();
        var load = second.Except(first).Order().ToArray();
        int cursor = await ChangeLoadoutAsync(actor, rows, unload, load: false);
        await CheckPassivesAsync(actor, proof, Passives[..3], cursor, remove: true);
        cursor = await ChangeLoadoutAsync(actor, rows, load, load: true);
        await CheckPassivesAsync(actor, proof, Passives[3..], cursor, remove: false);

        int ownBefore = actor.World.RpcSnapshot().Count;
        int friendBefore = friend.World.RpcSnapshot().Count;
        string cast = $"80 b9 48 04 {LittleEndianHex(target.ViewId)}";
        actor.SendRpc(cast);
        foreach (var (stat, value) in Upgrade)
        {
            var modifier = await ObserveAsync(friend, friendBefore,
                packet => Bonus(packet, 1096, stat, value, 300, remove: false), $"1096 on {friend.Handle}, stat {stat}");
            proof.Add(new { Ability = 1096, Recipient = friend.Handle, Stat = stat, Record = modifier.Hex });
        }
        var timer = BytePattern.Parse("80 bd 01 48 04 04 00 00 20 41 01");
        var reuse = await ObserveAsync(actor, ownBefore, packet => timer.IsMatch(packet.Bytes), "1096 ten-second timer");
        Check.That(!actor.World.RpcSnapshot().Skip(ownBefore).Any(packet => Bonus(packet, 1096, 162, 200, 300, remove: false)),
            "The selected friendly player receives Upgrade Master");
        proof.Add(new { Ability = 1096, Cast = cast, Reuse = reuse.Hex });

        cursor = await ChangeLoadoutAsync(actor, rows, load, load: false);
        await CheckPassivesAsync(actor, proof, Passives[3..], cursor, remove: true);
        cursor = await ChangeLoadoutAsync(actor, rows, load, load: true);
        await CheckPassivesAsync(actor, proof, Passives[3..], cursor, remove: false);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-passive-upgrade.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }
}
