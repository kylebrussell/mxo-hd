using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityOperativeSweepMode : NativeQaMode
{
    public override string Name => "ability-parity-operative-sweep";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 284, 164, 278, 285, 118, 131, 283, 120, 1122, 1119, 1115, 293, 292, 141, 1133, 1131, 1127 },
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
            "At a Hardline or Load Area, compare the stock Status panel and passive records with Improved Sneak Attack 284 unloaded and loaded. At learned rank 50 expect Confuse +25, Powerless +25 and Pacify +12. Keep the complete add and remove records.",
            "With Operative Target selected, cast Cutthroat Upgrade 1119 and Assassin Upgrade 1122. Record only caster modifiers: 1119 has Thrown Damage +3, Thrown Accuracy +5 and Combat Evasion +10. 1122 has Thrown Damage +6, Thrown Accuracy +5 and Stealth +5. Record replacement or toggle removal.",
            "Extra samples: observe Weak Points 164 and Wicked Aim 278 passive records, Infiltrator Upgrade 1115 and Stealth Countermeasures 293. The latter adds +80 Stealth Detection for 30 seconds. Watch it expire.",
            "On Operative Target, use Neuro-Dart 141 outside combat. Then interlock and try Cartwheel Escape 292. Record the target Confuse separately from the caster Combat Evasion +25 for 30 seconds.",
            "Keep outgoing ability and target requests, on/off modifier records and visible Status. Improved Sneak Attack and Cutthroat Upgrade have a native-only audit item at this build."
        },
        extraQaMetadata: new()
        {
            ["OperativeSweepNativeStatus"] = "Pending",
            ["NativeCloseableRows"] = 2,
            ["NativeCloseableIds"] = "284,1119"
        },
        testMobFixture: "ability-parity-operative-sweep.csv", loadAbilityAncestors: true);

    internal async Task VerifyHeadlessAsync(HeadlessPlayer actor, ServerInstance server)
    {
        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync("Operative Target");

        async Task Modifier(ushort id, ushort stat, int value, float seconds, bool remove = false)
        {
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Any(record =>
                ManageBonusRecord.TryDecode(record.Bytes, out var m) && m!.SourceId == id &&
                m.TargetId == stat && m.SignedValue == value && m.Duration == seconds &&
                m.AddRemoveType == (remove ? 1 : 0)),
                $"operative modifier {id}/{stat} {value} for {seconds}s (remove={remove})",
                remove ? TimeSpan.FromSeconds(36) : HeadlessPlayer.DefaultTimeout);
        }
        void Cast(ushort id)
        {
            static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));
            actor.SendRpc($"80 b9 {Le(id)} {Le(target.ViewId)}");
        }

        await Modifier(284, 1042, 25, 0);
        await Modifier(284, 1043, 25, 0);
        await Modifier(284, 1039, 12, 0);
        await Modifier(164, 1145, 10, 0);
        await Modifier(278, 1145, 15, 0);

        Cast(1119);
        await Modifier(1119, 1034, 3, 0);
        await Modifier(1119, 1032, 5, 0);
        await Modifier(1119, 520, 10, 0);
        Cast(1122);
        await Modifier(1122, 1034, 6, 0);
        await Modifier(1122, 1032, 5, 0);
        await Modifier(1122, 209, 5, 0);
        await Modifier(1119, 520, 10, 0, remove: true);
        Cast(1115);
        await Modifier(1115, 1009, 3, 0);
        await Modifier(1115, 1032, 3, 0);
        await Modifier(1115, 639, 3, 0);
        await Modifier(1115, 877, 10, 0);
        await Modifier(1122, 209, 5, 0, remove: true);

        Cast(293);
        await Modifier(293, 204, 80, 30);
        int cursor = server.DebugLog().Length;
        Cast(141);
        await actor.WaitUntilAsync(
            () => server.DebugLog()[cursor..].Contains($"[ABILITY] 141 lands on view {target.ViewId} for 0", StringComparison.Ordinal),
            "Neuro-Dart lands without immediate damage", HeadlessPlayer.DefaultTimeout);
        Cast(292);
        await Modifier(292, 520, 25, 30);
        await Modifier(293, 204, 80, 30, remove: true);
        await Modifier(292, 520, 25, 30, remove: true);
        Console.WriteLine("  ranked passives, self-only upgrades and replacement, Neuro-Dart landing, and both timed caster buffs verified");
    }
}
