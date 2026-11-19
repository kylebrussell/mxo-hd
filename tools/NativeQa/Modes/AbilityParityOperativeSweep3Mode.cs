using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityOperativeSweep3Mode : NativeQaMode
{
    public override string Name => "ability-parity-operative-sweep-3";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 110, 116, 123, 135, 143, 150, 154, 156, 185, 291 },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0" },
        readyMessages: new[]
        {
            "Confirm Status has the initial Self Defense values. At a Hardline or Load Area unload and reload Combat Toughness 116. Record both +7 raw resistance-bonus records and their removal.",
            "With Operative Target selected, cast Combat Evasion 110 (+20), Compel Close Combat 150 (+20 Force Combat), and Intensify Damage 185 (+1 to melee and ballistic). Record caster add/off and watch 185 expire naturally after 15 seconds. Long buffs can be dismissed from Status.",
            "Extra samples: activate Find Weakness 123 (+25 thrown at level 50), Martial Arts Prowess 135 (existing captured +40 melee), Stealth Awareness 156 (+15 detection at level 50), and Desperate Speed 291. Record their add/off records and visible Status.",
            "Record initial and changed Status, raw cast requests, and on/off records. Four rows can close after review: 110, 116, 150 and 185."
        },
        extraQaMetadata: new() { ["NativeCloseableRows"] = 4, ["NativeCloseableIds"] = "110,116,150,185" },
        testMobFixture: "ability-parity-operative-sweep-3.csv", loadAbilityAncestors: true);

    internal async Task VerifyHeadlessAsync(HeadlessPlayer actor)
    {
        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync("Operative Target");
        static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));

        async Task Modifier(ushort id, ushort stat, int value, float seconds, bool remove = false)
        {
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Any(record =>
                ManageBonusRecord.TryDecode(record.Bytes, out var m) && m!.SourceId == id && m.TargetId == stat &&
                m.SignedValue == value && m.Duration == seconds && m.AddRemoveType == (remove ? 1 : 0)),
                $"operative batch-three modifier {id}/{stat} {value} for {seconds}s (remove={remove})",
                remove ? TimeSpan.FromSeconds(25) : HeadlessPlayer.DefaultTimeout);
        }

        await Modifier(116, 1140, 7, 0);
        await Modifier(116, 1144, 7, 0);
        await Modifier(143, 526, 50, 0);
        await Modifier(154, 1046, 2, 0);
        foreach (var (id, stat, value, seconds) in new (ushort Id, ushort Stat, int Value, float Seconds)[]
        {
            (110, 520, 20, 300), (150, 519, 20, 300),
            (156, 204, 15, 300), (185, 159, 1, 15)
        })
        {
            actor.SendRpc($"80 b9 {Le(id)} {Le(target.ViewId)}");
            await Modifier(id, stat, value, seconds);
            if (id != 185)
            {
                actor.SendRpc($"80 bb {Le(id)}");
                await Modifier(id, stat, value, seconds, remove: true);
            }
        }
        await Modifier(185, 171, 1, 15);
        await Modifier(185, 159, 1, 15, remove: true);
        await Modifier(185, 171, 1, 15, remove: true);
        Console.WriteLine("  Operative and Soldier passives, resistance bonuses, and four self-buff applications and removals verified");
    }
}
